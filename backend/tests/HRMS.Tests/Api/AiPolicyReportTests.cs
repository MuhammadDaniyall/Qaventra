using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Policy Q&A answers only from company text (or says ask HR), and plain-words reports are HR-only and whitelisted. The AI is off here.</summary>
public class AiPolicyReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    async Task<(HttpClient Admin, HttpClient Employee)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        var id = (await Json(await admin.PostAsJsonAsync("/api/employees",
            new { firstName = "Sana", lastName = "Iqbal", joiningDate = "2024-03-15", status = "Probation" }, ApiFactory.Json))).GetProperty("id").GetGuid();
        var email = $"sana+{Guid.NewGuid():N}@{name.ToLowerInvariant()}.test";
        (await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Pol-Pass-1234", role = "Employee" })).EnsureSuccessStatusCode();
        return (admin, await factory.SignInAsync(email, "Pol-Pass-1234"));
    }

    [Fact]
    public async Task Policy_answers_come_only_from_company_text_with_the_source_or_say_ask_hr()
    {
        var (admin, emp) = await CompanyAsync("AiPolicy");
        (await admin.PostAsJsonAsync("/api/announcements", new
        {
            title = "Work from home policy", body = "Staff may work from home two days a week with their manager's approval.", kind = "Policy",
            publishFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2).ToString("yyyy-MM-dd"), isPinned = false, notify = false,
        }, ApiFactory.Json)).EnsureSuccessStatusCode();

        var hit = await Json(await emp.PostAsJsonAsync("/api/ai/policy", new { question = "Work from home kitne din allowed hai?" }, ApiFactory.Json));
        Assert.True(hit.GetProperty("found").GetBoolean());
        Assert.False(hit.GetProperty("usedAi").GetBoolean());
        Assert.Contains("two days a week", hit.GetProperty("answer").GetString());
        Assert.Contains(hit.GetProperty("sources").EnumerateArray(), s => s.GetProperty("title").GetString() == "Work from home policy");

        var miss = await Json(await emp.PostAsJsonAsync("/api/ai/policy", new { question = "Pension scheme for pets?" }, ApiFactory.Json));
        Assert.False(miss.GetProperty("found").GetBoolean());
        Assert.Contains("ask HR", miss.GetProperty("answer").GetString());
        Assert.Empty(miss.GetProperty("sources").EnumerateArray());

        Assert.Equal(HttpStatusCode.BadRequest, (await emp.PostAsJsonAsync("/api/ai/policy", new { question = "" }, ApiFactory.Json)).StatusCode);
    }

    [Fact]
    public async Task Reports_are_hr_only_and_pick_from_the_whitelist()
    {
        var (admin, emp) = await CompanyAsync("AiReport");
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync("/api/ai/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.PostAsJsonAsync("/api/ai/report", new { question = "headcount" }, ApiFactory.Json)).StatusCode);

        var catalogue = (await Json(await admin.GetAsync("/api/ai/reports"))).EnumerateArray().Select(r => r.GetProperty("key").GetString()).ToList();
        Assert.Contains("late_arrivals", catalogue);
        Assert.Contains("expiring_documents", catalogue);

        var head = await Json(await admin.PostAsJsonAsync("/api/ai/report", new { question = "Kitne log hain, headcount by department" }, ApiFactory.Json));
        Assert.Equal("headcount", head.GetProperty("report").GetString());
        Assert.False(head.GetProperty("usedAi").GetBoolean());
        Assert.NotEmpty(head.GetProperty("rows").EnumerateArray());

        var late = await Json(await admin.PostAsJsonAsync("/api/ai/report", new { question = "Lahore ke kitne log is mahine late aaye" }, ApiFactory.Json));
        Assert.Equal("late_arrivals", late.GetProperty("report").GetString());

        var joiners = await Json(await admin.PostAsJsonAsync("/api/ai/report", new { report = "joiners", from = "2024-03-01", to = "2024-03-31" }, ApiFactory.Json));
        Assert.Contains(joiners.GetProperty("rows").EnumerateArray(), r => r.EnumerateArray().Any(c => c.GetString() == "Sana Iqbal"));

        // Every whitelisted report runs on the database with all filters set.
        foreach (var key in catalogue)
            await Json(await admin.PostAsJsonAsync("/api/ai/report", new { report = key, department = "Sales", location = "Lahore", status = "Active", from = "2024-01-01", to = "2024-12-31" }, ApiFactory.Json));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/ai/report", new { report = "drop_table" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/ai/report", new { question = "xyzzy plugh" }, ApiFactory.Json)).StatusCode);
    }
}
