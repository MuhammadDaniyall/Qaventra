using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;

namespace HRMS.Api.Services;

/// <summary>
/// Google Gemini as the assistant's model, used when the site has a Gemini key and no Anthropic key (Gemini's free tier
/// needs no payment). Same tools, prompts and confirm-before-saving rule as the Claude path; called over Gemini's REST API.
/// </summary>
public partial class AiAssistantService
{
    bool UseGemini => string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(options.GeminiApiKey);

    static readonly JsonSerializerOptions GeminiJson = new(JsonSerializerDefaults.Web);

    /// <summary>The model that answered the last call; later rounds of the same question stay on it.</summary>
    string? geminiModel;

    /// <summary>Thought signatures only work with the model that wrote them; drop them before another model sees the history.</summary>
    static void StripThoughts(JsonObject body)
    {
        if (body["contents"] is not JsonArray contents) return;
        foreach (var c in contents.OfType<JsonObject>())
            if (c["parts"] is JsonArray parts)
            {
                foreach (var thought in parts.OfType<JsonObject>().Where(x => x["thought"]?.GetValue<bool>() == true).ToList())
                    parts.Remove(thought);
                foreach (var part in parts.OfType<JsonObject>())
                    part.Remove("thoughtSignature");
            }
    }

    /// <summary>One generateContent call. Returns the model's content (to echo back as-is) and logs the tokens.</summary>
    async Task<JsonObject> GeminiCallAsync(JsonObject body, AiMessageLog entry)
    {
        var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(90);
        HttpResponseMessage res = null!;
        var text = "";
        // A busy, failing, retired or used-up model (503, 500, 404, 429) hands over to the next one in the list.
        var models = options.GeminiModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (geminiModel is not null) models.Insert(0, geminiModel);
        foreach (var model in models.Distinct())
        {
            if (geminiModel is not null && model != geminiModel)
                StripThoughts(body);
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
            {
                Content = JsonContent.Create(body),
            };
            req.Headers.Add("x-goog-api-key", options.GeminiApiKey);
            res = await client.SendAsync(req);
            text = await res.Content.ReadAsStringAsync();
            if ((int)res.StatusCode is not (503 or 500 or 404 or 429))
            {
                geminiModel = model;
                break;
            }
        }
        if (!res.IsSuccessStatusCode)
        {
            log.LogWarning("Gemini call failed: {Status} {Body}", (int)res.StatusCode, text.Length > 500 ? text[..500] : text);
            entry.Error = $"{(int)res.StatusCode} {(text.Length > 480 ? text[..480] : text)}";
            db.AiMessageLogs.Add(entry);
            await db.SaveChangesAsync();
            if ((int)res.StatusCode is 429 or 503 or 500 or 404)
                throw new AiUnavailableException((int)res.StatusCode == 429
                    ? "The free AI allowance is used up for now. Please try again in a minute."
                    : "The AI assistant is busy right now. Please try again in a minute.");
            throw new DomainException((int)res.StatusCode switch
            {
                401 or 403 => "The AI assistant's key on this site is not valid. Please tell the platform administrator.",
                400 when text.Contains("API key", StringComparison.OrdinalIgnoreCase) => "The AI assistant's key on this site is not valid. Please tell the platform administrator.",
                429 => "The free AI allowance is used up for now. Please try again in a minute.",
                _ => "The AI assistant is busy right now. Please try again in a minute.",
            });
        }
        var json = JsonNode.Parse(text)!.AsObject();
        if (json["usageMetadata"] is JsonObject usage)
        {
            entry.InputTokens += usage["promptTokenCount"]?.GetValue<int>() ?? 0;
            entry.OutputTokens += (usage["candidatesTokenCount"]?.GetValue<int>() ?? 0) + (usage["thoughtsTokenCount"]?.GetValue<int>() ?? 0);
        }
        return json["candidates"]?[0]?["content"] as JsonObject ?? new JsonObject { ["role"] = "model", ["parts"] = new JsonArray() };
    }

    static JsonObject Part(string text) => new() { ["text"] = text };

