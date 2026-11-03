using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace HRMS.Api.Services;

/// <summary>Thrown when every model of a provider is busy or out of its free allowance, so the next provider can try.</summary>
public class AiUnavailableException(string message) : DomainException(message);

/// <summary>
/// OpenRouter (OpenAI-style chat API, many models behind one key) as the backup when Gemini is busy or used up for the
/// day. Its free models are tried in the order given; OpenRouter itself moves on to the next one when one fails.
/// </summary>
public partial class AiAssistantService
{
    bool HasOpenRouter => !string.IsNullOrWhiteSpace(options.OpenRouterApiKey);

    public async Task<object> HealthAsync(string baseUrl)
    {
        if (cache.TryGetValue("ai-health", out object? cached) && cached is not null) return cached;
        async Task<string> Probe(string url, Action<HttpRequestMessage>? auth = null)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var client = http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                auth?.Invoke(req);
                using var res = await client.SendAsync(req);
                return $"{(int)res.StatusCode} in {watch.ElapsedMilliseconds} ms";
            }
            catch (Exception ex)
            {
                return $"failed after {watch.ElapsedMilliseconds} ms: {ex.GetType().Name} {ex.Message}";
            }
        }
        var result = new
        {
            gemini = !string.IsNullOrWhiteSpace(options.GeminiApiKey)
                ? await Probe("https://generativelanguage.googleapis.com/v1beta/models?pageSize=1", r => r.Headers.Add("x-goog-api-key", options.GeminiApiKey))
                : "no key",
            openRouter = HasOpenRouter
                ? await Probe("https://openrouter.ai/api/v1/key", r => r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.OpenRouterApiKey))
                : "no key",
            selfCall = await Probe($"{baseUrl}/api/ai/status"),
            baseUrl,
        };
        cache.Set("ai-health", (object)result, TimeSpan.FromMinutes(1));
        return result;
    }

    /// <summary>The model that answered last; later rounds of the same question start with it.</summary>
    string? openRouterModel;

    async Task<JsonObject> OpenRouterCallAsync(JsonObject body, AiMessageLog entry)
    {
        var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var models = options.OpenRouterModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (openRouterModel is not null) models.Insert(0, openRouterModel);
        HttpResponseMessage res = null!;
        var text = "";
        JsonObject? json = null;
        JsonObject? message = null;
        // Free models come and go and are often busy: try each in turn until one answers. A spent daily allowance (429
        // "free-models-per-day") or a bad key stops the loop, since every other model would refuse too.
        foreach (var model in models.Distinct().Where(m => m == openRouterModel || !Resting(m)))
        {
            body["model"] = model;
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions") { Content = JsonContent.Create(body) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.OpenRouterApiKey);
            req.Headers.Add("X-Title", "Qaventra HRMS");
            try
            {
                res = await client.SendAsync(req);
                text = await res.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException)
            {
                Rest(model, 503);
                continue;
            }
            json = res.IsSuccessStatusCode ? JsonNode.Parse(text) as JsonObject : null;
            message = json?["choices"]?[0]?["message"] as JsonObject;
            if (message is not null)
            {
                openRouterModel = model;
                break;
            }
            if ((int)res.StatusCode is 401 or 403 || text.Contains("per-day", StringComparison.OrdinalIgnoreCase))
                break;
            Rest(model, (int)res.StatusCode);
        }
        if (message is null)
        {
            log.LogWarning("OpenRouter call failed: {Status} {Body}", (int)res.StatusCode, text.Length > 500 ? text[..500] : text);
            entry.Error = $"OpenRouter {(int)res.StatusCode} {(text.Length > 460 ? text[..460] : text)}";
            db.AiMessageLogs.Add(entry);
            await db.SaveChangesAsync();
            throw res is not null && (int)res.StatusCode is 401 or 403
                ? new DomainException("The AI assistant's key on this site is not valid. Please tell the platform administrator.")
                : new AiUnavailableException("The free AI allowance is used up for now. Please try again later.");
        }
        if (json!["usage"] is JsonObject usage)
        {
            entry.InputTokens += usage["prompt_tokens"]?.GetValue<int>() ?? 0;
            entry.OutputTokens += usage["completion_tokens"]?.GetValue<int>() ?? 0;
        }
        return message;
    }

    static JsonObject Fn(string name, string description, JsonObject parameters) =>
        new() { ["type"] = "function", ["function"] = new JsonObject { ["name"] = name, ["description"] = description, ["parameters"] = parameters } };

    async Task<AiReply> OpenRouterAskAsync(List<AiChatTurn> turns, bool voice, HttpClient api, int limit, int used)
    {
        var messages = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = await SystemPromptAsync(voice) });
        foreach (var t in turns.TakeLast(20))
            messages.Add(new JsonObject { ["role"] = t.Role == "user" ? "user" : "assistant", ["content"] = t.Text });
        var tools = new JsonArray(
            Fn("lookup", "Read the person's own HR data from the system. Sources: " + string.Join(" ", Sources.Select(s => $"{s.Key}: {s.Value.About}")), new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["source"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(Sources.Keys.Select(k => (JsonNode)k!).ToArray()) },
                    ["year"] = new JsonObject { ["type"] = "integer" },
                    ["month"] = new JsonObject { ["type"] = "integer" },
                },
                ["required"] = new JsonArray("source"),
            }),
            Fn("propose_action", """
                Prepare a change for the person to confirm; nothing is saved until they press Confirm. payload is a JSON object:
                apply_leave: {"leaveTypeId", "fromDate": "yyyy-MM-dd", "toDate": "yyyy-MM-dd", "reason", "reasonId" (from leave_reasons when the type has reasons)}; attendance_request: {"date", "kind": "MissedPunch" | "TimeCorrection" | "WorkFromHome", "checkIn": "HH:mm:ss", "checkOut": "HH:mm:ss", "reason"};
                helpdesk_ticket: {"categoryId", "subject", "description", "priority": "Low" | "Medium" | "High" | "Urgent"}; decide_approval: {"requestType", "subjectId", "approve", "note"}.
                expense_claim: {"title": text, "items": [{"date": "yyyy-MM-dd", "category": "Travel" | "Fuel" | "Meals" | "Accommodation" | "Medical" | "Mobile" | "OfficeSupplies" | "Training" | "Other", "description": text, "amount": number}]}.
                loan_request: {"title": text, "amount": number, "installments": months, "startYear": number, "startMonth": 1-12, "reason": text}.
                letter_request: {"templateId": id from letter_templates, "reason": text}.
                asset_request: {"typeId": id from asset_types, "reason": text, "neededBy": "yyyy-MM-dd" or null}.
                training_request: {"courseName": text, "provider": text, "cost": number or null, "reason": text}.
                summary is one line in the person's language describing what will be submitted.
                """, new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(ActionKinds.Select(k => (JsonNode)k!).ToArray()) },
                    ["summary"] = new JsonObject { ["type"] = "string" },
                    ["payload"] = new JsonObject { ["type"] = "object" },
                },
                ["required"] = new JsonArray("kind", "summary", "payload"),
            }));
        var actions = new List<AiAction>();
        var entry = new AiMessageLog { UserId = me.UserId ?? Guid.Empty, Voice = voice };
        var reply = "";
        for (var round = 0; round < MaxRounds; round++)
        {
            var message = await OpenRouterCallAsync(new JsonObject { ["messages"] = messages.DeepClone(), ["tools"] = tools.DeepClone() }, entry);
            reply = (message["content"]?.GetValueKind() == JsonValueKind.String ? message["content"]!.GetValue<string>() : "").Trim();
            var calls = (message["tool_calls"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            if (calls.Count == 0) break;
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = reply, ["tool_calls"] = message["tool_calls"]!.DeepClone() });
            foreach (var call in calls)
            {
                entry.ToolCalls++;
                var name = call["function"]?["name"]?.GetValue<string>() ?? "";
                Dictionary<string, JsonElement> args;
                try
                {
                    args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(call["function"]?["arguments"]?.GetValue<string>() ?? "{}") ?? [];
                }
                catch (JsonException)
                {
                    args = [];
                }
                if (args.TryGetValue("payload", out var p) && p.ValueKind == JsonValueKind.String)
                {
                    try { args["payload"] = JsonDocument.Parse(p.GetString()!).RootElement.Clone(); }
                    catch (JsonException) { /* reported by RunToolAsync */ }
                }
                var (result, _) = await RunToolAsync(api, name, args, actions);
                messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = call["id"]?.GetValue<string>(), ["content"] = result });
            }
        }
        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();
        if (reply.Length == 0) reply = actions.Count > 0 ? "Please check the details below and confirm." : "I couldn't find an answer to that.";
        return new AiReply(reply, actions, Math.Max(0, limit - used - 1));
    }

    async Task<AiCvScreen> OpenRouterCvAsync(string jobAndCandidate)
    {
        var entry = new AiMessageLog { UserId = me.UserId ?? Guid.Empty };
        var message = await OpenRouterCallAsync(new JsonObject
        {
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
            ["messages"] = new JsonArray(
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = """
                        You screen job applicants for a recruiter. Compare the candidate with the job and answer with JSON only:
                        {"score": 0-100 fit, "summary": two sentences, "strengths": up to 4 short lines with evidence, "gaps": up to 4 short lines,
                        "questions": 3 to 5 interview questions that probe the gaps}. Judge only skills, experience and qualifications; ignore age,
                        gender, religion, marital status and anything else unrelated to the work.
                        """,
                },
                new JsonObject { ["role"] = "user", ["content"] = jobAndCandidate }),
        }, entry);
        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();
        var text = message["content"]?.GetValue<string>() ?? "";
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        try
        {
            return JsonSerializer.Deserialize<AiCvScreen>(start >= 0 && end > start ? text[start..(end + 1)] : text, GeminiJson) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new DomainException("The AI's answer could not be read. Please try again.");
        }
    }

    async Task<AiInsights> OpenRouterInsightsAsync(string? company, DateOnly today, Dictionary<string, string> data)
    {
        var entry = new AiMessageLog { UserId = me.UserId ?? Guid.Empty };
        var message = await OpenRouterCallAsync(new JsonObject
        {
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
            ["messages"] = new JsonArray(
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = $"""
                        You write a short weekly read of {company}'s HR numbers for its HR team and CEO. Today is {today:d MMMM yyyy}. Use only the JSON data given.
                        Answer with JSON only: {"{"}"headline": one sentence, "findings": [3 to 6 items of {"{"}"title", "detail", "level": "good" | "watch" | "act", "action"{"}"}]{"}"}.
                        Details carry the actual numbers; do not invent any.
                        """,
                },
                new JsonObject { ["role"] = "user", ["content"] = JsonSerializer.Serialize(data) }),
        }, entry);
        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();
        var text = message["content"]?.GetValue<string>() ?? "";
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        try
        {
            return JsonSerializer.Deserialize<AiInsights>(start >= 0 && end > start ? text[start..(end + 1)] : text, GeminiJson) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new DomainException("The AI summary could not be read. Please try again.");
        }
    }
}
