using System.ComponentModel.DataAnnotations;
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

public record SubmitPayRequest(
    Guid? EmployeeId, PayRequestKind Kind, [StringLength(150)] string? Title, decimal? Amount, bool IsTaxable,
    int Year, int Month, DateOnly? WorkDate, decimal? Hours, [StringLength(1000)] string? Reason);

public record PayRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, PayRequestKind Kind, string Title, decimal Amount, bool IsTaxable,
    bool IsEarning, int Year, int Month, DateOnly? WorkDate, decimal? Hours, string? Reason, RequestStatus Status,
    DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote, DateTime? PaidAtUtc, bool InDraftPayroll);

public record RequestLoanRequest(
    Guid? EmployeeId, [StringLength(150)] string? Title, decimal Amount, int Installments, int StartYear, int StartMonth, [StringLength(1000)] string? Reason);

public record LoanDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string Title, decimal Amount, int Installments, decimal InstallmentAmount,
    int StartYear, int StartMonth, string? Reason, RequestStatus Status, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote,
    decimal Repaid, int InstallmentsTaken, decimal Remaining);

public record OvertimeQuoteDto(decimal Amount, decimal Multiplier);

[ApiController]
[Route("api/pay")]
[Authorize(Policy = Policies.CompanyUser)]
public class PayRequestsController(HrmsDbContext db, PayRequestService pay, ICurrentUser me) : ControllerBase
{
    [HttpGet("requests/my")]
    public Task<List<PayRequestDto>> MyRequests() => Project(db.PayRequests.Where(r => r.EmployeeId == me.EmployeeId)).ToListAsync();

    /// <summary>HR: every pay item, optionally for one payroll month.</summary>
    [HttpGet("requests"), Authorize(Policy = Policies.Hr)]
    public Task<List<PayRequestDto>> All([FromQuery] int? year, [FromQuery] int? month, [FromQuery] Guid? employeeId)
    {
        var q = db.PayRequests.AsQueryable();
        if (year is { } y && month is { } m)
            q = q.Where(r => r.Year == y && r.Month == m);
        if (employeeId is { } e)
            q = q.Where(r => r.EmployeeId == e);
        return Project(q).Take(1000).ToListAsync();
    }

    [HttpPost("requests")]
    public async Task<PayRequestDto> Submit(SubmitPayRequest req)
    {
        var employeeId = req.EmployeeId ?? me.EmployeeId ?? throw new DomainException("Choose the employee.");
        var r = await pay.SubmitAsync(new NewPayRequest(employeeId, req.Kind, req.Title ?? "", req.Amount, req.IsTaxable, req.Year, req.Month,
            req.WorkDate, req.Hours, req.Reason));
        return await Project(db.PayRequests.Where(x => x.Id == r.Id)).FirstAsync();
    }

    /// <summary>What an overtime claim would pay, before submitting.</summary>
    [HttpGet("overtime-quote")]
    public async Task<OvertimeQuoteDto> OvertimeQuote([FromQuery] DateOnly date, [FromQuery] decimal hours, [FromQuery] Guid? employeeId)
    {
        var id = me.IsHr() && employeeId is { } e ? e : me.EmployeeId ?? throw new DomainException("Choose the employee.");
        var multiplier = await db.PayrollSettings.Select(s => (decimal?)s.OvertimeMultiplier).FirstOrDefaultAsync() ?? 1.5m;
        return new OvertimeQuoteDto(await pay.OvertimeAmountAsync(id, date, hours), multiplier);
    }

    [HttpPost("requests/{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, DecideAttendanceRequest req)
    {
        await pay.DecideAsync(id, true, req.Note);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, DecideAttendanceRequest req)
    {
        await pay.DecideAsync(id, false, req.Note);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await pay.CancelAsync(id);
        return NoContent();
    }

    // ---------- Loans ----------

    [HttpGet("loans/my")]
    public Task<List<LoanDto>> MyLoans() => ProjectLoans(db.Loans.Where(l => l.EmployeeId == me.EmployeeId)).ToListAsync();

    [HttpGet("loans"), Authorize(Policy = Policies.Hr)]
    public Task<List<LoanDto>> Loans() => ProjectLoans(db.Loans).Take(1000).ToListAsync();

    [HttpPost("loans")]
    public async Task<LoanDto> RequestLoan(RequestLoanRequest req)
    {
        var employeeId = req.EmployeeId ?? me.EmployeeId ?? throw new DomainException("Choose the employee.");
        var loan = await pay.RequestLoanAsync(employeeId, req.Title ?? "", req.Amount, req.Installments, req.StartYear, req.StartMonth, req.Reason);
        return await ProjectLoans(db.Loans.Where(l => l.Id == loan.Id)).FirstAsync();
    }

    [HttpPost("loans/{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> ApproveLoan(Guid id, DecideAttendanceRequest req)
    {
        await pay.DecideLoanAsync(id, true, req.Note);
        return NoContent();
    }

    [HttpPost("loans/{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> RejectLoan(Guid id, DecideAttendanceRequest req)
    {
        await pay.DecideLoanAsync(id, false, req.Note);
        return NoContent();
    }

    [HttpPost("loans/{id:guid}/cancel")]
    public async Task<IActionResult> CancelLoan(Guid id)
    {
        await pay.CancelLoanAsync(id);
        return NoContent();
    }

    private IQueryable<PayRequestDto> Project(IQueryable<PayRequest> q) =>
        q.OrderByDescending(r => r.CreatedAtUtc).Select(r => new PayRequestDto(
            r.Id, r.EmployeeId, r.Employee!.FirstName + " " + r.Employee.LastName, r.Employee.EmployeeCode, r.Kind, r.Title, r.Amount, r.IsTaxable,
            r.Kind == PayRequestKind.Allowance || r.Kind == PayRequestKind.Bonus || r.Kind == PayRequestKind.Overtime,
            r.Year, r.Month, r.WorkDate, r.Hours, r.Reason, r.Status, r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote, r.PaidAtUtc,
            r.PayrollRunId != null && r.PaidAtUtc == null));

    /// <summary>Repaid counts installments in finalized payrolls only; draft ones are still estimates.</summary>
    private IQueryable<LoanDto> ProjectLoans(IQueryable<Loan> q) =>
        q.OrderByDescending(l => l.CreatedAtUtc).Select(l => new
        {
            l,
            Name = l.Employee!.FirstName + " " + l.Employee.LastName,
            l.Employee.EmployeeCode,
            Repaid = l.Repayments.Where(x => db.PayrollRuns.Any(p => p.Id == x.PayrollRunId && p.Status == PayrollRunStatus.Finalized)).Sum(x => (decimal?)x.Amount) ?? 0,
            Taken = l.Repayments.Count(x => db.PayrollRuns.Any(p => p.Id == x.PayrollRunId && p.Status == PayrollRunStatus.Finalized)),
        }).Select(x => new LoanDto(
            x.l.Id, x.l.EmployeeId, x.Name, x.EmployeeCode, x.l.Title, x.l.Amount, x.l.Installments, x.l.InstallmentAmount, x.l.StartYear,
            x.l.StartMonth, x.l.Reason, x.l.Status, x.l.CreatedAtUtc, x.l.DecidedAtUtc, x.l.DecisionNote, x.Repaid, x.Taken, x.l.Amount - x.Repaid));
}
