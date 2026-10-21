using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

public class OfficeLocation : TenantEntity
{
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; } = 200;
}

public class Shift : TenantEntity
{
    public string Name { get; set; } = "";
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int GraceMinutes { get; set; } = 15;
    public List<DayOfWeek> WeeklyOffDays { get; set; } = [];
    public bool IsDefault { get; set; }
}

public class Holiday : TenantEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
}

public class AttendanceRecord : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly Date { get; set; }
    public DateTime CheckInUtc { get; set; }
    public DateTime? CheckOutUtc { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public Guid? OfficeLocationId { get; set; }
    public AttendanceStatus Status { get; set; }
    public AttendanceSource Source { get; set; } = AttendanceSource.Web;
    public int WorkedMinutes { get; set; }
    /// <summary>Why the record was entered or changed, e.g. "Work from home (approved request)".</summary>
    public string? Note { get; set; }
}

/// <summary>An employee asks to add or fix a day's attendance; applied when the approval chain approves it.</summary>
public class AttendanceRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceRequestKind Kind { get; set; }
    public TimeOnly CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public string Reason { get; set; } = "";
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

public class LeaveType : TenantEntity
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal AnnualQuota { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public LeaveAccrual Accrual { get; set; } = LeaveAccrual.Upfront;
    /// <summary>Most unused days moved into next year; 0 means none carry forward.</summary>
    public decimal CarryForwardMax { get; set; }
    /// <summary>Longest single request in working days; null means no limit.</summary>
    public int? MaxConsecutiveDays { get; set; }
    /// <summary>Days of notice needed before the leave starts; 0 allows same-day requests (e.g. sick leave).</summary>
    public int MinNoticeDays { get; set; }
    /// <summary>Only this gender may apply (maternity, paternity); null means everyone.</summary>
    public Gender? ApplicableGender { get; set; }
    public bool AllowedOnProbation { get; set; } = true;
    public bool AllowHalfDay { get; set; } = true;
    public bool AllowShortLeave { get; set; }
    /// <summary>Credited by approved comp-off requests instead of a yearly quota.</summary>
    public bool IsCompensatory { get; set; }
}

public class LeaveBalance : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
    public int Year { get; set; }
    /// <summary>The year's quota (prorated for joiners). With monthly accrual it is earned gradually.</summary>
    public decimal Entitled { get; set; }
    public decimal Used { get; set; }
    /// <summary>Unused days brought from last year.</summary>
    public decimal CarriedForward { get; set; }
    /// <summary>Days added (or removed, negative) by HR or approved comp-offs.</summary>
    public decimal Adjustment { get; set; }
    public string? AdjustmentNote { get; set; }
    /// <summary>Days earned through approved comp-off requests.</summary>
    public decimal Credited { get; set; }

    public decimal Total => Entitled + CarriedForward + Adjustment + Credited;
    public decimal Remaining => Total - Used;
}

public class LeaveRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal Days { get; set; }
    public LeaveDuration Duration { get; set; } = LeaveDuration.FullDay;
    public HalfDayPart? HalfDayPart { get; set; }
    /// <summary>Short leave only.</summary>
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>Worked on a weekly off day or holiday; once approved, the days are added to compensatory leave.</summary>
public class CompOffRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly WorkedOn { get; set; }
    /// <summary>0.5 or 1.</summary>
    public decimal Days { get; set; }
    public string Reason { get; set; } = "";
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}
