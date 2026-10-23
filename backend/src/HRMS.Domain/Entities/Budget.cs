using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A headcount and salary plan for a period (usually a financial year), approved by the company admin.</summary>
public class BudgetPlan : TenantEntity
{
    public string Name { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public BudgetStatus Status { get; set; } = BudgetStatus.Draft;
    public string? Notes { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public List<BudgetPosition> Positions { get; set; } = [];
}

/// <summary>Approved headcount for a department and designation, with the planned monthly cost of each.</summary>
public class BudgetPosition : BaseEntity
{
    public Guid PlanId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public int Headcount { get; set; }
    /// <summary>Planned monthly gross per person, in PKR.</summary>
    public decimal MonthlyCost { get; set; }
    /// <summary>New positions: hired from this date (costed from then). Null means in place for the whole period.</summary>
    public DateOnly? FilledFrom { get; set; }
    public string? Notes { get; set; }
}
