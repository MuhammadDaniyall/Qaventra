using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

public class TrainingCategory : TenantEntity
{
    public string Name { get; set; } = "";
}

/// <summary>A course in the company's catalog. Mandatory ones are tracked for everyone (or some departments).</summary>
public class TrainingProgram : TenantEntity
{
    public string Title { get; set; } = "";
    public Guid? CategoryId { get; set; }
    public string? Description { get; set; }
    public TrainingMode Mode { get; set; } = TrainingMode.Classroom;
    public decimal? DurationHours { get; set; }
    /// <summary>Course page, video or material.</summary>
    public string? Link { get; set; }
    public bool IsMandatory { get; set; }
    /// <summary>Mandatory only: departments it applies to; empty means everyone.</summary>
    public string DepartmentIds { get; set; } = "";
    /// <summary>Mandatory only: new joiners must complete it within this many days.</summary>
    public int? CompleteWithinDays { get; set; }
    /// <summary>Must be repeated after this many months (e.g. 12 for yearly fire safety); null means once.</summary>
    public int? RepeatEveryMonths { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A scheduled run of a program: when, where, who teaches and who attends.</summary>
public class TrainingSession : TenantEntity
{
    public Guid ProgramId { get; set; }
    public TrainingProgram? Program { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    /// <summary>Room or address, or the meeting link.</summary>
    public string? Venue { get; set; }
    public Guid? TrainerEmployeeId { get; set; }
    /// <summary>An outside trainer or institute, when not an employee.</summary>
    public string? ExternalTrainer { get; set; }
    public int? Capacity { get; set; }
    /// <summary>Total cost of the session, in PKR.</summary>
    public decimal? Cost { get; set; }
    public TrainingSessionStatus Status { get; set; } = TrainingSessionStatus.Scheduled;
    public string? Notes { get; set; }
}

public class TrainingEnrollment : TenantEntity
{
    public Guid SessionId { get; set; }
    public TrainingSession? Session { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;
    public string? NominatedByName { get; set; }
    /// <summary>Assessment score, 0–100.</summary>
    public int? Score { get; set; }
    public DateOnly? CompletedOn { get; set; }
    /// <summary>Participant's rating of the session, 1–5.</summary>
    public int? FeedbackRating { get; set; }
    public int? TrainerRating { get; set; }
    public bool? WouldRecommend { get; set; }
    public string? FeedbackComment { get; set; }
    public DateTime? FeedbackAtUtc { get; set; }
}

/// <summary>An employee asks for a course: one from the catalog, or an outside course with its cost.</summary>
public class TrainingRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid? ProgramId { get; set; }
    /// <summary>Outside course name and provider, when not from the catalog.</summary>
    public string? CourseName { get; set; }
    public string? Provider { get; set; }
    public DateOnly? PreferredDate { get; set; }
    public decimal? Cost { get; set; }
    public string Reason { get; set; } = "";
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    /// <summary>Outside courses: done, certificate attached.</summary>
    public DateOnly? CompletedOn { get; set; }
}
