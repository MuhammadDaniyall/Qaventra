using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>An employee's reimbursement claim. Approved claims are paid through the next payroll or the final settlement.</summary>
public class ExpenseClaim : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Title { get; set; } = "";
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;
    public decimal Total { get; set; }

    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }

    /// <summary>The payroll run that includes this claim. Set on draft runs too, so reprocessing keeps it.</summary>
    public Guid? PayrollRunId { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? PaidVia { get; set; }
    /// <summary>Advance this claim is set against, and how much of the claim it covered (not paid again).</summary>
    public Guid? AdvanceId { get; set; }
    public decimal AdvanceAdjusted { get; set; }

    /// <summary>What payroll or the settlement still pays.</summary>
    public decimal Payable => Total - AdvanceAdjusted;

    public List<ExpenseItem> Items { get; set; } = [];
}

public class ExpenseItem : BaseEntity
{
    public Guid ExpenseClaimId { get; set; }
    public DateOnly Date { get; set; }
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public Guid? ExpenseTypeId { get; set; }
    /// <summary>Units for rate-based types (km, nights…).</summary>
    public decimal? Quantity { get; set; }
    public string? FromCity { get; set; }
    public string? ToCity { get; set; }
    public Guid? ReceiptId { get; set; }
    public string? ReceiptFileName { get; set; }
}

/// <summary>A receipt image or PDF, kept apart from the claim so listing claims stays light.</summary>
public class ExpenseReceipt : TenantEntity
{
    public Guid ExpenseClaimId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = [];
}
