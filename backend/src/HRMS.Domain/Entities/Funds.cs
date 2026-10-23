using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// One movement in a person's provident fund or gratuity: opening balance, monthly contribution, profit, withdrawal.
/// Employee and employer shares are kept apart (the employer share may only vest after some years).
/// </summary>
public class FundEntry : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public FundKind Fund { get; set; }
    public FundEntryKind Kind { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>Positive in, negative out.</summary>
    public decimal EmployeeAmount { get; set; }
    public decimal EmployerAmount { get; set; }
    public string? Note { get; set; }
    public Guid? PayrollRunId { get; set; }
}

/// <summary>A request to take money out of the provident fund or gratuity, through the approval chain.</summary>
public class FundWithdrawal : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public FundKind Fund { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    /// <summary>Pay it with this month's salary (as a tax-free addition) once approved.</summary>
    public bool PayWithSalary { get; set; } = true;
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>
/// Income and tax from before this employer (or outside salary) and tax credits for a tax year. When a person has any,
/// their monthly tax is worked out on the projected year: (tax on the year's income − credits − tax already paid) over the months left.
/// </summary>
public class TaxAdjustment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    /// <summary>Tax year code, e.g. "2025-26".</summary>
    public string TaxYearCode { get; set; } = "";
    public TaxAdjustmentKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

/// <summary>Share of a person's salary cost booked to a cost center (shares add up to 100%).</summary>
public class SalarySplit : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Guid CostCenterId { get; set; }
    public decimal Percent { get; set; }
}
