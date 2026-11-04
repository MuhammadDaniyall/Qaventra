using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record AutomationStatusDto(string? State, string? Holder, DateTime? LeaseUntilUtc, string? LastHolder, DateTime? LastRunAtUtc);
public record AutomationLeaseRequest([Required, StringLength(100)] string Holder, [Range(1, 120)] int Minutes, [Range(0, 600)] int DeferToOthersMinutes = 0);
public record AutomationLeaseDto(bool Granted, string? Holder, DateTime? LeaseUntilUtc, string? LastHolder, DateTime? LastRunAtUtc, string? Reason);
public record AutomationStateRequest([Required, StringLength(100)] string Holder, [Required] string State);
public record AutomationReleaseRequest([Required, StringLength(100)] string Holder);

/// <summary>
/// Shared progress and a lease for a company's scripted users (the virtual clients), so the same script on several
/// computers takes turns and never does a day's work twice. Only one holder writes at a time.
/// </summary>
[ApiController]
[Route("api/setup/automation")]
[Authorize(Policy = Policies.CompanyUser)]
[Authorize(Policy = Policies.Hr)]
public class AutomationController(HrmsDbContext db) : ControllerBase
{
    private const int MaxStateChars = 1_000_000;

    [HttpGet]
    public async Task<AutomationStatusDto> Get()
    {
        var t = await db.Tenants.AsNoTracking().FirstAsync(x => x.Id == db.CurrentTenantId);
        return new(t.AutomationStateJson, t.AutomationHolder, t.AutomationLeaseUntilUtc, t.AutomationLastHolder, t.AutomationLastRunAtUtc);
    }

    /// <summary>
    /// Takes (or extends) the lease. Refused while another holder's lease runs, and, with DeferToOthersMinutes, while
    /// another holder finished a run within that many minutes (so a preferred computer keeps the work while it is awake).
    /// </summary>
    [HttpPost("lease")]
    public async Task<AutomationLeaseDto> Lease(AutomationLeaseRequest req)
    {
        var now = DateTime.UtcNow;
        var holder = req.Holder.Trim();
        var id = db.CurrentTenantId;
        var t = await db.Tenants.AsNoTracking().FirstAsync(x => x.Id == id);
        if (req.DeferToOthersMinutes > 0 && t.AutomationLastHolder is { } last && last != holder && t.AutomationLastRunAtUtc is { } ran
            && ran > now.AddMinutes(-req.DeferToOthersMinutes) && (t.AutomationHolder is null || t.AutomationLeaseUntilUtc <= now))
            return new(false, t.AutomationHolder, t.AutomationLeaseUntilUtc, last, ran, $"{last} ran at {ran:HH:mm} UTC");
        // One atomic update, so two computers asking at once cannot both get it.
        var until = now.AddMinutes(req.Minutes);
        var got = await db.Tenants.Where(x => x.Id == id && (x.AutomationHolder == null || x.AutomationLeaseUntilUtc <= now || x.AutomationHolder == holder))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AutomationHolder, holder).SetProperty(x => x.AutomationLeaseUntilUtc, until));
        t = await db.Tenants.AsNoTracking().FirstAsync(x => x.Id == id);
        return new(got == 1, t.AutomationHolder, t.AutomationLeaseUntilUtc, t.AutomationLastHolder, t.AutomationLastRunAtUtc,
            got == 1 ? null : $"{t.AutomationHolder} is running until {t.AutomationLeaseUntilUtc:HH:mm} UTC");
    }

    [HttpPut("state")]
    public async Task<IActionResult> SaveState(AutomationStateRequest req)
    {
        if (req.State.Length > MaxStateChars)
            throw new DomainException("The state is too large.");
        var now = DateTime.UtcNow;
        var holder = req.Holder.Trim();
        var id = db.CurrentTenantId;
        var saved = await db.Tenants.Where(x => x.Id == id && x.AutomationHolder == holder && x.AutomationLeaseUntilUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AutomationStateJson, req.State));
        if (saved == 0)
            return Conflict(new { title = "Take the lease first." });
        return NoContent();
    }

    [HttpPost("release")]
    public async Task<IActionResult> Release(AutomationReleaseRequest req)
    {
        var now = DateTime.UtcNow;
        var holder = req.Holder.Trim();
        var id = db.CurrentTenantId;
        await db.Tenants.Where(x => x.Id == id && x.AutomationHolder == holder)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AutomationHolder, (string?)null).SetProperty(x => x.AutomationLeaseUntilUtc, (DateTime?)null)
                .SetProperty(x => x.AutomationLastHolder, holder).SetProperty(x => x.AutomationLastRunAtUtc, now));
        return NoContent();
    }
}
