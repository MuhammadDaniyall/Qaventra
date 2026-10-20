using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A reusable letter with {{Placeholders}} filled in from the employee's record when issued.</summary>
public class LetterTemplate : TenantEntity
{
    public string Name { get; set; } = "";
    public LetterCategory Category { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

/// <summary>A letter issued to one employee. Subject and body are stored as issued and never re-rendered.</summary>
public class IssuedLetter : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public LetterCategory Category { get; set; }
    /// <summary>Unique per company, e.g. HR/EXP/2026/0007.</summary>
    public string ReferenceNo { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateOnly IssuedOn { get; set; }
    public Guid? IssuedByUserId { get; set; }
    public string? IssuedByName { get; set; }
    /// <summary>When true the employee sees it under My documents.</summary>
    public bool SharedWithEmployee { get; set; } = true;
}
