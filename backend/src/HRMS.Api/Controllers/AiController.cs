using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Api.Controllers;

public record AiTurnRequest([Required, RegularExpression("user|assistant")] string Role, [Required, StringLength(4000)] string Text);
public record AiAskRequest([Required, MinLength(1), MaxLength(40)] List<AiTurnRequest> Messages, bool Voice = false);

/// <summary>The AI HR assistant: answers questions from the person's own data and prepares requests for them to confirm.</summary>
[ApiController]
[Route("api/ai")]
[Authorize(Policy = Policies.CompanyUser)]
public class AiController(AiAssistantService ai, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("status")]
    public Task<AiStatus> Status() => ai.StatusAsync();

    [HttpPost("ask")]
    public Task<AiReply> Ask(AiAskRequest req) =>
        ai.AskAsync(req.Messages.Select(m => new AiChatTurn(m.Role, m.Text)).ToList(), req.Voice, BaseUrl(), Bearer());

    [HttpGet("insights"), Authorize(Policy = Policies.Hr)]
    public Task<AiInsights> Insights([FromQuery] bool refresh = false) => ai.InsightsAsync(BaseUrl(), Bearer(), refresh);

    // The site redirects http to https, and a redirect would drop the sign-in header on the assistant's lookups.
    string BaseUrl() => $"{(env.IsDevelopment() ? Request.Scheme : "https")}://{Request.Host}";

    string Bearer()
    {
        var value = Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..] : value;
    }
}
