using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record SecuritySettingsDto(List<string> AllowedEmailDomains);
public record SaveSecuritySettingsRequest([MaxLength(20)] List<string> AllowedEmailDomains);
public record LastLoginDto(Guid UserId, string FullName, string Email, UserRole Role, bool IsActive, DateTime? LastLoginUtc, int FailedLast30Days);
public record ErrorLogDto(long Id, DateTime TimestampUtc, string? UserEmail, string? Method, string? Path, int? StatusCode, string? CorrelationId);

/// <summary>Company admin's security screen: allowed sign-in domains, when each login was last used, and server errors.</summary>
[ApiController]
[Route("api/setup/security")]
[Authorize(Policy = Policies.Admin)]
public class SecurityController(HrmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<SecuritySettingsDto> Get() => new(Parse(await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.AllowedEmailDomains).FirstOrDefaultAsync()));

    [HttpPut]
    public async Task<SecuritySettingsDto> Save(SaveSecuritySettingsRequest req)
    {
        var domains = req.AllowedEmailDomains.Select(d => d.Trim().TrimStart('@').ToLowerInvariant()).Where(d => d.Length > 0).Distinct().ToList();
        foreach (var d in domains)
            if (d.Length > 100 || !d.Contains('.') || d.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '-')))
                throw new DomainException($"\"{d}\" is not a valid email domain, e.g. yourcompany.com.");
        var tenant = await db.Tenants.FirstAsync(t => t.Id == db.CurrentTenantId);
        tenant.AllowedEmailDomains = domains.Count == 0 ? null : string.Join(',', domains);
        await db.SaveChangesAsync();
        return new(domains);
    }

    /// <summary>Every login with its last successful sign-in and failed attempts in the last 30 days.</summary>
    [HttpGet("last-logins")]
    public async Task<List<LastLoginDto>> LastLogins()
    {
        var tenant = db.CurrentTenantId;
        var since = DateTime.UtcNow.AddDays(-30);
        var last = await db.AuditLogs.Where(a => a.TenantId == tenant && a.Category == AuditCategory.Auth && a.Action == "LoginSucceeded" && a.UserId != null)
            .GroupBy(a => a.UserId!.Value).Select(g => new { g.Key, Last = g.Max(a => a.TimestampUtc) }).ToDictionaryAsync(x => x.Key, x => x.Last);
        var failed = await db.AuditLogs.Where(a => a.TenantId == tenant && a.Category == AuditCategory.Auth && a.Action == "LoginFailed" && a.UserId != null && a.TimestampUtc >= since)
            .GroupBy(a => a.UserId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var users = await db.Users.OrderBy(u => u.FullName).Select(u => new { u.Id, u.FullName, u.Email, u.Role, u.IsActive }).ToListAsync();
        return users.Select(u => new LastLoginDto(u.Id, u.FullName, u.Email, u.Role, u.IsActive,
                last.TryGetValue(u.Id, out var l) ? l : null, failed.GetValueOrDefault(u.Id)))
            .OrderBy(u => u.LastLoginUtc ?? DateTime.MinValue).ToList();
    }

    /// <summary>Requests that failed with a server error (500 and up) in the last 30 days.</summary>
    [HttpGet("errors")]
    public Task<List<ErrorLogDto>> Errors()
    {
        var tenant = db.CurrentTenantId;
        var since = DateTime.UtcNow.AddDays(-30);
        return db.AuditLogs.Where(a => a.TenantId == tenant && a.Category == AuditCategory.Request && a.StatusCode >= 500 && a.TimestampUtc >= since)
            .OrderByDescending(a => a.TimestampUtc).Take(200)
            .Select(a => new ErrorLogDto(a.Id, a.TimestampUtc, a.UserEmail, a.Action, a.Path, a.StatusCode, a.CorrelationId)).ToListAsync();
    }

    public static async Task<bool> DomainAllowedAsync(HrmsDbContext db, Guid tenantId, string email)
    {
        var allowed = Parse(await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.AllowedEmailDomains).FirstOrDefaultAsync());
        if (allowed.Count == 0)
            return true;
        var domain = email[(email.LastIndexOf('@') + 1)..].ToLowerInvariant();
        return allowed.Contains(domain);
    }

    private static List<string> Parse(string? csv) => (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
