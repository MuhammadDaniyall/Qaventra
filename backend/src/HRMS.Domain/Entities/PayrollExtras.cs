using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A change to someone's salary (increment, promotion, correction), kept as history next to the new salary row.</summary>
public class SalaryRevision : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public SalaryRevisionKind Kind { get; set; } = SalaryRevisionKind.Increment;
    public decimal OldBasic { get; set; }
    public decimal NewBasic { get; set; }
    public decimal OldGross { get; set; }
    public decimal NewGross { get; set; }
    public string? Reason { get; set; }
    public string? ByName { get; set; }
}

/// <summary>An allowance or deduction that repeats every month in a range, e.g. a car allowance or a canteen deduction.</summary>
public class RecurringPayItem : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Title { get; set; } = "";
    public bool IsEarning { get; set; } = true;
    public decimal Amount { get; set; }
    public bool IsTaxable { get; set; } = true;
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    /// <summary>Last month it applies; null means until stopped.</summary>
    public int? EndYear { get; set; }
    public int? EndMonth { get; set; }
    public string? Notes { get; set; }
}

/// <summary>An employee's pay for a month held back (not transferred) until released.</summary>
public class SalaryHold : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Reason { get; set; } = "";
    public string? HeldByName { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public string? ReleasedByName { get; set; }
}
