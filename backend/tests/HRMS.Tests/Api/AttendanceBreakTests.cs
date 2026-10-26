using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Breaks between check-in and check-out, and the team's break report.</summary>
public class AttendanceBreakTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task A_break_is_taken_ended_and_reported()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Break Co");
        // A shift that runs all day, so check-in is never refused for being too early or late.
        await Ok(await admin.PostAsJsonAsync("/api/setup/shifts", new
        {
            name = "All day", startTime = "00:00:00", endTime = "23:59:00", graceMinutes = 0, weeklyOffDays = Array.Empty<string>(), isDefault = true, breakMinutes = 45,
        }, ApiFactory.Json));
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Bilal", lastName = "B", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var email = $"bilal+{Guid.NewGuid():N}@break.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Break-Pass-1234", role = "Employee" }));
        var worker = await factory.SignInAsync(email, "Break-Pass-1234");

        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/attendance/break/start", new { kind = "Lunch" }, ApiFactory.Json)).StatusCode);
        await Ok(await worker.PostAsJsonAsync("/api/attendance/check-in", new { source = "Web" }));

        var started = await Ok(await worker.PostAsJsonAsync("/api/attendance/break/start", new { kind = "Prayer" }, ApiFactory.Json));
        Assert.Equal("Prayer", started.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, started.GetProperty("end").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/attendance/break/start", new { kind = "Tea" }, ApiFactory.Json)).StatusCode);

        var today = await Ok(await worker.GetAsync("/api/attendance/today"));
        Assert.Equal(45, today.GetProperty("allowedBreakMinutes").GetInt32());
        Assert.Single(today.GetProperty("breaks").EnumerateArray());

        // HR sees the person on a break right now.
        var report = await Ok(await admin.GetAsync("/api/attendance/breaks"));
        var row = Assert.Single(report.EnumerateArray());
        Assert.True(row.GetProperty("onBreak").GetBoolean());
        Assert.Equal(45, row.GetProperty("allowedMinutes").GetInt32());

        var ended = await Ok(await worker.PostAsync("/api/attendance/break/end", null));
        Assert.NotEqual(JsonValueKind.Null, ended.GetProperty("end").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsync("/api/attendance/break/end", null)).StatusCode);

        // Checking out ends an open break too.
        await Ok(await worker.PostAsJsonAsync("/api/attendance/break/start", new { kind = "Tea" }, ApiFactory.Json));
        await Ok(await worker.PostAsJsonAsync("/api/attendance/check-out", new { source = "Web" }));
        today = await Ok(await worker.GetAsync("/api/attendance/today"));
        Assert.All(today.GetProperty("breaks").EnumerateArray(), b => Assert.NotEqual(JsonValueKind.Null, b.GetProperty("end").ValueKind));
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/attendance/break/start", new { kind = "Lunch" }, ApiFactory.Json)).StatusCode);

        row = Assert.Single((await Ok(await admin.GetAsync("/api/attendance/breaks"))).EnumerateArray());
        Assert.Equal(2, row.GetProperty("count").GetInt32());
        Assert.False(row.GetProperty("onBreak").GetBoolean());
        // An employee can't read everyone's breaks.
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync("/api/attendance/breaks")).StatusCode);
    }
}
