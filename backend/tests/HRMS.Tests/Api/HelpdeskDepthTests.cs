using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Ticket types, automatic assignment, reminders and escalation, category rules, editing replies, labels and reports.</summary>
public class HelpdeskDepthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private async Task<(HttpClient Admin, Func<string, Task<(Guid Id, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "D", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Desk-Pass-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Desk-Pass-1234"));
        }
        return (admin, Person);
    }

    private static async Task<JsonElement> Category(HttpClient admin, string name) =>
        (await Ok(await admin.GetAsync("/api/helpdesk/categories"))).EnumerateArray().First(c => c.GetProperty("name").GetString() == name);

    private static async Task<JsonElement> Raise(HttpClient c, Guid category, string subject, Guid? typeId = null) =>
        await Ok(await c.PostAsJsonAsync("/api/helpdesk/tickets", new { categoryId = category, typeId, subject, description = "Details" }, ApiFactory.Json));

    [Fact]
    public async Task Types_set_the_target_and_agents_and_tickets_are_assigned_in_turn()
    {
        var (admin, person) = await CompanyAsync("Types");
        var (a1, _) = await person("Ali");
        var (a2, _) = await person("Bilal");
        var (a3, _) = await person("Rafay");
        var (_, worker) = await person("Wali");

        await Ok(await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "IT support", targetHours = 24, agentIds = new[] { a1, a2 }, assignmentMode = "RoundRobin" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "Empty", targetHours = 24, assignmentMode = "LeastBusy" }, ApiFactory.Json)).StatusCode);
        var it = (await Category(admin, "IT support")).GetProperty("id").GetGuid();

        // Round robin in a category without types: Ali, Bilal, Ali.
        var assigned = new List<Guid>();
        foreach (var s in new[] { "One", "Two", "Three" })
            assigned.Add((await Raise(worker, it, s)).GetProperty("assigneeEmployeeId").GetGuid());
        Assert.Equal([a1, a2, a1], assigned);

        // Once a category has types, a type is required; the VPN type has its own agent and a 2-hour target.
        await Ok(await admin.PostAsJsonAsync($"/api/helpdesk/categories/{it}/types", new { name = "VPN", targetHours = 2, agentIds = new[] { a3 } }));
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/helpdesk/tickets", new { categoryId = it, subject = "x", description = "y" }, ApiFactory.Json)).StatusCode);
        var vpnType = (await Category(admin, "IT support")).GetProperty("types")[0].GetProperty("id").GetGuid();
        var vpn = await Raise(worker, it, "VPN drops", vpnType);
        Assert.Equal(a3, vpn.GetProperty("assigneeEmployeeId").GetGuid());
        Assert.Equal("VPN", vpn.GetProperty("type").GetString());
        var hours = (vpn.GetProperty("dueAtUtc").GetDateTime() - vpn.GetProperty("createdAtUtc").GetDateTime()).TotalHours;
        Assert.InRange(hours, 1.99, 2.01);
    }

    [Fact]
    public async Task Reminders_and_escalation_reach_the_handlers_and_the_lead()
    {
        var (admin, person) = await CompanyAsync("Remind");
        var (agent, agentClient) = await person("Ian");
        var (lead, leadClient) = await person("Lubna");
        var (_, worker) = await person("Wali");
        await Ok(await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "Facilities", targetHours = 1, agentIds = new[] { agent }, leadEmployeeId = lead }, ApiFactory.Json));
        var cat = (await Category(admin, "Facilities")).GetProperty("id").GetGuid();
        var id = (await Raise(worker, cat, "Lift stuck")).GetProperty("id").GetGuid();

        // Due within the hour: the agent hears once.
        await Ok(await agentClient.GetAsync("/api/helpdesk/summary"));
        await Ok(await agentClient.GetAsync("/api/helpdesk/summary"));
        var bell = (await Ok(await agentClient.GetAsync("/api/notifications"))).ToString();
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(bell, "Due soon: HD-0001"));

        // Past its target: escalated to the agent and the lead; the lead can see it in the queue.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            var t = await db.Tickets.IgnoreQueryFilters().FirstAsync(x => x.Id == id);
            t.DueAtUtc = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }
        Assert.Equal(1, (await Ok(await leadClient.GetAsync("/api/helpdesk/summary"))).GetProperty("overdue").GetInt32());
        Assert.Contains("Overdue: HD-0001", (await Ok(await leadClient.GetAsync("/api/notifications"))).ToString());
        Assert.Contains("Overdue: HD-0001", (await Ok(await agentClient.GetAsync("/api/notifications"))).ToString());
        Assert.True((await Ok(await leadClient.GetAsync("/api/helpdesk/me"))).GetProperty("isAgent").GetBoolean());
    }

    [Fact]
    public async Task Category_rules_editing_labels_and_reports()
    {
        var (admin, person) = await CompanyAsync("Rules");
        var (agent, agentClient) = await person("Ian");
        var (_, worker) = await person("Wali");
        var (_, other) = await person("Sara");
        await Ok(await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "Quick", targetHours = 4, agentIds = new[] { agent }, agentCanClose = true, reopenDays = 0 }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "Normal", targetHours = 4, agentIds = new[] { agent }, reopenDays = 7 }, ApiFactory.Json));
        var quick = (await Category(admin, "Quick")).GetProperty("id").GetGuid();
        var normal = (await Category(admin, "Normal")).GetProperty("id").GetGuid();

        // Quick: the agent may close directly, and closed tickets cannot be reopened.
        var q = (await Raise(worker, quick, "Need a mouse")).GetProperty("id").GetGuid();
        await Ok(await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{q}/status", new { status = "Closed", note = "Handed over" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync($"/api/helpdesk/tickets/{q}/reopen", new { reason = "Broken" })).StatusCode);

        // Normal: agents cannot close; the employee closes and can still reopen within 7 days.
        var n = (await Raise(worker, normal, "Printer jam")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{n}/status", new { status = "Closed" }, ApiFactory.Json)).StatusCode);

        // Editing a reply: only the author, shortly after; deleting leaves a marker.
        await Ok(await worker.PostAsJsonAsync($"/api/helpdesk/tickets/{n}/comments", new { body = "It is on flor 2" }));
        var comment = (await Ok(await worker.GetAsync($"/api/helpdesk/tickets/{n}"))).GetProperty("comments").EnumerateArray().Last(c => !c.GetProperty("isSystem").GetBoolean());
        var cid = comment.GetProperty("id").GetGuid();
        Assert.True(comment.GetProperty("canEdit").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await agentClient.PutAsJsonAsync($"/api/helpdesk/tickets/{n}/comments/{cid}", new { body = "hacked" })).StatusCode);
        await Ok(await worker.PutAsJsonAsync($"/api/helpdesk/tickets/{n}/comments/{cid}", new { body = "It is on floor 2" }));
        var edited = (await Ok(await agentClient.GetAsync($"/api/helpdesk/tickets/{n}"))).GetProperty("comments").EnumerateArray().First(c => c.GetProperty("id").GetGuid() == cid);
        Assert.Equal("It is on floor 2", edited.GetProperty("body").GetString());
        Assert.NotEqual(JsonValueKind.Null, edited.GetProperty("editedAtUtc").ValueKind);
        await Ok(await worker.DeleteAsync($"/api/helpdesk/tickets/{n}/comments/{cid}"));
        Assert.True((await Ok(await agentClient.GetAsync($"/api/helpdesk/tickets/{n}"))).GetProperty("comments").EnumerateArray().First(c => c.GetProperty("id").GetGuid() == cid).GetProperty("isDeleted").GetBoolean());

        await Ok(await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{n}/status", new { status = "Resolved" }, ApiFactory.Json));
        await Ok(await worker.PostAsJsonAsync($"/api/helpdesk/tickets/{n}/close", new { rating = 4 }));
        Assert.True((await Ok(await worker.GetAsync($"/api/helpdesk/tickets/{n}"))).GetProperty("canReopen").GetBoolean());
        await Ok(await worker.PostAsJsonAsync($"/api/helpdesk/tickets/{n}/reopen", new { reason = "Jammed again" }));

        // Labels: the company's own names, defaults for the rest.
        var labels = await Ok(await admin.PutAsJsonAsync("/api/helpdesk/settings", new { statuses = new Dictionary<string, string> { ["WaitingOnEmployee"] = "Need info", ["Bogus"] = "x" }, priorities = new Dictionary<string, string> { ["Urgent"] = "P1" } }));
        Assert.Equal("Need info", labels.GetProperty("statuses").GetProperty("WaitingOnEmployee").GetString());
        Assert.Equal("Open", (await Ok(await worker.GetAsync("/api/helpdesk/settings"))).GetProperty("statuses").GetProperty("Open").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.PutAsJsonAsync("/api/helpdesk/settings", new { statuses = new Dictionary<string, string>(), priorities = new Dictionary<string, string>() })).StatusCode);

        // Report: two tickets, one resolved within target and rated 4 (the reopened one is open again).
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        var report = await Ok(await agentClient.GetAsync($"/api/helpdesk/report?from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}"));
        Assert.Equal(2, report.GetProperty("total").GetProperty("raised").GetInt32());
        Assert.Equal(1, report.GetProperty("total").GetProperty("resolved").GetInt32());
        Assert.Equal(100, report.GetProperty("total").GetProperty("slaMetPercent").GetInt32());
        Assert.Equal(2, report.GetProperty("categories").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/helpdesk/report?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}")).StatusCode);
        var xlsx = await agentClient.GetAsync($"/api/helpdesk/export?from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.True((await xlsx.Content.ReadAsByteArrayAsync()).Length > 1000);
    }
}
