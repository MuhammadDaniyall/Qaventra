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
