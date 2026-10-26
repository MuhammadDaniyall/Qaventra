using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record PayrollProcessResult(PayrollRun Run, IReadOnlyList<string> Warnings);

public class PayrollService(HrmsDbContext db, ScheduleService schedules, TenantClock clock, NotificationService notify, ICurrentUser me, IServiceProvider services)
{
    /// <summary>Calculates (or recalculates) a draft run for the month's monthly-paid people. A finalized run is locked.</summary>
    public Task<PayrollProcessResult> ProcessAsync(int year, int month)
    {
        if (month is < 1 or > 12)
            throw new DomainException("Month must be between 1 and 12.");
        var first = new DateOnly(year, month, 1);
        return ProcessAsync(new PayPeriod(PayFrequency.Monthly, first, first.AddMonths(1).AddDays(-1)));
    }

    /// <summary>
    /// Calculates (or recalculates) a draft run for one pay period of the people paid at that frequency. Weekly,
    /// bi-weekly and semi-monthly periods pay their share of the monthly salary; once-a-month amounts (EOBI, recurring
    /// items, loan installments, attendance rule penalties) go in the first period ending in each month.
    /// </summary>
    public async Task<PayrollProcessResult> ProcessAsync(PayPeriod pay)
    {
        var (year, month) = (pay.Year, pay.Month);
        var monthly = pay.Frequency == PayFrequency.Monthly;
        var monthlyItems = PayPeriods.TakesMonthlyItems(pay);
        var share = PayPeriods.ShareOfMonth(pay.Frequency);
        // For a monthly run these are the month; otherwise the pay period.
        var monthStart = pay.Start;
        var monthEnd = pay.End;
        var daysInMonth = pay.Days;
        var warnings = new List<string>();

        var taxYear = await db.TaxYears.Include(t => t.Slabs)
            .Where(t => t.StartDate <= monthStart)
            .OrderByDescending(t => t.StartDate)
            .FirstOrDefaultAsync()
            ?? throw new DomainException("No tax year is configured for this period.");
        if (monthStart > taxYear.EndDate)
            warnings.Add($"No tax slabs exist for this period; tax year {taxYear.Code} slabs were used. Add the current tax year's slabs.");

        var settings = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();

        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Frequency == pay.Frequency && r.PeriodStart == pay.Start);
        if (run is { Status: PayrollRunStatus.Finalized })
            throw new DomainException(monthly ? "This month's payroll is finalized and cannot be reprocessed." : "This period's payroll is finalized and cannot be reprocessed.");
        if (run is { Status: PayrollRunStatus.Submitted or PayrollRunStatus.Approved })
        {
            // Numbers may change, so an earlier approval no longer holds.
            run.Status = PayrollRunStatus.Draft;
            run.ApprovedByName = null;
            run.ApprovedAtUtc = null;
            run.SubmittedAtUtc = null;
            warnings.Add("Reprocessing put the payroll back to draft. Send it for approval again.");
        }
        if (run is null)
        {
            run = new PayrollRun { Year = year, Month = month, Frequency = pay.Frequency, PeriodStart = pay.Start, PeriodEnd = pay.End };
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

        // One-off pay items for this month or earlier that no finalized payroll has paid yet; same re-linking as claims.
        var period = year * 12 + month;
        var payItems = await db.PayRequests
            .Where(r => r.Status == RequestStatus.Approved && r.PaidAtUtc == null && (r.PayrollRunId == null || r.PayrollRunId == run.Id)
                        && r.Year * 12 + r.Month <= period)
            .ToListAsync();
        foreach (var r in payItems)
            r.PayrollRunId = null;

        // Approved piece work for this month or earlier that no finalized payroll has paid; same re-linking as pay items.
        var pieces = await db.PieceEntries.Include(x => x.Item)
            .Where(x => x.Status == RequestStatus.Approved && x.PaidAtUtc == null && (x.PayrollRunId == null || x.PayrollRunId == run.Id) && x.Date <= monthEnd)
            .ToListAsync();
        foreach (var x in pieces)
            x.PayrollRunId = null;

        var recurring = monthlyItems
            ? await db.RecurringPayItems
                .Where(r => r.StartYear * 12 + r.StartMonth <= period && (r.EndYear == null || r.EndYear * 12 + r.EndMonth >= period))
                .ToListAsync()
            : [];

        // Loan installments: drop this run's earlier ones (reprocess), then take the next installment of every active loan.
        db.LoanRepayments.RemoveRange(await db.LoanRepayments.Where(x => x.PayrollRunId == run.Id).ToListAsync());
        var loans = monthlyItems
            ? await db.Loans.Include(l => l.Repayments)
                .Where(l => l.Status == RequestStatus.Approved && l.StartYear * 12 + l.StartMonth <= period)
                .ToListAsync()
            : [];

        var employees = await db.Employees
            .Where(e => e.PayFrequency == pay.Frequency && e.JoiningDate <= monthEnd && (e.LeavingDate == null || e.LeavingDate >= monthStart))
            .ToListAsync();
        var employeeIds = employees.Select(e => e.Id).ToList();

        var salaries = (await db.EmployeeSalaries.Include(s => s.Components)
                .Where(s => employeeIds.Contains(s.EmployeeId) && s.EffectiveFrom <= monthEnd)
                .ToListAsync())
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(g => g.Key, g => monthly ? g.OrderByDescending(s => s.EffectiveFrom).First() : ShareOf(g.OrderByDescending(s => s.EffectiveFrom).First(), share));

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
        // Late arrivals and half days, for the penalties set in payroll settings.
        var flagged = settings.LatesPerPenalty > 0 || settings.DeductHalfDays
            ? (await db.AttendanceRecords.Where(a => employeeIds.Contains(a.EmployeeId) && a.Date >= monthStart && a.Date <= monthEnd
                                                     && (a.Status == AttendanceStatus.Late || a.Status == AttendanceStatus.HalfDay))
                .Select(a => new { a.EmployeeId, a.Status }).ToListAsync())
                .ToLookup(a => a.EmployeeId, a => a.Status)
            : null;
        var calendars = await schedules.ForEmployeesAsync(employeeIds, monthStart, monthEnd);
        var today = await clock.TodayAsync();
        // Penalty rules (late, early leave, absent…) that cost pay this month.
        var rulePenalties = monthly
            ? await services.GetRequiredService<HRMS.Api.Controllers.PenaltyService>().PayDaysAsync(employeeIds, monthStart.Year, monthStart.Month)
            : [];
        // Tax adjustments for the tax year (previous employer, credits) and what this employer already withheld this year.
        var adjustments = (await db.TaxAdjustments.Where(a => a.TaxYearCode == taxYear.Code && employeeIds.Contains(a.EmployeeId)).ToListAsync())
            .ToLookup(a => a.EmployeeId);
        var yearStart = new DateOnly(taxYear.StartDate.Year, taxYear.StartDate.Month, 1);
        var monthsLeft = Math.Max(1, 12 - ((monthStart.Year - yearStart.Year) * 12 + monthStart.Month - yearStart.Month));
        var startPeriod = yearStart.Year * 12 + yearStart.Month;
        var yearToDate = (await db.Payslips.Where(p => employeeIds.Contains(p.EmployeeId) && p.PayrollRun!.Status == PayrollRunStatus.Finalized
                                                       && p.PayrollRun.Year * 12 + p.PayrollRun.Month >= startPeriod && p.PayrollRun.Year * 12 + p.PayrollRun.Month < period)
                .Select(p => new { p.EmployeeId, p.TaxableIncome, p.IncomeTax }).ToListAsync())
            .GroupBy(p => p.EmployeeId).ToDictionary(g => g.Key, g => (Taxable: g.Sum(x => x.TaxableIncome), Tax: g.Sum(x => x.IncomeTax)));

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

            var calendar = calendars[employee.Id];
            foreach (var r in unpaidLeaves.Where(r => r.EmployeeId == employee.Id))
            {
                var overlap = WorkCalendar.Overlap(r.FromDate, r.ToDate, employedFrom, employedTo);
                if (overlap is { } o)
                    // Half day and short leave are single-day requests that already know their fraction of a day.
                    unpaid += r.Duration == LeaveDuration.FullDay ? calendar.CountWorkingDays(o.From, o.To) : r.Days;
            }

            if (attendanceDates is not null)
            {
                var present = attendanceDates[employee.Id].ToHashSet();
                var lastDay = employedTo < today ? employedTo : today.AddDays(-1);
                for (var d = employedFrom; d <= lastDay; d = d.AddDays(1))
                {
                    if (!calendar.IsWorkingDay(d) || present.Contains(d))
                        continue;
                    if (approvedLeaves.Any(r => r.EmployeeId == employee.Id && r.FromDate <= d && r.ToDate >= d))
                        continue;
                    unpaid++;
                }
            }

            var employeeClaims = claims.Where(c => c.EmployeeId == employee.Id).ToList();
            foreach (var c in employeeClaims)
                c.PayrollRunId = run.Id;

            var extras = new List<PayItem>();
            if (flagged is not null)
            {
                // A day's pay is the monthly gross over the days in the month, as unpaid days are.
                var dayRate = (salary.BasicSalary + salary.Components.Where(c => c.Kind == SalaryComponentKind.Earning).Sum(c => c.Amount)) / daysInMonth;
                var lates = flagged[employee.Id].Count(x => x == AttendanceStatus.Late);
                var halves = flagged[employee.Id].Count(x => x == AttendanceStatus.HalfDay);
                if (settings.LatesPerPenalty > 0 && lates >= settings.LatesPerPenalty)
                {
                    var days = lates / settings.LatesPerPenalty * settings.LatePenaltyDays;
                    extras.Add(new PayItem($"Late arrival penalty ({lates} late, {days:0.#} day{(days == 1 ? "" : "s")})",
                        SalaryComponentKind.Deduction, Math.Round(dayRate * days, 0), false));
                }
                if (settings.DeductHalfDays && halves > 0)
                    extras.Add(new PayItem($"Half days ({halves} × ½ day)", SalaryComponentKind.Deduction, Math.Round(dayRate * halves / 2m, 0), false));
            }
            if (rulePenalties.GetValueOrDefault(employee.Id) is { Count: > 0 } penaltyLines)
            {
                var dayRate = (salary.BasicSalary + salary.Components.Where(c => c.Kind == SalaryComponentKind.Earning).Sum(c => c.Amount)) / daysInMonth;
                foreach (var (label, days) in penaltyLines)
                    extras.Add(new PayItem($"{label}, {days:0.##} day{(days == 1 ? "" : "s")}", SalaryComponentKind.Deduction, Math.Round(dayRate * days, 0), false));
            }
            foreach (var item in payItems.Where(r => r.EmployeeId == employee.Id).OrderBy(r => r.Kind))
            {
                item.PayrollRunId = run.Id;
                var name = item.Kind switch
                {
                    PayRequestKind.Overtime => $"Overtime ({item.Hours:0.#} h)",
                    PayRequestKind.AdvanceSalary => "Advance salary recovery",
                    _ => item.Title,
                };
                extras.Add(new PayItem(name, item.IsEarning ? SalaryComponentKind.Earning : SalaryComponentKind.Deduction, item.Amount, item.IsEarning && item.IsTaxable));
            }
            foreach (var r in recurring.Where(r => r.EmployeeId == employee.Id))
                extras.Add(new PayItem(r.Title, r.IsEarning ? SalaryComponentKind.Earning : SalaryComponentKind.Deduction, r.Amount, r.IsEarning && r.IsTaxable));
            foreach (var group in pieces.Where(x => x.EmployeeId == employee.Id).GroupBy(x => x.Item!))
            {
                foreach (var x in group)
                    x.PayrollRunId = run.Id;
                extras.Add(new PayItem($"Piece work: {group.Key.Name} ({group.Sum(x => x.Quantity):0.##} {group.Key.Unit})", SalaryComponentKind.Earning,
                    Math.Round(group.Sum(x => x.Amount), 0), true));
            }
            foreach (var loan in loans.Where(l => l.EmployeeId == employee.Id))
            {
                var repaid = loan.Repayments.Where(x => x.PayrollRunId != run.Id).Sum(x => x.Amount);
                var remaining = loan.Amount - repaid;
                if (remaining <= 0)
                    continue;
                var installment = Math.Min(loan.InstallmentAmount, remaining);
                var number = loan.Repayments.Count(x => x.PayrollRunId != run.Id) + 1;
                db.LoanRepayments.Add(new LoanRepayment { LoanId = loan.Id, PayrollRunId = run.Id, Amount = installment });
                extras.Add(new PayItem($"{loan.Title} installment ({number}/{loan.Installments})", SalaryComponentKind.Deduction, installment, false));
            }

            // Provident fund: the member's share is deducted; the company's share is recorded on the payslip.
            var paidFactor = Math.Clamp((daysInMonth - unpaid) / daysInMonth, 0, 1);
            var fundBase = (settings.PfOnGross
                ? salary.BasicSalary + salary.Components.Where(c => c.Kind == SalaryComponentKind.Earning).Sum(c => c.Amount)
                : salary.BasicSalary) * paidFactor;
            var pf = settings.ProvidentFundEnabled && employee.ProvidentFundMember;
            var pfEmployee = pf ? Math.Round(fundBase * settings.PfEmployeePercent / 100m, 0, MidpointRounding.AwayFromZero) : 0m;
            var pfEmployer = pf ? Math.Round(fundBase * settings.PfEmployerPercent / 100m, 0, MidpointRounding.AwayFromZero) : 0m;
            if (pfEmployee > 0)
                extras.Add(new PayItem("Provident fund", SalaryComponentKind.Deduction, pfEmployee, false));

            // Voluntary pension: shares of the earned basic; the member's own share is deducted and earns a tax credit.
            var vps = settings.VpsEnabled && employee.VpsMember;
            var vpsBase = salary.BasicSalary * paidFactor;
            var vpsEmployee = vps ? Math.Round(vpsBase * employee.VpsEmployeePercent / 100m, 0, MidpointRounding.AwayFromZero) : 0m;
            var vpsEmployer = vps ? Math.Round(vpsBase * employee.VpsEmployerPercent / 100m, 0, MidpointRounding.AwayFromZero) : 0m;
            if (vpsEmployee > 0)
                extras.Add(new PayItem($"Voluntary pension ({employee.VpsProvider ?? "VPS"})", SalaryComponentKind.Deduction, vpsEmployee, false));

            Func<decimal, decimal>? taxFor = monthly ? null : t => PayPeriods.PeriodTax(taxYear, t, pay.Frequency);
            if (adjustments[employee.Id].Any() && !monthly)
            {
                var adj = adjustments[employee.Id].ToList();
                decimal Sum(TaxAdjustmentKind k) => adj.Where(a => a.Kind == k).Sum(a => a.Amount);
                var perYear = PayPeriods.PerYear(pay.Frequency);
                taxFor = periodTaxable =>
                {
                    var income = Sum(TaxAdjustmentKind.PreviousIncome) + Sum(TaxAdjustmentKind.OtherIncome) + periodTaxable * perYear;
                    var due = TaxCalculator.AnnualTax(taxYear, income) - Sum(TaxAdjustmentKind.TaxCredit) - Sum(TaxAdjustmentKind.PreviousTax);
                    return Math.Max(0, Math.Round(due / perYear, 0, MidpointRounding.AwayFromZero));
                };
            }
            else if (adjustments[employee.Id].Any())
            {
                var adj = adjustments[employee.Id].ToList();
                decimal Sum(TaxAdjustmentKind k) => adj.Where(a => a.Kind == k).Sum(a => a.Amount);
                var ytd = yearToDate.GetValueOrDefault(employee.Id);
                taxFor = monthTaxable =>
                {
                    var income = Sum(TaxAdjustmentKind.PreviousIncome) + Sum(TaxAdjustmentKind.OtherIncome) + ytd.Taxable + monthTaxable * monthsLeft;
                    var due = TaxCalculator.AnnualTax(taxYear, income) - Sum(TaxAdjustmentKind.TaxCredit) - Sum(TaxAdjustmentKind.PreviousTax) - ytd.Tax;
                    return Math.Max(0, Math.Round(due / monthsLeft, 0, MidpointRounding.AwayFromZero));
                };
            }

            // Section 63: the tax saved is the average tax rate times the contribution, up to the cap's share of taxable pay.
            var vpsCredit = 0m;
            if (vpsEmployee > 0)
            {
                var plainTax = taxFor ?? (t => TaxCalculator.MonthlyTax(taxYear, t));
                taxFor = monthTaxable =>
                {
                    var tax = plainTax(monthTaxable);
                    if (tax <= 0 || monthTaxable <= 0)
                        return tax;
                    var eligible = Math.Min(vpsEmployee, monthTaxable * settings.VpsCreditCapPercent / 100m);
                    vpsCredit = Math.Min(tax, Math.Round(tax / monthTaxable * eligible, 0, MidpointRounding.AwayFromZero));
                    return tax - vpsCredit;
                };
            }

            var result = PayrollCalculator.Calculate(new PayrollInput(
                salary, taxYear, daysInMonth, unpaid, monthlyItems ? settings.EobiEmployeeContribution : 0m, monthlyItems ? settings.EobiEmployerContribution : 0m,
                employeeClaims.Sum(c => c.Total - c.AdvanceAdjusted), extras, taxFor));
            var wages = result.GrossEarnings - employeeClaims.Sum(c => c.Total - c.AdvanceAdjusted);
            var socialSecurity = settings.SocialSecurityPercent > 0 && !string.IsNullOrEmpty(employee.SocialSecurityNo)
                ? Math.Round(Math.Min(wages, settings.SocialSecurityWageCeiling is { } ceiling ? ceiling * share : wages) * settings.SocialSecurityPercent / 100m, 0, MidpointRounding.AwayFromZero)
                : 0m;

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
                PfEmployee = pfEmployee,
                PfEmployer = pfEmployer,
                VpsEmployee = vpsEmployee,
                VpsEmployer = vpsEmployer,
                VpsTaxCredit = vpsCredit,
                SocialSecurityEmployer = socialSecurity,
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

    /// <summary>A detached copy of a monthly salary scaled to one shorter pay period; never saved.</summary>
    private static EmployeeSalary ShareOf(EmployeeSalary s, decimal share) => new()
    {
        EmployeeId = s.EmployeeId,
        EffectiveFrom = s.EffectiveFrom,
        BasicSalary = Math.Round(s.BasicSalary * share, 0, MidpointRounding.AwayFromZero),
        EobiApplicable = s.EobiApplicable,
        Components = s.Components.Select(c => new SalaryComponent { Name = c.Name, Kind = c.Kind, IsTaxable = c.IsTaxable, Amount = Math.Round(c.Amount * share, 0, MidpointRounding.AwayFromZero) }).ToList(),
    };

    public static string Label(PayrollRun run) => PayPeriods.Label(run.Frequency, run.PeriodStart, run.PeriodEnd);

    /// <summary>HR sends a processed month to the company admins to approve.</summary>
    public async Task SubmitAsync(Guid runId)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId) ?? throw new NotFoundException("Payroll run");
        if (run.Status != PayrollRunStatus.Draft)
            throw new DomainException("Only a draft payroll can be sent for approval.");
        run.Status = PayrollRunStatus.Submitted;
        run.SubmittedAtUtc = clock.UtcNow;
        run.ApprovalNote = null;
        var month = Label(run);
        var admins = await db.Users.Where(u => u.Role == UserRole.CompanyAdmin && u.IsActive && u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToListAsync();
        await notify.ToEmployeesAsync(admins, NotificationKind.ApprovalNeeded, $"Payroll for {month} to approve", $"Net pay PKR {run.TotalNet:N0}", $"/payroll/runs/{run.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>Company admin approves the month (HR can then finalize) or sends it back to draft with a note.</summary>
    public async Task DecideAsync(Guid runId, bool approve, string? note)
    {
        if (!me.IsCompanyAdmin())
            throw new ForbiddenException("Only the company admin approves payroll.");
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId) ?? throw new NotFoundException("Payroll run");
        if (run.Status != PayrollRunStatus.Submitted)
            throw new DomainException("This payroll is not waiting for approval.");
        if (!approve && string.IsNullOrWhiteSpace(note))
            throw new DomainException("Say what needs checking.");
        run.Status = approve ? PayrollRunStatus.Approved : PayrollRunStatus.Draft;
        run.ApprovedByName = approve ? await LetterService.CurrentUserNameAsync(db, me) : null;
        run.ApprovedAtUtc = approve ? clock.UtcNow : null;
        run.ApprovalNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var month = Label(run);
        await notify.ToHrAsync(NotificationKind.ApprovalNeeded, approve ? $"Payroll for {month} approved" : $"Payroll for {month} sent back",
            run.ApprovalNote, $"/payroll/runs/{run.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>Takes back "paid" from a month marked paid by mistake.</summary>
    public async Task UndisburseAsync(Guid runId)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId) ?? throw new NotFoundException("Payroll run");
        if (run.DisbursedAtUtc is null)
            throw new DomainException("This payroll is not marked paid.");
        run.DisbursedAtUtc = null;
        run.DisbursedVia = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Records that the finalized month's salaries were transferred.</summary>
    public async Task DisburseAsync(Guid runId, string? via)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId) ?? throw new NotFoundException("Payroll run");
        if (run.Status != PayrollRunStatus.Finalized)
            throw new DomainException("Finalize the payroll before marking it paid.");
        run.DisbursedAtUtc = clock.UtcNow;
        run.DisbursedVia = string.IsNullOrWhiteSpace(via) ? "Bank transfer" : via.Trim();
        await db.SaveChangesAsync();
    }

