using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// A roster entry: on this date the employee works this shift, or is off. Days without an entry follow the employee's
/// own shift and its weekly off days.
/// </summary>
public class ShiftAssignment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>Null when the employee is off that day.</summary>
    public Guid? ShiftId { get; set; }
    public bool IsOff { get; set; }
    public ShiftAssignmentSource Source { get; set; } = ShiftAssignmentSource.Roster;
}

/// <summary>An employee asks for another shift on some days, or to swap days with a colleague.</summary>
public class ShiftRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public ShiftRequestKind Kind { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    /// <summary>Change only: the shift wanted.</summary>
    public Guid? ShiftId { get; set; }
    /// <summary>Swap only: the colleague whose schedule is exchanged.</summary>
    public Guid? SwapWithEmployeeId { get; set; }
    public string Reason { get; set; } = "";
    public ShiftRequestStatus Status { get; set; } = ShiftRequestStatus.Pending;
    public DateTime? ColleagueRespondedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>A month whose attendance HR has closed: nothing in it can change until it is reopened.</summary>
public class AttendancePeriod : TenantEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public string? ClosedByName { get; set; }
}
