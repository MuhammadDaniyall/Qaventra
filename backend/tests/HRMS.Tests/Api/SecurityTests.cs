using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class SecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        var text = await res.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement;
    }

    private static Task<HttpResponseMessage> Login(HttpClient c, string email, string password) =>
        c.PostAsJsonAsync("/api/auth/login", new { email, password });

    [Fact]
    public async Task Only_allowed_domains_sign_in_but_admins_always_can()
    {
        var (admin, adminEmail, _) = await factory.CreateCompanyAsync("Domain Co");
        var emp = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Gmail", lastName = "User", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var outsider = $"someone+{Guid.NewGuid():N}@gmail.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{emp}/login", new { email = outsider, password = "Domain-Pass-123", role = "Employee" }));
        var anon = factory.CreateClient();
        Assert.True((await Login(anon, outsider, "Domain-Pass-123")).IsSuccessStatusCode);

        var saved = await Ok(await admin.PutAsJsonAsync("/api/setup/security", new { allowedEmailDomains = new[] { "@DomainCo.test", " " } }, ApiFactory.Json));
        Assert.Equal("domainco.test", Assert.Single(saved.GetProperty("allowedEmailDomains").EnumerateArray()).GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/setup/security", new { allowedEmailDomains = new[] { "not a domain" } }, ApiFactory.Json)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await Login(anon, outsider, "Domain-Pass-123")).StatusCode);
        Assert.True((await Login(anon, adminEmail, "Company-Pass-123")).IsSuccessStatusCode);

        var logins = await Ok(await admin.GetAsync("/api/setup/security/last-logins"));
        var row = logins.EnumerateArray().Single(u => u.GetProperty("email").GetString() == outsider);
        Assert.NotEqual(JsonValueKind.Null, row.GetProperty("lastLoginUtc").ValueKind);
        Assert.Equal(JsonValueKind.Array, (await Ok(await admin.GetAsync("/api/setup/security/errors"))).ValueKind);
    }
}
