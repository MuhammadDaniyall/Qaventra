using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record EmailTemplateDto(NotificationKind Kind, string Label, string? Subject, string? Intro, bool Enabled, bool IsCustom,
    RecordStampsDto? Stamps = null);
public record SaveEmailTemplateRequest([Required, StringLength(200)] string Subject, [StringLength(2000)] string? Intro, bool Enabled = true);
public record EmailLogDto(Guid Id, DateTime CreatedAtUtc, string To, string Subject, string? Kind, EmailLogStatus Status, string? Error);
public record EmailUsageDto(int SentThisMonth, int FailedThisMonth, int? QuotaPerMonth);
public record CompanySmtpDto(string Host, int Port, string? User, bool HasPassword, string From, string? FromName, bool EnableSsl, bool IsEnabled);
/// <param name="Password">Null keeps the saved password; empty clears it.</param>
public record SaveCompanySmtpRequest([Required, StringLength(200)] string Host, int Port, [StringLength(200)] string? User, [StringLength(300)] string? Password,
    [Required, EmailAddress, StringLength(200)] string From, [StringLength(100)] string? FromName, bool EnableSsl, bool IsEnabled);
public record TestEmailRequest([Required, EmailAddress, StringLength(200)] string To);

/// <summary>The company's email wording per notification kind, the log of emails sent, and this month's usage.</summary>
[ApiController]
[Route("api/setup/email")]
[Authorize(Policy = Policies.CompanyUser)]
[Authorize(Policy = Policies.Hr)]
public class EmailSettingsController(HrmsDbContext db, HRMS.Api.Services.SecretBox secrets, HRMS.Api.Services.IEmailSender sender) : ControllerBase
{
    /// <summary>Mail server ports a company may use (keeps the test send from probing other services).</summary>
    private static readonly int[] SmtpPorts = [25, 465, 587, 2525];

    [HttpGet("smtp"), Authorize(Policy = Policies.Admin)]
    public async Task<CompanySmtpDto?> Smtp()
    {
        var s = await db.CompanySmtps.AsNoTracking().FirstOrDefaultAsync();
        return s is null ? null : new CompanySmtpDto(s.Host, s.Port, s.User, s.PasswordCipher is not null, s.From, s.FromName, s.EnableSsl, s.IsEnabled);
    }

    [HttpPut("smtp"), Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> SaveSmtp(SaveCompanySmtpRequest req)
    {
        if (!SmtpPorts.Contains(req.Port))
            throw new HRMS.Domain.Common.DomainException("Use a mail port: 25, 465, 587 or 2525.");
        if (await IsInternalAsync(req.Host.Trim()))
            throw new HRMS.Domain.Common.DomainException("Use your mail provider's public server name.");
        var s = await db.CompanySmtps.FirstOrDefaultAsync();
        if (s is null)
            db.CompanySmtps.Add(s = new CompanySmtp());
        s.Host = req.Host.Trim();
        s.Port = req.Port;
        s.User = string.IsNullOrWhiteSpace(req.User) ? null : req.User.Trim();
        if (req.Password is not null)
            s.PasswordCipher = req.Password.Length == 0 ? null : secrets.Protect(req.Password);
        s.From = req.From.Trim();
        s.FromName = string.IsNullOrWhiteSpace(req.FromName) ? null : req.FromName.Trim();
        s.EnableSsl = req.EnableSsl;
        s.IsEnabled = req.IsEnabled;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>A host on this machine or a private network: not a company's mail provider, so refused.</summary>
    private static async Task<bool> IsInternalAsync(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        System.Net.IPAddress[] addresses;
        try
        {
            addresses = System.Net.IPAddress.TryParse(host, out var ip) ? [ip] : await System.Net.Dns.GetHostAddressesAsync(host);
        }
        catch (System.Net.Sockets.SocketException)
        {
            return false; // Unknown names fail later at the test send.
        }
        return addresses.Any(a =>
        {
            if (System.Net.IPAddress.IsLoopback(a) || a.IsIPv6LinkLocal || a.IsIPv6SiteLocal) return true;
            var b = a.MapToIPv4().GetAddressBytes();
            return b[0] == 10 || b[0] == 127 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || b[0] == 0;
        });
    }

    /// <summary>Sends one email through the saved server right away and says whether it worked.</summary>
    [HttpPost("smtp/test"), Authorize(Policy = Policies.Admin)]
    public async Task<object> TestSmtp(TestEmailRequest req, CancellationToken ct)
    {
        var s = await db.CompanySmtps.AsNoTracking().FirstOrDefaultAsync(ct) ?? throw new HRMS.Domain.Common.DomainException("Save the mail server first.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await sender.SendAsync(new HRMS.Api.Services.EmailMessage(req.To.Trim(), "Test email from Qaventra",
                "<p>Your mail server works. Emails from Qaventra will go out through it.</p>",
                Smtp: HRMS.Api.Services.EmailBackgroundService.ToOptions(s, secrets)), timeout.Token);
            return new { ok = true, error = (string?)null };
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var msg = ex is OperationCanceledException ? "The mail server did not answer in 20 seconds." : ex.Message;
            return new { ok = false, error = msg.Length > 300 ? msg[..300] : msg };
        }
    }

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
            ? new EmailTemplateDto(l.Key, l.Value, t.Subject, t.Intro, t.Enabled, true, RecordStampsDto.Of(t))
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
