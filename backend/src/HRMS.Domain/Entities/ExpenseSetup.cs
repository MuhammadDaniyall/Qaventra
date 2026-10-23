using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// A kind of expense people can claim, with how it is worked out (amount, rate × units, or rate × distance between
/// cities), limits and receipt rule. Grouped under one of the fixed categories for reports.
/// </summary>
public class ExpenseType : TenantEntity
{
    public string Name { get; set; } = "";
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public ExpenseCalculation Calculation { get; set; } = ExpenseCalculation.Amount;
    /// <summary>Unit for rate-based types, e.g. "km", "night", "meal".</summary>
    public string? Unit { get; set; }
    /// <summary>Pay per unit (per km for distance).</summary>
    public decimal? Rate { get; set; }
    /// <summary>Most a single item may claim.</summary>
    public decimal? PerClaimLimit { get; set; }
    /// <summary>Most a person may claim in a calendar year (approved and pending).</summary>
    public decimal? YearlyLimit { get; set; }
    public bool ReceiptRequired { get; set; }
    /// <summary>Comma-separated grade ids it is open to; empty = everyone.</summary>
    public string? GradeIds { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ExpenseTypeRate> GradeRates { get; set; } = [];
}

/// <summary>A different rate or yearly limit for one grade.</summary>
public class ExpenseTypeRate : TenantEntity
{
    public Guid ExpenseTypeId { get; set; }
    public Guid GradeId { get; set; }
    public decimal? Rate { get; set; }
    public decimal? YearlyLimit { get; set; }
}

/// <summary>Distance between two cities, for distance-based expense types.</summary>
public class DistanceRoute : TenantEntity
{
    public string FromCity { get; set; } = "";
    public string ToCity { get; set; } = "";
    public decimal Km { get; set; }
}

/// <summary>Money given ahead of spending (e.g. for a trip); claims are set against it and the rest is recovered.</summary>
public class ExpenseAdvance : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Purpose { get; set; } = "";
    public decimal Amount { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    /// <summary>Claims set against it so far.</summary>
    public decimal Adjusted { get; set; }
    /// <summary>Closed: nothing more can be set against it; any unspent part was recovered.</summary>
    public DateTime? ClosedAtUtc { get; set; }
    public decimal Recovered { get; set; }
}
