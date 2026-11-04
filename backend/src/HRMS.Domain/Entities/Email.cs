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

/// <summary>
/// The company's own mail server. When enabled, its emails go out through it instead of the platform's.
/// The password is encrypted (see SecretBox) and never sent back to the browser.
/// </summary>
public class CompanySmtp : TenantEntity
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? PasswordCipher { get; set; }
    public string From { get; set; } = "";
    public string? FromName { get; set; }
    public bool EnableSsl { get; set; } = true;
    public bool IsEnabled { get; set; }
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
