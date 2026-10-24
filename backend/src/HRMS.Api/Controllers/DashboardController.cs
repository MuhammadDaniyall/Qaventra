using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record NameCountDto(string Name, int Count);

public record TrendPointDto(DateOnly Date, int Present, int Late);

public record DashboardDto(
    int Headcount, int NewJoinersThisMonth, int PresentToday, int LateToday, int OnLeaveToday, int PendingApprovals,
    List<NameCountDto> ByDepartment, List<TrendPointDto> AttendanceTrend,
    PayrollRunDto? LastPayroll, List<HolidayDto> UpcomingHolidays);

/// <summary>One month of workforce numbers. Turnover is leavers over the average of the opening and closing headcount.</summary>
public record MonthTrendDto(
    int Year, int Month, int Headcount, int Joiners, int Leavers, decimal TurnoverPercent,
    decimal LeaveDays, int LateArrivals, decimal AverageWorkedHours);

public enum CelebrationKind { Birthday, Anniversary }

/// <summary>A birthday or work anniversary on <paramref name="Date"/> (this year). Birth years are never shown.</summary>
public record CelebrationDto(Guid EmployeeId, string Name, string? Designation, string? Department, CelebrationKind Kind, DateOnly Date, int? Years,
    bool IsToday, bool IsMe, bool WishedByMe);

public record WishRequest(CelebrationKind Kind);

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Policies.CompanyUser)]
public class DashboardController(HrmsDbContext db, TenantClock clock, ICurrentUser me, NotificationService notify) : ControllerBase
{
    [HttpGet]
    public async Task<DashboardDto> Get()
    {
        var today = await clock.TodayAsync();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var current = db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
        var isHr = me.IsHr();

        // Same as the Approvals inbox: every module's requests, all of them for HR, otherwise those waiting on me.
        var mine = me.EmployeeId;
        var pending = db.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending && a.EmployeeId != mine);
        if (!isHr)
            pending = pending.Where(a => a.Steps.Any(s => s.Status == ApprovalStepStatus.Current && s.ApproverEmployeeId == mine));

        var trendFrom = today.AddDays(-13);
        var trend = await db.AttendanceRecords.Where(a => a.Date >= trendFrom && a.Date <= today)
            .GroupBy(a => a.Date)
            .Select(g => new TrendPointDto(g.Key, g.Count(), g.Count(a => a.Status == AttendanceStatus.Late)))
            .ToListAsync();
        var trendFilled = Enumerable.Range(0, 14).Select(i => trendFrom.AddDays(i))
            .Select(d => trend.FirstOrDefault(t => t.Date == d) ?? new TrendPointDto(d, 0, 0)).ToList();

        var lastPayroll = isHr
            ? await db.PayrollRuns.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
                .Select(r => new PayrollRunDto(r.Id, r.Year, r.Month, r.Status, r.TaxYearCode, r.ProcessedAtUtc, r.FinalizedAtUtc,
                    r.Payslips.Count, r.TotalGross, r.TotalDeductions, r.TotalNet, r.SubmittedAtUtc, r.ApprovedByName, r.ApprovedAtUtc, r.ApprovalNote,
                    r.DisbursedAtUtc, r.DisbursedVia, 0))
                .FirstOrDefaultAsync()
            : null;

