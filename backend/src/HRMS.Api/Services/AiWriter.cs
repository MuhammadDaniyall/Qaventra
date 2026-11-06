using HRMS.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record AiWriteResult(string? Title, string Text, int Remaining);

file sealed class AiWriteJson
{
    public string? Title { get; set; }
    public string? Text { get; set; }
}

public record AiPayslipExplain(string Summary, List<string> Points, List<string> Changes);

file sealed class AiPayslipJson
{
    public string? Summary { get; set; }
    public List<string>? Points { get; set; }
    public List<string>? Changes { get; set; }
}

public partial class AiAssistantService
{
    /// <summary>
    /// A payslip in plain words: what was earned, what was taken and why, and what changed since the last one. The
    /// changes are worked out here, not by the model, so the numbers are always right; the model only phrases them.
    /// </summary>
    public async Task<AiPayslipExplain> ExplainPayslipAsync(HRMS.Api.Controllers.PayslipDto slip, HRMS.Api.Controllers.PayslipDto? prev, string? language)
    {
        static string Money(decimal v) => $"PKR {v:N0}";
        var changes = new List<string>();
        if (prev is not null)
        {
            if (slip.NetPay != prev.NetPay)
                changes.Add($"Net pay {(slip.NetPay > prev.NetPay ? "went up" : "went down")} by {Money(Math.Abs(slip.NetPay - prev.NetPay))} (from {Money(prev.NetPay)} to {Money(slip.NetPay)}).");
            var before = prev.Lines.GroupBy(l => l.Name).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
            var now = slip.Lines.GroupBy(l => l.Name).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
            foreach (var name in now.Keys.Union(before.Keys))
            {
                var a = before.GetValueOrDefault(name);
                var b = now.GetValueOrDefault(name);
                if (a == b) continue;
                changes.Add(a == 0 ? $"New this time: {name} {Money(b)}." : b == 0 ? $"Not there this time: {name} (was {Money(a)})." : $"{name}: {Money(a)} → {Money(b)}.");
            }
            if (slip.UnpaidDays != prev.UnpaidDays) changes.Add($"Unpaid days: {prev.UnpaidDays:0.#} → {slip.UnpaidDays:0.#}.");
            if (slip.IncomeTax != prev.IncomeTax) changes.Add($"Income tax: {Money(prev.IncomeTax)} → {Money(slip.IncomeTax)}.");
        }
        var points = slip.Lines.Select(l => $"{l.Name} ({l.Kind}): {Money(l.Amount)}").ToList();
        var plain = new AiPayslipExplain(
            $"For {slip.Label ?? $"{slip.Month}/{slip.Year}"}, gross pay was {Money(slip.GrossEarnings)} and net pay {Money(slip.NetPay)} after tax {Money(slip.IncomeTax)}, EOBI {Money(slip.EobiEmployee)} and other deductions {Money(slip.OtherDeductions)}.",
            points, changes);

        var data = System.Text.Json.JsonSerializer.Serialize(new
        {
            period = slip.Label ?? $"{slip.Month}/{slip.Year}",
            slip.DaysInMonth, slip.PaidDays, slip.UnpaidDays, slip.GrossEarnings, slip.TaxableIncome, slip.IncomeTax,
            slip.EobiEmployee, slip.OtherDeductions, slip.NetPay,
            lines = slip.Lines.Select(l => new { l.Name, kind = l.Kind.ToString(), l.Amount }),
            changesSinceLastPayslip = changes,
        });
        var lang = language == "ur" ? "Urdu (in Urdu script)" : "simple English";
        var answer = await FreeJsonAsync<AiPayslipJson>(
            "You explain a Pakistani employee's payslip to them in " + lang + ", kindly and simply, as if to someone who is not an accountant. " +
            "Use only the numbers given; never invent or recalculate amounts. Explain income tax (FBR salary slabs), EOBI and any deduction in one plain sentence each. " +
            "If there are changes since the last payslip, explain each likely reason in plain words, but say 'probably' when unsure. " +
            "Reply as JSON: {\"summary\": 2-3 sentences, \"points\": [one short line per earning or deduction], \"changes\": [one short line per change, empty if none]}.",
            data);
        if (answer is null || string.IsNullOrWhiteSpace(answer.Summary)) return plain;
        return new AiPayslipExplain(answer.Summary.Trim(), answer.Points?.Where(p => !string.IsNullOrWhiteSpace(p)).Take(20).ToList() ?? points,
            answer.Changes?.Where(c => !string.IsNullOrWhiteSpace(c)).Take(20).ToList() ?? changes);
    }

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
