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
    public async Task<AiReply> Ask(AiAskRequest req)
    {
        var bearer = Request.Headers.Authorization.ToString();
        bearer = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? bearer[7..] : bearer;
        // The site redirects http to https, and a redirect would drop the sign-in header on the assistant's lookups.
        var baseUrl = $"{(env.IsDevelopment() ? Request.Scheme : "https")}://{Request.Host}";
        return await ai.AskAsync(req.Messages.Select(m => new AiChatTurn(m.Role, m.Text)).ToList(), req.Voice, baseUrl, bearer);
    }
}
