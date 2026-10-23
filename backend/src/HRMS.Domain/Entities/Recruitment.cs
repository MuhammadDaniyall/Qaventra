using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A manager (or HR) asks to hire for a position; approved through the job request chain.</summary>
public class JobRequest : TenantEntity
{
    public Guid RequestedByEmployeeId { get; set; }
    public string Title { get; set; } = "";
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public int Positions { get; set; } = 1;
    /// <summary>Replacement only: who is being replaced.</summary>
    public Guid? ReplacesEmployeeId { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public int? ExperienceMinYears { get; set; }
    public string? Qualification { get; set; }
    public string? Skills { get; set; }
    public DateOnly? NeededBy { get; set; }
    public string Reason { get; set; } = "";
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>An opening people can apply to, internally and on the public careers page.</summary>
public class JobPost : TenantEntity
{
    public Guid? JobRequestId { get; set; }
    public string Title { get; set; } = "";
    /// <summary>Short public id used in the careers link.</summary>
    public string Slug { get; set; } = "";
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public string? Location { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public string Description { get; set; } = "";
    public string? Requirements { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public bool ShowSalary { get; set; }
    public int Positions { get; set; } = 1;
    public DateOnly? ClosesOn { get; set; }
    public JobPostStatus Status { get; set; } = JobPostStatus.Draft;
    /// <summary>Runs the hiring: screening, scheduling, offers.</summary>
    public Guid? RecruiterEmployeeId { get; set; }
    /// <summary>The future manager: shortlists, interviews and is the new hire's manager.</summary>
    public Guid? HiringManagerEmployeeId { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    // ---------- Shortlisting score ----------
    public decimal? ScreenMinExperienceYears { get; set; }
    public decimal? ScreenMaxExpectedSalary { get; set; }
    /// <summary>Comma-separated cities that score.</summary>
    public string? ScreenCities { get; set; }
    /// <summary>Comma-separated keywords looked for in skills, title, company and cover letter.</summary>
    public string? ScreenKeywords { get; set; }
}

/// <summary>A person who applied or was added; one record per person across jobs.</summary>
public class Candidate : TenantEntity
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Cnic { get; set; }
    public string? City { get; set; }
    public string? CurrentTitle { get; set; }
    public string? CurrentCompany { get; set; }
    public decimal? ExperienceYears { get; set; }
    public decimal? ExpectedSalary { get; set; }
    public int? NoticeDays { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Skills { get; set; }
    public CandidateSource Source { get; set; } = CandidateSource.Manual;
    public string? ReferredBy { get; set; }
    public bool IsBlacklisted { get; set; }
    public string? BlacklistReason { get; set; }
    /// <summary>Set once hired.</summary>
    public Guid? EmployeeId { get; set; }
    /// <summary>Secret for the candidate's status page (no sign-in).</summary>
    public string? PortalToken { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>A candidate in one job's pipeline.</summary>
public class JobApplication : TenantEntity
{
    public Guid JobPostId { get; set; }
    public JobPost? JobPost { get; set; }
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public ApplicationStage Stage { get; set; } = ApplicationStage.Applied;
    public string? CoverLetter { get; set; }
    /// <summary>Recruiter's 1–5 rating from screening.</summary>
    public int? Rating { get; set; }
    public string? RejectionReason { get; set; }
    public decimal? OfferedSalary { get; set; }
    public DateOnly? OfferedJoiningDate { get; set; }
    public string? OfferNote { get; set; }
    public OfferStatus? OfferStatus { get; set; }
    public DateTime? StageChangedAtUtc { get; set; }
    public DateTime? HiredAtUtc { get; set; }
}

/// <summary>A note or stage change on an application (the hiring log).</summary>
public class ApplicationEvent : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public string Text { get; set; } = "";
    public string? ByName { get; set; }
    public bool IsNote { get; set; }
}

public class Interview : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public JobApplication? Application { get; set; }
    public int Round { get; set; } = 1;
    public string Title { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public InterviewMode Mode { get; set; } = InterviewMode.Onsite;
    /// <summary>Address for onsite, link for online, number for phone.</summary>
    public string? Where { get; set; }
    /// <summary>Comma-separated employee ids.</summary>
    public string InterviewerIds { get; set; } = "";
    public InterviewStatus Status { get; set; } = InterviewStatus.Scheduled;
    public string? Notes { get; set; }
    /// <summary>Questions with marks the interviewers score.</summary>
    public Guid? TemplateId { get; set; }
}

/// <summary>Interview questions grouped by category, each scored out of its marks.</summary>
public class InterviewTemplate : TenantEntity
{
    public string Name { get; set; } = "";
    public List<InterviewQuestion> Questions { get; set; } = [];
}

public class InterviewQuestion : TenantEntity
{
    public Guid TemplateId { get; set; }
    public string Text { get; set; } = "";
    public string? Category { get; set; }
    public int MaxMarks { get; set; } = 10;
    public int Order { get; set; }
}

/// <summary>One interviewer's verdict.</summary>
public class InterviewFeedback : TenantEntity
{
    public Guid InterviewId { get; set; }
    public Guid InterviewerEmployeeId { get; set; }
    public int Rating { get; set; }
    public HireRecommendation Recommendation { get; set; }
    public string? Strengths { get; set; }
    public string? Concerns { get; set; }
    /// <summary>JSON list of { questionId, marks } against the interview's template.</summary>
    public string? MarksJson { get; set; }
    public int? TotalMarks { get; set; }
    public int? MaxMarks { get; set; }
}
