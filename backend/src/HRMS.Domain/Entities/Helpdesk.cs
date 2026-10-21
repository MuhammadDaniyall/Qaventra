using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>What a ticket is about (IT, Admin, Payroll...), who handles it and how fast it should be resolved.</summary>
public class TicketCategory : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Target time from raising to resolving; tickets past it show as overdue.</summary>
    public int TargetHours { get; set; } = 24;
    /// <summary>Employees who handle these tickets (comma-separated ids). HR can always handle every ticket.</summary>
    public string AgentIds { get; set; } = "";
    public bool IsActive { get; set; } = true;
    /// <summary>Hears about tickets that go overdue (escalation). HR when not set.</summary>
    public Guid? LeadEmployeeId { get; set; }
    public TicketAssignment AssignmentMode { get; set; } = TicketAssignment.Manual;
    /// <summary>Agents may close a ticket themselves instead of waiting for the employee to confirm.</summary>
    public bool AgentCanClose { get; set; }
    /// <summary>Resolved tickets close by themselves after this many days; 0 never.</summary>
    public int AutoCloseDays { get; set; } = 3;
    /// <summary>The employee may reopen a closed ticket for this many days; 0 never (resolved ones can always be reopened).</summary>
    public int ReopenDays { get; set; } = 7;
}

/// <summary>A narrower kind of ticket inside a category, e.g. VPN under IT, with its own target and agents.</summary>
public class TicketType : TenantEntity
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Overrides the category's target when set.</summary>
    public int? TargetHours { get; set; }
    /// <summary>Overrides the category's agents when not empty (comma-separated ids).</summary>
    public string AgentIds { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

/// <summary>Company help desk preferences: its own names for statuses and priorities (JSON maps of enum name to label).</summary>
public class HelpdeskSettings : TenantEntity
{
    public string StatusLabels { get; set; } = "{}";
    public string PriorityLabels { get; set; } = "{}";
}

public class Ticket : TenantEntity
{
    /// <summary>Per company, e.g. 42 shown as HD-0042.</summary>
    public int Number { get; set; }
    /// <summary>Who the ticket is for.</summary>
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid RaisedByEmployeeId { get; set; }
    public Guid CategoryId { get; set; }
    public TicketCategory? Category { get; set; }
    public Guid? TypeId { get; set; }
    public TicketType? Type { get; set; }
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public Guid? AssigneeEmployeeId { get; set; }
    /// <summary>Colleagues pulled in to help (comma-separated ids); they can read and reply.</summary>
    public string InvolvedIds { get; set; } = "";
    public DateTime DueAtUtc { get; set; }
    public DateTime? FirstResponseAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    /// <summary>1–5 from the employee when closing.</summary>
    public int? Rating { get; set; }
    public string? Feedback { get; set; }
    /// <summary>When the "due soon" reminder went out.</summary>
    public DateTime? DueSoonRemindedAtUtc { get; set; }
    /// <summary>When the overdue escalation went out.</summary>
    public DateTime? EscalatedAtUtc { get; set; }
}

/// <summary>A reply, an internal note (agents only) or a line recording a change.</summary>
public class TicketComment : TenantEntity
{
    public Guid TicketId { get; set; }
    public Guid? AuthorEmployeeId { get; set; }
    public string AuthorName { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsInternal { get; set; }
    public bool IsSystem { get; set; }
    public DateTime? EditedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}
