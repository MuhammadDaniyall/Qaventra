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
    /// <summary>When the course last became mandatory; the completion window does not start before it.</summary>
    public DateOnly? MandatorySince { get; set; }
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

/// <summary>A multiple-choice test for a course. Answers are scored on the server; the right options never reach the browser before submission.</summary>
public class TrainingQuiz : TenantEntity
{
    public Guid ProgramId { get; set; }
    public TrainingProgram? Program { get; set; }
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    /// <summary>Percent of questions that must be right to pass (1–100).</summary>
    public int PassPercent { get; set; } = 60;
    /// <summary>Minutes allowed from start to submit; null = no limit.</summary>
    public int? TimeLimitMinutes { get; set; }
    /// <summary>Attempts each person may make; 0 = unlimited.</summary>
    public int MaxAttempts { get; set; } = 3;
    public bool IsActive { get; set; } = true;
    public List<TrainingQuizQuestion> Questions { get; set; } = [];
}

public class TrainingQuizQuestion : TenantEntity
{
    public Guid QuizId { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = "";
    /// <summary>The choices as a JSON array of strings (2–6).</summary>
    public string OptionsJson { get; set; } = "[]";
    /// <summary>Index of the right choice in <see cref="OptionsJson"/>.</summary>
    public int CorrectIndex { get; set; }
}

/// <summary>One sitting of a quiz. Open until submitted; a late submission is recorded as failed.</summary>
public class TrainingQuizAttempt : TenantEntity
{
    public Guid QuizId { get; set; }
    public TrainingQuiz? Quiz { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime StartedAtUtc { get; set; }
    /// <summary>Submit by this time (time limit plus a short grace); null = no limit.</summary>
    public DateTime? DeadlineUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    /// <summary>Question ids in the order they were shown (JSON array).</summary>
    public string QuestionIdsJson { get; set; } = "[]";
    /// <summary>Chosen option per question id (JSON object), stored on submission.</summary>
    public string? AnswersJson { get; set; }
    public int Correct { get; set; }
    public int Total { get; set; }
    public int ScorePercent { get; set; }
    public bool Passed { get; set; }
    public bool TimedOut { get; set; }
}

/// <summary>Issued when a quiz is passed. The code lets anyone check it on the public verify page.</summary>
public class TrainingCertificate : TenantEntity
{
    public Guid AttemptId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ProgramId { get; set; }
    /// <summary>Short random code, unique across all companies, used in the public verify link.</summary>
    public string Code { get; set; } = "";
    public DateOnly IssuedOn { get; set; }
    /// <summary>Name and course as printed, kept even if the record or course is renamed later.</summary>
    public string EmployeeName { get; set; } = "";
    public string CourseTitle { get; set; } = "";
    public int ScorePercent { get; set; }
}
