using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>Someone to call if something happens to the employee. Employees keep their own list.</summary>
public class EmergencyContact : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public string Relation { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? AltPhone { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// An employee's request to correct their own personal details. Applied to the employee record once approved.
/// <see cref="ChangesJson"/> holds a list of { field, from, to }.
/// </summary>
public class ProfileChangeRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string ChangesJson { get; set; } = "[]";
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>
/// One step in an employee's job history: transfer, promotion, new manager, re-hire. Names are copied at the time,
/// so the history reads the same after departments or titles are renamed.
/// </summary>
public class JobChange : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public JobChangeKind Kind { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public string? FromDesignation { get; set; }
    public string? ToDesignation { get; set; }
    public string? FromManager { get; set; }
    public string? ToManager { get; set; }
    public string? Note { get; set; }
    public string? RecordedByName { get; set; }
}
