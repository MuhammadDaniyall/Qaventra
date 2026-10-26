using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class PerformanceExtrasTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        var text = await res.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement;
    }

    [Fact]
    public async Task Kpis_peer_feedback_and_check_ins_on_a_review()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Perf Extras");
        var dept = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Sales" }))).GetProperty("id").GetGuid();
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "X", joiningDate = "2024-01-01", status = "Active", managerId = manager, departmentId = dept }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@perfx.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Perf-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Perf-Pass-1234"));
        }
        var (lead, leadClient) = await Person("Lead", null, "Manager");
        var (worker, workerClient) = await Person("Worker", lead, "Employee");
        var (peer, peerClient) = await Person("Peer", lead, "Employee");

        // KPI library: HR writes it, everyone can read the active ones.
        var kpi = await Ok(await admin.PostAsJsonAsync("/api/performance/kpis", new { name = "Monthly sales", category = "Sales", unit = "PKR", defaultTarget = "2,000,000", defaultWeight = 40 }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.PostAsJsonAsync("/api/performance/kpis", new { name = "Mine", defaultWeight = 10 }, ApiFactory.Json)).StatusCode);
        Assert.Contains((await Ok(await workerClient.GetAsync("/api/performance/kpis"))).EnumerateArray(), k => k.GetProperty("name").GetString() == "Monthly sales");
        await Ok(await admin.PutAsJsonAsync($"/api/performance/kpis/{kpi.GetProperty("id").GetGuid()}", new { name = "Monthly sales", defaultWeight = 40, isActive = false }, ApiFactory.Json));
        Assert.Empty((await Ok(await workerClient.GetAsync("/api/performance/kpis"))).EnumerateArray());

        await Ok(await admin.PostAsJsonAsync("/api/performance/templates", new
        {
            name = "Annual",
            sections = new object[] { new { name = "Goals", weight = 100, kind = "Goals", items = Array.Empty<object>() } },
        }, ApiFactory.Json));
        var template = (await Ok(await admin.GetAsync("/api/performance/templates")))[0].GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync("/api/performance/cycles", new { name = "2026", periodFrom = "2026-01-01", periodTo = "2026-12-31", templateId = template, departmentIds = new[] { dept } }));
        var cycle = (await Ok(await admin.GetAsync("/api/performance/cycles")))[0].GetProperty("id").GetGuid();
        await Ok(await admin.PostAsync($"/api/performance/cycles/{cycle}/launch", null));
        var review = Assert.Single((await Ok(await workerClient.GetAsync("/api/performance/reviews/my"))).EnumerateArray()).GetProperty("id").GetGuid();

        // Feedback: the manager asks a peer; the employee can neither ask nor see it.
        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.PostAsJsonAsync($"/api/performance/reviews/{review}/feedback", new { employeeIds = new[] { peer } })).StatusCode);
        await Ok(await leadClient.PostAsJsonAsync($"/api/performance/reviews/{review}/feedback", new { employeeIds = new[] { peer, worker } }));
        var request = Assert.Single((await Ok(await peerClient.GetAsync("/api/performance/feedback/mine"))).EnumerateArray());
        await Ok(await peerClient.PutAsJsonAsync($"/api/performance/feedback/{request.GetProperty("id").GetGuid()}", new { rating = 4, comment = "Closes deals fast." }));
        var given = Assert.Single((await Ok(await leadClient.GetAsync($"/api/performance/reviews/{review}/feedback"))).EnumerateArray());
        Assert.Equal(4, given.GetProperty("rating").GetInt32());
        Assert.Empty((await Ok(await workerClient.GetAsync($"/api/performance/reviews/{review}/feedback"))).EnumerateArray());

        // Check-ins: both sides add notes; an outsider can't see the review.
        await Ok(await workerClient.PostAsJsonAsync($"/api/performance/reviews/{review}/check-ins", new { period = "Q3 2026", progress = 60, note = "Two big accounts signed." }));
        await Ok(await leadClient.PostAsJsonAsync($"/api/performance/reviews/{review}/check-ins", new { period = "Q3 2026", note = "Good pace." }));
        var checkIns = (await Ok(await leadClient.GetAsync($"/api/performance/reviews/{review}/check-ins"))).EnumerateArray().ToList();
        Assert.Equal(2, checkIns.Count);
        Assert.Contains(checkIns, c => c.GetProperty("byEmployee").GetBoolean() && c.GetProperty("progress").GetInt32() == 60);
        Assert.Equal(HttpStatusCode.NotFound, (await peerClient.GetAsync($"/api/performance/reviews/{review}/check-ins")).StatusCode);
    }
}
