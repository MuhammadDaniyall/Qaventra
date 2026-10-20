namespace HRMS.Domain.Entities;

/// <summary>
/// Append-only record of one event. TenantId is null for platform (SuperAdmin) events and anonymous requests.
/// Not an ITenantEntity on purpose: SuperAdmin reads across companies, and company admins are filtered explicitly.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public AuditCategory Category { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public UserRole? Role { get; set; }

    /// <summary>For Auth: LoginSucceeded, LoginFailed... For Data: Created, Updated, Deleted. For Request: the HTTP method.</summary>
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    /// <summary>JSON of changed fields, { "Field": { "old": ..., "new": ... } }. Secrets are masked.</summary>
    public string? Changes { get; set; }

    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public int? DurationMs { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
}