        return new DashboardDto(
            await current.CountAsync(),
            await current.CountAsync(e => e.JoiningDate >= monthStart),
            await db.AttendanceRecords.CountAsync(a => a.Date == today),
            await db.AttendanceRecords.CountAsync(a => a.Date == today && a.Status == AttendanceStatus.Late),
            await db.LeaveRequests.CountAsync(r => r.Status == LeaveStatus.Approved && r.FromDate <= today && r.ToDate >= today),
            await pending.CountAsync(),
            await current.GroupBy(e => e.Department != null ? e.Department.Name : "Unassigned")
                .OrderByDescending(g => g.Count()).Select(g => new NameCountDto(g.Key, g.Count())).ToListAsync(),
            trendFilled,
            lastPayroll,
            await db.Holidays.Where(h => h.Date >= today).OrderBy(h => h.Date).Take(5)
                .Select(h => new HolidayDto(h.Id, h.Date, h.Name)).ToListAsync());
    }

    /// <summary>The last 12 months (this month included) for the HR reports dashboard.</summary>
    [HttpGet("trends"), Authorize(Policy = Policies.Hr)]
    public async Task<List<MonthTrendDto>> Trends()
    {
        var today = await clock.TodayAsync();
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);

        var people = await db.Employees.AsNoTracking()
            .Select(e => new { e.JoiningDate, e.LeavingDate }).ToListAsync();
        var leave = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Status == LeaveStatus.Approved && r.FromDate >= first)
            .Select(r => new { r.FromDate, r.Days }).ToListAsync();
        var attendance = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date >= first)
            .GroupBy(a => new { a.Date.Year, a.Date.Month })
            .Select(g => new
            {
                g.Key.Year, g.Key.Month,
                Late = g.Count(a => a.Status == AttendanceStatus.Late),
                Records = g.Count(a => a.WorkedMinutes > 0),
                Minutes = g.Sum(a => (long)a.WorkedMinutes),
            })
            .ToListAsync();

        // On the books on a day: joined by then and not yet gone.
        int OnBooks(DateOnly day) => people.Count(p => p.JoiningDate <= day && (p.LeavingDate is null || p.LeavingDate >= day));

        var months = new List<MonthTrendDto>();
        for (var i = 0; i < 12; i++)
        {
            var start = first.AddMonths(i);
            var end = start.AddMonths(1).AddDays(-1);
            var last = end > today ? today : end;
            var opening = OnBooks(start.AddDays(-1));
            var closing = OnBooks(last);
            var leavers = people.Count(p => p.LeavingDate is { } d && d >= start && d <= last);
            var average = (opening + closing) / 2m;
            var a = attendance.FirstOrDefault(x => x.Year == start.Year && x.Month == start.Month);
            months.Add(new MonthTrendDto(
                start.Year, start.Month, closing,
                people.Count(p => p.JoiningDate >= start && p.JoiningDate <= last),
                leavers,
                average > 0 ? Math.Round(leavers / average * 100, 1) : 0,
                leave.Where(r => r.FromDate >= start && r.FromDate <= end).Sum(r => r.Days),
                a?.Late ?? 0,
                a is { Records: > 0 } ? Math.Round(a.Minutes / 60m / a.Records, 1) : 0));
        }
        return months;
    }

    /// <summary>Birthdays and work anniversaries today and in the next 7 days, for everyone in the company.</summary>
    [HttpGet("celebrations")]
    public async Task<List<CelebrationDto>> Celebrations()
    {
        var today = await clock.TodayAsync();
        var people = await db.Employees.AsNoTracking()
            .Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .Select(e => new
            {
                e.Id, Name = e.FirstName + " " + e.LastName, e.DateOfBirth, e.JoiningDate,
                Designation = e.Designation != null ? e.Designation.Title : null,
                Department = e.Department != null ? e.Department.Name : null,
            })
            .ToListAsync();
        var wished = await WishedTodayAsync(today);

        var list = new List<CelebrationDto>();
        foreach (var p in people)
        {
            if (p.DateOfBirth is { } dob && Next(dob, today) is { } bday)
                list.Add(new(p.Id, p.Name, p.Designation, p.Department, CelebrationKind.Birthday, bday, null, bday == today, p.Id == me.EmployeeId,
                    wished.Contains((p.Id, CelebrationKind.Birthday))));
            if (Next(p.JoiningDate, today) is { } anniversary && anniversary.Year - p.JoiningDate.Year >= 1)
                list.Add(new(p.Id, p.Name, p.Designation, p.Department, CelebrationKind.Anniversary, anniversary, anniversary.Year - p.JoiningDate.Year,
                    anniversary == today, p.Id == me.EmployeeId, wished.Contains((p.Id, CelebrationKind.Anniversary))));
        }
        return list.OrderBy(c => c.Date).ThenBy(c => c.Kind).ThenBy(c => c.Name).ToList();
    }

    /// <summary>Sends the person a notification (and push) with the wish. Once a day per person and occasion.</summary>
    [HttpPost("celebrations/{employeeId:guid}/wish")]
    public async Task<IActionResult> Wish(Guid employeeId, WishRequest req)
    {
        if (employeeId == me.EmployeeId)
            throw new DomainException("You can't wish yourself.");
        var today = await clock.TodayAsync();
        var c = (await Celebrations()).FirstOrDefault(x => x.EmployeeId == employeeId && x.Kind == req.Kind && x.IsToday)
            ?? throw new DomainException("There is nothing to celebrate for them today.");
        if (c.WishedByMe)
            return NoContent();
        var from = await db.Users.IgnoreQueryFilters().Where(u => u.Id == me.UserId).Select(u => u.FullName).FirstAsync();
        var title = req.Kind == CelebrationKind.Birthday
            ? $"{from} wished you a happy birthday"
            : $"{from} congratulated you on {c.Years} {(c.Years == 1 ? "year" : "years")} with the company";
        await notify.ToEmployeesAsync([employeeId], NotificationKind.Celebration, title, null, "/dashboard");
        db.CelebrationWishes.Add(new CelebrationWish { FromUserId = me.UserId!.Value, EmployeeId = employeeId, Kind = (int)req.Kind, Date = today });
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<HashSet<(Guid, CelebrationKind)>> WishedTodayAsync(DateOnly today) =>
        (await db.CelebrationWishes.Where(w => w.FromUserId == me.UserId && w.Date == today).Select(w => new { w.EmployeeId, w.Kind }).ToListAsync())
        .Select(w => (w.EmployeeId, (CelebrationKind)w.Kind)).ToHashSet();

    /// <summary>This year's (or next year's) date of a yearly occasion if it falls within the next 7 days. 29 Feb counts on 28 Feb.</summary>
    private static DateOnly? Next(DateOnly original, DateOnly today)
    {
        foreach (var year in new[] { today.Year, today.Year + 1 })
        {
            var day = Math.Min(original.Day, DateTime.DaysInMonth(year, original.Month));
            var date = new DateOnly(year, original.Month, day);
            if (date >= today && date <= today.AddDays(7))
                return date;
        }
        return null;
    }
}
