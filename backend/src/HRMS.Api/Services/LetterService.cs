using System.Globalization;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public class LetterService(HrmsDbContext db, ICurrentUser me, TenantClock clock, NotificationService notify, ApprovalEngine approvals)
{
    private static readonly CultureInfo Pk = CultureInfo.GetCultureInfo("en-PK");

    /// <summary>Gives a company the standard templates the first time it opens letters (or after deleting them all).</summary>
    public async Task EnsureDefaultTemplatesAsync()
    {
        if (await db.LetterTemplates.AnyAsync())
            return;
        db.LetterTemplates.AddRange(DefaultTemplates.All.Select(t => new LetterTemplate
        {
            Name = t.Name, Category = t.Category, Subject = t.Subject, Body = t.Body,
        }));
        await db.SaveChangesAsync();
    }

    public async Task<(string Subject, string Body)> RenderAsync(Guid employeeId, string subject, string body, IReadOnlyDictionary<string, string?>? extra = null)
    {
        var values = await ValuesForAsync(employeeId);
        foreach (var (k, v) in extra ?? new Dictionary<string, string?>())
            values[k] = v;
        return (LetterRenderer.Render(subject, values), LetterRenderer.Render(body, values));
    }

    /// <summary>
    /// Issues a letter. From a template that needs approval it waits for the letter approval chain (hidden from the
    /// employee until approved); <paramref name="extra"/> overrides placeholder values (e.g. the new salary).
    /// </summary>
    public async Task<IssuedLetter> IssueAsync(Guid employeeId, LetterCategory category, string subject, string body, DateOnly? issuedOn, bool shareWithEmployee,
        Guid? templateId = null, IReadOnlyDictionary<string, string?>? extra = null, string? requestReason = null)
    {
        var template = templateId is { } tid ? await db.LetterTemplates.FirstOrDefaultAsync(t => t.Id == tid) ?? throw new NotFoundException("Template") : null;
        var unknown = LetterRenderer.UnknownKeys(subject + "\n" + body);
        if (unknown.Count > 0)
            throw new DomainException($"Unknown placeholder(s): {string.Join(", ", unknown.Select(k => "{{" + k + "}}"))}.");

        var (renderedSubject, renderedBody) = await RenderAsync(employeeId, subject, body, extra);
        var date = issuedOn ?? await clock.TodayAsync();
        var prefix = $"HR/{LetterRenderer.CategoryCode(category)}/{date.Year}/";
        var taken = await db.IssuedLetters.Where(l => l.ReferenceNo.StartsWith(prefix)).Select(l => l.ReferenceNo).ToListAsync();
        var next = taken.Select(r => int.TryParse(r[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;

        var letter = new IssuedLetter
        {
            EmployeeId = employeeId,
            Category = category,
            ReferenceNo = $"{prefix}{next:D4}",
            Subject = renderedSubject.Trim(),
            Body = renderedBody.Trim(),
            IssuedOn = date,
            IssuedByUserId = me.UserId,
            IssuedByName = await CurrentUserNameAsync(db, me),
            SharedWithEmployee = shareWithEmployee,
            TemplateId = template?.Id,
            Requested = requestReason is not null,
            RequestReason = requestReason,
            MarginTopMm = template?.MarginTopMm ?? 25,
            MarginSideMm = template?.MarginSideMm ?? 20,
            Footer = template?.Footer,
        };
        db.IssuedLetters.Add(letter);
        if (template is { RequiresApproval: true } || requestReason is not null)
        {
            letter.Status = LetterStatus.Pending;
            letter.SharedWithEmployee = false;
            var approval = await approvals.StartAsync(ApprovalRequestType.Letter, letter.Id, employeeId,
                $"{letter.Subject} ({letter.ReferenceNo}){(requestReason is null ? "" : $" · {requestReason}")}");
            if (approval.Status == ApprovalStatus.Approved)
                await ReleaseAsync(letter, shareWithEmployee || requestReason is not null);
        }
        else if (shareWithEmployee)
            await notify.ToEmployeesAsync([employeeId], NotificationKind.Letter, $"New letter: {letter.Subject}", letter.ReferenceNo, $"/letters/{letter.Id}");
        await db.SaveChangesAsync();
        return letter;
    }

    private async Task ReleaseAsync(IssuedLetter letter, bool share)
    {
        letter.Status = LetterStatus.Issued;
        letter.SharedWithEmployee = share;
        if (share)
            await notify.ToEmployeesAsync([letter.EmployeeId], NotificationKind.Letter, $"New letter: {letter.Subject}", letter.ReferenceNo, $"/letters/{letter.Id}");
    }

    /// <summary>An employee asks for a self-service letter; it goes through the letter approval chain.</summary>
    public async Task<IssuedLetter> RequestAsync(Guid employeeId, Guid templateId, string reason)
    {
        var t = await db.LetterTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.IsActive && x.SelfService) ?? throw new NotFoundException("Letter");
        if (await db.IssuedLetters.AnyAsync(l => l.EmployeeId == employeeId && l.TemplateId == templateId && l.Status == LetterStatus.Pending))
            throw new DomainException("You have already asked for this letter.");
        return await IssueAsync(employeeId, t.Category, t.Subject, t.Body, null, true, t.Id, null, string.IsNullOrWhiteSpace(reason) ? "Requested" : reason.Trim());
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var letter = await db.IssuedLetters.FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Letter");
        if (letter.Status != LetterStatus.Pending) throw new DomainException("This letter is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Letter, letter.Id, letter.EmployeeId, approve, note);
        letter.DecisionNote = note;
        if (outcome == ApprovalStatus.Approved)
            await ReleaseAsync(letter, true);
        else if (outcome == ApprovalStatus.Rejected)
            letter.Status = LetterStatus.Rejected;
        await db.SaveChangesAsync();
    }

    /// <summary>Issues the company's letter for an event (job change, salary revision, exit…) when a template is set for it.</summary>
    public async Task AutoIssueAsync(Guid employeeId, LetterAutoUse use, IReadOnlyDictionary<string, string?>? extra = null)
    {
        var t = await db.LetterTemplates.FirstOrDefaultAsync(x => x.IsActive && x.AutoUse == use);
        if (t is null) return;
        await IssueAsync(employeeId, t.Category, t.Subject, t.Body, null, true, t.Id, extra);
    }

    /// <summary>The offer letter for a candidate, when an Offer template is set; replaces an earlier one for the same application.</summary>
    public async Task<CandidateLetter?> CandidateLetterAsync(JobApplication app, Candidate candidate, string jobTitle)
    {
        var t = await db.LetterTemplates.FirstOrDefaultAsync(x => x.IsActive && x.AutoUse == LetterAutoUse.Offer);
        if (t is null) return null;
        var company = await db.Tenants.Where(x => x.Id == db.CurrentTenantId).Select(x => new { x.Name, x.Address, x.City }).FirstAsync();
        var today = await clock.TodayAsync();
        var values = new Dictionary<string, string?>
        {
            ["CandidateName"] = $"{candidate.FirstName} {candidate.LastName}".Trim(),
            ["EmployeeName"] = $"{candidate.FirstName} {candidate.LastName}".Trim(),
            ["FirstName"] = candidate.FirstName,
            ["JobTitle"] = jobTitle,
            ["Designation"] = jobTitle,
            ["OfferedSalary"] = app.OfferedSalary is { } s ? Money(s) : null,
            ["GrossSalary"] = app.OfferedSalary is { } g ? Money(g) : null,
            ["OfferedJoiningDate"] = app.OfferedJoiningDate is { } d ? LongDate(d) : null,
            ["JoiningDate"] = app.OfferedJoiningDate is { } j ? LongDate(j) : null,
            ["CompanyName"] = company.Name,
            ["CompanyAddress"] = FullAddress(company.Address, company.City),
            ["Today"] = LongDate(today),
        };
        db.CandidateLetters.RemoveRange(await db.CandidateLetters.Where(c => c.ApplicationId == app.Id).ToListAsync());
        var prefix = $"HR/OFR/{today.Year}/C";
        var taken = await db.CandidateLetters.Where(l => l.ReferenceNo.StartsWith(prefix)).CountAsync();
        var letter = new CandidateLetter
        {
            ApplicationId = app.Id, CandidateName = values["CandidateName"]!, ReferenceNo = $"{prefix}{taken + 1:D4}",
            Subject = LetterRenderer.Render(t.Subject, values).Trim(), Body = LetterRenderer.Render(t.Body, values).Trim(), IssuedOn = today,
            IssuedByName = await CurrentUserNameAsync(db, me), MarginTopMm = t.MarginTopMm, MarginSideMm = t.MarginSideMm, Footer = t.Footer,
        };
        db.CandidateLetters.Add(letter);
        return letter;
    }

    private async Task<Dictionary<string, string?>> ValuesForAsync(Guid employeeId)
    {
        var e = await db.Employees.Include(x => x.Department).Include(x => x.Designation).Include(x => x.Manager)
            .FirstOrDefaultAsync(x => x.Id == employeeId) ?? throw new NotFoundException("Employee");
        var company = await db.Tenants.Where(t => t.Id == db.CurrentTenantId)
            .Select(t => new { t.Name, t.Address, t.City }).FirstAsync();
        var today = await clock.TodayAsync();
        var salary = await CurrentSalaryAsync(db, employeeId, today);

        return new Dictionary<string, string?>
        {
            ["EmployeeName"] = e.FullName,
            ["FirstName"] = e.FirstName,
            ["EmployeeCode"] = e.EmployeeCode,
            ["Designation"] = e.Designation?.Title,
            ["Department"] = e.Department?.Name,
            ["JoiningDate"] = LongDate(e.JoiningDate),
            ["LeavingDate"] = e.LeavingDate is { } left ? LongDate(left) : null,
            ["Cnic"] = e.Cnic,
            ["ManagerName"] = e.Manager?.FullName,
            ["BasicSalary"] = salary is null ? null : Money(salary.Value.Basic),
            ["GrossSalary"] = salary is null ? null : Money(salary.Value.Gross),
            ["CompanyName"] = company.Name,
            ["CompanyAddress"] = FullAddress(company.Address, company.City),
            ["Today"] = LongDate(today),
            ["ConfirmationDate"] = e.ConfirmationDate is { } c ? LongDate(c) : null,
            ["EffectiveDate"] = LongDate(today),
        };
    }

    /// <summary>Address with the city appended, unless the address already names it.</summary>
    public static string? FullAddress(string? address, string? city)
    {
        address = address?.Trim();
        city = city?.Trim();
        if (string.IsNullOrEmpty(city) || (address?.Contains(city, StringComparison.OrdinalIgnoreCase) ?? false))
            return string.IsNullOrEmpty(address) ? null : address;
        return string.IsNullOrEmpty(address) ? city : $"{address}, {city}";
    }

    public static string LongDate(DateOnly d) => d.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    public static string Money(decimal amount) => "Rs " + amount.ToString("N0", Pk);

    /// <summary>Basic and gross (basic + earnings) monthly salary in effect on a date, or null if none is set.</summary>
    public static async Task<(decimal Basic, decimal Gross)?> CurrentSalaryAsync(HrmsDbContext db, Guid employeeId, DateOnly on)
    {
        var s = await db.EmployeeSalaries.Include(x => x.Components)
            .Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= on)
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync();
        if (s is null)
            return null;
        return (s.BasicSalary, s.BasicSalary + s.Components.Where(c => c.Kind == SalaryComponentKind.Earning).Sum(c => c.Amount));
    }

    /// <summary>Display name of the signed-in user, including the platform owner (who belongs to no company).</summary>
    public static Task<string?> CurrentUserNameAsync(HrmsDbContext db, ICurrentUser me) =>
        db.Users.IgnoreQueryFilters().Where(u => u.Id == me.UserId).Select(u => u.FullName).FirstOrDefaultAsync();
}

/// <summary>Starter templates written for Pakistani companies; HR edits them freely.</summary>
public static class DefaultTemplates
{
    public record Template(string Name, LetterCategory Category, string Subject, string Body);

    public static readonly IReadOnlyList<Template> All =
    [
        new("Offer letter", LetterCategory.Offer, "Offer of employment — {{Designation}}",
            """
            Dear {{FirstName}},

            We are pleased to offer you the position of {{Designation}} in the {{Department}} department at {{CompanyName}}. Your expected joining date is {{JoiningDate}}.

            Your monthly gross salary will be {{GrossSalary}}, including a basic salary of {{BasicSalary}}, subject to applicable taxes and deductions. You will report to {{ManagerName}}.

            This offer is subject to verification of your documents and references. Please sign and return a copy of this letter to confirm your acceptance.

            We look forward to welcoming you to the team.
            """),
        new("Appointment letter", LetterCategory.Appointment, "Letter of appointment",
            """
            Dear {{EmployeeName}},

            With reference to your application and subsequent interview, you are appointed as {{Designation}} in the {{Department}} department with effect from {{JoiningDate}}.

            Your employee code is {{EmployeeCode}}. Your monthly gross salary will be {{GrossSalary}}. You will be on probation for three months, after which your confirmation will depend on your performance.

            Your employment is governed by the company's policies as amended from time to time. Please sign the duplicate copy of this letter as your acceptance.
            """),
        new("Confirmation letter", LetterCategory.Confirmation, "Confirmation of employment",
            """
            Dear {{EmployeeName}},

            We are pleased to inform you that, on successful completion of your probation, your employment as {{Designation}} is confirmed with effect from {{Today}}.

            All other terms and conditions of your appointment remain unchanged. We appreciate your contribution and wish you continued success.
            """),
        new("Salary increment letter", LetterCategory.Increment, "Salary revision",
            """
            Dear {{EmployeeName}},

            In recognition of your performance and contribution, we are pleased to revise your compensation. With effect from {{Today}}, your monthly gross salary will be {{GrossSalary}}, including a basic salary of {{BasicSalary}}.

            All other terms of your employment remain the same. Thank you for your hard work, and we look forward to your continued success at {{CompanyName}}.
            """),
        new("Salary certificate", LetterCategory.SalaryCertificate, "Salary certificate",
            """
            TO WHOM IT MAY CONCERN

            This is to certify that {{EmployeeName}}, holding CNIC {{Cnic}}, is employed with {{CompanyName}} as {{Designation}} since {{JoiningDate}}.

            Their current monthly gross salary is {{GrossSalary}}.

            This certificate is issued on the employee's request without any liability on the part of the company.
            """),
        new("Experience certificate", LetterCategory.Experience, "Experience certificate",
            """
            TO WHOM IT MAY CONCERN

            This is to certify that {{EmployeeName}}, holding CNIC {{Cnic}}, worked with {{CompanyName}} as {{Designation}} in the {{Department}} department from {{JoiningDate}} to {{LeavingDate}}.

            During this period we found them diligent, professional and dependable. We wish them every success in their future endeavours.
            """),
        new("Relieving letter", LetterCategory.Relieving, "Relieving letter",
            """
            Dear {{EmployeeName}},

            With reference to your resignation, you are relieved from your duties as {{Designation}} at the close of business on {{LeavingDate}}.

            Your full and final settlement has been processed as per company policy. We thank you for your services and wish you the very best for the future.
            """),
        new("Warning letter", LetterCategory.Warning, "Warning letter",
            """
            Dear {{EmployeeName}},

            It has been brought to our attention that [describe the issue]. This conduct is not in line with the company's policies.

            You are hereby warned to refrain from such conduct in future. Any repetition may lead to strict disciplinary action, up to and including termination of employment.

            Please acknowledge receipt of this letter.
            """),
    ];
}
