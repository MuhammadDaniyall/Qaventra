using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>Company-wide performance rules (JSON fields hold small lists edited together).</summary>
public class PerformanceSettings : TenantEntity
{
    /// <summary>Names for ratings 1..5, e.g. ["Unsatisfactory", ..., "Outstanding"].</summary>
    public string RatingLabelsJson { get; set; } = "[]";
    /// <summary>Target share of people per rating 1..5 in percent (the bell curve), e.g. [5,15,50,20,10].</summary>
    public string BellCurveJson { get; set; } = "[]";
    /// <summary>Suggested raise by final score: [{ "minScore": 4.5, "raisePercent": 15 }, ...].</summary>
    public string RaiseSlabsJson { get; set; } = "[]";
}

/// <summary>
/// What a review looks at: weighted sections, either the person's own goals (set at the start of a cycle) or fixed
/// competencies listed here. Sections are JSON: [{ name, weight, kind, items: [{ title, description }] }].
/// </summary>
public class PerformanceTemplate : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string SectionsJson { get; set; } = "[]";
    /// <summary>The employee rates themselves before their manager does.</summary>
    public bool SelfReview { get; set; } = true;
}

/// <summary>A review round (e.g. "Annual review 2026") for everyone or some departments.</summary>
public class ReviewCycle : TenantEntity
{
    public string Name { get; set; } = "";
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public Guid TemplateId { get; set; }
    /// <summary>Comma-separated department ids; empty means everyone.</summary>
    public string DepartmentIds { get; set; } = "";
    public DateOnly? GoalsDue { get; set; }
    public DateOnly? SelfReviewDue { get; set; }
    public DateOnly? ManagerReviewDue { get; set; }
    public ReviewCycleStatus Status { get; set; } = ReviewCycleStatus.Draft;
    /// <summary>The template's sections and self-review flag, frozen at launch so later edits do not change running reviews.</summary>
    public string SectionsJson { get; set; } = "[]";
    public bool SelfReview { get; set; } = true;
    public DateTime? LaunchedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}

/// <summary>One person's review in a cycle.</summary>
public class PerformanceReview : TenantEntity
{
    public Guid CycleId { get; set; }
    public ReviewCycle? Cycle { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    /// <summary>The manager at launch; they write the manager review.</summary>
    public Guid? ReviewerEmployeeId { get; set; }
    public ReviewStage Stage { get; set; } = ReviewStage.Goals;
    public string? SelfComment { get; set; }
    public decimal? SelfScore { get; set; }
    public decimal? ManagerScore { get; set; }
    public string? ManagerComment { get; set; }
    public string? Strengths { get; set; }
    public string? Improvements { get; set; }
    public string? DevelopmentPlan { get; set; }
    /// <summary>1 low, 2 medium, 3 high: the second axis of the 9-box.</summary>
    public int? Potential { get; set; }
    /// <summary>HR's calibrated rating (1..5); the manager's score rounded until HR changes it.</summary>
    public int? FinalRating { get; set; }
    public string? CalibrationNote { get; set; }
    public bool? EmployeeAgrees { get; set; }
    public string? EmployeeSignOffComment { get; set; }
    public DateTime? GoalsSubmittedAtUtc { get; set; }
    public DateTime? SelfSubmittedAtUtc { get; set; }
    public DateTime? ManagerSubmittedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public DateTime? SignedOffAtUtc { get; set; }
    public List<ReviewItem> Items { get; set; } = [];
}

/// <summary>A goal or competency being rated, with its weight inside its section.</summary>
public class ReviewItem : BaseEntity
{
    public Guid ReviewId { get; set; }
    public int SectionIndex { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Goals only: what success looks like.</summary>
    public string? Target { get; set; }
    public decimal Weight { get; set; }
    public int Order { get; set; }
    public int? SelfScore { get; set; }
    public string? SelfComment { get; set; }
    public int? ManagerScore { get; set; }
    public string? ManagerComment { get; set; }
}
