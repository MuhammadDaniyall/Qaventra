using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// A one-off amount for one payroll month: allowance, bonus, overtime (earnings) or deduction, advance salary (deductions).
/// Employees request advance salary and overtime; HR enters allowances, bonuses and deductions directly.
/// </summary>
public class PayRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public PayRequestKind Kind { get; set; }
    public string Title { get; set; } = "";
    public decimal Amount { get; set; }
    /// <summary>Earnings only: counts towards income tax.</summary>
    public bool IsTaxable { get; set; } = true;
    /// <summary>The payroll month it belongs to (paid or recovered then, or in the first payroll after).</summary>
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>Overtime only.</summary>
    public DateOnly? WorkDate { get; set; }
    public decimal? Hours { get; set; }
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    /// <summary>The payroll run that includes it; set on draft runs too, so reprocessing keeps it once.</summary>
    public Guid? PayrollRunId { get; set; }
    public DateTime? PaidAtUtc { get; set; }

    public bool IsEarning => Kind is PayRequestKind.Allowance or PayRequestKind.Bonus or PayRequestKind.Overtime or PayRequestKind.Commission;
}

/// <summary>A loan repaid in monthly installments deducted by payroll.</summary>
public class Loan : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Title { get; set; } = "";
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public decimal InstallmentAmount { get; set; }
    /// <summary>First payroll month that deducts an installment.</summary>
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public List<LoanRepayment> Repayments { get; set; } = [];
}

/// <summary>One installment taken by one payroll run (draft runs too; replaced when the run is reprocessed).</summary>
public class LoanRepayment : BaseEntity
{
    public Guid LoanId { get; set; }
    public Guid PayrollRunId { get; set; }
    public decimal Amount { get; set; }
}
