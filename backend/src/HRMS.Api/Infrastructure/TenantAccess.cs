using HRMS.Api.Auth;
using HRMS.Domain;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HRMS.Api.Infrastructure;

public static class AccessCodes
{
    public const string CompanyInactive = "CompanyInactive";
    public const string SubscriptionExpired = "SubscriptionExpired";
    public const string UserInactive = "UserInactive";
}

/// <summary>Answers "may this company / user use the system right now?", cached briefly so it can run on every request.</summary>
public class TenantAccessChecker(IServiceScopeFactory scopes, IMemoryCache cache, TimeProvider time)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private record TenantState(bool IsActive, DateOnly? EndsOn, string TimeZoneId);

    public async Task<TenantAccess> TenantAsync(Guid tenantId)
    {
        var state = await cache.GetOrCreateAsync($"tenant-access:{tenantId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            return await db.Tenants.Where(t => t.Id == tenantId)
                .Select(t => new TenantState(t.IsActive, t.SubscriptionEndsOn, t.TimeZoneId))
                .FirstOrDefaultAsync();
        });

        if (state is null)
            return TenantAccess.Inactive; // deleted company
        var tenant = new Domain.Entities.Tenant { IsActive = state.IsActive, SubscriptionEndsOn = state.EndsOn };
        return tenant.AccessOn(LocalToday(state.TimeZoneId));
    }

    /// <summary>What a token is checked against: the login's current state. Null when the login no longer exists.</summary>
    public record UserState(bool IsActive, UserRole Role, Guid SecurityStamp);

    public async Task<UserState?> UserAsync(Guid userId)
    {
        return await cache.GetOrCreateAsync($"user-active:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            return await db.Users.IgnoreQueryFilters().Where(u => u.Id == userId)
                .Select(u => new UserState(u.IsActive, u.Role, u.SecurityStamp))
                .FirstOrDefaultAsync();
        });
    }

    public void Invalidate(Guid tenantId) => cache.Remove($"tenant-access:{tenantId}");

    public void InvalidateUser(Guid userId) => cache.Remove($"user-active:{userId}");

    public DateOnly LocalToday(string timeZoneId)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, Services.TimeZones.Find(timeZoneId)));
    }

    public static (string Code, string Message) Describe(TenantAccess access) => access switch
    {
        TenantAccess.Expired => (AccessCodes.SubscriptionExpired, "Your company's subscription has expired. Contact your HR software provider to renew."),
        _ => (AccessCodes.CompanyInactive, "Your company's account is inactive. Contact your HR software provider."),
    };
}

/// <summary>
/// Blocks every request from a company user when their company is inactive or expired, or their login is disabled,
/// so a suspension takes effect immediately and not only at the next sign-in. A token issued before a role change or
/// password reset gets 401. The owner is never blocked.
/// </summary>
public class TenantAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ICurrentUser me, TenantAccessChecker checker, IProblemDetailsService problems)
    {
        if (me.UserId is { } userId && me.Role is not null && me.Role != UserRole.SuperAdmin)
        {
            string? code = null, message = null;
            var user = await checker.UserAsync(userId);
            if (user is not { IsActive: true })
            {
                (code, message) = (AccessCodes.UserInactive, "Your login is disabled. Contact your administrator.");
            }
            else if (user.Role != me.Role || http.User.FindFirst(HrmsClaims.SecurityStamp)?.Value != user.SecurityStamp.ToString())
            {
                // The role changed or the password was reset since this token was issued: sign in again.
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails { Status = 401, Title = "Your session has ended. Please sign in again." },
                });
                return;
            }
            else if (me.TenantId is { } tenantId)
            {
                var access = await checker.TenantAsync(tenantId);
                if (access != TenantAccess.Allowed)
                    (code, message) = TenantAccessChecker.Describe(access);
            }

            if (code is not null)
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                var details = new ProblemDetails { Status = 403, Title = message };
                details.Extensions["code"] = code;
                await problems.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = details });
                return;
            }
        }
        await next(http);
    }
}
