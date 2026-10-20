using HRMS.Api.Auth;
using HRMS.Domain;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record AuditLogDto(
    long Id, DateTime TimestampUtc, AuditCategory Category, Guid? TenantId, string? CompanyName,
    Guid? UserId, string? UserEmail, UserRole? Role, string Action, string? EntityType, string? EntityId,
    string? Changes, string? Path, int? StatusCode, int? DurationMs, string? IpAddress, string? UserAgent, string? CorrelationId);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Read-only. SuperAdmin sees every company's events and platform events; a company admin sees only their company.</summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = nameof(UserRole.SuperAdmin) + "," + nameof(UserRole.CompanyAdmin))]
public class AuditLogsController(HrmsDbContext db, ICurrentUser me) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<AuditLogDto>> Get(
        [FromQuery] AuditCategory? category,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? user,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = db.AuditLogs.AsNoTracking();
        if (me.Role == UserRole.SuperAdmin)
        {
            var scope = tenantId ?? me.TenantId;
            if (scope is not null)
                q = q.Where(l => l.TenantId == scope);
        }
        else
        {
            q = q.Where(l => l.TenantId == me.TenantId);
        }

        if (category is not null) q = q.Where(l => l.Category == category);
        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(l => l.UserEmail!.Contains(user));
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(l => l.Action.Contains(action));
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(l => l.EntityType == entityType);
        if (fromUtc is not null) q = q.Where(l => l.TimestampUtc >= fromUtc);
        if (toUtc is not null) q = q.Where(l => l.TimestampUtc <= toUtc);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogDto(
                l.Id, l.TimestampUtc, l.Category, l.TenantId,
                db.Tenants.Where(t => t.Id == l.TenantId).Select(t => t.Name).FirstOrDefault(),
                l.UserId, l.UserEmail, l.Role, l.Action, l.EntityType, l.EntityId, l.Changes,
                l.Path, l.StatusCode, l.DurationMs, l.IpAddress, l.UserAgent, l.CorrelationId))
            .ToListAsync();

        return new PagedResult<AuditLogDto>(items, total, page, pageSize);
    }
}
