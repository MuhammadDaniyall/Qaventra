using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A company's approval chain for one kind of request. No steps means requests are approved automatically.</summary>
public class ApprovalFlow : TenantEntity
{
    public ApprovalRequestType RequestType { get; set; }
    public List<ApprovalFlowStep> Steps { get; set; } = [];
}

public class ApprovalFlowStep : BaseEntity
{
    public Guid ApprovalFlowId { get; set; }
    public int Order { get; set; }
    public ApproverKind Kind { get; set; }
    /// <summary>Set only for <see cref="ApproverKind.SpecificEmployee"/>.</summary>
    public Guid? EmployeeId { get; set; }
}

/// <summary>One submitted request moving through its chain. Steps are resolved to people when it is submitted.</summary>
public class ApprovalRequest : TenantEntity
{
    public ApprovalRequestType RequestType { get; set; }
    /// <summary>Id of the leave request, expense claim, etc.</summary>
    public Guid SubjectId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    /// <summary>One line shown in the approvals inbox, e.g. "Annual Leave · 3 days · 21–23 Oct".</summary>
    public string Summary { get; set; } = "";
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public int CurrentStep { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    /// <summary>HR approved it over the remaining steps.</summary>
    public bool Forced { get; set; }
    /// <summary>Who filed it, when someone (HR or the manager) filed it on the employee's behalf.</summary>
    public string? FiledByName { get; set; }
    public List<ApprovalStep> Steps { get; set; } = [];
}

public class ApprovalStep : BaseEntity
{
    public Guid ApprovalRequestId { get; set; }
    public int Order { get; set; }
    public ApproverKind Kind { get; set; }
    /// <summary>The person who must decide; null for an HR step (anyone in HR).</summary>
    public Guid? ApproverEmployeeId { get; set; }
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Waiting;
    public Guid? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? Note { get; set; }
}

/// <summary>An approver hands their approvals to someone else for a while, e.g. while on leave.</summary>
public class ApprovalDelegation : TenantEntity
{
    public Guid FromEmployeeId { get; set; }
    public Guid ToEmployeeId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    /// <summary>Comma-separated request type numbers; empty means every kind of request.</summary>
    public string RequestTypes { get; set; } = "";
    public string? Reason { get; set; }
    public bool IsCancelled { get; set; }
    public string? CreatedByName { get; set; }
}
