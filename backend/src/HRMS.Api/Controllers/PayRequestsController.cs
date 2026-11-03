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
    DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote, DateTime? PaidAtUtc, bool InDraftPayroll, RecordStampsDto Stamps);

public record RequestLoanRequest(
    Guid? EmployeeId, [StringLength(150)] string? Title, decimal Amount, int Installments, int StartYear, int StartMonth, [StringLength(1000)] string? Reason, Guid? LoanTypeId = null);

public record LoanDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string Title, decimal Amount, int Installments, decimal InstallmentAmount,
    int StartYear, int StartMonth, string? Reason, RequestStatus Status, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote,
    decimal Repaid, int InstallmentsTaken, decimal Remaining, RecordStampsDto Stamps, decimal Markup = 0, decimal ProcessingFee = 0);

public record LoanTypeDto(Guid Id, string Name, decimal? MaxAmount, int? MaxInstallments, decimal MarkupPercent, decimal ProcessingFee, int MinServiceMonths, bool IsActive);
public record SaveLoanTypeRequest([Required, StringLength(100)] string Name, [Range(1, 50_000_000)] decimal? MaxAmount, [Range(1, 60)] int? MaxInstallments,
    [Range(0, 100)] decimal MarkupPercent, [Range(0, 1_000_000)] decimal ProcessingFee, [Range(0, 600)] int MinServiceMonths, bool IsActive = true);

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
        var loan = await pay.RequestLoanAsync(employeeId, req.Title ?? "", req.Amount, req.Installments, req.StartYear, req.StartMonth, req.Reason, req.LoanTypeId);
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
            r.PayrollRunId != null && r.PaidAtUtc == null && r.Status == RequestStatus.Approved,
            new RecordStampsDto(r.CreatedAtUtc, r.CreatedBy, r.ModifiedAtUtc, r.ModifiedBy)));

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
            x.l.StartMonth, x.l.Reason, x.l.Status, x.l.CreatedAtUtc, x.l.DecidedAtUtc, x.l.DecisionNote, x.Repaid, x.Taken, x.l.Amount + x.l.Markup - x.Repaid,
            new RecordStampsDto(x.l.CreatedAtUtc, x.l.CreatedBy, x.l.ModifiedAtUtc, x.l.ModifiedBy), x.l.Markup, x.l.ProcessingFee));

    // ---------- Loan types ----------

    [HttpGet("loan-types")]
    public Task<List<LoanTypeDto>> LoanTypes() => db.LoanTypes.OrderBy(t => t.Name)
        .Select(t => new LoanTypeDto(t.Id, t.Name, t.MaxAmount, t.MaxInstallments, t.MarkupPercent, t.ProcessingFee, t.MinServiceMonths, t.IsActive)).ToListAsync();

    [HttpPost("loan-types"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddLoanType(SaveLoanTypeRequest req)
    {
        var t = new LoanType();
        await ApplyLoanTypeAsync(t, req);
        db.LoanTypes.Add(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("loan-types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateLoanType(Guid id, SaveLoanTypeRequest req)
    {
        var t = await db.LoanTypes.FirstOrDefaultAsync(x => x.Id == id) ?? throw new HRMS.Api.Infrastructure.NotFoundException("Loan type");
        await ApplyLoanTypeAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyLoanTypeAsync(LoanType t, SaveLoanTypeRequest req)
    {
        var name = req.Name.Trim();
        if (await db.LoanTypes.AnyAsync(x => x.Id != t.Id && x.Name == name))
            throw new DomainException($"There is already a {name} loan type.");
        (t.Name, t.MaxAmount, t.MaxInstallments, t.MarkupPercent, t.ProcessingFee, t.MinServiceMonths, t.IsActive) =
            (name, req.MaxAmount, req.MaxInstallments, req.MarkupPercent, req.ProcessingFee, req.MinServiceMonths, req.IsActive);
    }
}
