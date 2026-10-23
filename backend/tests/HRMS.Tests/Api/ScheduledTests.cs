using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Alerts (birthdays, anniversaries, probation) and reports emailed on a schedule.</summary>
public class ScheduledTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Func<string, Guid?, DateOnly, DateOnly?, Task<(Guid Id, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, DateOnly joined, DateOnly? born)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new
            {
                firstName = first, lastName = "A", joiningDate = Iso(joined), status = joined > Today.AddMonths(-6) ? "Probation" : "Active", managerId = manager,
                dateOfBirth = born is { } b ? Iso(b) : null,
            }, ApiFactory.Json))).GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant().Replace(" ", "")}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Alert-Pass-1234", role = manager is null ? "Manager" : "Employee" }));
            return (id, await factory.SignInAsync(email, "Alert-Pass-1234"));
        }
        return (admin, Person);
    }

    [Fact]
    public async Task Alerts_find_the_right_people_and_reach_the_chosen_recipients()
    {
        var (admin, person) = await CompanyAsync("Alerts Co");
        var (maha, mahaClient) = await person("Maha", null, Today.AddYears(-5), null);
        var (sana, sanaClient) = await person("Sana", maha, Today.AddYears(-3), Today.AddYears(-30));
        var (omar, omarClient) = await person("Omar", maha, Today.AddMonths(-3).AddDays(7), Today.AddDays(1).AddYears(-25));

        // Birthday today: Sana is wished and everyone hears.
        var birthday = await Ok(await admin.PostAsJsonAsync("/api/scheduled/alerts", new
        {
            name = "Birthdays", kind = "Birthday", daysBefore = 0, probationMonths = 3, sendHour = 9, toEmployee = true, toEveryone = true,
            titleTemplate = "Happy birthday, {firstname}!", bodyTemplate = "{name} turns {years} today.",
        }, ApiFactory.Json));
        var preview = await Ok(await admin.GetAsync($"/api/scheduled/alerts/{birthday.GetProperty("id").GetGuid()}/preview"));
        Assert.Equal("Happy birthday, Sana!", Assert.Single(preview.GetProperty("titles").EnumerateArray()).GetString());
        Assert.Equal(1, (await Ok(await admin.PostAsync($"/api/scheduled/alerts/{birthday.GetProperty("id").GetGuid()}/run", null))).GetProperty("people").GetInt32());
        Assert.Contains("Happy birthday, Sana!", (await Ok(await omarClient.GetAsync("/api/notifications"))).ToString());
        Assert.Contains("turns 30 today", (await Ok(await sanaClient.GetAsync("/api/notifications"))).ToString());

        // Probation ends in 7 days: only the manager is told; Omar's birthday tomorrow is not today's.
        var probation = await Ok(await admin.PostAsJsonAsync("/api/scheduled/alerts", new
        {
            name = "Probation", kind = "ProbationEnd", daysBefore = 7, probationMonths = 3, sendHour = 9, toManager = true,
            titleTemplate = "{name}'s probation ends on {date}",
        }, ApiFactory.Json));
        await Ok(await admin.PostAsync($"/api/scheduled/alerts/{probation.GetProperty("id").GetGuid()}/run", null));
        Assert.Contains("Omar A's probation ends", (await Ok(await mahaClient.GetAsync("/api/notifications"))).ToString());
        Assert.DoesNotContain("probation ends", (await Ok(await omarClient.GetAsync("/api/notifications"))).ToString());

        // An alert needs someone to send to; non-HR cannot manage alerts.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/scheduled/alerts", new { name = "x", kind = "Birthday", sendHour = 9, probationMonths = 3, titleTemplate = "x" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mahaClient.GetAsync("/api/scheduled/alerts")).StatusCode);

        var runs = await Ok(await admin.GetAsync("/api/scheduled/runs"));
        Assert.Equal(2, runs.GetArrayLength());
        _ = (sana, omar);
    }

    [Fact]
    public async Task Reports_preview_schedule_and_send()
    {
        var (admin, person) = await CompanyAsync("Reports Co");
        var (maha, _) = await person("Maha", null, Today.AddYears(-5), null);
        await person("Sana", maha, Today.AddDays(-3), null);

        var bad = await admin.PostAsJsonAsync("/api/scheduled/reports", new { name = "Nobody", kind = "Headcount", frequency = "Weekly", day = 1, sendHour = 9 }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        await Ok(await admin.PostAsJsonAsync("/api/scheduled/reports", new
        {
            name = "Weekly headcount", kind = "Headcount", frequency = "Weekly", day = 1, sendHour = 9, recipientIds = new[] { maha }, extraEmails = "ceo@example.com; not-an-email",
        }, ApiFactory.Json));
        var report = Assert.Single((await Ok(await admin.GetAsync("/api/scheduled/reports"))).EnumerateArray());
        Assert.Equal("ceo@example.com", report.GetProperty("extraEmails").GetString());
        var next = report.GetProperty("nextRunAtUtc").GetDateTime();
        Assert.True(next > DateTime.UtcNow && next < DateTime.UtcNow.AddDays(8));

        var id = report.GetProperty("id").GetGuid();
        var table = await Ok(await admin.GetAsync($"/api/scheduled/reports/{id}/preview"));
        var total = table.GetProperty("rows").EnumerateArray().Last();
        Assert.Equal("Total", total[0].GetString());
        Assert.Equal("3", total[1].GetString()); // the company admin's own record, Maha and Sana
        Assert.Equal("2", total[3].GetString()); // the admin record (set up today) and Sana joined this week

        var excel = await admin.GetAsync($"/api/scheduled/reports/{id}/excel");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.True((await excel.Content.ReadAsByteArrayAsync()).Length > 1000);

        Assert.Equal(2, (await Ok(await admin.PostAsync($"/api/scheduled/reports/{id}/send", null))).GetProperty("recipients").GetInt32());
        var draft = await Ok(await admin.PostAsJsonAsync("/api/scheduled/reports/preview", new { name = "Draft", kind = "JoinersAndLeavers", frequency = "Monthly", day = 1, sendHour = 9, recipientIds = new[] { maha } }, ApiFactory.Json));
        Assert.Contains(draft.GetProperty("rows").EnumerateArray(), r => r[0].GetString() == "Joined" && r[2].GetString() == "Sana A");
    }
}
