using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record PayrollProcessResult(PayrollRun Run, IReadOnlyList<string> Warnings);

public class PayrollService(HrmsDbContext db, LeaveService leave, TenantClock clock, NotificationService notify)
{
    /// <summary>Calculates (or recalculates) a draft run for the month. A finalized run is locked.</summary>
    public async Task<PayrollProcessResult> ProcessAsync(int year, int month)
    {
        if (month is < 1 or > 12)
            throw new DomainException("Month must be between 1 and 12.");

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var daysInMonth = monthEnd.Day;
        var warnings = new List<string>();

        var taxYear = await db.TaxYears.Include(t => t.Slabs)
            .Where(t => t.StartDate <= monthStart)
            .OrderByDescending(t => t.StartDate)
            .FirstOrDefaultAsync()
            ?? throw new DomainException("No tax year is configured for this period.");
        if (monthStart > taxYear.EndDate)
            warnings.Add($"No tax slabs exist for this period; tax year {taxYear.Code} slabs were used. Add the current tax year's slabs.");

        var settings = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();

        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Year == year && r.Month == month);
        if (run is { Status: PayrollRunStatus.Finalized })
            throw new DomainException("This month's payroll is finalized and cannot be reprocessed.");
        if (run is null)
        {
            run = new PayrollRun { Year = year, Month = month };
            db.PayrollRuns.Add(run);
        }
        else
        {
            db.Payslips.RemoveRange(run.Payslips);
            run.Payslips.Clear();
        }

        // Approved expense claims ride on this run until it is finalized; recomputed on every reprocess.
        var claims = await ExpenseService.Payable(db, run.Id).ToListAsync();
        foreach (var c in claims)
            c.PayrollRunId = null;

        var employees = await db.Employees
            .Where(e => e.JoiningDate <= monthEnd && (e.LeavingDate == null || e.LeavingDate >= monthStart))
            .ToListAsync();
        var employeeIds = employees.Select(e => e.Id).ToList();

