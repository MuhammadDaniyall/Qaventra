using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>The ID card design is kept per company on the server, so every HR user prints the same card.</summary>
public class IdCardTemplateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Design_is_saved_per_company()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("IdCard");
        var empty = await (await admin.GetAsync("/api/employees/id-card-template")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, empty.ValueKind);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/employees/id-card-template", new { color = "#112233", portrait = true, printBack = true })).StatusCode);
        var saved = await (await admin.GetAsync("/api/employees/id-card-template")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("#112233", saved.GetProperty("color").GetString());
        Assert.True(saved.GetProperty("portrait").GetBoolean());

        var (other, _, _) = await factory.CreateCompanyAsync("IdCard2");
        var theirs = await (await other.GetAsync("/api/employees/id-card-template")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(theirs.TryGetProperty("color", out _));
    }
}
