using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>A ticket type can send every ticket to one agent and involve fixed people.</summary>
public class TicketTypeLeadTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Type_lead_gets_the_ticket_and_followers_are_involved()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("TypeLead");
        async Task<Guid> Person(string first) =>
            (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "H", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json))
                .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var agent = await Person("Asim");
        var lead = await Person("Laiba");
        var follower = await Person("Fahad");
        var worker = await Person("Wasim");

        (await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "IT", targetHours = 8, agentIds = new[] { agent } })).EnsureSuccessStatusCode();
        var category = (await (await admin.GetAsync("/api/helpdesk/categories")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json))[0].GetProperty("id").GetGuid();
        var saved = await admin.PostAsJsonAsync($"/api/helpdesk/categories/{category}/types",
            new { name = "VPN", agentIds = Array.Empty<Guid>(), isActive = true, leadEmployeeId = lead, involveIds = new[] { follower } }, ApiFactory.Json);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
        var type = (await (await admin.GetAsync("/api/helpdesk/categories")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json))[0]
            .GetProperty("types")[0];
        Assert.Equal(lead, type.GetProperty("leadEmployeeId").GetGuid());

        var created = await admin.PostAsJsonAsync("/api/helpdesk/tickets", new
        {
            forEmployeeId = worker, categoryId = category, typeId = type.GetProperty("id").GetGuid(), subject = "VPN down", description = "Cannot connect",
        }, ApiFactory.Json);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var detail = await (await admin.GetAsync($"/api/helpdesk/tickets/{id}")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        Assert.Equal(lead, detail.GetProperty("ticket").GetProperty("assigneeEmployeeId").GetGuid());
        Assert.Contains(follower, detail.GetProperty("involvedIds").EnumerateArray().Select(x => x.GetGuid()));
    }
}
