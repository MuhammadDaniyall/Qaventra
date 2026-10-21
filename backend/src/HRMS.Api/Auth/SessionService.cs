using HRMS.Api.Controllers;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Auth;

public class SessionService(HrmsDbContext db, JwtTokenService tokens)
{
    /// <param name="actingTenantId">SuperAdmin only: the company they are opening.</param>
    public async Task<SessionDto> CreateAsync(User user, Guid? actingTenantId = null)
    {
        var (token, expires) = tokens.Create(user, actingTenantId);
        return new SessionDto(token, expires, await ToMeAsync(user, actingTenantId));
    }

    public async Task<MeDto> ToMeAsync(User user, Guid? actingTenantId)
    {
        var acting = user.Role == UserRole.SuperAdmin ? actingTenantId : null;
        var tenantId = user.TenantId != Guid.Empty ? user.TenantId : acting;
        var company = tenantId is null ? null
            : await db.Tenants.Where(t => t.Id == tenantId).Select(t => new { t.Name, t.SubscriptionEndsOn }).FirstOrDefaultAsync();
        return new MeDto(user.Id, user.Email, user.FullName, user.Role, tenantId, company?.Name, user.EmployeeId, acting is not null, company?.SubscriptionEndsOn, user.MustChangePassword && acting is null);
    }
}
