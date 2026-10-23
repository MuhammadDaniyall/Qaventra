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

public record PenaltyRuleDto(Guid Id, string Name, PenaltyFlag Flag, int EveryCount, int FreeCount, int MinMinutes, bool Repeat, PenaltyAction Action,
    decimal Days, Guid? LeaveTypeId, bool IsActive);

public record SavePenaltyRuleRequest([Required, StringLength(150)] string Name, PenaltyFlag Flag, [Range(1, 31)] int EveryCount, [Range(0, 31)] int FreeCount,
    [Range(0, 600)] int MinMinutes, bool Repeat, PenaltyAction Action, [Range(0.25, 31)] decimal Days, Guid? LeaveTypeId, bool IsActive = true);

/// <summary>One rule's outcome for one person in a month.</summary>
public record PenaltyLine(Guid RuleId, string RuleName, PenaltyFlag Flag, int Occurrences, PenaltyAction Action, decimal Days, Guid? LeaveTypeId);

public record PenaltyPreviewRow(Guid EmployeeId, string EmployeeCode, string Name, List<PenaltyLine> Lines, decimal PayDays, decimal LeaveDays);

public record OffDayRequestDto(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, DateOnly FromDate, DateOnly ToDate, List<DayOfWeek> OffDays,
    List<DayOfWeek> UsualOffDays, string Reason, RequestStatus Status, DateTime CreatedAtUtc, string? DecisionNote);

public record RequestOffDayRequest(DateOnly FromDate, DateOnly ToDate, List<DayOfWeek> OffDays, [Required, StringLength(500)] string Reason);

/// <summary>
/// Attendance penalty rules: counts flags (late, early leave, half/short day, absent, missing check-out) in a month and
/// turns them into days of pay or leave. Pay penalties go on the payslip; leave ones come off the balance when the month is closed.
/// </summary>
public class PenaltyService(HrmsDbContext db, ScheduleService schedules, TenantClock clock, LeaveService leave)
{
    public async Task<Dictionary<Guid, List<PenaltyLine>>> ComputeAsync(IReadOnlyCollection<Guid> employeeIds, int year, int month)
    {
        var result = employeeIds.Distinct().ToDictionary(id => id, _ => new List<PenaltyLine>());
        var rules = await db.PenaltyRules.Where(r => r.IsActive).ToListAsync();
        if (rules.Count == 0 || result.Count == 0)
            return result;
        var ids = result.Keys.ToList();
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var today = await clock.TodayAsync();
        var countedTo = monthEnd < today ? monthEnd : today.AddDays(-1);
        var records = (await db.AttendanceRecords.Where(a => ids.Contains(a.EmployeeId) && a.Date >= monthStart && a.Date <= monthEnd)
            .Select(a => new { a.EmployeeId, a.Date, a.Status, a.LateMinutes, a.EarlyLeaveMinutes, Out = a.CheckOutUtc != null }).ToListAsync()).ToLookup(a => a.EmployeeId);

        Dictionary<Guid, int>? absences = null;
        if (rules.Any(r => r.Flag == PenaltyFlag.Absent))
        {
            absences = [];
            var people = await db.Employees.Where(e => ids.Contains(e.Id)).Select(e => new { e.Id, e.JoiningDate, e.LeavingDate }).ToListAsync();
            var calendars = await schedules.ForEmployeesAsync(ids, monthStart, monthEnd);
            var leaves = (await db.LeaveRequests.Where(r => ids.Contains(r.EmployeeId) && r.Status == LeaveStatus.Approved && r.FromDate <= monthEnd && r.ToDate >= monthStart)
                .Select(r => new { r.EmployeeId, r.FromDate, r.ToDate }).ToListAsync()).ToLookup(r => r.EmployeeId);
            foreach (var p in people)
            {
                var present = records[p.Id].Select(r => r.Date).ToHashSet();
                var from = p.JoiningDate > monthStart ? p.JoiningDate : monthStart;
                var to = p.LeavingDate is { } l && l < countedTo ? l : countedTo;
                var n = 0;
                for (var d = from; d <= to; d = d.AddDays(1))
                    if (calendars[p.Id].IsWorkingDay(d) && !present.Contains(d) && !leaves[p.Id].Any(x => x.FromDate <= d && x.ToDate >= d))
                        n++;
                absences[p.Id] = n;
            }
        }

        foreach (var id in ids)
        {
            var mine = records[id].ToList();
            foreach (var rule in rules)
            {
                var n = rule.Flag switch
                {
                    PenaltyFlag.Late => mine.Count(r => r.Status == AttendanceStatus.Late && r.LateMinutes >= rule.MinMinutes),
                    PenaltyFlag.EarlyLeave => mine.Count(r => r.EarlyLeaveMinutes > 0 && r.EarlyLeaveMinutes >= rule.MinMinutes),
                    PenaltyFlag.HalfDay => mine.Count(r => r.Status == AttendanceStatus.HalfDay),
                    PenaltyFlag.ShortDay => mine.Count(r => r.Status == AttendanceStatus.ShortDay),
                    PenaltyFlag.MissingCheckOut => mine.Count(r => !r.Out && r.Date < today),
                    PenaltyFlag.Absent => absences?.GetValueOrDefault(id) ?? 0,
                    _ => 0,
                };
                var days = DaysFor(rule, n);
                if (days > 0)
                    result[id].Add(new PenaltyLine(rule.Id, rule.Name, rule.Flag, n, rule.Action, days, rule.LeaveTypeId));
            }
        }
        return result;
    }

