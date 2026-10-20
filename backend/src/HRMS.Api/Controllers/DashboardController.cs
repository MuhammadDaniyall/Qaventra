using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain;
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

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Policies.CompanyUser)]
public class DashboardController(HrmsDbContext db, TenantClock clock, ICurrentUser me) : ControllerBase
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
                    r.Payslips.Count, r.TotalGross, r.TotalDeductions, r.TotalNet))
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
}
