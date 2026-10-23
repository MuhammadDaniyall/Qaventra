using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>Client or internal work people log hours against.</summary>
public class Project : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Client { get; set; }
    public string? Description { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    /// <summary>Planned hours; actual hours are compared against it.</summary>
    public decimal? BudgetHours { get; set; }
    public bool Billable { get; set; } = true;
    /// <summary>Open to everyone (true) or only to its members.</summary>
    public bool OpenToAll { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public List<ProjectMember> Members { get; set; } = [];
    public List<ProjectTask> Tasks { get; set; } = [];
}

public class ProjectMember : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid EmployeeId { get; set; }
    /// <summary>Cost (or billing) per hour, for project cost reports.</summary>
    public decimal? HourlyRate { get; set; }
}

public class ProjectTask : TenantEntity
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public Guid? AssigneeEmployeeId { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    public bool IsDone { get; set; }
}

/// <summary>One person's week of logged hours. Submitted to their approver; approved weeks are locked.</summary>
public class Timesheet : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    /// <summary>Monday of the week.</summary>
    public DateOnly WeekStart { get; set; }
    public TimesheetStatus Status { get; set; } = TimesheetStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public List<TimesheetLine> Lines { get; set; } = [];
}

public class TimesheetLine : TenantEntity
{
    public Guid TimesheetId { get; set; }
    public DateOnly Date { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public int Minutes { get; set; }
    public string? Note { get; set; }
}
