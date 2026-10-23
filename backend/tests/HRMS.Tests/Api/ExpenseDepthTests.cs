using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Expense types with rates, distances and limits; advances set against claims and closed.</summary>
public class ExpenseDepthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static string Yesterday => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-1).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Types_distances_limits_and_advances()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("ExpDepth");
        var grade = (await Ok(await admin.PostAsJsonAsync("/api/lookups", new { kind = "Grade", name = "M1" }, ApiFactory.Json))).GetProperty("id").GetGuid();
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Ema", lastName = "X", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/file", new { gradeId = grade, allowWebCheckIn = true, allowMobileCheckIn = true }, ApiFactory.Json));
        var email = $"ema+{Guid.NewGuid():N}@exp.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Exp-Pass-1234", role = "Employee" }));
        var ema = await factory.SignInAsync(email, "Exp-Pass-1234");

        await Ok(await admin.PostAsJsonAsync("/api/expense-setup/routes", new { fromCity = "Lahore", toCity = "Islamabad", km = 380 }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/expense-setup/types", new
        {
            name = "Mileage", category = "Fuel", calculation = "Distance", rate = 30, yearlyLimit = 30000, receiptRequired = false, isActive = true,
            gradeRates = new[] { new { gradeId = grade, rate = 40, yearlyLimit = 40000 } },
        }, ApiFactory.Json));
        var mileage = (await Ok(await ema.GetAsync("/api/expense-setup/types/for"))).EnumerateArray().Single();
        Assert.Equal(40m, mileage.GetProperty("rate").GetDecimal());
        var typeId = mileage.GetProperty("id").GetGuid();

        // Distance is looked up both ways; 380 km × 40 = 15,200.
        object Item(string from, string to) => new { date = Yesterday, category = "Other", description = "Client visit", amount = 0, expenseTypeId = typeId, fromCity = from, toCity = to };
        var claim = await Ok(await ema.PostAsJsonAsync("/api/expenses", new { title = "Trip", items = new[] { Item("Islamabad", "Lahore") } }, ApiFactory.Json));
        Assert.Equal(15200m, claim.GetProperty("total").GetDecimal());
        // A second and third trip would pass 40,000 for the year.
        await Ok(await ema.PostAsJsonAsync("/api/expenses", new { title = "Trip 2", items = new[] { Item("Lahore", "Islamabad") } }, ApiFactory.Json));
        var over = await ema.PostAsJsonAsync("/api/expenses", new { title = "Trip 3", items = new[] { Item("Lahore", "Islamabad") } }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains("a year", await over.Content.ReadAsStringAsync());

        // Advance of 20,000; a 15,200 claim against it pays nothing more; closing recovers 4,800.
        await Ok(await ema.PostAsJsonAsync("/api/expense-setup/advances", new { purpose = "Karachi trip", amount = 20000 }, ApiFactory.Json));
        var adv = (await Ok(await admin.GetAsync("/api/expense-setup/advances?status=Pending"))).EnumerateArray().Single().GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync($"/api/approvals/ExpenseAdvance/{adv}/decide", new { approve = true }, ApiFactory.Json));
        var claim2 = await Ok(await ema.PostAsJsonAsync("/api/expenses", new
        {
            title = "Karachi", advanceId = adv, items = new[] { new { date = Yesterday, category = "Travel", description = "Hotel", amount = 15200 } },
        }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync($"/api/approvals/Expense/{claim2.GetProperty("id").GetGuid()}/decide", new { approve = true }, ApiFactory.Json));
        var open = (await Ok(await ema.GetAsync("/api/expense-setup/advances/open"))).EnumerateArray().Single();
        Assert.Equal(4800m, open.GetProperty("outstanding").GetDecimal());
        await Ok(await admin.PostAsync($"/api/expense-setup/advances/{adv}/close", null));
        var pay = await Ok(await admin.GetAsync($"/api/pay/requests?employeeId={id}"));
        Assert.Contains(pay.EnumerateArray(), r => r.GetProperty("title").GetString() == "Unspent expense advance" && r.GetProperty("amount").GetDecimal() == 4800m);
        Assert.Contains(pay.EnumerateArray(), r => r.GetProperty("title").GetString() == "Expense advance" && r.GetProperty("amount").GetDecimal() == 20000m);
    }
}
