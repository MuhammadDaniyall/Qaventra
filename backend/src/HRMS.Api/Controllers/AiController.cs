using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Api.Controllers;

public record AiTurnRequest([Required, RegularExpression("user|assistant")] string Role, [Required, StringLength(4000)] string Text);
public record AiWriteRequest([Required, StringLength(30)] string Kind, [StringLength(2000)] string? Brief, [StringLength(12000)] string? Draft,
    [StringLength(60)] string? Tone, [StringLength(5)] string? Language);
public record AiPolicyRequest([Required, StringLength(500, MinimumLength = 3)] string Question, [StringLength(5)] string? Language);
public record AiReportRequest([StringLength(500)] string? Question, [StringLength(40)] string? Report, [StringLength(80)] string? Department,
    [StringLength(80)] string? Location, DateOnly? From, DateOnly? To, [StringLength(30)] string? Status);
public record AiAskRequest([Required, MinLength(1), MaxLength(40)] List<AiTurnRequest> Messages, bool Voice = false);

/// <summary>The AI HR assistant: answers questions from the person's own data and prepares requests for them to confirm.</summary>
[ApiController]
[Route("api/ai")]
[Authorize(Policy = Policies.CompanyUser)]
public class AiController(AiAssistantService ai, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>Whether this server can reach the AI providers and its own API; yes/no and timings only, no secrets.</summary>
    [HttpGet("health"), AllowAnonymous]
    public Task<object> Health() => ai.HealthAsync(BaseUrl());

    [HttpGet("status")]
    public Task<AiStatus> Status() => ai.StatusAsync();

    [HttpPost("ask")]
    public Task<AiReply> Ask(AiAskRequest req) =>
        ai.AskAsync(req.Messages.Select(m => new AiChatTurn(m.Role, m.Text)).ToList(), req.Voice, BaseUrl(), Bearer());

    [HttpGet("insights"), Authorize(Policy = Policies.Hr)]
    public Task<AiInsights> Insights([FromQuery] bool refresh = false) => ai.InsightsAsync(BaseUrl(), Bearer(), refresh);

    [HttpPost("cv/{applicationId:guid}"), Authorize(Policy = Policies.Hr)]
    public Task<AiCvScreen> ScreenCv(Guid applicationId, [FromServices] AttachmentService files, [FromQuery] bool refresh = false) =>
        ai.ScreenCvAsync(applicationId, files, refresh);

    /// <summary>Writes or improves HR text (announcements, job ads, letters, policies, reviews) for the person to edit.</summary>
    /// <summary>Answers a question only from the company's own policies, leave settings and holidays, naming the source.</summary>
    [HttpPost("policy")]
    public Task<AiPolicyAnswer> Policy(AiPolicyRequest req) => ai.AskPolicyAsync(req.Question, req.Language);

    /// <summary>The fixed list of reports the AI may choose from.</summary>
    [HttpGet("reports"), Authorize(Policy = Policies.Hr)]
    public List<AiReportInfo> Reports() => AiAssistantService.ReportCatalogue;

    /// <summary>A question in plain words becomes one whitelisted report with filters; the server runs the query.</summary>
    [HttpPost("report"), Authorize(Policy = Policies.Hr)]
    public Task<AiReportResult> Report(AiReportRequest req) =>
        ai.RunReportAsync(req.Question, req.Report, req.Department, req.Location, req.From, req.To, req.Status);

    [HttpPost("write")]
    public Task<AiWriteResult> Write(AiWriteRequest req) => ai.WriteAsync(req.Kind, req.Brief, req.Draft, req.Tone, req.Language);

    // The site redirects http to https, and a redirect would drop the sign-in header on the assistant's lookups.
    string BaseUrl() => $"{(env.IsDevelopment() ? Request.Scheme : "https")}://{Request.Host}";

    string Bearer()
    {
        var value = Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..] : value;
    }
}
