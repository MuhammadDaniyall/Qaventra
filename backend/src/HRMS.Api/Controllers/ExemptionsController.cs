using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record ExemptionDto(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, DateOnly Date, AttendanceStatus Flag, string Reason, RequestStatus Status,
    DateTime CreatedAtUtc, string? DecisionNote, string? CheckIn);

public record RequestExemptionRequest(DateOnly Date, [Required, StringLength(500)] string Reason);

public record ExemptionDecisionRequest([StringLength(1000)] string? Note);

public record AttendanceSettingsDto([Range(0, 31)] int ExemptionsPerMonth, [Range(1, 60)] int ExemptionWithinDays);

public record ExemptionQuotaDto(int Allowed, int Used, int WithinDays);

/// <summary>Service for exemptions; also used by the approvals inbox.</summary>
public class ExemptionService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals)
{
    public async Task<AttendanceSettings> SettingsAsync() => await db.AttendanceSettings.FirstOrDefaultAsync() ?? new AttendanceSettings();

    /// <summary>Excuses the day's late arrival or early leave; the flag comes from the attendance record.</summary>
    public async Task<AttendanceExemption> RequestAsync(DateOnly date, string reason)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var s = await SettingsAsync();
        if (s.ExemptionsPerMonth == 0)
            throw new DomainException("Your company does not take exemption requests.");
        var today = await clock.TodayAsync();
        if (date > today || date < today.AddDays(-s.ExemptionWithinDays))
            throw new DomainException($"Ask within {s.ExemptionWithinDays} days of the day.");
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == date)
                     ?? throw new DomainException("There is no attendance on that day. Use an attendance request instead.");
        if (record.Status is not (AttendanceStatus.Late or AttendanceStatus.HalfDay))
            throw new DomainException("That day is not marked late or half day.");
        if (await db.AttendanceExemptions.AnyAsync(x => x.EmployeeId == employeeId && x.Date == date && (x.Status == RequestStatus.Pending || x.Status == RequestStatus.Approved)))
            throw new DomainException("You have already asked for this day.");
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        var used = await db.AttendanceExemptions.CountAsync(x => x.EmployeeId == employeeId && x.Date >= monthStart && x.Date < monthStart.AddMonths(1)
                                                                  && (x.Status == RequestStatus.Pending || x.Status == RequestStatus.Approved));
        if (used >= s.ExemptionsPerMonth)
            throw new DomainException($"You can ask for {s.ExemptionsPerMonth} exemption(s) a month; this month's are used.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Say why.");

        var x = new AttendanceExemption { EmployeeId = employeeId, Date = date, Flag = record.Status, Reason = reason.Trim() };
        db.AttendanceExemptions.Add(x);
        var what = record.Status == AttendanceStatus.Late ? $"late arrival ({(await clock.ToLocalAsync(record.CheckInUtc)):HH:mm})" : "half day";
        var approval = await approvals.StartAsync(ApprovalRequestType.Exemption, x.Id, employeeId, $"Excuse {what} on {date:ddd d MMM} · {x.Reason}");
        if (approval.Status == ApprovalStatus.Approved)
            await ApplyAsync(x, "Approved automatically");
        await db.SaveChangesAsync();
        return x;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var x = await db.AttendanceExemptions.FirstOrDefaultAsync(e => e.Id == id) ?? throw new NotFoundException("Exemption");
        if (x.Status != RequestStatus.Pending)
            throw new DomainException("This request is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Exemption, x.Id, x.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
            await ApplyAsync(x, note);
        else if (outcome == ApprovalStatus.Rejected)
        {
            x.Status = RequestStatus.Rejected;
            x.DecidedAtUtc = clock.UtcNow;
            x.DecisionNote = note;
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid id)
    {
        var x = await db.AttendanceExemptions.FirstOrDefaultAsync(e => e.Id == id && e.EmployeeId == me.EmployeeId) ?? throw new NotFoundException("Exemption");
        if (x.Status != RequestStatus.Pending)
            throw new DomainException("Only pending requests can be cancelled.");
        await approvals.CancelAsync(ApprovalRequestType.Exemption, x.Id);
        x.Status = RequestStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    /// <summary>The day counts as present: no late penalty or half-day deduction in payroll.</summary>
    private async Task ApplyAsync(AttendanceExemption x, string? note)
    {
        x.Status = RequestStatus.Approved;
        x.DecidedAtUtc = clock.UtcNow;
        x.DecisionNote = note;
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(r => r.EmployeeId == x.EmployeeId && r.Date == x.Date);
        if (record is not null && record.Status == x.Flag)
        {
            record.Status = AttendanceStatus.Present;
            var what = x.Flag == AttendanceStatus.Late ? "Late" : "Half day";
            record.Note = string.IsNullOrEmpty(record.Note) ? $"{what} excused (approved exemption)" : $"{record.Note}; {what.ToLowerInvariant()} excused";
        }
    }
}

[ApiController]
[Route("api/exemptions")]
[Authorize(Policy = Policies.CompanyUser)]
public class ExemptionsController(HrmsDbContext db, ExemptionService exemptions, ApprovalEngine approvals, TenantClock clock, ICurrentUser me) : ControllerBase
{
    [HttpGet("my")]
    public async Task<List<ExemptionDto>> My() => await ProjectAsync(db.AttendanceExemptions.Where(x => x.EmployeeId == me.EmployeeId));

    [HttpGet("quota")]
    public async Task<ExemptionQuotaDto> Quota()
    {
        var s = await exemptions.SettingsAsync();
        var today = await clock.TodayAsync();
        var start = new DateOnly(today.Year, today.Month, 1);
        var used = await db.AttendanceExemptions.CountAsync(x => x.EmployeeId == me.EmployeeId && x.Date >= start && (x.Status == RequestStatus.Pending || x.Status == RequestStatus.Approved));
        return new ExemptionQuotaDto(s.ExemptionsPerMonth, used, s.ExemptionWithinDays);
    }

    [HttpPost]
    public async Task<IActionResult> Request(RequestExemptionRequest req)
    {
        await exemptions.RequestAsync(req.Date, req.Reason);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await exemptions.CancelAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, ExemptionDecisionRequest? req)
    {
        await exemptions.DecideAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, ExemptionDecisionRequest? req)
    {
        await exemptions.DecideAsync(id, false, req?.Note);
        return NoContent();
    }

    /// <summary>HR: all; others: the ones waiting on them.</summary>
    [HttpGet("approvals"), Authorize(Policy = Policies.Approver)]
    public async Task<List<ExemptionDto>> Approvals([FromQuery] RequestStatus status = RequestStatus.Pending)
    {
        var q = db.AttendanceExemptions.Where(x => x.Status == status && x.EmployeeId != me.EmployeeId);
        if (!me.IsHr())
        {
            var waiting = await approvals.WaitingForMeAsync(ApprovalRequestType.Exemption);
            q = q.Where(x => waiting.Contains(x.Id));
        }
        return await ProjectAsync(q);
    }

    [HttpGet("settings")]
    public async Task<AttendanceSettingsDto> Settings()
    {
        var s = await exemptions.SettingsAsync();
        return new AttendanceSettingsDto(s.ExemptionsPerMonth, s.ExemptionWithinDays);
    }

    [HttpPut("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveSettings(AttendanceSettingsDto req)
    {
        var s = await db.AttendanceSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new AttendanceSettings();
            db.AttendanceSettings.Add(s);
        }
        s.ExemptionsPerMonth = req.ExemptionsPerMonth;
        s.ExemptionWithinDays = req.ExemptionWithinDays;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<List<ExemptionDto>> ProjectAsync(IQueryable<AttendanceExemption> q)
    {
        var rows = await q.OrderByDescending(x => x.Date).Take(300).Select(x => new
        {
            X = x, Name = x.Employee!.FirstName + " " + x.Employee.LastName, x.Employee.EmployeeCode,
            In = db.AttendanceRecords.Where(r => r.EmployeeId == x.EmployeeId && r.Date == x.Date).Select(r => (DateTime?)r.CheckInUtc).FirstOrDefault(),
        }).ToListAsync();
        var list = new List<ExemptionDto>();
        foreach (var r in rows)
            list.Add(new ExemptionDto(r.X.Id, r.X.EmployeeId, r.Name, r.EmployeeCode, r.X.Date, r.X.Flag, r.X.Reason, r.X.Status, r.X.CreatedAtUtc, r.X.DecisionNote,
                r.In is { } t ? (await clock.ToLocalAsync(t)).ToString("HH:mm") : null));
        return list;
    }
}