    /// <summary>Penalty days for a number of occurrences: free ones first, then one penalty per <c>EveryCount</c> (or once a month).</summary>
    public static decimal DaysFor(PenaltyRule rule, int occurrences)
    {
        var counted = Math.Max(0, occurrences - rule.FreeCount);
        var times = rule.Repeat ? counted / Math.Max(1, rule.EveryCount) : counted >= rule.EveryCount ? 1 : 0;
        return times * rule.Days;
    }

    /// <summary>
    /// Pay deductions for payroll: from the charges recorded at close when the month is closed, otherwise worked out live
    /// from the pay rules. Returns (label, days) per person.
    /// </summary>
    public async Task<Dictionary<Guid, List<(string Label, decimal Days)>>> PayDaysAsync(IReadOnlyCollection<Guid> employeeIds, int year, int month)
    {
        var ids = employeeIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => new List<(string, decimal)>());
        if (await db.AttendancePeriods.AnyAsync(p => p.Year == year && p.Month == month))
        {
            foreach (var c in await db.PenaltyCharges.Where(c => c.Year == year && c.Month == month && c.PayDays > 0 && ids.Contains(c.EmployeeId)).ToListAsync())
                result[c.EmployeeId].Add(($"{c.RuleName} ({c.Occurrences}×{(c.LeaveDays > 0 ? ", leave ran out" : "")})", c.PayDays));
            return result;
        }
        foreach (var (id, lines) in await ComputeAsync(ids, year, month))
            foreach (var l in lines.Where(l => l.Action == PenaltyAction.DeductPay))
                result[id].Add(($"{l.RuleName} ({l.Occurrences}×)", l.Days));
        return result;
    }

    /// <summary>When a month is closed: records each person's penalties and takes leave penalties off their balance.</summary>
    public async Task ApplyOnCloseAsync(int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var ids = await db.Employees.Where(e => e.JoiningDate <= monthEnd && (e.LeavingDate == null || e.LeavingDate >= monthStart)).Select(e => e.Id).ToListAsync();
        foreach (var (id, lines) in await ComputeAsync(ids, year, month))
            foreach (var l in lines)
            {
                var charge = new PenaltyCharge
                {
                    EmployeeId = id, Year = year, Month = month, RuleId = l.RuleId, RuleName = l.RuleName, Occurrences = l.Occurrences, LeaveTypeId = l.LeaveTypeId,
                };
                if (l.Action == PenaltyAction.DeductLeave && l.LeaveTypeId is { } typeId)
                {
                    await leave.EnsureBalancesAsync(id, year);
                    var balance = await db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == id && b.LeaveTypeId == typeId && b.Year == year);
                    var take = balance is null ? 0 : Math.Min(l.Days, Math.Max(0, balance.Remaining));
                    if (balance is not null && take > 0)
                    {
                        balance.Adjustment -= take;
                        balance.AdjustmentNote = $"Attendance penalty {monthStart:MMM yyyy}: {l.RuleName}";
                    }
                    charge.LeaveDays = take;
                    charge.PayDays = l.Days - take;
                }
                else
                    charge.PayDays = l.Days;
                db.PenaltyCharges.Add(charge);
            }
        await db.SaveChangesAsync();
    }

    /// <summary>When a month is reopened: gives leave penalties back and forgets the charges.</summary>
    public async Task UndoOnReopenAsync(int year, int month)
    {
        var charges = await db.PenaltyCharges.Where(c => c.Year == year && c.Month == month).ToListAsync();
        foreach (var c in charges.Where(c => c.LeaveDays > 0 && c.LeaveTypeId is not null))
        {
            var balance = await db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == c.EmployeeId && b.LeaveTypeId == c.LeaveTypeId && b.Year == year);
            if (balance is not null)
                balance.Adjustment += c.LeaveDays;
        }
        db.PenaltyCharges.RemoveRange(charges);
        await db.SaveChangesAsync();
    }
}

