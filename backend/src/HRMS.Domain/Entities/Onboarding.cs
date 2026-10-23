using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A reusable onboarding plan: the tasks to do around someone's first days.</summary>
public class OnboardingTemplate : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Started automatically for every new employee.</summary>
    public bool IsDefault { get; set; }
    /// <summary>Starter kit queued for hand-over when onboarding starts.</summary>
    public Guid? AssetKitId { get; set; }
    public string? WelcomeMessage { get; set; }
    public List<OnboardingTemplateTask> Tasks { get; set; } = [];
}

public class OnboardingTemplateTask : BaseEntity
{
    public Guid TemplateId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public OnboardingTaskCategory Category { get; set; } = OnboardingTaskCategory.Paperwork;
    public OnboardingAssignee AssigneeKind { get; set; } = OnboardingAssignee.Hr;
    public Guid? AssigneeEmployeeId { get; set; }
    /// <summary>Days from the joining date; negative means before the first day.</summary>
    public int DueOffsetDays { get; set; }
    public int Order { get; set; }
}

/// <summary>One person's onboarding: first-day details and the tasks for everyone involved.</summary>
public class Onboarding : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string TemplateName { get; set; } = "";
    public DateOnly JoiningDate { get; set; }
    public TimeOnly? ReportingTime { get; set; }
    public string? ReportingPlace { get; set; }
    /// <summary>The colleague who shows them around.</summary>
    public Guid? BuddyEmployeeId { get; set; }
    public string? Instructions { get; set; }
    public OnboardingStatus Status { get; set; } = OnboardingStatus.InProgress;
    public DateTime? CompletedAtUtc { get; set; }
    /// <summary>The new joiner's answers to the introduction questions (JSON).</summary>
    public string? IntroductionJson { get; set; }
    public DateTime? IntroducedAtUtc { get; set; }
    public string? StartedByName { get; set; }
    public List<OnboardingTask> Tasks { get; set; } = [];
}

public class OnboardingTask : BaseEntity
{
    public Guid OnboardingId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public OnboardingTaskCategory Category { get; set; }
    public OnboardingAssignee AssigneeKind { get; set; }
    /// <summary>Who does it; null means anyone in HR.</summary>
    public Guid? AssigneeEmployeeId { get; set; }
    public DateOnly DueOn { get; set; }
    public int Order { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletedByName { get; set; }
    public string? Note { get; set; }
}
