using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>The approver allows less on a line; the claim total and what gets paid follow.</summary>
public class ExpenseApprovedAmountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Approver_cuts_one_line()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("ExpCut");
        var emp = (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = "Kashif", lastName = "C", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-2).ToString("yyyy-MM-dd");
        var claim = await (await admin.PostAsJsonAsync("/api/expenses", new
        {
            title = "Client visit", employeeId = emp,
            items = new[]
            {
                new { date = day, category = "Travel", description = "Taxi", amount = 3000 },
                new { date = day, category = "Meals", description = "Dinner", amount = 5000 },
            },
        }, ApiFactory.Json)).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        var id = claim.GetProperty("id").GetGuid();
        var detail = await (await admin.GetAsync($"/api/expenses/{id}")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        var dinner = detail.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("description").GetString() == "Dinner").GetProperty("id").GetGuid();

        var cut = await admin.PutAsJsonAsync($"/api/expenses/{id}/approved-amounts", new { lines = new[] { new { itemId = dinner, approvedAmount = 2500 } } }, ApiFactory.Json);
        Assert.True(cut.IsSuccessStatusCode, await cut.Content.ReadAsStringAsync());
        var after = await (await admin.GetAsync($"/api/expenses/{id}")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        Assert.Equal(5500m, after.GetProperty("claim").GetProperty("total").GetDecimal());
        Assert.Equal(2500m, after.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == dinner).GetProperty("approvedAmount").GetDecimal());

        var decided = await admin.PostAsJsonAsync($"/api/approvals/Expense/{id}/decide", new { approve = true, force = true, note = "OK" }, ApiFactory.Json);
        Assert.True(decided.IsSuccessStatusCode, await decided.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync($"/api/expenses/{id}/approved-amounts", new { lines = new[] { new { itemId = dinner, approvedAmount = 1 } } }, ApiFactory.Json)).StatusCode);
    }
}
