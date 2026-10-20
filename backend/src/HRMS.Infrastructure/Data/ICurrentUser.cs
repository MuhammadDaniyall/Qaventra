using HRMS.Domain;

namespace HRMS.Infrastructure.Data;

/// <summary>The signed-in user and request details, read from the JWT and HTTP context. Values are null when absent.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    Guid? EmployeeId { get; }
    UserRole? Role { get; }
    string? Email { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    /// <summary>Ties data-change logs to the request log of the same call.</summary>
    string? CorrelationId { get; }
}
