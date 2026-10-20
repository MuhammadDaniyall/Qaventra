using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>Local date and time in the signed-in company's time zone.</summary>
public class TenantClock(HrmsDbContext db, TimeProvider time)
{
    private TimeZoneInfo? _zone;

    public async Task<TimeZoneInfo> ZoneAsync()
    {
        if (_zone is not null)
            return _zone;

        var zoneId = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.TimeZoneId).FirstOrDefaultAsync();
        _zone = TimeZones.Find(zoneId);
        return _zone;
    }

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<DateTime> LocalNowAsync() => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, await ZoneAsync());

    public async Task<DateOnly> TodayAsync() => DateOnly.FromDateTime(await LocalNowAsync());

    public async Task<DateTime> ToLocalAsync(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, await ZoneAsync());

    public async Task<DateTime> ToUtcAsync(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), await ZoneAsync());
}
