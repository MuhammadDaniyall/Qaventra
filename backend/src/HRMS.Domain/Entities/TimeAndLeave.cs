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
}

public class LeaveType : TenantEntity
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal AnnualQuota { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class LeaveBalance : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
    public int Year { get; set; }
    public decimal Entitled { get; set; }
    public decimal Used { get; set; }

    public decimal Remaining => Entitled - Used;
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
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}
