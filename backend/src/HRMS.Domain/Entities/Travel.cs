using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>Company travel rules. One row per company, created on first save.</summary>
public class TravelSettings : TenantEntity
{
    /// <summary>Requests with less notice than this must say why they are urgent.</summary>
    public int MinNoticeDays { get; set; } = 3;
    /// <summary>Daily allowance per day away, in PKR.</summary>
    public decimal PerDiemDomestic { get; set; }
    public decimal PerDiemInternational { get; set; }
    /// <summary>Days after returning to submit the expense settlement.</summary>
    public int SettleWithinDays { get; set; } = 15;
    /// <summary>Comma-separated employee ids of the travel desk, who book tickets and hotels. Empty means HR.</summary>
    public string DeskIds { get; set; } = "";
}

public class TravelReason : TenantEntity
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class TravelRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid? ReasonId { get; set; }
    public string Purpose { get; set; } = "";
    public TripType TripType { get; set; } = TripType.Return;
    public bool IsInternational { get; set; }
    public DateOnly DepartOn { get; set; }
    /// <summary>Last day away; the departure day for a one-way trip.</summary>
    public DateOnly ReturnOn { get; set; }
    /// <summary>Set when the request came with less notice than the company asks for.</summary>
    public string? UrgentReason { get; set; }

    public bool NeedsHotel { get; set; }
    public string? HotelCity { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }
    public bool NeedsPickDrop { get; set; }
    public string? PickDropNotes { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? AdvanceRequested { get; set; }

    public TravelStatus Status { get; set; } = TravelStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }

    // Booked by the travel desk.
    public string? HotelName { get; set; }
    public string? HotelRef { get; set; }
    public decimal? HotelCost { get; set; }
    public string? BookingNotes { get; set; }
    public string? BookedByName { get; set; }
    public DateTime? BookedAtUtc { get; set; }

    // Advance handed over before the trip.
    public decimal? AdvancePaid { get; set; }
    public DateOnly? AdvancePaidOn { get; set; }
    public string? AdvancePaidVia { get; set; }

    // Settlement after the trip.
    public SettlementStatus Settlement { get; set; } = SettlementStatus.None;
    public int? PerDiemDays { get; set; }
    public decimal? PerDiemRate { get; set; }
    public string? SettlementNote { get; set; }
    public DateTime? SettlementSubmittedAtUtc { get; set; }
    public string? SettlementDecisionNote { get; set; }
    public DateTime? SettledAtUtc { get; set; }
    /// <summary>The reimbursement or recovery sent to payroll.</summary>
    public Guid? PayRequestId { get; set; }

    public List<TravelLeg> Legs { get; set; } = [];
    public List<TravelExpense> Expenses { get; set; } = [];
}

public class TravelLeg : BaseEntity
{
    public Guid TravelRequestId { get; set; }
    public int Order { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public DateOnly Date { get; set; }
    public TravelMode Mode { get; set; } = TravelMode.Air;
    /// <summary>"Morning", "after 5 pm", a flight number…</summary>
    public string? Preference { get; set; }
    public string? BookingRef { get; set; }
    public decimal? BookingCost { get; set; }
}

/// <summary>A cost the traveller paid themselves, claimed in the settlement.</summary>
public class TravelExpense : BaseEntity
{
    public Guid TravelRequestId { get; set; }
    public DateOnly Date { get; set; }
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}
