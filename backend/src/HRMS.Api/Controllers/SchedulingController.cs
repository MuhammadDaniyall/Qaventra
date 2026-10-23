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

public record RosterShiftDto(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>One day for one employee. Rostered = set on the roster rather than from their usual shift.</summary>
public record RosterCellDto(DateOnly Date, Guid? ShiftId, bool IsOff, bool IsHoliday, bool Rostered, ShiftAssignmentSource? Source);

public record RosterRowDto(Guid EmployeeId, string EmployeeCode, string Name, string? Department, Guid? DefaultShiftId, List<RosterCellDto> Days);

public record RosterDto(DateOnly From, DateOnly To, List<RosterShiftDto> Shifts, List<RosterRowDto> Rows, List<string> ClosedMonths);

public record AssignRosterRequest(
    [Required, MaxLength(500)] List<Guid> EmployeeIds, DateOnly From, DateOnly To, List<DayOfWeek>? Weekdays, Guid? ShiftId, bool Off, bool Clear);

public record ShiftChangeRequestBody(DateOnly From, DateOnly To, Guid ShiftId, [Required, StringLength(1000)] string Reason);

public record ShiftSwapRequestBody(Guid ColleagueId, DateOnly From, DateOnly To, [Required, StringLength(1000)] string Reason);

public record DecideShiftRequest([StringLength(1000)] string? Note);

public record ShiftRequestDto(
    Guid Id, ShiftRequestKind Kind, Guid EmployeeId, string EmployeeName, string EmployeeCode, DateOnly FromDate, DateOnly ToDate,
    string? ShiftName, Guid? SwapWithEmployeeId, string? SwapWithName, string Reason, ShiftRequestStatus Status,
    DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote);

public record MonthlyRowDto(
    Guid EmployeeId, string EmployeeCode, string Name, string? Department, int WorkingDays, int Present, int Late, int HalfDay,
    decimal Leave, int Absent, int OffDays, int WorkedMinutes, decimal PenaltyDays);

public record MonthlyAttendanceDto(int Year, int Month, bool Closed, DateTime? ClosedAtUtc, string? ClosedByName, bool CanClose,
    DateOnly CountedTo, List<MonthlyRowDto> Rows);

[ApiController]
[Route("api/attendance")]
[Authorize(Policy = Policies.CompanyUser)]
public class SchedulingController(
    HrmsDbContext db, ScheduleService schedule, ShiftRequestService shiftRequests, ApprovalEngine approvals, TenantClock clock, ICurrentUser me, PenaltyService penalties)
    : ControllerBase
{
    // ---------- Roster ----------

    [HttpGet("roster"), Authorize(Policy = Policies.Approver)]
    public async Task<RosterDto> Roster([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? departmentId)
    {
        if (to < from || to.DayNumber - from.DayNumber > 41)
            throw new DomainException("Show between 1 and 42 days at a time.");
        var q = db.Employees.Where(e => (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation) && e.JoiningDate <= to);
        if (me.Role == UserRole.Manager)
            q = q.Where(e => e.ManagerId == me.EmployeeId || e.Id == me.EmployeeId);
        if (departmentId is { } d)
            q = q.Where(e => e.DepartmentId == d);
        var people = await q.OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : null, e.ShiftId })
            .Take(500).ToListAsync();
        var calendars = await schedule.ForEmployeesAsync(people.Select(p => p.Id).ToList(), from, to);
        var roster = (await db.ShiftAssignments.Where(a => a.Date >= from && a.Date <= to).Select(a => new { a.EmployeeId, a.Date, a.Source }).ToListAsync())
            .ToDictionary(a => (a.EmployeeId, a.Date), a => a.Source);
        var shifts = await db.Shifts.OrderBy(s => s.StartTime).Select(s => new RosterShiftDto(s.Id, s.Name, s.StartTime, s.EndTime)).ToListAsync();
        var closed = await db.AttendancePeriods.Where(p => p.Year * 12 + p.Month >= from.Year * 12 + from.Month && p.Year * 12 + p.Month <= to.Year * 12 + to.Month)
            .Select(p => p.Year + "-" + p.Month).ToListAsync();

        var rows = people.Select(p =>
        {
            var c = calendars[p.Id];
            var days = new List<RosterCellDto>();
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var source = roster.TryGetValue((p.Id, day), out var s) ? s : (ShiftAssignmentSource?)null;
                days.Add(new RosterCellDto(day, c.ShiftOn(day)?.Id, c.IsOff(day), c.IsHoliday(day), source is not null, source));
            }
            return new RosterRowDto(p.Id, p.EmployeeCode, p.Name, p.Department, p.ShiftId, days);
        }).ToList();
        return new RosterDto(from, to, shifts, rows, closed);
    }

    [HttpPut("roster"), Authorize(Policy = Policies.Hr)]
    public async Task<object> Assign(AssignRosterRequest req)
    {
        var count = await schedule.AssignAsync(req.EmployeeIds ?? [], req.From, req.To, req.Weekdays, req.ShiftId, req.Off, req.Clear);
        return new { days = count };
    }

    // ---------- Shift requests ----------

    /// <summary>People an employee can swap shifts with: everyone current, own department first.</summary>
    [HttpGet("colleagues")]
    public async Task<List<LookupDto>> Colleagues()
    {
        var mine = MyId();
        var dept = await db.Employees.Where(e => e.Id == mine).Select(e => e.DepartmentId).FirstOrDefaultAsync();
        return await db.Employees.Where(e => e.Id != mine && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation))
            .OrderBy(e => e.DepartmentId == dept ? 0 : 1).ThenBy(e => e.FirstName)
            .Select(e => new LookupDto(e.Id, e.FirstName + " " + e.LastName + (e.Department != null ? " · " + e.Department.Name : "")))
            .Take(1000).ToListAsync();
    }

    /// <summary>The signed-in employee's requests, plus swaps colleagues asked them for.</summary>
    [HttpGet("shift-requests/my")]
    public Task<List<ShiftRequestDto>> MyShiftRequests() =>
        ProjectAsync(db.ShiftRequests.Where(r => r.EmployeeId == me.EmployeeId || (r.SwapWithEmployeeId == me.EmployeeId && r.Status != ShiftRequestStatus.Cancelled)));

    [HttpGet("shift-requests"), Authorize(Policy = Policies.Approver)]
    public async Task<List<ShiftRequestDto>> ShiftRequests([FromQuery] ShiftRequestStatus status = ShiftRequestStatus.Pending)
    {
        var q = db.ShiftRequests.Where(r => r.Status == status && r.EmployeeId != me.EmployeeId);
        if (!me.IsHr())
        {
            var mine = status == ShiftRequestStatus.Pending
                ? await approvals.WaitingForMeAsync(ApprovalRequestType.ShiftChange)
                : approvals.DecidedByMe(ApprovalRequestType.ShiftChange);
            q = q.Where(r => mine.Contains(r.Id));
        }
        return await ProjectAsync(q);
    }

    [HttpPost("shift-requests/change")]
    public async Task<ShiftRequestDto> RequestChange(ShiftChangeRequestBody req)
    {
        var r = await shiftRequests.RequestChangeAsync(MyId(), req.From, req.To, req.ShiftId, req.Reason);
        return (await ProjectAsync(db.ShiftRequests.Where(x => x.Id == r.Id))).Single();
    }

    [HttpPost("shift-requests/swap")]
    public async Task<ShiftRequestDto> RequestSwap(ShiftSwapRequestBody req)
    {
        var r = await shiftRequests.RequestSwapAsync(MyId(), req.ColleagueId, req.From, req.To, req.Reason);
        return (await ProjectAsync(db.ShiftRequests.Where(x => x.Id == r.Id))).Single();
    }

    [HttpPost("shift-requests/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        await shiftRequests.RespondAsync(id, accept: true);
        return NoContent();
    }

    [HttpPost("shift-requests/{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id)
    {
        await shiftRequests.RespondAsync(id, accept: false);
        return NoContent();
    }

    [HttpPost("shift-requests/{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, DecideShiftRequest req)
    {
        await shiftRequests.DecideAsync(id, approve: true, req.Note);
        return NoContent();
    }

    [HttpPost("shift-requests/{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, DecideShiftRequest req)
    {
        await shiftRequests.DecideAsync(id, approve: false, req.Note);
        return NoContent();
    }

    [HttpPost("shift-requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await shiftRequests.CancelAsync(id);
        return NoContent();
    }

    // ---------- Monthly summary and closing ----------

    /// <summary>Per-employee totals for a month, counted up to yesterday for the current month.</summary>
    [HttpGet("monthly"), Authorize(Policy = Policies.Approver)]
    public async Task<MonthlyAttendanceDto> Monthly([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? departmentId)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2100)
            throw new DomainException("Choose a valid month.");
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var today = await clock.TodayAsync();
        var countedTo = monthEnd < today ? monthEnd : today.AddDays(-1);

        var q = db.Employees.Where(e => e.JoiningDate <= monthEnd && (e.LeavingDate == null || e.LeavingDate >= monthStart)
                                        && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation || e.LeavingDate != null));
        if (me.Role == UserRole.Manager)
            q = q.Where(e => e.ManagerId == me.EmployeeId);
        if (departmentId is { } d)
            q = q.Where(e => e.DepartmentId == d);
        var people = await q.OrderBy(e => e.EmployeeCode)
            .Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : null, e.JoiningDate, e.LeavingDate })
            .ToListAsync();
        var ids = people.Select(p => p.Id).ToList();
        var calendars = await schedule.ForEmployeesAsync(ids, monthStart, monthEnd);
        var records = (await db.AttendanceRecords.Where(a => ids.Contains(a.EmployeeId) && a.Date >= monthStart && a.Date <= monthEnd)
            .Select(a => new { a.EmployeeId, a.Date, a.Status, a.WorkedMinutes }).ToListAsync()).ToLookup(a => a.EmployeeId);
        var leaves = (await db.LeaveRequests.Where(r => ids.Contains(r.EmployeeId) && r.Status == LeaveStatus.Approved && r.FromDate <= monthEnd && r.ToDate >= monthStart)
            .Select(r => new { r.EmployeeId, r.FromDate, r.ToDate, r.Duration, r.Days }).ToListAsync()).ToLookup(r => r.EmployeeId);
        var settings = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();
        var period = await db.AttendancePeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month);
        var ruleDays = period is not null
            ? (await db.PenaltyCharges.Where(c => c.Year == year && c.Month == month && ids.Contains(c.EmployeeId)).ToListAsync())
                .GroupBy(c => c.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(c => c.PayDays + c.LeaveDays))
            : (await penalties.ComputeAsync(ids, year, month)).ToDictionary(x => x.Key, x => x.Value.Sum(l => l.Days));

        var rows = people.Select(p =>
        {
            var c = calendars[p.Id];
            var from = p.JoiningDate > monthStart ? p.JoiningDate : monthStart;
            var to = p.LeavingDate is { } l && l < countedTo ? l : countedTo;
            var mine = records[p.Id].ToList();
            var byDate = mine.ToDictionary(r => r.Date);
            int working = 0, off = 0, absent = 0;
            decimal leave = 0;
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (!c.IsWorkingDay(day)) { off++; continue; }
                working++;
                var onLeave = leaves[p.Id].FirstOrDefault(r => r.FromDate <= day && r.ToDate >= day);
                if (onLeave is not null)
                    leave += onLeave.Duration == LeaveDuration.FullDay ? 1 : onLeave.Days;
                else if (!byDate.ContainsKey(day))
                    absent++;
            }
            var late = mine.Count(r => r.Status == AttendanceStatus.Late);
            var half = mine.Count(r => r.Status == AttendanceStatus.HalfDay);
            var penalty = (settings.LatesPerPenalty > 0 ? late / settings.LatesPerPenalty * settings.LatePenaltyDays : 0)
                          + (settings.DeductHalfDays ? half / 2m : 0) + ruleDays.GetValueOrDefault(p.Id);
            return new MonthlyRowDto(p.Id, p.EmployeeCode, p.Name, p.Department, working, mine.Count, late, half, leave, absent, off,
                mine.Sum(r => r.WorkedMinutes), penalty);
        }).ToList();
        return new MonthlyAttendanceDto(year, month, period is not null, period?.ClosedAtUtc, period?.ClosedByName, monthEnd < today, countedTo, rows);
    }

    /// <summary>Locks a finished month: no punches, corrections, requests or roster changes until reopened.</summary>
    [HttpPost("periods/{year:int}/{month:int}/close"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Close(int year, int month)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2100)
            throw new DomainException("Choose a valid month.");
        var monthEnd = new DateOnly(year, month, 1).AddMonths(1).AddDays(-1);
        if (monthEnd >= await clock.TodayAsync())
            throw new DomainException("A month can be closed once it has ended.");
        if (await db.AttendancePeriods.AnyAsync(p => p.Year == year && p.Month == month))
            throw new DomainException("This month is already closed.");
        db.AttendancePeriods.Add(new AttendancePeriod
        {
            Year = year, Month = month, ClosedAtUtc = clock.UtcNow,
            ClosedByName = await db.Users.IgnoreQueryFilters().Where(u => u.Id == me.UserId).Select(u => u.FullName).FirstOrDefaultAsync(),
        });
        await db.SaveChangesAsync();
        await penalties.ApplyOnCloseAsync(year, month);
        return NoContent();
    }

    [HttpPost("periods/{year:int}/{month:int}/reopen"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Reopen(int year, int month)
    {
        var period = await db.AttendancePeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month) ?? throw new NotFoundException("Closed month");
        if (await db.PayrollRuns.AnyAsync(r => r.Year == year && r.Month == month && r.Status == PayrollRunStatus.Finalized))
            throw new DomainException("Payroll for this month is finalized, so its attendance stays closed.");
        db.AttendancePeriods.Remove(period);
        await db.SaveChangesAsync();
        await penalties.UndoOnReopenAsync(year, month);
        return NoContent();
    }

    private Guid MyId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");

    private async Task<List<ShiftRequestDto>> ProjectAsync(IQueryable<ShiftRequest> q)
    {
        var rows = await q.OrderByDescending(r => r.CreatedAtUtc).Take(200).Select(r => new
        {
            r.Id, r.Kind, r.EmployeeId, r.Employee!.FirstName, r.Employee.LastName, r.Employee.EmployeeCode, r.FromDate, r.ToDate,
            ShiftName = db.Shifts.Where(s => s.Id == r.ShiftId).Select(s => s.Name).FirstOrDefault(),
            r.SwapWithEmployeeId,
            SwapWith = db.Employees.Where(e => e.Id == r.SwapWithEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            r.Reason, r.Status, r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote,
        }).ToListAsync();
        return rows.Select(r => new ShiftRequestDto(r.Id, r.Kind, r.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, r.FromDate, r.ToDate,
            r.ShiftName, r.SwapWithEmployeeId, r.SwapWith, r.Reason, r.Status, r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote)).ToList();
    }
}
