using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>An announcement can be limited to designations; others do not see it.</summary>
public class AnnouncementDesignationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Only_the_chosen_designation_sees_it()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("AnnTitle");
        async Task<Guid> Title(string t) =>
            (await (await admin.PostAsJsonAsync("/api/designations", new { title = t })).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var driver = await Title("Driver");
        var clerk = await Title("Clerk");
        async Task<HttpClient> Person(string first, Guid designation)
        {
            var id = (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "A", joiningDate = "2024-01-01", status = "Active", designationId = designation }, ApiFactory.Json))
                .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@anntitle.test";
            (await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Ann-Pass-1234", role = "Employee" })).EnsureSuccessStatusCode();
            return await factory.SignInAsync(email, "Ann-Pass-1234");
        }
        var dan = await Person("Dan", driver);
        var cal = await Person("Cal", clerk);

        var res = await admin.PostAsJsonAsync("/api/announcements", new
        {
            title = "Vehicle checks", body = "Bring the logbook", kind = "News", publishFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-1).ToString("yyyy-MM-dd"),
            isPinned = false, notify = false, designationIds = new[] { driver },
        }, ApiFactory.Json);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        static async Task<List<string?>> Titles(HttpClient c) =>
            (await (await c.GetAsync("/api/announcements")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).EnumerateArray().Select(a => a.GetProperty("title").GetString()).ToList();
        Assert.Contains("Vehicle checks", await Titles(dan));
        Assert.DoesNotContain("Vehicle checks", await Titles(cal));
    }
}