    async Task<AiReply> GeminiAskAsync(List<AiChatTurn> turns, bool voice, HttpClient api, int limit, int used)
    {
        var contents = new JsonArray();
        foreach (var t in turns.TakeLast(20))
            contents.Add(new JsonObject { ["role"] = t.Role == "user" ? "user" : "model", ["parts"] = new JsonArray(Part(t.Text)) });
        var tools = new JsonArray(new JsonObject
        {
            ["functionDeclarations"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "lookup",
                    ["description"] = "Read the person's own HR data from the system. Sources: " + string.Join(" ", Sources.Select(s => $"{s.Key}: {s.Value.About}")),
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new JsonObject
                        {
                            ["source"] = new JsonObject { ["type"] = "STRING", ["enum"] = new JsonArray(Sources.Keys.Select(k => (JsonNode)k!).ToArray()) },
                            ["year"] = new JsonObject { ["type"] = "INTEGER", ["description"] = "For my_leave_balances, holidays and my_attendance_month." },
                            ["month"] = new JsonObject { ["type"] = "INTEGER", ["description"] = "1-12, for my_attendance_month." },
                        },
                        ["required"] = new JsonArray("source"),
                    },
                },
                new JsonObject
                {
                    ["name"] = "propose_action",
                    ["description"] = """
                        Prepare a change for the person to confirm; nothing is saved until they press Confirm. payload is a JSON object written as a string:
                        apply_leave: {"leaveTypeId": id from leave_types, "fromDate": "yyyy-MM-dd", "toDate": "yyyy-MM-dd", "reason": text, "reasonId": id from leave_reasons when that type has reasons}.
                        attendance_request: {"date": "yyyy-MM-dd", "kind": "MissedPunch" | "TimeCorrection" | "WorkFromHome", "checkIn": "HH:mm:ss", "checkOut": "HH:mm:ss", "reason": text}.
                        helpdesk_ticket: {"categoryId": id from helpdesk_categories, "subject": text, "description": text, "priority": "Low" | "Medium" | "High" | "Urgent"}.
                        decide_approval: {"requestType": type from approvals_inbox, "subjectId": id from approvals_inbox, "approve": true | false, "note": text or null}.
                        summary is one line in the person's language describing exactly what will be submitted.
                        """,
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new JsonObject
                        {
                            ["kind"] = new JsonObject { ["type"] = "STRING", ["enum"] = new JsonArray(ActionKinds.Select(k => (JsonNode)k!).ToArray()) },
                            ["summary"] = new JsonObject { ["type"] = "STRING" },
                            ["payload"] = new JsonObject { ["type"] = "STRING", ["description"] = "The payload as a JSON object string." },
                        },
                        ["required"] = new JsonArray("kind", "summary", "payload"),
                    },
                }),
        });
        var system = await SystemPromptAsync(voice);
        var actions = new List<AiAction>();
        var entry = new AiMessageLog { UserId = me.UserId ?? Guid.Empty, Voice = voice };
        var reply = "";
        for (var round = 0; round < MaxRounds; round++)
        {
            var content = await GeminiCallAsync(new JsonObject
            {
                ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(Part(system)) },
                ["contents"] = contents.DeepClone(),
                ["tools"] = tools.DeepClone(),
            }, entry);
            var parts = content["parts"] as JsonArray ?? [];
            reply = string.Join("", parts.Select(p => p?["thought"]?.GetValue<bool>() == true ? null : p?["text"]?.GetValue<string>())).Trim();
            var calls = parts.Select(p => p?["functionCall"]).OfType<JsonObject>().ToList();
            if (calls.Count == 0) break;
            contents.Add(content.DeepClone());
            var responses = new JsonArray();
            foreach (var call in calls)
            {
                entry.ToolCalls++;
                var name = call["name"]?.GetValue<string>() ?? "";
                var args = (call["args"] as JsonObject ?? []).ToDictionary(kv => kv.Key, kv => JsonSerializer.SerializeToElement(kv.Value));
                if (name == "propose_action" && args.TryGetValue("payload", out var p) && p.ValueKind == JsonValueKind.String)
                {
                    try { args["payload"] = JsonDocument.Parse(p.GetString()!).RootElement.Clone(); }
                    catch (JsonException) { /* left as text; RunToolAsync reports it */ }
                }
                var (result, isError) = await RunToolAsync(api, name, args, actions);
                responses.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = name,
                        ["response"] = new JsonObject { [isError ? "error" : "result"] = result },
                    },
                });
            }
            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = responses });
        }
        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();
        if (reply.Length == 0) reply = actions.Count > 0 ? "Please check the details below and confirm." : "I couldn't find an answer to that.";
        return new AiReply(reply, actions, Math.Max(0, limit - used - 1));
    }

    /// <summary>One call that must answer with JSON matching T (described in the instructions).</summary>
    async Task<T> GeminiJsonAsync<T>(string system, JsonArray parts)
    {
        var entry = new AiMessageLog { UserId = me.UserId ?? Guid.Empty };
        var content = await GeminiCallAsync(new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(Part(system)) },
            ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = parts }),
            ["generationConfig"] = new JsonObject { ["responseMimeType"] = "application/json" },
        }, entry);
        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();
        var text = string.Concat((content["parts"] as JsonArray ?? []).Select(p => p?["thought"]?.GetValue<bool>() == true ? null : p?["text"]?.GetValue<string>()));
        try
        {
            return JsonSerializer.Deserialize<T>(text, GeminiJson) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new DomainException("The AI's answer could not be read. Please try again.");
        }
    }

    Task<AiInsights> GeminiInsightsAsync(string? company, DateOnly today, Dictionary<string, string> data) =>
        GeminiJsonAsync<AiInsights>($"""
            You write a short weekly read of {company}'s HR numbers for its HR team and CEO. Today is {today:d MMMM yyyy}.
            Use only the JSON data given. Answer with JSON: {"{"}"headline": one sentence, "findings": [3 to 6 items of {"{"}"title", "detail", "level", "action"{"}"}]{"}"}.
            Each finding is about headcount, joiners and leavers, absence, lateness, leave, penalties, pending approvals or payroll cost:
            a short title, one or two sentences of detail with the actual numbers, level "good", "watch" or "act", and one concrete next step.
            Plain English. Do not invent numbers; skip what the data lacks.
            """, new JsonArray(Part(JsonSerializer.Serialize(data))));

    Task<AiCvScreen> GeminiCvAsync(string jobAndCandidate, byte[]? pdf)
    {
        var parts = new JsonArray();
        if (pdf is not null)
            parts.Add(new JsonObject { ["inlineData"] = new JsonObject { ["mimeType"] = "application/pdf", ["data"] = Convert.ToBase64String(pdf) } });
        parts.Add(Part(jobAndCandidate));
        return GeminiJsonAsync<AiCvScreen>("""
            You screen job applicants for a recruiter. Compare the candidate with the job and answer with JSON:
            {"score": 0-100 fit, "summary": two sentences, "strengths": up to 4 short lines with evidence from the CV,
            "gaps": up to 4 short lines, "questions": 3 to 5 interview questions that probe the gaps, "fromCv": true}.
            Judge only skills, experience and qualifications; ignore age, gender, religion, marital status, photos and anything else unrelated to the work.
            """, parts);
    }
}