/// <summary>Requests to move weekly days off for a period; also used by the approvals inbox.</summary>
public class OffDayService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, ScheduleService schedules)
{
    public async Task<OffDayRequest> RequestAsync(RequestOffDayRequest req)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        if (req.ToDate < req.FromDate)
            throw new DomainException("The end date is before the start date.");
        if (req.ToDate.DayNumber - req.FromDate.DayNumber > 92)
            throw new DomainException("Ask for at most three months at a time.");
        if (req.FromDate < await clock.TodayAsync())
            throw new DomainException("Start from today or later.");
        var offDays = (req.OffDays ?? []).Distinct().ToList();
        if (offDays.Count is 0 or > 3)
            throw new DomainException("Choose one to three days off a week.");
        var employee = await db.Employees.Include(e => e.Shift).FirstAsync(e => e.Id == employeeId);
        if (employee.Shift is null)
            throw new DomainException("You have no shift; ask HR to set one first.");
        if (employee.Shift.WeeklyOffDays.OrderBy(d => d).SequenceEqual(offDays.OrderBy(d => d)))
            throw new DomainException("Those are already your days off.");
        if (await db.OffDayRequests.AnyAsync(x => x.EmployeeId == employeeId && x.Status == RequestStatus.Pending && x.FromDate <= req.ToDate && x.ToDate >= req.FromDate))
            throw new DomainException("You already have a pending request for those dates.");
        await schedules.EnsureOpenAsync(req.FromDate, req.ToDate);

