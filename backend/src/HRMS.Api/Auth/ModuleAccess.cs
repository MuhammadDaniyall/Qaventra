using HRMS.Domain;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HRMS.Api.Auth;

/// <summary>The HR modules an access template can include, and the API paths each one covers.</summary>
public static class AccessModules
{
    public record Module(string Key, string Label, string[] Paths);

    public static readonly Module[] All =
    [
        new("employees", "Employees and their files", ["/api/employees", "/api/imports", "/api/exports"]),
        new("organization", "Organization settings", ["/api/setup", "/api/departments", "/api/designations", "/api/lookups"]),
        new("attendance", "Attendance, roster and penalties", ["/api/attendance", "/api/attendance-rules", "/api/exemptions", "/api/biometric"]),
        new("leave", "Leave", ["/api/leave", "/api/leave-rules"]),
        new("payroll", "Payroll and pay changes", ["/api/payroll", "/api/pay-changes", "/api/pay"]),
        new("expenses", "Expenses", ["/api/expenses", "/api/expense-setup"]),
        new("approvals", "Approvals (force approve, delegations)", ["/api/approvals"]),
        new("recruitment", "Recruitment", ["/api/recruitment", "/api/recruitment-extras"]),
        new("onboarding", "Onboarding", ["/api/onboarding"]),
        new("separations", "Separations", ["/api/separations", "/api/separation-setup"]),
        new("letters", "HR letters", ["/api/letters"]),
        new("assets", "Assets", ["/api/assets"]),
        new("helpdesk", "Help desk", ["/api/helpdesk"]),
        new("performance", "Performance", ["/api/performance"]),
        new("training", "Training", ["/api/training"]),
        new("travel", "Travel", ["/api/travel"]),
        new("budget", "Manpower budget", ["/api/budget"]),
        new("piecework", "Piece work", ["/api/piece-work"]),
        new("desks", "Desks and rooms", ["/api/desks"]),
        new("projects", "Projects and timesheets", ["/api/projects", "/api/timesheets"]),
        new("announcements", "Announcements and polls", ["/api/announcements"]),
        new("reports", "Reports, alerts and scheduled reports", ["/api/reports", "/api/scheduled"]),
        new("suggestions", "Suggestions", ["/api/suggestions"]),
    ];

    /// <summary>The module a request path belongs to (longest matching prefix), or null when it is not a module path.</summary>
    public static string? For(string path)
    {
        string? best = null;
        var length = 0;
        foreach (var m in All)
            foreach (var p in m.Paths)
                if (p.Length > length && path.StartsWith(p, StringComparison.OrdinalIgnoreCase)
                    && (path.Length == p.Length || path[p.Length] is '/' or '?'))
                {
                    best = m.Key;
                    length = p.Length;
                }
        return best;
    }

    public static List<string> Parse(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(k => All.Any(m => m.Key == k)).ToList();
}

/// <summary>Role check for the Hr and Approver policies, adjusted by the user's access template.</summary>
public record ModuleRoleRequirement(params UserRole[] Roles) : IAuthorizationRequirement;

/// <summary>
/// Without a template: the usual role check. With one: an HR user may only use the template's modules (other HR
/// paths are refused); a manager or employee may also use those modules' HR screens.
/// </summary>
public class ModuleRoleHandler(IServiceScopeFactory scopes, IMemoryCache cache) : AuthorizationHandler<ModuleRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ModuleRoleRequirement requirement)
    {
        var user = context.User;
        if (!Enum.TryParse<UserRole>(user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, out var role))
            return;
        if (role is UserRole.SuperAdmin or UserRole.CompanyAdmin)
        {
            if (requirement.Roles.Contains(role)) context.Succeed(requirement);
            return;
        }
        var path = (context.Resource as HttpContext)?.Request.Path.Value ?? "";
        var module = AccessModules.For(path);
        var modules = Guid.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId)
            ? await ModulesAsync(userId) : null;
        if (requirement.Roles.Contains(role))
        {
            // An HR user with a template stays inside its modules.
            if (role != UserRole.HrManager || modules is null || module is null || modules.Contains(module))
                context.Succeed(requirement);
            return;
        }
        if (modules is not null && module is not null && modules.Contains(module))
            context.Succeed(requirement);
    }

    /// <summary>The user's template modules, or null when they have none. Cached briefly.</summary>
    private async Task<List<string>?> ModulesAsync(Guid userId) =>
        await cache.GetOrCreateAsync($"access-modules:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            var csv = await db.Users.IgnoreQueryFilters().Where(u => u.Id == userId && u.AccessTemplateId != null)
                .Select(u => db.AccessTemplates.IgnoreQueryFilters().Where(t => t.Id == u.AccessTemplateId).Select(t => t.Modules).FirstOrDefault())
                .FirstOrDefaultAsync();
            return csv is null ? null : AccessModules.Parse(csv);
        });

    public static void Forget(IMemoryCache cache, Guid userId) => cache.Remove($"access-modules:{userId}");
}
