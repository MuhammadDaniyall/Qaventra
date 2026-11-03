using System.Net.Http.Headers;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HRMS.Api.Services;

public class AiOptions
{
    /// <summary>Anthropic API key; the assistant is off when empty.</summary>
    public string? ApiKey { get; set; }
    /// <summary>Google Gemini key (free tier available); used when there is no Anthropic key.</summary>
    public string? GeminiApiKey { get; set; }
    /// <summary>
    /// Gemini models tried in order, comma-separated: the next one is used when one is busy, failing or out of its free
    /// allowance. Flash-Lite answers fastest; Gemma and Flash follow.
    /// </summary>
    public string GeminiModels { get; set; } = "gemini-flash-lite-latest,gemma-4-31b-it,gemini-flash-latest";
    /// <summary>OpenRouter key: the backup when Gemini is busy or used up, or the only model when there is no other key.</summary>
    public string? OpenRouterApiKey { get; set; }
    /// <summary>Ask OpenRouter before Gemini (faster while its small daily allowance lasts).</summary>
    public bool OpenRouterFirst { get; set; } = true;
    /// <summary>OpenRouter models tried one after another, comma-separated: every free model that can use tools, biggest first.</summary>
    public string OpenRouterModels { get; set; } = "nvidia/nemotron-3-ultra-550b-a55b:free,google/gemma-4-31b-it:free,thinkingmachines/inkling:free,"
        + "nvidia/nemotron-3.5-super-vl:free,google/gemma-4-26b-a4b-it:free,dots-studio/dots-3-note-preview:free,xingchen-agi/xing4.0-29b-a4b:free,"
        + "thinkingmachines/inkling-small:free,nvidia/nemotron-3.5-lightning:free,nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free,"
        + "recursive/flint-v1.0:free,cohere/north-mini-code:free,liquid/lfm-2.5-2.6b:free,openrouter/free";
    public string Model { get; set; } = "claude-haiku-5-5";
    /// <summary>Questions a company may ask a month unless the platform owner sets its own number.</summary>
    public int MessagesPerMonth { get; set; } = 1000;
}

public record AiChatTurn(string Role, string Text);
public record AiAction(string Kind, string Summary, JsonElement Payload);
public record AiReply(string Reply, List<AiAction> Actions, int Remaining);
public record AiStatus(bool Enabled, int Limit, int Used);
public record AiFinding(string Title, string Detail, string Level, string Action);
public record AiCvScreen(int Score, string Summary, List<string> Strengths, List<string> Gaps, List<string> Questions, bool FromCv);
public record AiInsights(string Headline, List<AiFinding> Findings, DateTime MadeAtUtc);

