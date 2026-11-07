using System.Globalization;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>Times in the company's chosen clock: "6:30 PM" (the default) or "18:30".</summary>
public static class TimeText
{
    public static string Of(DateTime t, bool h24) => t.ToString(h24 ? "HH:mm" : "h:mm tt", CultureInfo.InvariantCulture);

    public static Task<bool> Use24HourAsync(HrmsDbContext db) =>
        db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.Use24HourTime).FirstOrDefaultAsync();
}
