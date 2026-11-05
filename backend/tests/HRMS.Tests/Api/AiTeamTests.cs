using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>AI Team: robots start off, find facts from the records, propose only when allowed, wait for a person, and stop on the emergency stop.</summary>
public class AiTeamTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Document_watcher_finds_expiring_ids_and_proposes_reminders_for_approval()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("AiTeam");
        var empId = (await Json(await admin.PostAsJsonAsync("/api/employees",
            new { firstName = "Zara", lastName = "Khan", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json))).GetProperty("id").GetGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            var e = await db.Employees.IgnoreQueryFilters().FirstAsync(x => x.Id == empId);
            e.CnicExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
            await db.SaveChangesAsync();
        }

        var team = await Json(await admin.GetAsync("/api/ai-team"));
        var robots = team.GetProperty("robots").EnumerateArray().ToList();
        Assert.Equal(8, robots.Count);
        Assert.All(robots, r => Assert.False(r.GetProperty("enabled").GetBoolean()));
        var watcher = robots.First(r => r.GetProperty("kind").GetString() == "DocumentWatcher");
        var id = watcher.GetProperty("id").GetGuid();

        // Advisory: finds it, proposes nothing.
        var run = await Json(await admin.PostAsync($"/api/ai-team/{id}/run", null));
        Assert.Equal("Done", run.GetProperty("status").GetString());
        Assert.Contains(run.GetProperty("findings").EnumerateArray(), f => f.GetProperty("title").GetString()!.Contains("Zara Khan: CNIC expires"));
        Assert.Equal(0, run.GetProperty("proposals").GetInt32());

        // Supervised: proposes a reminder, which waits for a person; the same reminder is not proposed twice.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/ai-team/{id}",
            new { enabled = true, autonomy = "Supervised", ownerUserId = (Guid?)null, runHour = 9, runsPerMonth = 40 }, ApiFactory.Json)).StatusCode);
        Assert.Equal(1, (await Json(await admin.PostAsync($"/api/ai-team/{id}/run", null))).GetProperty("proposals").GetInt32());
        Assert.Equal(0, (await Json(await admin.PostAsync($"/api/ai-team/{id}/run", null))).GetProperty("proposals").GetInt32());
        var pending = (await Json(await admin.GetAsync("/api/ai-team/proposals"))).EnumerateArray().ToList();
        var proposal = Assert.Single(pending);
        Assert.Equal("notify_employee", proposal.GetProperty("action").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/ai-team/proposals/{proposal.GetProperty("id").GetGuid()}/decide",
            new { approve = true, body = "Please renew your CNIC this week." })).StatusCode);
        Assert.Empty((await Json(await admin.GetAsync("/api/ai-team/proposals"))).EnumerateArray());
        var decided = Assert.Single((await Json(await admin.GetAsync("/api/ai-team/proposals?pending=false"))).EnumerateArray());
        Assert.Equal("Approved", decided.GetProperty("status").GetString());

        // Every run is logged with its steps.
        var runs = (await Json(await admin.GetAsync($"/api/ai-team/runs?robotId={id}"))).EnumerateArray().ToList();
        Assert.Equal(3, runs.Count);
        Assert.Contains(runs[0].GetProperty("steps").EnumerateArray(), s => s.GetString()!.StartsWith("Started"));

        // Emergency stop: nothing runs until started again.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/ai-team/stop", new { stopped = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/ai-team/{id}/run", null)).StatusCode);
        Assert.True((await Json(await admin.GetAsync("/api/ai-team"))).GetProperty("stopped").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/ai-team/stop", new { stopped = false })).StatusCode);
    }

    [Fact]
    public async Task Advice_only_robots_cannot_be_given_more_autonomy_and_budgets_hold()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("AiTeam2");
        var robots = (await Json(await admin.GetAsync("/api/ai-team"))).GetProperty("robots").EnumerateArray().ToList();
        var risk = robots.First(r => r.GetProperty("kind").GetString() == "PeopleRisk").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/ai-team/{risk}",
            new { enabled = true, autonomy = "Routine", runHour = 9, runsPerMonth = 40 }, ApiFactory.Json)).StatusCode);

        var payroll = robots.First(r => r.GetProperty("kind").GetString() == "PayrollChecker").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/ai-team/{payroll}",
            new { enabled = false, autonomy = "Advisory", runHour = 9, runsPerMonth = 1 }, ApiFactory.Json)).StatusCode);
        Assert.Equal("Done", (await Json(await admin.PostAsync($"/api/ai-team/{payroll}/run", null))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/ai-team/{payroll}/run", null)).StatusCode);

        // Every robot runs on an empty company without failing.
        foreach (var r in robots.Where(r => r.GetProperty("kind").GetString() != "PayrollChecker"))
            Assert.Equal("Done", (await Json(await admin.PostAsync($"/api/ai-team/{r.GetProperty("id").GetGuid()}/run", null))).GetProperty("status").GetString());
    }
}
