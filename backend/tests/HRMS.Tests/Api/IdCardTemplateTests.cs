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

    [Fact]
    public async Task Designations_keep_a_code()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("DesigCode");
        var d = await (await admin.PostAsJsonAsync("/api/designations", new { title = "Software Engineer", code = "SE-2" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SE-2", d.GetProperty("code").GetString());
        (await admin.PutAsJsonAsync($"/api/designations/{d.GetProperty("id").GetGuid()}", new { title = "Senior Engineer" })).EnsureSuccessStatusCode();
        var list = await (await admin.GetAsync("/api/designations")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SE-2", list[0].GetProperty("code").GetString());
    }
}
