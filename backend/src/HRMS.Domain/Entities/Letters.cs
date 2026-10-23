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
    /// <summary>Letters from this template go through the letter approval chain before the employee gets them.</summary>
    public bool RequiresApproval { get; set; }
    /// <summary>Employees may ask for this letter themselves (e.g. salary certificate, experience letter).</summary>
    public bool SelfService { get; set; }
    /// <summary>Issued automatically when this happens.</summary>
    public LetterAutoUse? AutoUse { get; set; }
    public int MarginTopMm { get; set; } = 25;
    public int MarginSideMm { get; set; } = 20;
    /// <summary>Printed at the bottom of every page.</summary>
    public string? Footer { get; set; }
}

/// <summary>A letter to a job candidate (e.g. an offer), filled in from their application.</summary>
public class CandidateLetter : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public string CandidateName { get; set; } = "";
    public string ReferenceNo { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateOnly IssuedOn { get; set; }
    public string? IssuedByName { get; set; }
    public int MarginTopMm { get; set; } = 25;
    public int MarginSideMm { get; set; } = 20;
    public string? Footer { get; set; }
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
    public LetterStatus Status { get; set; } = LetterStatus.Issued;
    public Guid? TemplateId { get; set; }
    /// <summary>Asked for by the employee (self-service), with their reason.</summary>
    public bool Requested { get; set; }
    public string? RequestReason { get; set; }
    public string? DecisionNote { get; set; }
    public int MarginTopMm { get; set; } = 25;
    public int MarginSideMm { get; set; } = 20;
    public string? Footer { get; set; }
}
