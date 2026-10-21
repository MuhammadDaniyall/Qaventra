using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// News or a policy shown to employees between its dates. An empty audience means everyone;
/// otherwise only the listed departments (comma-separated ids).
/// </summary>
public class Announcement : TenantEntity
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public AnnouncementKind Kind { get; set; } = AnnouncementKind.News;
    public DateOnly PublishFrom { get; set; }
    public DateOnly? PublishTo { get; set; }
    public string DepartmentIds { get; set; } = "";
    public bool IsPinned { get; set; }
    public string? LinkUrl { get; set; }
    public string? CreatedByName { get; set; }
    /// <summary>Set once the audience has been notified, so editing does not notify again.</summary>
    public DateTime? NotifiedAtUtc { get; set; }
}

/// <summary>Someone opened an announcement.</summary>
public class AnnouncementRead : TenantEntity
{
    public Guid AnnouncementId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime ReadAtUtc { get; set; }
}

/// <summary>A single-choice question open between two dates, for everyone or some departments.</summary>
public class Poll : TenantEntity
{
    public string Question { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly OpensOn { get; set; }
    public DateOnly ClosesOn { get; set; }
    public string DepartmentIds { get; set; } = "";
    /// <summary>Votes are counted but nobody, HR included, can see who voted for what.</summary>
    public bool IsAnonymous { get; set; }
    public PollResultsVisibility ResultsVisibility { get; set; } = PollResultsVisibility.AfterVoting;
    public string? CreatedByName { get; set; }
    public List<PollOption> Options { get; set; } = [];
}

public class PollOption : BaseEntity
{
    public Guid PollId { get; set; }
    public string Text { get; set; } = "";
    public int Order { get; set; }
}

public class PollVote : TenantEntity
{
    public Guid PollId { get; set; }
    public Guid OptionId { get; set; }
    /// <summary>Kept for anonymous polls too, only to stop a second vote; never shown.</summary>
    public Guid EmployeeId { get; set; }
}

/// <summary>An extra field HR adds to every employee's profile.</summary>
public class CustomField : TenantEntity
{
    public string Label { get; set; } = "";
    public CustomFieldType Type { get; set; } = CustomFieldType.Text;
    /// <summary>Dropdown choices, one per line.</summary>
    public string? Options { get; set; }
    public bool IsRequired { get; set; }
    /// <summary>Employees see it on their own profile (read-only).</summary>
    public bool VisibleToEmployee { get; set; } = true;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CustomFieldValue : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Guid FieldId { get; set; }
    public string Value { get; set; } = "";
}