    public async Task FinalizeAsync(Guid runId)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId) ?? throw new NotFoundException("Payroll run");
        if (run.Status == PayrollRunStatus.Finalized)
            throw new DomainException("This payroll is already finalized.");
        var settings = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();
        if (settings.RequireApproval && run.Status != PayrollRunStatus.Approved)
            throw new DomainException(run.Status == PayrollRunStatus.Submitted ? "This payroll is waiting for approval." : "Send this payroll for approval first.");
        run.Status = PayrollRunStatus.Finalized;
        run.FinalizedAtUtc = clock.UtcNow;

        foreach (var item in await db.PayRequests.Where(r => r.PayrollRunId == run.Id && r.Status == RequestStatus.Approved).ToListAsync())
            item.PaidAtUtc = run.FinalizedAtUtc;

        foreach (var x in await db.PieceEntries.Where(x => x.PayrollRunId == run.Id && x.Status == RequestStatus.Approved).ToListAsync())
            x.PaidAtUtc = run.FinalizedAtUtc;

        var claims = await db.ExpenseClaims.Where(c => c.PayrollRunId == run.Id && c.Status == ExpenseStatus.Approved).ToListAsync();
        foreach (var c in claims)
        {
            c.Status = ExpenseStatus.Paid;
            c.PaidAtUtc = run.FinalizedAtUtc;
            c.PaidVia = $"Payroll {Label(run)}";
        }

        // Paid attendance should not change any more: close the month if it has ended. Shorter periods leave the month open
        // for the people paid monthly.
        var monthEnd = run.PeriodEnd;
        var closing = run.Frequency == PayFrequency.Monthly && monthEnd < await clock.TodayAsync()
                      && !await db.AttendancePeriods.AnyAsync(p => p.Year == run.Year && p.Month == run.Month);
        if (closing)
            db.AttendancePeriods.Add(new AttendancePeriod { Year = run.Year, Month = run.Month, ClosedAtUtc = clock.UtcNow, ClosedByName = "Payroll finalized" });

        // Provident fund contributions go into each member's ledger.
        foreach (var p in await db.Payslips.Where(p => p.PayrollRunId == run.Id && (p.PfEmployee > 0 || p.PfEmployer > 0)).ToListAsync())
            db.FundEntries.Add(new FundEntry
            {
                EmployeeId = p.EmployeeId, Fund = FundKind.ProvidentFund, Kind = FundEntryKind.Contribution, Date = monthEnd,
                EmployeeAmount = p.PfEmployee, EmployerAmount = p.PfEmployer, PayrollRunId = run.Id, Note = $"Payroll {Label(run)}",
            });

        var month = Label(run);
        var paid = await db.Payslips.Where(p => p.PayrollRunId == run.Id).Select(p => p.EmployeeId).ToListAsync();
        await notify.ToEmployeesAsync(paid, NotificationKind.Payslip, $"Your payslip for {month} is ready", null, "/me/payslips");
        await db.SaveChangesAsync();
        if (closing)
            await services.GetRequiredService<HRMS.Api.Controllers.PenaltyService>().ApplyOnCloseAsync(run.Year, run.Month);
    }
}
