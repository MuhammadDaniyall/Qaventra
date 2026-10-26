using System.Security.Claims;
using HRMS.Domain;
using HRMS.Infrastructure.Data;

namespace HRMS.Api.Auth;

public static class HrmsClaims
{
    public const string TenantId = "tenant_id";
    public const string EmployeeId = "employee_id";
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? _actingAs;

    private ClaimsPrincipal? Principal => _actingAs ?? accessor.HttpContext?.User;

    /// <summary>Background work done on someone's behalf (such as a reply by email) runs with their rights.</summary>
    public void ActAs(ClaimsPrincipal principal) => _actingAs = principal;

    public Guid? UserId => ReadGuid(ClaimTypes.NameIdentifier);
    public Guid? TenantId => ReadGuid(HrmsClaims.TenantId);
    public Guid? EmployeeId => ReadGuid(HrmsClaims.EmployeeId);

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;

    private Guid? ReadGuid(string claim) =>
        Guid.TryParse(Principal?.FindFirstValue(claim), out var id) ? id : null;
}

public static class RoleChecks
{
    /// <summary>
    /// Full HR rights inside a company: the company's admin, its HR managers, and the platform owner
    /// (SuperAdmin) while they have opened that company.
    /// </summary>
    public static bool IsHr(this ICurrentUser me) => me.Role is UserRole.SuperAdmin or UserRole.CompanyAdmin or UserRole.HrManager;

    public static bool IsCompanyAdmin(this ICurrentUser me) => me.Role is UserRole.SuperAdmin or UserRole.CompanyAdmin;
}

public static class Policies
{
    /// <summary>Platform owner only.</summary>
    public const string SuperAdmin = "SuperAdmin";
    /// <summary>Any user who belongs to a company (everyone except SuperAdmin).</summary>
    public const string CompanyUser = "CompanyUser";
    /// <summary>Company admin or HR manager.</summary>
    public const string Hr = "Hr";
    /// <summary>Company admin only.</summary>
    public const string Admin = "Admin";
    /// <summary>Anyone who can approve requests: admin, HR or line manager.</summary>
    public const string Approver = "Approver";
}
