using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A clearance checklist used when someone leaves: items by department, in levels (later levels open once earlier ones are cleared).</summary>
public class ClearanceTemplate : TenantEntity
{
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
    public List<ClearanceTemplateItem> Items { get; set; } = [];
}

public class ClearanceTemplateItem : TenantEntity
{
    public Guid TemplateId { get; set; }
    public string Department { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>1–10: items of level 2 wait until every level-1 item is cleared.</summary>
    public int Level { get; set; } = 1;
    /// <summary>Who clears it (besides HR); null = HR.</summary>
    public Guid? ResponsibleEmployeeId { get; set; }
    public int Order { get; set; }
}

/// <summary>Questions asked in exit interviews.</summary>
public class ExitInterviewTemplate : TenantEntity
{
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
    public List<ExitQuestion> Questions { get; set; } = [];
}

public class ExitQuestion : TenantEntity
{
    public Guid TemplateId { get; set; }
    public string Text { get; set; } = "";
    public string? Category { get; set; }
    public ExitQuestionKind Kind { get; set; } = ExitQuestionKind.Rating;
    public int Order { get; set; }
}
