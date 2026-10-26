using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record EmailTemplateDto(NotificationKind Kind, string Label, string? Subject, string? Intro, bool Enabled, bool IsCustom);
public record SaveEmailTemplateRequest([Required, StringLength(200)] string Subject, [StringLength(2000)] string? Intro, bool Enabled = true);
public record EmailLogDto(Guid Id, DateTime CreatedAtUtc, string To, string Subject, string? Kind, EmailLogStatus Status, string? Error);
public record EmailUsageDto(int SentThisMonth, int FailedThisMonth, int? QuotaPerMonth);

/// <summary>The company's email wording per notification kind, the log of emails sent, and this month's usage.</summary>
[ApiController]
[Route("api/setup/email")]
[Authorize(Policy = Policies.Hr)]
public class EmailSettingsController(HrmsDbContext db) : ControllerBase
{
    private static readonly Dictionary<NotificationKind, string> Labels = new()
    {
        [NotificationKind.ApprovalNeeded] = "Something waits for approval",
        [NotificationKind.RequestDecided] = "A request was approved or rejected",
        [NotificationKind.Payslip] = "Payslip ready",
        [NotificationKind.Letter] = "Letter issued",
        [NotificationKind.Separation] = "Resignation and exit",
        [NotificationKind.Announcement] = "Announcement",
        [NotificationKind.Poll] = "Poll",
        [NotificationKind.Ticket] = "Help desk ticket",
        [NotificationKind.Asset] = "Asset",
        [NotificationKind.Onboarding] = "Onboarding",
        [NotificationKind.Performance] = "Performance review",
        [NotificationKind.Recruitment] = "Recruitment",
        [NotificationKind.Travel] = "Travel",
        [NotificationKind.Training] = "Training",
        [NotificationKind.Alert] = "Alert",
        [NotificationKind.Report] = "Scheduled report",
        [NotificationKind.Celebration] = "Birthday and anniversary wishes",
    };

    [HttpGet("templates")]
    public async Task<List<EmailTemplateDto>> Templates()
    {
        var saved = await db.EmailTemplates.ToDictionaryAsync(t => t.Kind);
        return Labels.Select(l => saved.TryGetValue(l.Key, out var t)
            ? new EmailTemplateDto(l.Key, l.Value, t.Subject, t.Intro, t.Enabled, true)
            : new EmailTemplateDto(l.Key, l.Value, null, null, true, false)).ToList();
    }

    [HttpPut("templates/{kind}")]
    public async Task<IActionResult> Save(NotificationKind kind, SaveEmailTemplateRequest req)
    {
        if (!Labels.ContainsKey(kind))
            return NotFound();
        var t = await db.EmailTemplates.FirstOrDefaultAsync(x => x.Kind == kind);
        if (t is null)
            db.EmailTemplates.Add(t = new EmailTemplate { Kind = kind });
        t.Subject = req.Subject.Trim();
        t.Intro = string.IsNullOrWhiteSpace(req.Intro) ? null : req.Intro.Trim();
        t.Enabled = req.Enabled;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Back to the standard email (the notification's own title).</summary>
    [HttpDelete("templates/{kind}")]
    public async Task<IActionResult> Reset(NotificationKind kind)
    {
        await db.EmailTemplates.Where(x => x.Kind == kind).ExecuteDeleteAsync();
        return NoContent();
    }

    [HttpGet("log")]
    public async Task<PagedResult<EmailLogDto>> Log([FromQuery] EmailLogStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = db.EmailLogs.AsNoTracking();
        if (status is not null) q = q.Where(l => l.Status == status);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new EmailLogDto(l.Id, l.CreatedAtUtc, l.To, l.Subject, l.Kind, l.Status, l.Error)).ToListAsync();
        return new PagedResult<EmailLogDto>(items, total, page, pageSize);
    }

    [HttpGet("usage")]
    public async Task<EmailUsageDto> Usage()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var month = db.EmailLogs.Where(l => l.CreatedAtUtc >= monthStart);
        var quota = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.EmailQuotaPerMonth).FirstOrDefaultAsync();
        return new(await month.CountAsync(l => l.Status == EmailLogStatus.Sent), await month.CountAsync(l => l.Status == EmailLogStatus.Failed), quota);
    }
}
