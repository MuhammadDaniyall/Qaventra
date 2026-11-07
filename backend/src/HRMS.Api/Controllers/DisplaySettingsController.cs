using HRMS.Api.Auth;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record DisplaySettingsDto(bool Use24HourTime);

/// <summary>How the company's screens, reports and Excel files show times.</summary>
[ApiController]
[Route("api/setup/display")]
[Authorize(Policy = Policies.CompanyUser)]
public class DisplaySettingsController(HrmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<DisplaySettingsDto> Get() =>
        new(await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.Use24HourTime).FirstOrDefaultAsync());

    [HttpPut, Authorize(Policy = Policies.Hr)]
    public async Task<DisplaySettingsDto> Save(DisplaySettingsDto req)
    {
        var tenant = await db.Tenants.FirstAsync(t => t.Id == db.CurrentTenantId);
        tenant.Use24HourTime = req.Use24HourTime;
        await db.SaveChangesAsync();
        return new(tenant.Use24HourTime);
    }
}
