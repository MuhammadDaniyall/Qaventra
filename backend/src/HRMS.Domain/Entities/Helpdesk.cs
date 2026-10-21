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
}
