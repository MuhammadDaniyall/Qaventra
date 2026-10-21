using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Missed punch / work from home requests: rules, approval chain and the attendance they create.</summary>
public class AttendanceRequestTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private static DateOnly LastWeekday(int daysBack)
    {
        var d = Today.AddDays(-daysBack);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(-1);
        return d;
    }

    [Fact]
    public async Task Approved_request_writes_the_days_attendance_and_rules_are_enforced()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Punches");
        async Task<(Guid Id, HttpClient Client)> Person(string first, Guid? managerId, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "P", joiningDate = "2025-01-01", status = "Active", managerId }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@punches.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Punch-Pass-1", role }));
            return (id, await factory.SignInAsync(email, "Punch-Pass-1"));
        }
        var lead = await Person("Lena", null, "Manager");
        var worker = await Person("Wali", lead.Id, "Employee");

        var day = LastWeekday(3);
        var req = await Ok(await worker.Client.PostAsJsonAsync("/api/attendance/requests", new
        {
            date = day.ToString("yyyy-MM-dd"), kind = "MissedPunch", checkIn = "09:10:00", checkOut = "18:05:00", reason = "Card reader was down",
        }, ApiFactory.Json));
        Assert.Equal("Pending", req.GetProperty("status").GetString());

        // One pending request per day; no future days; not too far back; check-out after check-in.
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.Client.PostAsJsonAsync("/api/attendance/requests", new { date = day.ToString("yyyy-MM-dd"), kind = "TimeCorrection", checkIn = "09:00:00", reason = "x" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.Client.PostAsJsonAsync("/api/attendance/requests", new { date = Today.AddDays(2).ToString("yyyy-MM-dd"), kind = "WorkFromHome", checkIn = "09:00:00", reason = "x" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.Client.PostAsJsonAsync("/api/attendance/requests", new { date = Today.AddDays(-60).ToString("yyyy-MM-dd"), kind = "MissedPunch", checkIn = "09:00:00", reason = "x" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.Client.PostAsJsonAsync("/api/attendance/requests", new { date = LastWeekday(5).ToString("yyyy-MM-dd"), kind = "MissedPunch", checkIn = "18:00:00", checkOut = "09:00:00", reason = "x" }, ApiFactory.Json)).StatusCode);

        // The manager sees it in the inbox and approves; the attendance appears with source Request.
        var inbox = await Ok(await lead.Client.GetAsync("/api/approvals/inbox"));
        Assert.Contains(inbox.EnumerateArray(), i => i.GetProperty("requestType").GetString() == "Attendance");
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.Client.PostAsJsonAsync($"/api/attendance/requests/{req.GetProperty("id").GetGuid()}/approve", new { })).StatusCode);
        await Ok(await lead.Client.PostAsJsonAsync($"/api/attendance/requests/{req.GetProperty("id").GetGuid()}/approve", new { note = "OK" }));

        var month = await Ok(await worker.Client.GetAsync($"/api/attendance/my?year={day.Year}&month={day.Month}"));
        var record = month.EnumerateArray().Single(a => a.GetProperty("date").GetString() == day.ToString("yyyy-MM-dd"));
        Assert.Equal("Request", record.GetProperty("source").GetString());
        Assert.Contains("Card reader", record.GetProperty("note").GetString());
        Assert.Equal(535, record.GetProperty("workedMinutes").GetInt32());

        // A rejected request leaves attendance alone; a cancelled one disappears from the inbox.
        var wfh = await Ok(await worker.Client.PostAsJsonAsync("/api/attendance/requests", new { date = LastWeekday(6).ToString("yyyy-MM-dd"), kind = "WorkFromHome", checkIn = "10:00:00", checkOut = "17:00:00", reason = "Plumber at home" }, ApiFactory.Json));
        await Ok(await lead.Client.PostAsJsonAsync($"/api/attendance/requests/{wfh.GetProperty("id").GetGuid()}/reject", new { note = "Please apply for leave" }));
        var mine = await Ok(await worker.Client.GetAsync("/api/attendance/requests/my"));
        Assert.Equal("Rejected", mine.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == wfh.GetProperty("id").GetGuid()).GetProperty("status").GetString());

        var duty = await Ok(await worker.Client.PostAsJsonAsync("/api/attendance/requests", new { date = LastWeekday(8).ToString("yyyy-MM-dd"), kind = "OfficialDuty", checkIn = "09:00:00", checkOut = "17:30:00", reason = "Client site" }, ApiFactory.Json));
        await Ok(await worker.Client.PostAsync($"/api/attendance/requests/{duty.GetProperty("id").GetGuid()}/cancel", null));
        Assert.DoesNotContain((await Ok(await lead.Client.GetAsync("/api/approvals/inbox"))).EnumerateArray(), i => i.GetProperty("subjectId").GetGuid() == duty.GetProperty("id").GetGuid());
    }
}
