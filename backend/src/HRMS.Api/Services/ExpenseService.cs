using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record NewExpenseItem(DateOnly Date, ExpenseCategory Category, string Description, decimal Amount, NewReceipt? Receipt);

public record NewReceipt(string FileName, string ContentType, string Base64);

public class ExpenseService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalPolicy approvals)
{
    public const int MaxItems = 20;
    public const int MaxReceiptBytes = 3 * 1024 * 1024;
    public static readonly string[] ReceiptTypes = ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    public async Task<ExpenseClaim> SubmitAsync(Guid employeeId, string title, IReadOnlyList<NewExpenseItem> items)
    {
        if (items.Count is 0 or > MaxItems)
            throw new DomainException($"A claim needs between 1 and {MaxItems} items.");
        var today = await clock.TodayAsync();
        if (items.Any(i => i.Date > today))
            throw new DomainException("Expense dates cannot be in the future.");
        if (items.Any(i => i.Date < today.AddDays(-365)))
            throw new DomainException("Expenses older than one year cannot be claimed.");
        if (items.Any(i => i.Amount <= 0))
            throw new DomainException("Every item needs an amount greater than zero.");

        var claim = new ExpenseClaim { EmployeeId = employeeId, Title = title.Trim() };
        foreach (var i in items)
        {
            var item = new ExpenseItem
            {
                Date = i.Date,
                Category = i.Category,
                Description = i.Description.Trim(),
                Amount = Math.Round(i.Amount, 2),
            };
            if (i.Receipt is { } r)
            {
                var receipt = new ExpenseReceipt
                {
                    ExpenseClaimId = claim.Id,
                    FileName = Path.GetFileName(r.FileName.Trim()) is { Length: > 0 } n ? n[..Math.Min(n.Length, 200)] : "receipt",
                    ContentType = r.ContentType,
                    Data = DecodeReceipt(r),
                };
                db.ExpenseReceipts.Add(receipt);
                item.ReceiptId = receipt.Id;
                item.ReceiptFileName = receipt.FileName;
            }
            claim.Items.Add(item);
        }
        claim.Total = claim.Items.Sum(i => i.Amount);
        db.ExpenseClaims.Add(claim);
        await db.SaveChangesAsync();
        return claim;
    }

    private static byte[] DecodeReceipt(NewReceipt r)
    {
        if (!ReceiptTypes.Contains(r.ContentType))
            throw new DomainException("Receipts must be a JPG, PNG, WebP image or a PDF.");
        var base64 = r.Base64.Contains(',') ? r.Base64[(r.Base64.IndexOf(',') + 1)..] : r.Base64;
        byte[] data;
        try
        {
            data = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new DomainException("A receipt file could not be read.");
        }
        if (data.Length == 0 || data.Length > MaxReceiptBytes)
            throw new DomainException("Each receipt must be smaller than 3 MB.");
        return data;
    }

    /// <summary>The claimant, anyone who may approve it, and HR may view a claim.</summary>
    public async Task<bool> CanViewAsync(ExpenseClaim claim) =>
        claim.EmployeeId == me.EmployeeId || me.IsHr() || await approvals.CanApproveForAsync(claim.EmployeeId);

    public async Task DecideAsync(Guid claimId, bool approve, string? note)
    {
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId) ?? throw new NotFoundException("Expense claim");
        if (claim.Status != ExpenseStatus.Pending)
            throw new DomainException($"This claim is already {claim.Status.ToString().ToLowerInvariant()}.");
        if (!await approvals.CanApproveForAsync(claim.EmployeeId))
            throw new ForbiddenException("You cannot approve or reject this claim.");

        claim.Status = approve ? ExpenseStatus.Approved : ExpenseStatus.Rejected;
        claim.DecidedByUserId = me.UserId;
        claim.DecidedAtUtc = clock.UtcNow;
        claim.DecisionNote = note;
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid claimId)
    {
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId) ?? throw new NotFoundException("Expense claim");
        if (claim.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this claim.");
        if (claim.Status != ExpenseStatus.Pending)
            throw new DomainException("Only a pending claim can be cancelled.");
        claim.Status = ExpenseStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    /// <summary>Approved claims not yet paid and not locked into another payroll run.</summary>
    public static IQueryable<ExpenseClaim> Payable(HrmsDbContext db, Guid? forRunId = null) =>
        db.ExpenseClaims.Where(c => c.Status == ExpenseStatus.Approved && (c.PayrollRunId == null || c.PayrollRunId == forRunId));
}
