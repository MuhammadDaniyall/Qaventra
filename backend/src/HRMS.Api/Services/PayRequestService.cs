using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record NewPayRequest(
    Guid EmployeeId, PayRequestKind Kind, string Title, decimal? Amount, bool IsTaxable, int Year, int Month,
    DateOnly? WorkDate, decimal? Hours, string? Reason);

/// <summary>
/// One-off pay items and loans. Employees request advance salary, overtime and loans (through approval chains);
/// HR enters allowances, bonuses and deductions, which are approved as entered. Payroll picks up approved items.
/// </summary>
public class PayRequestService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, NotificationService notify)
{
    public static string KindLabel(PayRequestKind k) => k switch
    {
        PayRequestKind.Allowance => "Allowance",
        PayRequestKind.Bonus => "Bonus",
        PayRequestKind.Overtime => "Overtime",
        PayRequestKind.Deduction => "Deduction",
        _ => "Advance salary",
    };

    private static bool EmployeeMayRequest(PayRequestKind k) => k is PayRequestKind.AdvanceSalary or PayRequestKind.Overtime;

    public async Task<PayRequest> SubmitAsync(NewPayRequest r)
    {
        var self = r.EmployeeId == me.EmployeeId;
        if (!me.IsHr() && !self)
            throw new ForbiddenException("You can only make requests for yourself.");
        if (self && !EmployeeMayRequest(r.Kind) && !me.IsHr())
            throw new ForbiddenException("Allowances, bonuses and deductions are entered by HR.");
        if (r.Month is < 1 or > 12 || r.Year is < 2000 or > 2100)
            throw new DomainException("Choose a valid payroll month.");
        var today = await clock.TodayAsync();
        var period = new DateOnly(r.Year, r.Month, 1);
        if (period < new DateOnly(today.Year, today.Month, 1).AddMonths(-1))
            throw new DomainException("The payroll month is too far in the past.");
        if (await db.PayrollRuns.AnyAsync(p => p.Year == r.Year && p.Month == r.Month && p.Status == PayrollRunStatus.Finalized))
            throw new DomainException($"Payroll for {period:MMMM yyyy} is finalized. Choose the next month.");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == r.EmployeeId) ?? throw new NotFoundException("Employee");
        if (!employee.IsCurrent)
            throw new DomainException("This employee has left; use the final settlement instead.");

        decimal amount;
        if (r.Kind == PayRequestKind.Overtime)
        {
            if (r.WorkDate is not { } d || d > today || d < today.AddDays(-60))
                throw new DomainException("Overtime needs the date worked, within the last 60 days.");
            if (r.Hours is not { } h || h <= 0 || h > 16)
                throw new DomainException("Overtime hours must be between 0 and 16.");
            amount = await OvertimeAmountAsync(r.EmployeeId, d, h);
        }
        else
        {
            if (r.Amount is not { } a || a <= 0 || a > 10_000_000)
                throw new DomainException("Enter an amount greater than zero.");
            amount = Math.Round(a, 0);
            if (r.Kind == PayRequestKind.AdvanceSalary)
            {
                var salary = await LetterService.CurrentSalaryAsync(db, r.EmployeeId, today);
                if (salary is { } s && amount > s.Gross)
                    throw new DomainException($"An advance can be at most one month's gross salary ({LetterService.Money(s.Gross)}). Apply for a loan for more.");
            }
        }

        var request = new PayRequest
        {
            EmployeeId = r.EmployeeId,
            Kind = r.Kind,
            Title = string.IsNullOrWhiteSpace(r.Title) ? KindLabel(r.Kind) : r.Title.Trim(),
            Amount = amount,
            IsTaxable = r.Kind is PayRequestKind.Overtime || (r.Kind is PayRequestKind.Allowance or PayRequestKind.Bonus && r.IsTaxable),
            Year = r.Year,
            Month = r.Month,
            WorkDate = r.Kind == PayRequestKind.Overtime ? r.WorkDate : null,
            Hours = r.Kind == PayRequestKind.Overtime ? r.Hours : null,
            Reason = r.Reason?.Trim(),
        };
        db.PayRequests.Add(request);

        // HR entering an item for someone else decides it; anything an employee asks for goes through the chain.
        if (me.IsHr() && !self)
        {
            Close(request, approved: true, "Entered by HR");
            await notify.ToEmployeesAsync([request.EmployeeId], NotificationKind.RequestDecided,
                $"{KindLabel(request.Kind)} added to your {period:MMMM} payroll", $"{request.Title} · {LetterService.Money(request.Amount)}", "/me/pay");
            await db.SaveChangesAsync();
            return request;
        }

