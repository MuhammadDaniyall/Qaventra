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

public record ReceiptUpload([Required, StringLength(200)] string FileName, [Required, StringLength(100)] string ContentType, [Required] string Base64);

public record ExpenseItemRequest(
    DateOnly Date,
    ExpenseCategory Category,
    [Required, StringLength(500)] string Description,
    [Range(0.01, 10_000_000)] decimal Amount,
    ReceiptUpload? Receipt);

public record SubmitExpenseRequest([Required, StringLength(150)] string Title, [Required, MinLength(1), MaxLength(ExpenseService.MaxItems)] List<ExpenseItemRequest> Items);

public record DecideExpenseRequest([StringLength(1000)] string? Note);

public record ExpenseClaimDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string Title, ExpenseStatus Status,
    decimal Total, int ItemCount, int ReceiptCount, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote,
    DateTime? PaidAtUtc, string? PaidVia);

public record ExpenseItemDto(Guid Id, DateOnly Date, ExpenseCategory Category, string Description, decimal Amount, Guid? ReceiptId, string? ReceiptFileName);

public record ExpenseClaimDetailDto(ExpenseClaimDto Claim, List<ExpenseItemDto> Items, bool CanDecide);

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = Policies.CompanyUser)]
public class ExpensesController(HrmsDbContext db, ExpenseService expenses, ApprovalEngine approvals, ICurrentUser me) : ControllerBase
{
    [HttpGet("my")]
    public Task<List<ExpenseClaimDto>> My() => Project(db.ExpenseClaims.Where(c => c.EmployeeId == me.EmployeeId)).ToListAsync();

    [HttpPost]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<ExpenseClaimDto> Submit(SubmitExpenseRequest req)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var claim = await expenses.SubmitAsync(employeeId, req.Title, req.Items.Select(i => new NewExpenseItem(
            i.Date, i.Category, i.Description, i.Amount,
            i.Receipt is { } r ? new NewReceipt(r.FileName, r.ContentType, r.Base64) : null)).ToList());
        return await Project(db.ExpenseClaims.Where(c => c.Id == claim.Id)).FirstAsync();
    }

    /// <summary>Claims for the signed-in approver: all for HR; for others, those whose approval step is theirs.</summary>
    [HttpGet("approvals"), Authorize(Policy = Policies.Approver)]
    public Task<List<ExpenseClaimDto>> Approvals([FromQuery] ExpenseStatus status = ExpenseStatus.Pending)
    {
        var q = db.ExpenseClaims.Where(c => c.Status == status && c.EmployeeId != me.EmployeeId);
        if (!me.IsHr())
        {
            var waiting = approvals.WaitingForMe(ApprovalRequestType.Expense);
            var chained = approvals.WithChain(ApprovalRequestType.Expense);
            var decided = approvals.DecidedByMe(ApprovalRequestType.Expense);
            q = status == ExpenseStatus.Pending
                ? q.Where(c => waiting.Contains(c.Id) || (!chained.Contains(c.Id) && c.Employee!.ManagerId == me.EmployeeId))
                : q.Where(c => decided.Contains(c.Id) || c.Employee!.ManagerId == me.EmployeeId);
        }
        return Project(q).Take(500).ToListAsync();
    }

    [HttpGet("{id:guid}")]
    public async Task<ExpenseClaimDetailDto> Get(Guid id)
    {
        var claim = await db.ExpenseClaims.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Expense claim");
        if (!await expenses.CanViewAsync(claim))
            throw new NotFoundException("Expense claim");

        var dto = await Project(db.ExpenseClaims.Where(c => c.Id == id)).FirstAsync();
        var items = claim.Items.OrderBy(i => i.Date)
            .Select(i => new ExpenseItemDto(i.Id, i.Date, i.Category, i.Description, i.Amount, i.ReceiptId, i.ReceiptFileName)).ToList();
        var canDecide = claim.Status == ExpenseStatus.Pending && await expenses.CanDecideAsync(claim);
        return new ExpenseClaimDetailDto(dto, items, canDecide);
    }

    [HttpGet("{id:guid}/receipts/{receiptId:guid}")]
    public async Task<IActionResult> Receipt(Guid id, Guid receiptId)
    {
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == id) ?? throw new NotFoundException("Expense claim");
        if (!await expenses.CanViewAsync(claim))
            throw new NotFoundException("Receipt");
        var receipt = await db.ExpenseReceipts.FirstOrDefaultAsync(r => r.Id == receiptId && r.ExpenseClaimId == id)
            ?? throw new NotFoundException("Receipt");
        return File(receipt.Data, receipt.ContentType, receipt.FileName);
    }

    [HttpPost("{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, DecideExpenseRequest req)
    {
        await expenses.DecideAsync(id, approve: true, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, DecideExpenseRequest req)
    {
        await expenses.DecideAsync(id, approve: false, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await expenses.CancelAsync(id);
        return NoContent();
    }

    private static IQueryable<ExpenseClaimDto> Project(IQueryable<ExpenseClaim> q) =>
        q.OrderByDescending(c => c.CreatedAtUtc).Select(c => new ExpenseClaimDto(
            c.Id, c.EmployeeId, c.Employee!.FirstName + " " + c.Employee.LastName, c.Employee.EmployeeCode, c.Title, c.Status,
            c.Total, c.Items.Count, c.Items.Count(i => i.ReceiptId != null), c.CreatedAtUtc, c.DecidedAtUtc, c.DecisionNote,
            c.PaidAtUtc, c.PaidVia));
}