        var x = new OffDayRequest { EmployeeId = employeeId, FromDate = req.FromDate, ToDate = req.ToDate, OffDays = offDays, Reason = req.Reason.Trim() };
        db.OffDayRequests.Add(x);
        var approval = await approvals.StartAsync(ApprovalRequestType.OffDay, x.Id, employeeId,
            $"Days off {string.Join(", ", offDays.Select(d => d.ToString()[..3]))} instead of {string.Join(", ", employee.Shift.WeeklyOffDays.Select(d => d.ToString()[..3]))}, {req.FromDate:d MMM}–{req.ToDate:d MMM} · {x.Reason}");
        if (approval.Status == ApprovalStatus.Approved)
            await ApplyAsync(x, employee.Shift, "Approved automatically");
        await db.SaveChangesAsync();
        return x;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var x = await db.OffDayRequests.Include(r => r.Employee).ThenInclude(e => e!.Shift).FirstOrDefaultAsync(e => e.Id == id) ?? throw new NotFoundException("Request");
        if (x.Status != RequestStatus.Pending)
            throw new DomainException("This request is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.OffDay, x.Id, x.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
        {
            if (x.Employee!.Shift is not { } shift)
                throw new DomainException("This employee has no shift any more.");
            await ApplyAsync(x, shift, note);
        }
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
        var x = await db.OffDayRequests.FirstOrDefaultAsync(e => e.Id == id && e.EmployeeId == me.EmployeeId) ?? throw new NotFoundException("Request");
        if (x.Status != RequestStatus.Pending)
            throw new DomainException("Only pending requests can be cancelled.");
        await approvals.CancelAsync(ApprovalRequestType.OffDay, x.Id);
        x.Status = RequestStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    /// <summary>Rewrites the roster: the new days are off, the usual days off become working days on the person's shift.</summary>
    private async Task ApplyAsync(OffDayRequest x, Shift shift, string? note)
    {
        x.Status = RequestStatus.Approved;
        x.DecidedAtUtc = clock.UtcNow;
        x.DecisionNote = note;
        await schedules.AssignAsync([x.EmployeeId], x.FromDate, x.ToDate, x.OffDays, null, off: true, clear: false, ShiftAssignmentSource.Request);
        var nowWorking = shift.WeeklyOffDays.Except(x.OffDays).ToList();
        if (nowWorking.Count > 0)
            await schedules.AssignAsync([x.EmployeeId], x.FromDate, x.ToDate, nowWorking, shift.Id, off: false, clear: false, ShiftAssignmentSource.Request);
    }
}

[ApiController]
[Route("api/attendance-rules")]
[Authorize(Policy = Policies.CompanyUser)]
public class AttendanceRulesController(HrmsDbContext db, PenaltyService penalties, OffDayService offDays, ApprovalEngine approvals, ICurrentUser me) : ControllerBase
{
    // ---------- Penalty rules ----------

    [HttpGet("penalties")]
    public Task<List<PenaltyRuleDto>> Rules() =>
        db.PenaltyRules.OrderBy(r => r.Flag).ThenBy(r => r.Name)
            .Select(r => new PenaltyRuleDto(r.Id, r.Name, r.Flag, r.EveryCount, r.FreeCount, r.MinMinutes, r.Repeat, r.Action, r.Days, r.LeaveTypeId, r.IsActive))
            .ToListAsync();