        var summary = request.Kind == PayRequestKind.Overtime
            ? $"Overtime · {request.Hours:0.#} h on {request.WorkDate:d MMM} · {LetterService.Money(amount)}"
            : $"{KindLabel(request.Kind)} · {LetterService.Money(amount)} · {period:MMM yyyy} payroll";
        var approval = await approvals.StartAsync(ApprovalRequestType.PayRequest, request.Id, request.EmployeeId, summary);
        if (approval.Status == ApprovalStatus.Approved)
            Close(request, approved: true, "Approved automatically");
        await db.SaveChangesAsync();
        return request;
    }

    /// <summary>Hourly basic (basic / 30 / 8) x the company's overtime multiplier x hours.</summary>
    public async Task<decimal> OvertimeAmountAsync(Guid employeeId, DateOnly on, decimal hours)
    {
        var salary = await LetterService.CurrentSalaryAsync(db, employeeId, on)
            ?? throw new DomainException("No salary is set for this employee, so overtime cannot be worked out.");
        var multiplier = await db.PayrollSettings.Select(s => (decimal?)s.OvertimeMultiplier).FirstOrDefaultAsync() ?? 1.5m;
        return Math.Round(salary.Basic / 30m / 8m * multiplier * hours, 0, MidpointRounding.AwayFromZero);
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var request = await db.PayRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Pay request");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException($"This request is already {request.Status.ToString().ToLowerInvariant()}.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.PayRequest, request.Id, request.EmployeeId, approve, note);
        if (outcome != ApprovalStatus.Pending)
            Close(request, outcome == ApprovalStatus.Approved, note);
        await db.SaveChangesAsync();
    }

    /// <summary>The requester may cancel while pending; HR may also remove an approved item that is not paid yet.</summary>
    public async Task CancelAsync(Guid id)
    {
        var request = await db.PayRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Pay request");
        var owner = request.EmployeeId == me.EmployeeId;
        if (request.PaidAtUtc is not null)
            throw new DomainException("This item has already been paid in a finalized payroll.");
        if (request.Status == RequestStatus.Pending && (owner || me.IsHr()))
            await approvals.CancelAsync(ApprovalRequestType.PayRequest, request.Id);
        else if (!(request.Status == RequestStatus.Approved && me.IsHr()))
            throw new ForbiddenException("This request cannot be cancelled.");
        request.Status = RequestStatus.Cancelled;
        request.PayrollRunId = null;
        await db.SaveChangesAsync();
    }

    // ---------- Loans ----------

    public async Task<Loan> RequestLoanAsync(Guid employeeId, string title, decimal amount, int installments, int startYear, int startMonth, string? reason)
    {
        if (employeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You can only apply for a loan for yourself.");
        if (amount is <= 0 or > 50_000_000)
            throw new DomainException("Enter a loan amount greater than zero.");
        if (installments is < 1 or > 60)
            throw new DomainException("Installments must be between 1 and 60.");
        var today = await clock.TodayAsync();
        if (new DateOnly(startYear, startMonth, 1) < new DateOnly(today.Year, today.Month, 1))
            throw new DomainException("Repayment cannot start in a past month.");
        if (await db.Loans.AnyAsync(l => l.EmployeeId == employeeId && l.Status == RequestStatus.Pending))
            throw new DomainException("There is already a loan request waiting for approval.");

        var loan = new Loan
        {
            EmployeeId = employeeId,
            Title = string.IsNullOrWhiteSpace(title) ? "Loan" : title.Trim(),
            Amount = Math.Round(amount, 0),
            Installments = installments,
            InstallmentAmount = Math.Ceiling(Math.Round(amount, 0) / installments),
            StartYear = startYear,
            StartMonth = startMonth,
            Reason = reason?.Trim(),
        };
        db.Loans.Add(loan);
        // HR recording a loan for someone else (already agreed offline) needs no further approval.
        if (me.IsHr() && employeeId != me.EmployeeId)
        {
            loan.Status = RequestStatus.Approved;
            loan.DecidedAtUtc = clock.UtcNow;
            loan.DecisionNote = "Recorded by HR";
            await db.SaveChangesAsync();
            return loan;
        }
        var approval = await approvals.StartAsync(ApprovalRequestType.Loan, loan.Id, employeeId,
            $"{loan.Title} · {LetterService.Money(loan.Amount)} in {installments} installment(s) from {new DateOnly(startYear, startMonth, 1):MMM yyyy}");
        if (approval.Status == ApprovalStatus.Approved)
        {
            loan.Status = RequestStatus.Approved;
            loan.DecidedAtUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync();
        return loan;
    }

    public async Task DecideLoanAsync(Guid id, bool approve, string? note)
    {
        var loan = await db.Loans.FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Loan");
        if (loan.Status != RequestStatus.Pending)
            throw new DomainException($"This loan request is already {loan.Status.ToString().ToLowerInvariant()}.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Loan, loan.Id, loan.EmployeeId, approve, note);
        if (outcome != ApprovalStatus.Pending)
        {
            loan.Status = outcome == ApprovalStatus.Approved ? RequestStatus.Approved : RequestStatus.Rejected;
            loan.DecidedAtUtc = clock.UtcNow;
            loan.DecisionNote = note;
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelLoanAsync(Guid id)
    {
        var loan = await db.Loans.Include(l => l.Repayments).FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Loan");
        if (loan.Status != RequestStatus.Pending || (loan.EmployeeId != me.EmployeeId && !me.IsHr()))
            throw new DomainException("Only a loan request that is still waiting can be cancelled.");
        loan.Status = RequestStatus.Cancelled;
        await approvals.CancelAsync(ApprovalRequestType.Loan, loan.Id);
        await db.SaveChangesAsync();
    }

    private void Close(PayRequest r, bool approved, string? note)
    {
        r.Status = approved ? RequestStatus.Approved : RequestStatus.Rejected;
        r.DecidedAtUtc = clock.UtcNow;
        r.DecisionNote = note;
    }
}
