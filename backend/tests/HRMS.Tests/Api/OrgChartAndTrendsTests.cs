using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class OrgChartAndTrendsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Read(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static async Task<Guid> CreateEmployee(HttpClient client, string first, Guid? managerId = null,
        string status = "Active", string joiningDate = "2026-01-01", string? leavingDate = null)
    {
        var e = await Read(await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = first,
            lastName = "Test",
            joiningDate,
            status,
            leavingDate,
            managerId,
        }, ApiFactory.Json));
        return e.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Org_chart_lists_current_employees_with_their_manager()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Org Chart Co");
        var boss = await CreateEmployee(admin, "Bushra");
        var report = await CreateEmployee(admin, "Rehan", boss);
        await CreateEmployee(admin, "Gone", boss, status: "Resigned", leavingDate: "2026-02-01");

        var nodes = (await Read(await admin.GetAsync("/api/employees/org-chart"))).EnumerateArray().ToList();

        // The company admin's own employee record is on the chart too; people who left are not.
        Assert.DoesNotContain(nodes, n => n.GetProperty("name").GetString()!.StartsWith("Gone"));
        var rehan = nodes.Single(n => n.GetProperty("id").GetGuid() == report);
        Assert.Equal(boss, rehan.GetProperty("managerId").GetGuid());
        Assert.Equal(JsonValueKind.Null, nodes.Single(n => n.GetProperty("id").GetGuid() == boss).GetProperty("managerId").ValueKind);
    }

    [Fact]
    public async Task Trends_cover_twelve_months_and_count_joiners_and_leavers()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Trends Co");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thisMonth = new DateOnly(today.Year, today.Month, 1).ToString("yyyy-MM-dd");
        // The company admin's own employee record already counts, so compare against the starting numbers.
        var before = (await Read(await admin.GetAsync("/api/dashboard/trends"))).EnumerateArray().Last();
        await CreateEmployee(admin, "Old", joiningDate: "2020-01-01");
        await CreateEmployee(admin, "New", joiningDate: thisMonth);

        var months = (await Read(await admin.GetAsync("/api/dashboard/trends"))).EnumerateArray().ToList();

        Assert.Equal(12, months.Count);
        var current = months[^1];
        Assert.Equal(today.Year, current.GetProperty("year").GetInt32());
        Assert.Equal(today.Month, current.GetProperty("month").GetInt32());
        Assert.Equal(before.GetProperty("joiners").GetInt32() + 1, current.GetProperty("joiners").GetInt32());
        Assert.Equal(before.GetProperty("headcount").GetInt32() + 2, current.GetProperty("headcount").GetInt32());
    }

    [Fact]
    public async Task Trends_are_for_hr_only()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Trends Access Co");
        var res = await factory.CreateClient().GetAsync("/api/dashboard/trends");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.True((await admin.GetAsync("/api/dashboard/trends")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_rehire_keeps_the_earlier_tenure_in_past_months()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Trends Rehire Co");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        var left = new DateOnly(today.Year, today.Month, 1).AddMonths(-3).AddDays(9);
        var id = await CreateEmployee(admin, "Returning", status: "Resigned", joiningDate: "2020-01-01", leavingDate: left.ToString("yyyy-MM-dd"));
        var before = (await Read(await admin.GetAsync("/api/dashboard/trends"))).EnumerateArray().ToList();

        await Read(await admin.PostAsJsonAsync($"/api/employees/{id}/rehire", new { joiningDate = today.ToString("yyyy-MM-dd"), status = "Active" }, ApiFactory.Json));
        var after = (await Read(await admin.GetAsync("/api/dashboard/trends"))).EnumerateArray().ToList();

        // Earlier months read the same (the old tenure and its leaving still count); this month gains a joiner.
        int N(JsonElement m, string p) => m.GetProperty(p).GetInt32();
        for (var i = 0; i < 11; i++)
            foreach (var p in new[] { "headcount", "joiners", "leavers" })
                Assert.Equal(N(before[i], p), N(after[i], p));
        Assert.Equal(N(before[11], "joiners") + 1, N(after[11], "joiners"));
        Assert.Equal(N(before[11], "headcount") + 1, N(after[11], "headcount"));
    }
}
