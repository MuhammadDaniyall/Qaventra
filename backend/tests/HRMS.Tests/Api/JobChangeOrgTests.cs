using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>A job change can move grade, division and cost center too, and the history keeps the old and new names.</summary>
public class JobChangeOrgTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Promotion_changes_grade_and_division()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("GradeMove");
        async Task<Guid> Lookup(string kind, string name) =>
            (await (await admin.PostAsJsonAsync("/api/lookups", new { kind, name }, ApiFactory.Json)).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var g1 = await Lookup("Grade", "G1");
        var g2 = await Lookup("Grade", "G2");
        var corp = await Lookup("Division", "Corporate");
        var id = (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = "Tariq", lastName = "T", joiningDate = "2023-01-01", status = "Active" }, ApiFactory.Json))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/employees/{id}/file", new { gradeId = g1 }, ApiFactory.Json)).EnsureSuccessStatusCode();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/employees/{id}/job-changes",
            new { kind = "Promotion", effectiveDate = today, gradeId = corp }, ApiFactory.Json)).StatusCode);
        var res = await admin.PostAsJsonAsync($"/api/employees/{id}/job-changes", new { kind = "Promotion", effectiveDate = today, gradeId = g2, divisionId = corp }, ApiFactory.Json);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var file = await (await admin.GetAsync($"/api/employees/{id}/file")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        Assert.Equal(g2, file.GetProperty("gradeId").GetGuid());
        Assert.Equal(corp, file.GetProperty("divisionId").GetGuid());
        var history = await (await admin.GetAsync($"/api/employees/{id}/job-history")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        var change = history.EnumerateArray().First(h => h.GetProperty("kind").GetString() == "Promotion");
        Assert.Equal("G1", change.GetProperty("fromGrade").GetString());
        Assert.Equal("G2", change.GetProperty("toGrade").GetString());
        Assert.Equal("Corporate", change.GetProperty("toDivision").GetString());
    }
}
