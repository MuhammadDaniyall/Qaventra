using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Two computers running the virtual clients take turns: one lease at a time, shared progress, and a preferred computer.</summary>
public class AutomationLeaseTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task One_holder_at_a_time_and_the_other_defers()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Automation");
        Assert.True((await Json(await admin.PostAsJsonAsync("/api/setup/automation/lease", new { holder = "mac", minutes = 40 }))).GetProperty("granted").GetBoolean());
        // Windows cannot take it while the Mac holds it, nor save state.
        Assert.False((await Json(await admin.PostAsJsonAsync("/api/setup/automation/lease", new { holder = "windows", minutes = 40 }))).GetProperty("granted").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/setup/automation/state", new { holder = "windows", state = "{}" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/setup/automation/state", new { holder = "mac", state = "{\"done\":1}" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/setup/automation/release", new { holder = "mac" })).StatusCode);

        // Just after the Mac ran, Windows defers to it; without deferring it may run and sees the Mac's progress.
        var deferred = await Json(await admin.PostAsJsonAsync("/api/setup/automation/lease", new { holder = "windows", minutes = 40, deferToOthersMinutes = 35 }));
        Assert.False(deferred.GetProperty("granted").GetBoolean());
        Assert.Equal("mac", deferred.GetProperty("lastHolder").GetString());
        Assert.True((await Json(await admin.PostAsJsonAsync("/api/setup/automation/lease", new { holder = "windows", minutes = 40 }))).GetProperty("granted").GetBoolean());
        Assert.Equal("{\"done\":1}", (await Json(await admin.GetAsync("/api/setup/automation"))).GetProperty("state").GetString());
    }
}
