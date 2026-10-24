using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>An in-app message for one user (and, if they allow it, an email).</summary>
public class Notification : TenantEntity
{
    public Guid UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    /// <summary>App path to open, e.g. /approvals or /letters/{id}.</summary>
    public string? Link { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

/// <summary>Who wished whom on which day, so a wish is sent once a day per occasion.</summary>
public class CelebrationWish : TenantEntity
{
    public Guid FromUserId { get; set; }
    public Guid EmployeeId { get; set; }
    /// <summary>0 = birthday, 1 = work anniversary.</summary>
    public int Kind { get; set; }
    public DateOnly Date { get; set; }
}
