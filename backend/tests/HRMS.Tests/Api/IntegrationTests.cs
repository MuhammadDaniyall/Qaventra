using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class IntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        var text = await res.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement;
    }

    private HttpClient WithKey(string key)
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Api-Key", key);
        return c;
    }

    [Fact]
    public async Task A_key_reads_its_own_company_until_it_is_revoked()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Key Co");
        var (other, _, _) = await factory.CreateCompanyAsync("Other Key Co");
        await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Kamran", lastName = "API", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json));
        await Ok(await other.PostAsJsonAsync("/api/employees", new { firstName = "Hidden", lastName = "Elsewhere", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json));

        var created = await Ok(await admin.PostAsJsonAsync("/api/setup/api-tokens", new { name = "Accounting" }, ApiFactory.Json));
        var key = created.GetProperty("key").GetString()!;
        var id = created.GetProperty("token").GetProperty("id").GetGuid();

        var people = await Ok(await WithKey(key).GetAsync("/api/integration/employees"));
        Assert.Contains(people.EnumerateArray(), p => p.GetProperty("firstName").GetString() == "Kamran");
        Assert.DoesNotContain(people.EnumerateArray(), p => p.GetProperty("firstName").GetString() == "Hidden");
        Assert.Equal(HttpStatusCode.BadRequest, (await WithKey(key).GetAsync("/api/integration/attendance?from=2026-01-01&to=2026-12-31")).StatusCode);
        Assert.True((await WithKey(key).GetAsync("/api/integration/attendance?from=2026-01-01&to=2026-01-31")).IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey("qvk_wrong").GetAsync("/api/integration/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/integration/employees")).StatusCode);

        await Ok(await admin.PostAsync($"/api/setup/api-tokens/{id}/revoke", null));
        Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey(key).GetAsync("/api/integration/employees")).StatusCode);
        var list = await Ok(await admin.GetAsync("/api/setup/api-tokens"));
        Assert.NotEqual(JsonValueKind.Null, list.EnumerateArray().Single().GetProperty("revokedAtUtc").ValueKind);
    }
}
