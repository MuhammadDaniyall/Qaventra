using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Approving over the chain (HR) and handing approvals to someone else for a while.</summary>
public class DelegationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Func<string, Guid?, Task<(Guid Id, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "A", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant().Replace(" ", "")}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Deleg-Pass-1234", role = manager is null ? "Manager" : "Employee" }));
            return (id, await factory.SignInAsync(email, "Deleg-Pass-1234"));
        }
        return (admin, Person);
    }

    private static object Trip(int days) => new
    {
        purpose = "Client visit", tripType = "OneWay", isInternational = false,
        legs = new[] { new { from = "Lahore", to = "Karachi", date = Iso(Today.AddDays(days)), mode = "Air", preference = (string?)null } },
    };

    [Fact]
    public async Task A_delegate_approves_while_the_manager_is_away()
    {
        var (admin, person) = await CompanyAsync("Deleg Co");
        var (maha, mahaClient) = await person("Maha", null);
        var (deputy, deputyClient) = await person("Dawood", null);
        var (sana, sanaClient) = await person("Sana", maha);

        // Maha hands only travel approvals to Dawood for this week.
        await Ok(await mahaClient.PostAsJsonAsync("/api/approvals/delegations", new { toEmployeeId = deputy, fromDate = Iso(Today), toDate = Iso(Today.AddDays(6)), requestTypes = new[] { "Travel" }, reason = "Annual leave" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await mahaClient.PostAsJsonAsync("/api/approvals/delegations", new { toEmployeeId = deputy, fromDate = Iso(Today.AddDays(2)), toDate = Iso(Today.AddDays(3)), requestTypes = Array.Empty<string>() }, ApiFactory.Json)).StatusCode);
        // Someone who is not HR cannot set one up for their manager; it would be their own approvals, to themselves.
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync("/api/approvals/delegations", new { fromEmployeeId = maha, toEmployeeId = sana, fromDate = Iso(Today), toDate = Iso(Today) }, ApiFactory.Json)).StatusCode);

        var trip = (await Ok(await sanaClient.PostAsJsonAsync("/api/travel", Trip(10), ApiFactory.Json))).GetProperty("id").GetGuid();
        Assert.Contains("needs your approval", (await Ok(await deputyClient.GetAsync("/api/notifications"))).ToString());
        var item = Assert.Single((await Ok(await deputyClient.GetAsync("/api/approvals/inbox"))).EnumerateArray());
        Assert.Equal("Maha A", item.GetProperty("actingFor").GetString());
        Assert.True(item.GetProperty("assignedToMe").GetBoolean());

        await Ok(await deputyClient.PostAsJsonAsync($"/api/approvals/Travel/{trip}/decide", new { approve = true, note = "OK" }, ApiFactory.Json));
        var timeline = await Ok(await sanaClient.GetAsync($"/api/approvals/Travel/{trip}"));
        Assert.Equal("Approved", timeline.GetProperty("status").GetString());
        Assert.Equal("Dawood A for Maha A", timeline.GetProperty("steps")[0].GetProperty("decidedByName").GetString());

        // Leave was not delegated: still Maha's.
        Assert.Empty((await Ok(await deputyClient.GetAsync("/api/approvals/inbox"))).EnumerateArray());

        // Maha ends it early; it stops covering today.
        var d = Assert.Single((await Ok(await mahaClient.GetAsync("/api/approvals/delegations"))).EnumerateArray());
        await Ok(await mahaClient.PostAsync($"/api/approvals/delegations/{d.GetProperty("id").GetGuid()}/cancel", null));
        Assert.False((await Ok(await mahaClient.GetAsync("/api/approvals/delegations")))[0].GetProperty("isActive").GetBoolean());
        _ = admin;
    }

    [Fact]
    public async Task HR_can_approve_over_the_chain_with_a_reason()
    {
        var (admin, person) = await CompanyAsync("Force Co");
        var (maha, mahaClient) = await person("Maha", null);
        var (sana, sanaClient) = await person("Sana", maha);
        // Two steps: line manager, then HR.
        await Ok(await admin.PutAsJsonAsync("/api/approvals/flows/Travel", new { steps = new[] { new { kind = "LineManager", employeeId = (Guid?)null }, new { kind = "Hr", employeeId = (Guid?)null } } }, ApiFactory.Json));
        var trip = (await Ok(await sanaClient.PostAsJsonAsync("/api/travel", Trip(10), ApiFactory.Json))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await mahaClient.PostAsJsonAsync($"/api/approvals/Travel/{trip}/decide", new { approve = true, force = true, note = "x" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/approvals/Travel/{trip}/decide", new { approve = true, force = true }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/approvals/Travel/{trip}/decide", new { approve = true, force = true, note = "Client meeting moved up" }, ApiFactory.Json));

        var timeline = await Ok(await sanaClient.GetAsync($"/api/approvals/Travel/{trip}"));
        Assert.True(timeline.GetProperty("forced").GetBoolean());
        Assert.All(timeline.GetProperty("steps").EnumerateArray(), s => Assert.Equal("Approved", s.GetProperty("status").GetString()));
        Assert.Equal("Approved", (await Ok(await sanaClient.GetAsync($"/api/travel/{trip}"))).GetProperty("status").GetString());
        _ = sana;
    }
}