        var salaries = (await db.EmployeeSalaries.Include(s => s.Components)
                .Where(s => employeeIds.Contains(s.EmployeeId) && s.EffectiveFrom <= monthEnd)
                .ToListAsync())
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First());

        var unpaidLeaves = await db.LeaveRequests
            .Where(r => employeeIds.Contains(r.EmployeeId) && r.Status == LeaveStatus.Approved
                        && !r.LeaveType!.IsPaid && r.FromDate <= monthEnd && r.ToDate >= monthStart)
            .ToListAsync();

        var approvedLeaves = settings.DeductAbsentDays
            ? await db.LeaveRequests.Where(r => employeeIds.Contains(r.EmployeeId) && r.Status == LeaveStatus.Approved
                                                && r.FromDate <= monthEnd && r.ToDate >= monthStart).ToListAsync()
            : [];
        var attendanceDates = settings.DeductAbsentDays
            ? (await db.AttendanceRecords.Where(a => employeeIds.Contains(a.EmployeeId) && a.Date >= monthStart && a.Date <= monthEnd)
                .Select(a => new { a.EmployeeId, a.Date }).ToListAsync())
                .ToLookup(a => a.EmployeeId, a => a.Date)
            : null;
        var today = await clock.TodayAsync();

        foreach (var employee in employees)
        {
            if (!salaries.TryGetValue(employee.Id, out var salary))
            {
                warnings.Add($"{employee.EmployeeCode} {employee.FullName}: no salary set, skipped.");
                continue;
            }

            // Days outside employment are unpaid.
            var employedFrom = employee.JoiningDate > monthStart ? employee.JoiningDate : monthStart;
            var employedTo = employee.LeavingDate is { } left && left < monthEnd ? left : monthEnd;
            decimal unpaid = (employedFrom.DayNumber - monthStart.DayNumber) + (monthEnd.DayNumber - employedTo.DayNumber);

            var (weeklyOff, holidays) = await leave.CalendarForAsync(employee.Id, monthStart, monthEnd);
            foreach (var r in unpaidLeaves.Where(r => r.EmployeeId == employee.Id))
            {
                var overlap = WorkCalendar.Overlap(r.FromDate, r.ToDate, employedFrom, employedTo);
                if (overlap is { } o)
                    // Half day and short leave are single-day requests that already know their fraction of a day.
                    unpaid += r.Duration == LeaveDuration.FullDay ? WorkCalendar.CountWorkingDays(o.From, o.To, weeklyOff, holidays) : r.Days;
            }

            if (attendanceDates is not null)
            {
                var present = attendanceDates[employee.Id].ToHashSet();
                var lastDay = employedTo < today ? employedTo : today.AddDays(-1);
                for (var d = employedFrom; d <= lastDay; d = d.AddDays(1))
                {
                    if (weeklyOff.Contains(d.DayOfWeek) || holidays.Contains(d) || present.Contains(d))
                        continue;
                    if (approvedLeaves.Any(r => r.EmployeeId == employee.Id && r.FromDate <= d && r.ToDate >= d))
                        continue;
                    unpaid++;
                }
            }

            var employeeClaims = claims.Where(c => c.EmployeeId == employee.Id).ToList();
            foreach (var c in employeeClaims)
                c.PayrollRunId = run.Id;

            var result = PayrollCalculator.Calculate(new PayrollInput(
                salary, taxYear, daysInMonth, unpaid, settings.EobiEmployeeContribution, settings.EobiEmployerContribution,
                employeeClaims.Sum(c => c.Total)));

            var payslip = new Payslip
            {
                PayrollRunId = run.Id,
                EmployeeId = employee.Id,
                DaysInMonth = daysInMonth,
                UnpaidDays = unpaid,
                PaidDays = result.PaidDays,
                GrossEarnings = result.GrossEarnings,
                TaxableIncome = result.TaxableIncome,
                IncomeTax = result.IncomeTax,
                EobiEmployee = result.EobiEmployee,
                EobiEmployer = result.EobiEmployer,
                OtherDeductions = result.OtherDeductions,
                NetPay = result.NetPay,
                Lines = result.Lines.Select(l => new PayslipLine { Name = l.Name, Kind = l.Kind, Amount = l.Amount }).ToList(),
            };
            run.Payslips.Add(payslip);
            db.Payslips.Add(payslip);
        }

        run.TaxYearCode = taxYear.Code;
        run.ProcessedAtUtc = clock.UtcNow;
        run.TotalGross = run.Payslips.Sum(p => p.GrossEarnings);
        run.TotalNet = run.Payslips.Sum(p => p.NetPay);
        run.TotalDeductions = run.TotalGross - run.TotalNet;
        await db.SaveChangesAsync();
        return new PayrollProcessResult(run, warnings);
    }

    public async Task FinalizeAsync(Guid runId)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId) ?? throw new NotFoundException("Payroll run");
        if (run.Status == PayrollRunStatus.Finalized)
            throw new DomainException("This payroll is already finalized.");
        run.Status = PayrollRunStatus.Finalized;
        run.FinalizedAtUtc = clock.UtcNow;

        var claims = await db.ExpenseClaims.Where(c => c.PayrollRunId == run.Id && c.Status == ExpenseStatus.Approved).ToListAsync();
        foreach (var c in claims)
        {
            c.Status = ExpenseStatus.Paid;
            c.PaidAtUtc = run.FinalizedAtUtc;
            c.PaidVia = $"Payroll {new DateOnly(run.Year, run.Month, 1):MMMM yyyy}";
        }

        var month = new DateOnly(run.Year, run.Month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var paid = await db.Payslips.Where(p => p.PayrollRunId == run.Id).Select(p => p.EmployeeId).ToListAsync();
        await notify.ToEmployeesAsync(paid, NotificationKind.Payslip, $"Your payslip for {month} is ready", null, "/me/payslips");
        await db.SaveChangesAsync();
    }
}
