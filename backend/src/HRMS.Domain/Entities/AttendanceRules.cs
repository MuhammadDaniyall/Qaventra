using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// A request to move weekly days off for a period, e.g. Friday instead of Sunday for the next two months.
/// Once approved, the roster for those days is rewritten.
/// </summary>
public class OffDayRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    /// <summary>The new weekly days off in that period.</summary>
    public List<DayOfWeek> OffDays { get; set; } = [];
    public string Reason { get; set; } = "";
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>
/// "Every 3 late arrivals cost half a day": counts an attendance flag in a month and deducts pay or leave.
/// </summary>
public class PenaltyRule : TenantEntity
{
    public string Name { get; set; } = "";
    public PenaltyFlag Flag { get; set; }
    /// <summary>Occurrences that make one penalty.</summary>
    public int EveryCount { get; set; } = 3;
    /// <summary>Occurrences forgiven each month before counting starts.</summary>
    public int FreeCount { get; set; }
    /// <summary>Late arrivals only count when at least this many minutes late (0 = any late).</summary>
    public int MinMinutes { get; set; }
    /// <summary>True: a penalty for every <see cref="EveryCount"/>; false: at most one a month.</summary>
    public bool Repeat { get; set; } = true;
    public PenaltyAction Action { get; set; } = PenaltyAction.DeductPay;
    public decimal Days { get; set; } = 1;
    /// <summary>Leave type to deduct from; when its balance runs out, the rest comes off pay.</summary>
    public Guid? LeaveTypeId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Penalties worked out when a month's attendance was closed. Leave deductions are applied then; pay ones by payroll.</summary>
public class PenaltyCharge : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid RuleId { get; set; }
    public string RuleName { get; set; } = "";
    public int Occurrences { get; set; }
    /// <summary>Days taken from the leave balance.</summary>
    public decimal LeaveDays { get; set; }
    public Guid? LeaveTypeId { get; set; }
    /// <summary>Days of pay deducted (pay rules, and any leave shortfall).</summary>
    public decimal PayDays { get; set; }
}
