using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Help desk tickets from raising to closing, and who may see and act on them.</summary>
public class HelpdeskTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "H", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Desk-Pass-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Desk-Pass-1234"));
        }
        return (admin, Person);
    }

    private static async Task<JsonElement> Detail(HttpClient c, Guid id) => await Ok(await c.GetAsync($"/api/helpdesk/tickets/{id}"));

    private static string Status(JsonElement detail) => detail.GetProperty("ticket").GetProperty("status").GetString()!;

    [Fact]
    public async Task A_ticket_goes_from_raised_to_rated()
    {
        var (admin, person) = await CompanyAsync("Desk");
        var (agent, agentClient) = await person("Ian");
        var (worker, workerClient) = await person("Wali");
        var (_, other) = await person("Sara");
        await Ok(await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "IT support", targetHours = 8, agentIds = new[] { agent } }));
        var category = Assert.Single((await Ok(await workerClient.GetAsync("/api/helpdesk/categories"))).EnumerateArray()).GetProperty("id").GetGuid();
        Assert.True((await Ok(await agentClient.GetAsync("/api/helpdesk/me"))).GetProperty("isAgent").GetBoolean());
        Assert.False((await Ok(await workerClient.GetAsync("/api/helpdesk/me"))).GetProperty("isAgent").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.PostAsJsonAsync("/api/helpdesk/tickets", new { forEmployeeId = agent, categoryId = category, subject = "x", description = "y" }, ApiFactory.Json)).StatusCode);
        var ticket = await Ok(await workerClient.PostAsJsonAsync("/api/helpdesk/tickets", new { categoryId = category, subject = "Laptop will not charge", description = "Charger light is off.", priority = "High" }, ApiFactory.Json));
        var id = ticket.GetProperty("id").GetGuid();
        Assert.Equal("HD-0001", ticket.GetProperty("code").GetString());
        Assert.Contains("Laptop will not charge", (await Ok(await agentClient.GetAsync("/api/notifications"))).ToString());

        // Others cannot see it; the agent's queue has it.
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/helpdesk/tickets/{id}")).StatusCode);
        Assert.Single((await Ok(await agentClient.GetAsync("/api/helpdesk/tickets?scope=unassigned"))).EnumerateArray());
        Assert.Empty((await Ok(await other.GetAsync("/api/helpdesk/tickets"))).EnumerateArray());

        // Agent takes it and replies; an internal note stays hidden from the employee.
        Assert.Equal(HttpStatusCode.BadRequest, (await agentClient.PutAsJsonAsync($"/api/helpdesk/tickets/{id}", new { assigneeEmployeeId = worker, priority = "High", categoryId = category }, ApiFactory.Json)).StatusCode);
        await Ok(await agentClient.PutAsJsonAsync($"/api/helpdesk/tickets/{id}", new { assigneeEmployeeId = agent, priority = "Urgent", categoryId = category }, ApiFactory.Json));
        await Ok(await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/comments", new { body = "Bring it to the IT room at 3pm." }));
        await Ok(await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/comments", new { body = "Probably the adapter, spare in cabinet B.", isInternal = true }));
        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/comments", new { body = "sneaky", isInternal = true })).StatusCode);
        var seen = await Detail(workerClient, id);
        Assert.Equal("InProgress", Status(seen));
        Assert.DoesNotContain(seen.GetProperty("comments").EnumerateArray(), c => c.GetProperty("isInternal").GetBoolean());
        Assert.Contains((await Detail(agentClient, id)).GetProperty("comments").EnumerateArray(), c => c.GetProperty("isInternal").GetBoolean());
        Assert.False(seen.GetProperty("isAgent").GetBoolean());

        // Waiting on the employee; their reply puts it back in progress.
        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/status", new { status = "Resolved" }, ApiFactory.Json)).StatusCode);
        await Ok(await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/status", new { status = "WaitingOnEmployee", note = "Which model is it?" }, ApiFactory.Json));
        await Ok(await workerClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/comments", new { body = "Dell Latitude 5420" }));
        Assert.Equal("InProgress", Status(await Detail(agentClient, id)));

        // Resolved, then the employee closes it with a rating; closed tickets take no more replies.
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/close", new { rating = 5 })).StatusCode);
        await Ok(await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/status", new { status = "Resolved", note = "Adapter replaced" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.Forbidden, (await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/close", new { rating = 5 })).StatusCode);
        await Ok(await workerClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/close", new { rating = 5, feedback = "Quick, thanks" }));
        var closed = await Detail(workerClient, id);
        Assert.Equal("Closed", Status(closed));
        Assert.Equal(5, closed.GetProperty("ticket").GetProperty("rating").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync($"/api/helpdesk/tickets/{id}/comments", new { body = "one more" })).StatusCode);
    }

    [Fact]
    public async Task Reopen_involve_cancel_and_queue_counts()
    {
        var (admin, person) = await CompanyAsync("Desk2");
        var (agent, agentClient) = await person("Ian");
        var (_, workerClient) = await person("Wali");
        var (helper, helperClient) = await person("Hina");
        await Ok(await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "Facilities", targetHours = 24, agentIds = new[] { agent } }));
        var category = (await Ok(await admin.GetAsync("/api/helpdesk/categories")))[0].GetProperty("id").GetGuid();

        var a = (await Ok(await workerClient.PostAsJsonAsync("/api/helpdesk/tickets", new { categoryId = category, subject = "AC not cooling", description = "Room 2" }, ApiFactory.Json))).GetProperty("id").GetGuid();
        var b = (await Ok(await workerClient.PostAsJsonAsync("/api/helpdesk/tickets", new { categoryId = category, subject = "Chair broken", description = "Desk 14" }, ApiFactory.Json))).GetProperty("id").GetGuid();

        // Involve a colleague: they can read and reply.
        Assert.Equal(HttpStatusCode.NotFound, (await helperClient.GetAsync($"/api/helpdesk/tickets/{a}")).StatusCode);
        await Ok(await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{a}/involve", new { employeeIds = new[] { helper } }));
        await Ok(await helperClient.PostAsJsonAsync($"/api/helpdesk/tickets/{a}/comments", new { body = "Vendor visits Thursday." }));
        Assert.Single((await Ok(await helperClient.GetAsync("/api/helpdesk/tickets/my"))).EnumerateArray());

        // Resolve, reopen with a reason.
        await Ok(await agentClient.PostAsJsonAsync($"/api/helpdesk/tickets/{a}/status", new { status = "Resolved" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync($"/api/helpdesk/tickets/{a}/reopen", new { reason = " " })).StatusCode);
        await Ok(await workerClient.PostAsJsonAsync($"/api/helpdesk/tickets/{a}/reopen", new { reason = "Still warm in the afternoon" }));
        Assert.Equal("InProgress", Status(await Detail(workerClient, a)));

        // Cancel the other; the queue and summary count only what is still open.
        await Ok(await workerClient.PostAsync($"/api/helpdesk/tickets/{b}/cancel", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsync($"/api/helpdesk/tickets/{b}/cancel", null)).StatusCode);
        Assert.Single((await Ok(await agentClient.GetAsync("/api/helpdesk/tickets"))).EnumerateArray());
        Assert.Equal(2, (await Ok(await agentClient.GetAsync("/api/helpdesk/tickets?status=any"))).GetArrayLength());
        var summary = await Ok(await agentClient.GetAsync("/api/helpdesk/summary"));
        Assert.Equal(1, summary.GetProperty("inProgress").GetInt32());
        Assert.Equal(1, summary.GetProperty("unassigned").GetInt32());
        Assert.Equal(0, summary.GetProperty("overdue").GetInt32());

        // A category with tickets cannot be deleted.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/helpdesk/categories/{category}")).StatusCode);
    }
}
