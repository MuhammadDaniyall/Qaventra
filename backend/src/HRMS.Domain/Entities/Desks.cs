using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

public class DeskSettings : TenantEntity
{
    /// <summary>How far ahead people may book.</summary>
    public int MaxDaysAhead { get; set; } = 14;
    /// <summary>When on, a booking not checked into within <see cref="ReleaseAfterMinutes"/> of its start is freed for others.</summary>
    public bool RequireCheckIn { get; set; }
    public int ReleaseAfterMinutes { get; set; } = 60;
    /// <summary>Full-day desk bookings start at this company-local hour (for check-in and release).</summary>
    public int DayStartsAt { get; set; } = 9;
}

public class Floor : TenantEntity
{
    public Guid OfficeLocationId { get; set; }
    public OfficeLocation? Office { get; set; }
    public string Name { get; set; } = "";
    public int Order { get; set; }
}

/// <summary>A desk, meeting room or cabin people can book.</summary>
public class Space : TenantEntity
{
    public Guid FloorId { get; set; }
    public Floor? Floor { get; set; }
    public string Name { get; set; } = "";
    public SpaceKind Kind { get; set; } = SpaceKind.Desk;
    public int Seats { get; set; } = 1;
    /// <summary>e.g. "Dual monitor, standing desk" or "Projector, video call".</summary>
    public string? Features { get; set; }
    /// <summary>A permanent desk: only this person sits here, others cannot book it.</summary>
    public Guid? AssignedEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SpaceBooking : TenantEntity
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly Date { get; set; }
    /// <summary>Company-local times; both null means the whole day (desks).</summary>
    public TimeOnly? From { get; set; }
    public TimeOnly? To { get; set; }
    /// <summary>Meeting rooms: what the meeting is about.</summary>
    public string? Title { get; set; }
    public int Attendees { get; set; } = 1;
    public BookingStatus Status { get; set; } = BookingStatus.Booked;
    public DateTime? CheckedInAtUtc { get; set; }
}
