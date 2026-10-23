using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Manpower budget: plan from current staff, approval by the company admin, and the check used by hiring requests.</summary>
public class BudgetTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    [Fact]
    public async Task A_budget_is_planned_from_staff_approved_and_checked_by_hiring()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Budget Co");
        var dept = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Sales" }))).GetProperty("id").GetGuid();
        var desig = (await Ok(await admin.PostAsJsonAsync("/api/designations", new { title = "Sales officer" }))).GetProperty("id").GetGuid();
        for (var i = 0; i < 2; i++)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = $"S{i}", lastName = "A", joiningDate = "2024-01-01", status = "Active", departmentId = dept, designationId = desig }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 100000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json));
        }

        // No approved budget: hiring is not limited.
        Assert.True((await Ok(await admin.GetAsync($"/api/budget/check?departmentId={dept}&designationId={desig}"))).GetProperty("inBudget").GetBoolean());

        var plan = await Ok(await admin.PostAsJsonAsync("/api/budget/plans?fromStaff=true", new { name = "FY", fromDate = Iso(Today.AddDays(-30)), toDate = Iso(Today.AddDays(334)) }, ApiFactory.Json));
        var id2 = plan.GetProperty("id").GetGuid();
        var detail = await Ok(await admin.GetAsync($"/api/budget/plans/{id2}"));
        var sales = detail.GetProperty("lines").EnumerateArray().First(l => l.GetProperty("departmentId").ValueKind != JsonValueKind.Null && l.GetProperty("departmentId").GetGuid() == dept);
        Assert.Equal(2, sales.GetProperty("headcount").GetInt32());
        Assert.Equal(100000m, sales.GetProperty("monthlyCost").GetDecimal());

        // Plan 3 sales officers instead of 2, keep the other lines.
        var lines = detail.GetProperty("lines").EnumerateArray().Select(l => new
        {
            departmentId = l.GetProperty("departmentId").ValueKind == JsonValueKind.Null ? (Guid?)null : l.GetProperty("departmentId").GetGuid(),
            designationId = l.GetProperty("designationId").ValueKind == JsonValueKind.Null ? (Guid?)null : l.GetProperty("designationId").GetGuid(),
            headcount = l.GetProperty("designationId").ValueKind != JsonValueKind.Null && l.GetProperty("designationId").GetGuid() == desig ? 3 : l.GetProperty("headcount").GetInt32(),
            monthlyCost = l.GetProperty("monthlyCost").GetDecimal(),
        }).ToList();
        await Ok(await admin.PutAsJsonAsync($"/api/budget/plans/{id2}/positions", new { positions = lines }, ApiFactory.Json));
        await Ok(await admin.PostAsync($"/api/budget/plans/{id2}/submit", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/budget/plans/{id2}/positions", new { positions = lines }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/budget/plans/{id2}/decide", new { approve = false }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/budget/plans/{id2}/decide", new { approve = true }, ApiFactory.Json));

        var check = await Ok(await admin.GetAsync($"/api/budget/check?departmentId={dept}&designationId={desig}"));
        Assert.True(check.GetProperty("inBudget").GetBoolean());
        Assert.Equal(1, check.GetProperty("open").GetInt32());
        var other = await Ok(await admin.GetAsync($"/api/budget/check?departmentId={dept}"));
        Assert.False(other.GetProperty("inBudget").GetBoolean());

        detail = await Ok(await admin.GetAsync($"/api/budget/plans/{id2}"));
        sales = detail.GetProperty("lines").EnumerateArray().First(l => l.GetProperty("designationId").ValueKind != JsonValueKind.Null && l.GetProperty("designationId").GetGuid() == desig);
        Assert.Equal(1, sales.GetProperty("vacant").GetInt32());
        Assert.True(sales.GetProperty("variance").GetDecimal() > 0);
    }
}
