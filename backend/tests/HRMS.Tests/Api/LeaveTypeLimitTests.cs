using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Leave types can limit how far ahead, how far back and how many days a month people request.</summary>
public class LeaveTypeLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ahead_back_and_monthly_limits()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("LeaveLimits");
        var id = (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = "Lubna", lastName = "L", joiningDate = "2020-01-01", status = "Active" }, ApiFactory.Json))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var email = $"lubna+{Guid.NewGuid():N}@limits.test";
        (await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Limit-Pass-1234", role = "Employee" })).EnsureSuccessStatusCode();
        var lubna = await factory.SignInAsync(email, "Limit-Pass-1234");

        var created = await admin.PostAsJsonAsync("/api/setup/leave-types", new
        {
            name = "Outing", code = "OUT", annualQuota = 20, isPaid = true, isActive = true, maxDaysInAdvance = 60, maxBackdatedDays = 3, monthlyLimit = 1,
        }, ApiFactory.Json);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var type = (await created.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        DateOnly Weekday(DateOnly d) { while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1); return d; }
        Task<HttpResponseMessage> Ask(DateOnly d) => lubna.PostAsJsonAsync("/api/leave", new { leaveTypeId = type, fromDate = d.ToString("yyyy-MM-dd"), toDate = d.ToString("yyyy-MM-dd"), reason = "x" }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, (await Ask(Weekday(today.AddDays(90)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ask(Weekday(today.AddDays(-20)))).StatusCode);
        // Two weekdays in the same month: the second one passes the one-day monthly limit.
        var first = Weekday(new DateOnly(today.Year, today.Month, 1).AddMonths(1));
        var second = Weekday(first.AddDays(1));
        var ok = await Ask(first);
        Assert.True(ok.IsSuccessStatusCode, await ok.Content.ReadAsStringAsync());
        var over = await Ask(second);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains("a month", await over.Content.ReadAsStringAsync());
    }
}
