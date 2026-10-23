using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

public class PieceCategory : TenantEntity
{
    public string Name { get; set; } = "";
}

/// <summary>A piece of work paid per unit, e.g. "Collar stitching, per piece, PKR 12".</summary>
public class PieceItem : TenantEntity
{
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>"piece", "dozen", "kg"…</summary>
    public string Unit { get; set; } = "piece";
    public decimal Rate { get; set; }
    /// <summary>Expected units per day, for efficiency.</summary>
    public decimal? DailyTarget { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A different rate for one person on one item (e.g. a senior tailor).</summary>
public class PieceRate : TenantEntity
{
    public Guid ItemId { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>Units someone produced on a day. Approved entries are paid with that month's salary.</summary>
public class PieceEntry : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid ItemId { get; set; }
    public PieceItem? Item { get; set; }
    public DateOnly Date { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Rate at the time of entry, so later changes do not alter it.</summary>
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public string? EnteredByName { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public Guid? PayrollRunId { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}
