using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// A named set of HR modules (payroll, recruitment…). Given to an HR user it limits them to those modules; given to a
/// manager or employee it opens those modules' HR screens to them.
/// </summary>
public class AccessTemplate : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Comma-separated module keys, see AccessModules.</summary>
    public string Modules { get; set; } = "";
}

/// <summary>An idea or complaint for the company, optionally anonymous.</summary>
public class Suggestion : TenantEntity
{
    /// <summary>Kept even when anonymous so people see their own; never shown to reviewers when anonymous.</summary>
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid? CategoryId { get; set; }
    public bool IsAnonymous { get; set; }
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public SuggestionStatus Status { get; set; } = SuggestionStatus.New;
    public string? Response { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}

/// <summary>Where suggestions go: its reviewers (or HR when none) are told about each new one.</summary>
public class SuggestionCategory : TenantEntity
{
    public string Name { get; set; } = "";
    /// <summary>Comma-separated employee ids; empty = HR.</summary>
    public string? ReviewerIds { get; set; }
    public bool IsActive { get; set; } = true;
}
