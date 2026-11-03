using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Overtime on a working day earns comp-off (4 extra hours = half a day), and a day is paid or taken off, not both.</summary>
public class CompOffOvertimeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Long_day_earns_half_a_day_and_cannot_also_be_paid()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("CompOT");
        var id = (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = "Owais", lastName = "O", joiningDate = "2023-01-01", status = "Active" }, ApiFactory.Json))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var email = $"owais+{Guid.NewGuid():N}@compot.test";
        (await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "CompOt-Pass-1234", role = "Employee" })).EnsureSuccessStatusCode();
        var owais = await factory.SignInAsync(email, "CompOt-Pass-1234");

        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-3);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(-1);
        var iso = day.ToString("yyyy-MM-dd");
        // About 14 hours worked on a normal day: roughly 5-6 hours beyond the shift.
        (await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = id, date = iso, checkIn = "08:00:00", checkOut = "22:00:00" }, ApiFactory.Json)).EnsureSuccessStatusCode();

        var full = await owais.PostAsJsonAsync("/api/leave/comp-off", new { workedOn = iso, days = 1, reason = "Release night" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, full.StatusCode);
        Assert.Contains("extra hours earn", await full.Content.ReadAsStringAsync());
        var half = await owais.PostAsJsonAsync("/api/leave/comp-off", new { workedOn = iso, days = 0.5, reason = "Release night" }, ApiFactory.Json);
        Assert.True(half.IsSuccessStatusCode, await half.Content.ReadAsStringAsync());

        var paid = await owais.PostAsJsonAsync("/api/pay/requests", new
        {
            kind = "Overtime", workDate = iso, hours = 5, year = day.Year, month = day.Month, reason = "Release night",
        }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, paid.StatusCode);
        Assert.Contains("comp-off", await paid.Content.ReadAsStringAsync());
    }
}