/// <summary>
/// The in-app HR assistant. Claude answers from the person's own data by calling this API's read endpoints with the
/// person's own sign-in, so it can never see more than they can. Changes are only proposed: the page shows each one
/// with a Confirm button and the person's click sends the normal request.
/// </summary>
public partial class AiAssistantService(AiOptions options, HrmsDbContext db, ICurrentUser me, TenantClock clock, IHttpClientFactory http, IMemoryCache cache,
    ILogger<AiAssistantService> log)
{
    const int MaxRounds = 6;
    const int MaxResultChars = 12_000;

    /// <summary>What the assistant may look up: key → API path and a description for the model.</summary>
    static readonly Dictionary<string, (string Path, string About)> Sources = new()
    {
        ["my_profile"] = ("/api/employees/me", "The person's own employee record: code, department, designation, manager, joining date, shift."),
        ["my_leave_balances"] = ("/api/leave/balances", "Leave balances per leave type for a year (pass year)."),
        ["my_leave_requests"] = ("/api/leave/my", "The person's leave requests and their status."),
        ["leave_types"] = ("/api/setup/leave-types", "Company leave types with ids and codes. Needed before proposing a leave request."),
        ["leave_reasons"] = ("/api/leave-rules/reasons", "Reasons per leave type (leaveTypeId, id, name). When a leave type has active reasons, a leave request must carry one as reasonId: pick the closest to what the person said, or ask."),
        ["holidays"] = ("/api/setup/holidays", "Public and company holidays for a year (pass year)."),
        ["my_attendance_month"] = ("/api/attendance/my", "Day-by-day attendance for a month: check-in/out, late minutes, status (pass year and month)."),
        ["today"] = ("/api/attendance/today", "Today's check-in/out, shift and whether checking in is allowed."),
        ["my_attendance_requests"] = ("/api/attendance/requests/my", "Missed punch, time correction and work-from-home requests."),
        ["my_payslips"] = ("/api/payroll/my-payslips", "The person's payslips with gross, deductions, tax and net pay by month."),
        ["my_funds"] = ("/api/funds/me", "Provident fund and gratuity balances."),
        ["my_loans"] = ("/api/pay/loans/my", "Loans and their installments."),
        ["my_pay_requests"] = ("/api/pay/requests/my", "Overtime, advance salary and other pay requests."),
        ["my_expenses"] = ("/api/expenses/my", "Expense claims and their status."),
        ["my_assets"] = ("/api/assets/my", "Company assets the person holds and asset requests."),
        ["my_tickets"] = ("/api/helpdesk/tickets/my", "The person's helpdesk tickets."),
        ["helpdesk_categories"] = ("/api/helpdesk/categories", "Helpdesk categories with ids. Needed before proposing a ticket."),
        ["letter_templates"] = ("/api/letters/self-service", "Letters the person may request (salary certificate, experience letter…) with ids."),
        ["asset_types"] = ("/api/assets/types", "Kinds of company assets (laptop, SIM…) with ids, for an asset request."),
        ["loan_types"] = ("/api/pay/loan-types", "Kinds of loan with limits, markup and fee; a loan request must name one when any exist."),
        ["my_training"] = ("/api/training/my", "Training sessions the person is booked on or completed."),
        ["announcements"] = ("/api/announcements", "Company announcements and news."),
        ["approvals_inbox"] = ("/api/approvals/inbox", "Requests waiting for this person's approval (managers and HR)."),
        ["dashboard"] = ("/api/dashboard", "The person's home dashboard summary; for HR and admins it includes company-wide counts."),
    };

    static readonly string[] ActionKinds = ["apply_leave", "attendance_request", "helpdesk_ticket", "decide_approval", "expense_claim", "loan_request",
        "letter_request", "asset_request", "training_request"];

    /// <summary>The person's everyday data, loaded before the first model call so most questions need no lookup round.</summary>
    string preloaded = "";

    static readonly string[] Preload = ["my_profile", "my_leave_balances", "leave_types", "leave_reasons", "holidays", "today"];

    async Task PreloadAsync(HttpClient api)
    {
        var parts = await Task.WhenAll(Preload.Select(async key =>
        {
            var (content, isError) = await RunToolAsync(api, "lookup", new Dictionary<string, JsonElement> { ["source"] = JsonSerializer.SerializeToElement(key) }, []);
            return isError ? "" : $"{key}: {(content.Length > 4000 ? content[..4000] : content)}";
        }));
        preloaded = string.Join("\n", parts.Where(p => p.Length > 0));
    }

    public bool Enabled => !string.IsNullOrWhiteSpace(options.ApiKey) || !string.IsNullOrWhiteSpace(options.GeminiApiKey) || HasOpenRouter;

    public async Task<AiStatus> StatusAsync()
    {
        var (limit, used) = await AllowanceAsync();
        return new AiStatus(Enabled, limit, used);
    }

    async Task<(int Limit, int Used)> AllowanceAsync()
    {
        var own = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.AiMessagesPerMonth).FirstOrDefaultAsync();
        var today = await clock.TodayAsync();
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-14);
        var used = await db.AiMessageLogs.CountAsync(x => x.CreatedAtUtc >= monthStart && x.Error == null);
        return (own ?? options.MessagesPerMonth, used);
    }

    /// <summary>
    /// A short written read of the company's numbers for HR: what changed, what to watch, what to do. Built from the
    /// same dashboard and trend data HR already sees; one AI call a day per company, cached.
    /// </summary>
    public async Task<AiInsights> InsightsAsync(string baseUrl, string bearer, bool refresh)
    {
        if (!Enabled) throw new DomainException("The AI assistant is not switched on for this site yet.");
        var today = await clock.TodayAsync();
        var key = $"ai-insights:{db.CurrentTenantId}:{today:yyyyMMdd}";
        if (!refresh && cache.TryGetValue(key, out AiInsights? cached) && cached is not null) return cached;
        var (limit, used) = await AllowanceAsync();
        if (used >= limit) throw new DomainException("Your company has used this month's AI questions. Ask your administrator to raise the limit.");

        var api = http.CreateClient();
        api.BaseAddress = new Uri(baseUrl);
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        api.Timeout = TimeSpan.FromSeconds(30);
        var last = today.AddMonths(-1);
        var data = new Dictionary<string, string>();
        foreach (var (name, path) in new[]
        {
            ("dashboard", "/api/dashboard"),
            ("monthly_trends", "/api/dashboard/trends"),
            ("attendance_penalties_last_month", $"/api/attendance-rules/penalties/preview?year={last.Year}&month={last.Month}"),
            ("waiting_approvals", "/api/approvals/inbox"),
        })
        {
            var res = await api.GetAsync(path);
            if (!res.IsSuccessStatusCode) continue;
            var body = await res.Content.ReadAsStringAsync();
            data[name] = body.Length > MaxResultChars ? body[..MaxResultChars] : body;
        }

        var company = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.Name).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            AiInsights? g = null;
            if (UseGemini)
                try { g = await GeminiInsightsAsync(company, today, data); }
                catch (AiUnavailableException) when (HasOpenRouter) { }
            g ??= await OpenRouterInsightsAsync(company, today, data);
            g = g with { MadeAtUtc = DateTime.UtcNow };
            cache.Set(key, g, TimeSpan.FromHours(24));
            return g;
        }
        var entry = new AiMessageLog { UserId = me.UserId ?? Guid.Empty };
        string text;
        try
        {
            var client = new AnthropicClient { ApiKey = options.ApiKey };
            static JsonElement J(object o) => JsonSerializer.SerializeToElement(o);
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = options.Model,
                MaxTokens = 3000,
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Low,
                    Format = new JsonOutputFormat
                    {
                        Schema = new Dictionary<string, JsonElement>
                        {
                            ["type"] = J("object"),
                            ["properties"] = J(new
                            {
                                headline = new { type = "string" },
                                findings = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            title = new { type = "string" },
                                            detail = new { type = "string" },
                                            level = new { type = "string", @enum = new[] { "good", "watch", "act" } },
                                            action = new { type = "string" },
                                        },
                                        required = new[] { "title", "detail", "level", "action" },
                                        additionalProperties = false,
                                    },
                                },
                            }),
                            ["required"] = J(new[] { "headline", "findings" }),
                            ["additionalProperties"] = J(false),
                        },
                    },
                },
                System = $"""
                    You write a short weekly read of {company}'s HR numbers for its HR team and CEO. Today is {today:d MMMM yyyy}.
                    Use only the JSON data given. Give a one-sentence headline and 3 to 6 findings: changes in headcount,
                    joiners and leavers, absence, lateness, leave, penalties, pending approvals or payroll cost that matter.
                    Each finding: a short title, one or two sentences of detail with the actual numbers, a level (good, watch
                    or act) and one concrete next step. Plain English, no jargon. Do not invent numbers; skip what the data lacks.
                    """,
                Messages = [new UserMessageParam { Content = JsonSerializer.Serialize(data) }],
            });
            entry.InputTokens = (int)response.Usage.InputTokens;
            entry.OutputTokens = (int)response.Usage.OutputTokens;
            text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(b => b.Text));
        }
        catch (AnthropicApiException ex)
        {
            log.LogWarning(ex, "AI insights call failed");
            entry.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            db.AiMessageLogs.Add(entry);
            await db.SaveChangesAsync();
            throw new DomainException("The AI summary could not be made right now. Please try again later.");
        }
        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();

        var result = JsonSerializer.Deserialize<AiInsights>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new DomainException("The AI summary could not be read. Please try again.");
        result = result with { MadeAtUtc = DateTime.UtcNow };
        cache.Set(key, result, TimeSpan.FromHours(24));
        return result;
    }

    /// <summary>Reads a candidate's CV against the job and scores the fit. One AI call; cached for a day per application.</summary>
    public async Task<AiCvScreen> ScreenCvAsync(Guid applicationId, AttachmentService files, bool refresh)
    {
        if (!Enabled) throw new DomainException("The AI assistant is not switched on for this site yet.");
        var key = $"ai-cv:{applicationId}";
        if (!refresh && cache.TryGetValue(key, out AiCvScreen? cached) && cached is not null) return cached;
        var app = await db.JobApplications.Include(a => a.JobPost).Include(a => a.Candidate).FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new HRMS.Api.Infrastructure.NotFoundException("Application");
        var (limit, used) = await AllowanceAsync();
        if (used >= limit) throw new DomainException("Your company has used this month's AI questions. Ask your administrator to raise the limit.");

        var c = app.Candidate!;
        var cvId = await db.Attachments.Where(x => x.OwnerType == AttachmentOwner.Candidate && x.OwnerId == c.Id && x.ContentType == "application/pdf")
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(options.ApiKey) && (UseGemini || HasOpenRouter))
        {
            var pdf = cvId is { } gid ? (await files.ReadAsync(gid)).Data : null;
            var brief = $"""
                Job: {app.JobPost!.Title}
                Description: {app.JobPost.Description}
                Requirements: {app.JobPost.Requirements}

                Candidate form: {c.FirstName} {c.LastName}; current title {c.CurrentTitle}; {c.ExperienceYears} years' experience; skills: {c.Skills}.
                """;
            AiCvScreen? g = null;
            if (UseGemini)
                try { g = await GeminiCvAsync(brief + (pdf is null ? "No PDF CV was uploaded; judge from the form only and say so in the summary." : "The CV is attached."), pdf); }
                catch (AiUnavailableException) when (HasOpenRouter) { }
            // OpenRouter's free models take text only: send the words read from the PDF instead of the file.
            var cvText = pdf is null ? "" : CvReader.Text("cv.pdf", pdf);
            if (cvText.Length > 20000) cvText = cvText[..20000];
            g ??= await OpenRouterCvAsync(brief + (cvText.Length == 0
                ? "No readable PDF CV; judge from the form only and say so in the summary."
                : "CV text:\n" + cvText));
            g = g with { Score = Math.Clamp(g.Score, 0, 100), FromCv = pdf is not null };
            cache.Set(key, g, TimeSpan.FromHours(24));
            return g;
        }
        var content = new List<ContentBlockParam>();
        if (cvId is { } id)
        {
            var (_, data) = await files.ReadAsync(id);
            content.Add(new DocumentBlockParam { Source = new Base64PdfSource { Data = Convert.ToBase64String(data) } });
        }
        content.Add(new TextBlockParam
        {
            Text = $"""
                Job: {app.JobPost!.Title}
                Description: {app.JobPost.Description}
                Requirements: {app.JobPost.Requirements}

                Candidate form: {c.FirstName} {c.LastName}; current title {c.CurrentTitle}; {c.ExperienceYears} years' experience; skills: {c.Skills}.
                {(cvId is null ? "No PDF CV was uploaded; judge from the form only and say so in the summary." : "The CV is attached.")}
                """,
        });

        var entry = new AiMessageLog { UserId = me.UserId ?? Guid.Empty };
        string text;
        try
        {
            static JsonElement J(object o) => JsonSerializer.SerializeToElement(o);
            var list = new { type = "array", items = new { type = "string" } };
            var response = await new AnthropicClient { ApiKey = options.ApiKey }.Messages.Create(new MessageCreateParams
            {
                Model = options.Model,
                MaxTokens = 3000,
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Low,
                    Format = new JsonOutputFormat
                    {
                        Schema = new Dictionary<string, JsonElement>
                        {
                            ["type"] = J("object"),
                            ["properties"] = J(new { score = new { type = "integer" }, summary = new { type = "string" }, strengths = list, gaps = list, questions = list }),
                            ["required"] = J(new[] { "score", "summary", "strengths", "gaps", "questions" }),
                            ["additionalProperties"] = J(false),
                        },
                    },
                },
                System = """
                    You screen job applicants for a recruiter. Compare the candidate with the job and give: score 0-100 for fit,
                    a two-sentence summary, up to 4 strengths and up to 4 gaps (each one short line, with evidence from the CV),
                    and 3 to 5 interview questions that probe the gaps. Judge only skills, experience and qualifications;
                    ignore age, gender, religion, marital status, photos and anything else unrelated to the work.
                    """,
                Messages = [new UserMessageParam { Content = content }],
            });
            entry.InputTokens = (int)response.Usage.InputTokens;
            entry.OutputTokens = (int)response.Usage.OutputTokens;
            text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(b => b.Text));
        }
        catch (AnthropicApiException ex)
        {
            log.LogWarning(ex, "AI CV screening failed");
            entry.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            db.AiMessageLogs.Add(entry);
            await db.SaveChangesAsync();
            throw new DomainException("The CV could not be read by the AI right now. Please try again later.");
        }
        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();

        var result = JsonSerializer.Deserialize<AiCvScreen>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new DomainException("The AI's answer could not be read. Please try again.");
        result = result with { Score = Math.Clamp(result.Score, 0, 100), FromCv = cvId is not null };
        cache.Set(key, result, TimeSpan.FromHours(24));
        return result;
    }

    public async Task<AiReply> AskAsync(List<AiChatTurn> turns, bool voice, string baseUrl, string bearer)
    {
        if (!Enabled) throw new DomainException("The AI assistant is not switched on for this site yet.");
        if (turns.Count == 0 || turns[^1].Role != "user") throw new DomainException("Ask a question first.");
        var (limit, used) = await AllowanceAsync();
        if (used >= limit) throw new DomainException("Your company has used this month's AI questions. Ask your administrator to raise the limit.");

        var client = new AnthropicClient { ApiKey = options.ApiKey };
        var api = http.CreateClient();
        api.BaseAddress = new Uri(baseUrl);
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        api.Timeout = TimeSpan.FromSeconds(30);
        await PreloadAsync(api);
        if (string.IsNullOrWhiteSpace(options.ApiKey) && (UseGemini || HasOpenRouter))
        {
            // Free providers: OpenRouter first while its daily allowance lasts (it answers in seconds), then Gemini.
            if (HasOpenRouter && (options.OpenRouterFirst || !UseGemini))
            {
                try { return await OpenRouterAskAsync(turns, voice, api, limit, used); }
                catch (AiUnavailableException) when (UseGemini) { }
                return await GeminiAskAsync(turns, voice, api, limit, used);
            }
            try { return await GeminiAskAsync(turns, voice, api, limit, used); }
            catch (AiUnavailableException) when (HasOpenRouter) { }
            return await OpenRouterAskAsync(turns, voice, api, limit, used);
        }

        var messages = turns.TakeLast(20)
            .Select(t => t.Role == "user" ? (MessageParam)new UserMessageParam { Content = t.Text } : new AssistantMessageParam { Content = t.Text })
            .ToList();
        var system = await SystemPromptAsync(voice);
        var actions = new List<AiAction>();
        var entry = new AiMessageLog { UserId = me.UserId ?? Guid.Empty, Voice = voice };
        var reply = "";
        try
        {
            for (var round = 0; round < MaxRounds; round++)
            {
                var response = await client.Messages.Create(new MessageCreateParams
                {
                    Model = options.Model,
                    MaxTokens = 4000,
                    OutputConfig = new OutputConfig { Effort = Effort.Low },
                    System = system,
                    Tools = Tools(),
                    Messages = messages,
                });
                entry.InputTokens += (int)response.Usage.InputTokens;
                entry.OutputTokens += (int)response.Usage.OutputTokens;

                if (response.StopReason == "refusal")
                {
                    reply = "Sorry, I can't help with that one. Please ask HR directly.";
                    break;
                }

                var echo = new List<ContentBlockParam>();
                var results = new List<ContentBlockParam>();
                var text = new List<string>();
                foreach (var block in response.Content)
                {
                    if (block.TryPickText(out var t))
                    {
                        text.Add(t.Text);
                        echo.Add(new TextBlockParam { Text = t.Text });
                    }
                    else if (block.TryPickThinking(out var th))
                        echo.Add(new ThinkingBlockParam { Thinking = th.Thinking, Signature = th.Signature });
                    else if (block.TryPickRedactedThinking(out var rt))
                        echo.Add(new RedactedThinkingBlockParam { Data = rt.Data });
                    else if (block.TryPickToolUse(out var use))
                    {
                        echo.Add(new ToolUseBlockParam { ID = use.ID, Name = use.Name, Input = use.Input });
                        entry.ToolCalls++;
                        var (content, isError) = await RunToolAsync(api, use.Name, use.Input, actions);
                        results.Add(new ToolResultBlockParam { ToolUseID = use.ID, Content = content, IsError = isError });
                    }
                }
                reply = string.Join("\n\n", text).Trim();
                if (results.Count == 0) break;
                messages.Add(new AssistantMessageParam { Content = echo });
                messages.Add(new UserMessageParam { Content = results });
            }
        }
        catch (AnthropicApiException ex)
        {
            log.LogWarning(ex, "AI assistant call failed");
            entry.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            db.AiMessageLogs.Add(entry);
            await db.SaveChangesAsync();
            throw new DomainException(ex is AnthropicUnauthorizedException
                ? "The AI assistant's key on this site is not valid. Please tell the platform administrator."
                : "The AI assistant is busy right now. Please try again in a minute.");
        }

        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();
        if (reply.Length == 0) reply = actions.Count > 0 ? "Please check the details below and confirm." : "I couldn't find an answer to that.";
        return new AiReply(reply, actions, Math.Max(0, limit - used - 1));
    }

    async Task<string> SystemPromptAsync(bool voice)
    {
        var who = await db.Users.Where(u => u.Id == me.UserId).Select(u => new { u.FullName }).FirstOrDefaultAsync();
        var company = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.Name).FirstOrDefaultAsync();
        var today = await clock.TodayAsync();
        var role = me.Role switch
        {
            UserRole.CompanyAdmin or UserRole.HrManager => "HR / administrator (can see company-wide figures on the dashboard and approve requests)",
            UserRole.Manager => "line manager (approves their team's requests)",
            _ => "employee",
        };
        var style = voice
            ? "The person is speaking and will hear your answer read aloud: answer in one to three short sentences, plain text only, no lists, tables or symbols."
            : "Keep answers short and direct. Use a short list or table only when it genuinely helps; amounts in PKR with thousands separators.";
        return $"""
            You are the HR assistant inside Qaventra, the HR system of {company}. You are talking to {who?.FullName ?? "a user"}, whose role is {role}.
            Today is {today:dddd, d MMMM yyyy}.

            Reply in the language and script the person writes in: Urdu script for Urdu script, Roman Urdu (Urdu in English letters) for Roman Urdu, English for English.
            {style}

            Answer from the person's real data. Use the lookup tool for anything about their leave, attendance, pay, loans, funds, assets,
            tickets, training, approvals or company holidays and announcements; never guess a number or a date. If a lookup has no answer,
            say so and suggest who in HR to ask. Do not discuss other employees' personal data.

            You cannot change anything yourself. When the person asks you to apply for leave, correct attendance, raise a helpdesk ticket or
            approve/reject a request, claim an expense, ask for a loan, a letter, an asset or a training, look up whatever ids you need, then
            call propose_action with the exact details. The page shows it with
            a Confirm button; say briefly what you prepared and ask them to confirm. Never claim a request was submitted. If a required detail
            (dates, leave type, reason) is missing, ask for it first. Work out relative dates ("kal", "tomorrow", "next Monday") from today's date.
            """ + (preloaded.Length == 0 ? "" : $"""


            Already loaded for this person (use it directly; call lookup only for anything else):
            {preloaded}
            """);
    }

    static List<ToolUnion> Tools()
    {
        static JsonElement J(object o) => JsonSerializer.SerializeToElement(o);
        return
        [
            new Tool
            {
                Name = "lookup",
                Description = "Read the person's own HR data from the system. Sources: "
                    + string.Join(" ", Sources.Select(s => $"{s.Key}: {s.Value.About}")),
                InputSchema = new()
                {
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["source"] = J(new { type = "string", @enum = Sources.Keys.ToArray() }),
                        ["year"] = J(new { type = "integer", description = "For my_leave_balances, holidays and my_attendance_month." }),
                        ["month"] = J(new { type = "integer", description = "1-12, for my_attendance_month." }),
                    },
                    Required = ["source"],
                },
            },
            new Tool
            {
                Name = "propose_action",
                Description = """
                    Prepare a change for the person to confirm; nothing is saved until they press Confirm. kind and payload:
                    apply_leave: {"leaveTypeId": id from leave_types, "fromDate": "yyyy-MM-dd", "toDate": "yyyy-MM-dd", "reason": text, "reasonId": id from leave_reasons when that type has reasons}.
                    attendance_request: {"date": "yyyy-MM-dd", "kind": "MissedPunch" | "TimeCorrection" | "WorkFromHome", "checkIn": "HH:mm:ss", "checkOut": "HH:mm:ss", "reason": text} (past or today only).
                    helpdesk_ticket: {"categoryId": id from helpdesk_categories, "subject": text, "description": text, "priority": "Low" | "Medium" | "High" | "Urgent"}.
                    decide_approval: {"requestType": type from approvals_inbox, "subjectId": id from approvals_inbox, "approve": true | false, "note": text or null}.
                    expense_claim: {"title": text, "items": [{"date": "yyyy-MM-dd", "category": "Travel" | "Fuel" | "Meals" | "Accommodation" | "Medical" | "Mobile" | "OfficeSupplies" | "Training" | "Other", "description": text, "amount": number}]}.
                    loan_request: {"title": text, "amount": number, "installments": months, "startYear": number, "startMonth": 1-12, "reason": text, "loanTypeId": id from loan_types when the company has types}.
                    letter_request: {"templateId": id from letter_templates, "reason": text}.
                    asset_request: {"typeId": id from asset_types, "reason": text, "neededBy": "yyyy-MM-dd" or null}.
                    training_request: {"courseName": text, "provider": text, "cost": number or null, "reason": text}.
                    summary is one line in the person's language describing exactly what will be submitted.
                    """,
                InputSchema = new()
                {
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["kind"] = J(new { type = "string", @enum = ActionKinds }),
                        ["summary"] = J(new { type = "string" }),
                        ["payload"] = J(new { type = "object" }),
                    },
                    Required = ["kind", "summary", "payload"],
                },
            },
        ];
    }

    async Task<(string Content, bool IsError)> RunToolAsync(HttpClient api, string name, IReadOnlyDictionary<string, JsonElement> input, List<AiAction> actions)
    {
        try
        {
            if (name == "propose_action")
            {
                var kind = input.TryGetValue("kind", out var k) ? k.GetString() ?? "" : "";
                if (!ActionKinds.Contains(kind)) return ($"Unknown kind '{kind}'.", true);
                if (!input.TryGetValue("payload", out var payload) || payload.ValueKind != JsonValueKind.Object) return ("payload must be an object.", true);
                var summary = input.TryGetValue("summary", out var s) ? s.GetString() ?? "" : "";
                actions.Add(new AiAction(kind, summary, payload.Clone()));
                return ("Shown to the person with a Confirm button. It is not submitted until they confirm.", false);
            }
            if (name != "lookup") return ($"Unknown tool '{name}'.", true);

            var key = input.TryGetValue("source", out var src) ? src.GetString() ?? "" : "";
            if (!Sources.TryGetValue(key, out var source)) return ($"Unknown source '{key}'.", true);
            var today = await clock.TodayAsync();
            int Num(string field, int fallback) => input.TryGetValue(field, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;
            var path = key switch
            {
                "my_leave_balances" or "holidays" => $"{source.Path}?year={Num("year", today.Year)}",
                "my_attendance_month" => $"{source.Path}?year={Num("year", today.Year)}&month={Num("month", today.Month)}",
                _ => source.Path,
            };
            var res = await api.GetAsync(path);
            var body = await res.Content.ReadAsStringAsync();
            if (res.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
                return ("This person cannot see that information.", true);
            if (!res.IsSuccessStatusCode) return ($"The lookup failed ({(int)res.StatusCode}).", true);
            return (body.Length > MaxResultChars ? body[..MaxResultChars] + " …(cut)" : body, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            log.LogWarning(ex, "AI tool {Tool} failed", name);
            return ("The lookup failed; try again or answer without it.", true);
        }
    }
}
