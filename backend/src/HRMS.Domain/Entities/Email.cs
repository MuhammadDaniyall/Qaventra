using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// A company's own wording for one kind of notification email. Subject and Intro may use {name}, {company},
/// {title} and {body}; the event's details still follow the intro.
/// </summary>
public class EmailTemplate : TenantEntity
{
    public NotificationKind Kind { get; set; }
    public string Subject { get; set; } = "";
    public string? Intro { get; set; }
    /// <summary>Off = no email for this kind at all (the in-app notification still appears).</summary>
    public bool Enabled { get; set; } = true;
}

public enum EmailLogStatus { Sent = 1, Failed = 2, OverQuota = 3, NotConfigured = 4 }

/// <summary>One email the company sent or tried to send.</summary>
public class EmailLog : TenantEntity
{
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string? Kind { get; set; }
    public EmailLogStatus Status { get; set; }
    public string? Error { get; set; }
}
