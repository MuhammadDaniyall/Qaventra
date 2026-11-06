using HRMS.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record AiWriteResult(string? Title, string Text, int Remaining);

file sealed class AiWriteJson
{
    public string? Title { get; set; }
    public string? Text { get; set; }
}

public partial class AiAssistantService
{
    /// <summary>What each writing helper is for, and how the free model should write it.</summary>
    static readonly Dictionary<string, string> WriteKinds = new()
    {
        ["announcement"] = "a company announcement for all staff. Warm, clear, short paragraphs. Title under 80 characters.",
        ["job_ad"] = "a job advert for a careers page: a short intro about the role, what the person will do, and why join. No requirements list (that is separate). Title is the job title.",
        ["requirements"] = "the requirements for a job advert, one per line, no bullets or numbering, 5 to 8 lines. No title.",
        ["letter"] = "a formal HR letter on company letterhead. Keep any {{placeholders}} in the draft exactly as written. Title is the letter's subject.",
        ["policy"] = "a company HR policy: purpose, who it covers, the rules, and what happens if they are broken. Plain headings. Title is the policy name.",
        ["review"] = "a manager's performance review comment about an employee: balanced, specific, kind, with one or two things to improve. No title.",
        ["email"] = "a short professional email from HR. Title is the email subject.",
        ["improve"] = "a better version of the draft: same meaning, clearer, correct grammar, professional. Keep the same language. No title unless the draft has one.",
    };

    /// <summary>
    /// Writes or improves a piece of HR text from a short brief, on the free models only. The person always edits and
    /// saves it themselves; nothing is sent or saved here.
    /// </summary>
    public async Task<AiWriteResult> WriteAsync(string kind, string? brief, string? draft, string? tone, string? language)
    {
        if (!WriteKinds.TryGetValue(kind, out var what)) throw new DomainException("Unknown writing kind.");
        if (!UseGemini && !HasOpenRouter) throw new DomainException("AI is not turned on for this server.");
        if (string.IsNullOrWhiteSpace(brief) && string.IsNullOrWhiteSpace(draft))
            throw new DomainException("Write a line about what you need.");
        var company = await db.Tenants.Where(t => t.Id == me.TenantId).Select(t => t.Name).FirstOrDefaultAsync() ?? "the company";
        var lang = language == "ur" ? "Urdu (in Urdu script)" : "English";
        var system =
            $"You write HR text for {company}, a company in Pakistan. Write {what} " +
            $"Tone: {(string.IsNullOrWhiteSpace(tone) ? "professional and friendly" : tone)}. Write in {lang}. " +
            "Plain text only, no markdown symbols like ** or #. Never invent names, numbers, dates or amounts that are not given; " +
            "leave a clear [placeholder] instead. Reply as JSON: {\"title\": string or null, \"text\": string}.";
        var data = $"Brief: {brief?.Trim()}\n\nCurrent draft (may be empty):\n{draft?.Trim()}";
        var answer = await FreeJsonAsync<AiWriteJson>(system, data);
        var (limit, used) = await AllowanceAsync();
        if (answer is null || string.IsNullOrWhiteSpace(answer.Text))
            throw new DomainException(used >= limit
                ? "This month's AI allowance is used up."
                : "The AI did not answer. Please try again in a minute.");
        return new AiWriteResult(string.IsNullOrWhiteSpace(answer.Title) ? null : answer.Title.Trim(), answer.Text.Trim(), Math.Max(0, limit - used));
    }
}