    [HttpPost("penalties"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddRule(SavePenaltyRuleRequest req)
    {
        var r = new PenaltyRule();
        await ApplyAsync(r, req);
        db.PenaltyRules.Add(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("penalties/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateRule(Guid id, SavePenaltyRuleRequest req)
    {
        await ApplyAsync(await db.PenaltyRules.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Rule"), req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("penalties/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        db.PenaltyRules.Remove(await db.PenaltyRules.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Rule"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(PenaltyRule r, SavePenaltyRuleRequest req)
    {
        if (!Enum.IsDefined(req.Flag) || !Enum.IsDefined(req.Action))
            throw new DomainException("Choose what is counted and what it costs.");
        if (req.Action == PenaltyAction.DeductLeave && (req.LeaveTypeId is not { } t || !await db.LeaveTypes.AnyAsync(x => x.Id == t)))
            throw new DomainException("Choose the leave type to deduct from.");
        r.Name = req.Name.Trim();
        r.Flag = req.Flag;
        r.EveryCount = req.EveryCount;
        r.FreeCount = req.FreeCount;
        r.MinMinutes = req.Flag is PenaltyFlag.Late or PenaltyFlag.EarlyLeave ? req.MinMinutes : 0;
        r.Repeat = req.Repeat;
        r.Action = req.Action;
        r.Days = req.Days;
        r.LeaveTypeId = req.Action == PenaltyAction.DeductLeave ? req.LeaveTypeId : null;
        r.IsActive = req.IsActive;
    }

    /// <summary>What the rules would charge each person for a month (recorded charges once the month is closed).</summary>
    [HttpGet("penalties/preview"), Authorize(Policy = Policies.Hr)]
    public async Task<List<PenaltyPreviewRow>> Preview([FromQuery] int year, [FromQuery] int month)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2100)
            throw new DomainException("Choose a valid month.");
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var people = await db.Employees.Where(e => e.JoiningDate <= end && (e.LeavingDate == null || e.LeavingDate >= start))
            .Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName }).ToListAsync();
        Dictionary<Guid, List<PenaltyLine>> lines;
        if (await db.AttendancePeriods.AnyAsync(p => p.Year == year && p.Month == month))
        {
            var rules = await db.PenaltyRules.ToDictionaryAsync(r => r.Id);
            lines = (await db.PenaltyCharges.Where(c => c.Year == year && c.Month == month).ToListAsync())
                .GroupBy(c => c.EmployeeId)
                .ToDictionary(g => g.Key, g => g.SelectMany(c => new[]
                {
                    c.LeaveDays > 0 ? new PenaltyLine(c.RuleId, c.RuleName, rules.GetValueOrDefault(c.RuleId)?.Flag ?? PenaltyFlag.Late, c.Occurrences, PenaltyAction.DeductLeave, c.LeaveDays, c.LeaveTypeId) : null,
                    c.PayDays > 0 ? new PenaltyLine(c.RuleId, c.RuleName, rules.GetValueOrDefault(c.RuleId)?.Flag ?? PenaltyFlag.Late, c.Occurrences, PenaltyAction.DeductPay, c.PayDays, null) : null,
                }).OfType<PenaltyLine>().ToList());
        }
        else
            lines = await penalties.ComputeAsync(people.Select(p => p.Id).ToList(), year, month);
        return people.Where(p => lines.GetValueOrDefault(p.Id) is { Count: > 0 }).OrderBy(p => p.Name)
            .Select(p =>
            {
                var l = lines[p.Id];
                return new PenaltyPreviewRow(p.Id, p.EmployeeCode, p.Name, l, l.Where(x => x.Action == PenaltyAction.DeductPay).Sum(x => x.Days),
                    l.Where(x => x.Action == PenaltyAction.DeductLeave).Sum(x => x.Days));
            }).ToList();
    }

    // ---------- Off-day requests ----------

    [HttpGet("off-days/my")]
    public Task<List<OffDayRequestDto>> MyOffDays() => ProjectAsync(db.OffDayRequests.Where(x => x.EmployeeId == me.EmployeeId));

    [HttpPost("off-days")]
    public async Task<IActionResult> RequestOffDays(RequestOffDayRequest req)
    {
        await offDays.RequestAsync(req);
        return NoContent();
    }

    [HttpPost("off-days/{id:guid}/cancel")]
    public async Task<IActionResult> CancelOffDays(Guid id)
    {
        await offDays.CancelAsync(id);
        return NoContent();
    }

    [HttpPost("off-days/{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> ApproveOffDays(Guid id, ExemptionDecisionRequest? req)
    {
        await offDays.DecideAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("off-days/{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> RejectOffDays(Guid id, ExemptionDecisionRequest? req)
    {
        await offDays.DecideAsync(id, false, req?.Note);
        return NoContent();
    }

    [HttpGet("off-days/approvals"), Authorize(Policy = Policies.Approver)]
    public async Task<List<OffDayRequestDto>> OffDayApprovals([FromQuery] RequestStatus status = RequestStatus.Pending)
    {
        var q = db.OffDayRequests.Where(x => x.Status == status && x.EmployeeId != me.EmployeeId);
        if (!me.IsHr())
        {
            var waiting = await approvals.WaitingForMeAsync(ApprovalRequestType.OffDay);
            q = q.Where(x => waiting.Contains(x.Id));
        }
        return await ProjectAsync(q);
    }

    private async Task<List<OffDayRequestDto>> ProjectAsync(IQueryable<OffDayRequest> q)
    {
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).Take(200)
            .Select(x => new { X = x, Name = x.Employee!.FirstName + " " + x.Employee.LastName, x.Employee.EmployeeCode, Usual = x.Employee.Shift != null ? x.Employee.Shift.WeeklyOffDays : null })
            .ToListAsync();
        return rows.Select(r => new OffDayRequestDto(r.X.Id, r.X.EmployeeId, r.Name, r.EmployeeCode, r.X.FromDate, r.X.ToDate, r.X.OffDays, r.Usual ?? [],
            r.X.Reason, r.X.Status, r.X.CreatedAtUtc, r.X.DecisionNote)).ToList();
    }
}
