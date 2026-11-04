using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Expense quotas: pro-rata for joiners, a monthly cap, a type only for women, and taxable types paid as a taxable earning.</summary>
public class ExpenseQuotaTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Quotas_prorate_cap_monthly_and_follow_gender()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("ExpQuota");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        var joined = new DateOnly(today.Year, today.Month, 1).AddMonths(-2); // two months before this one: same year unless it is Jan/Feb
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Qasim", lastName = "Q", gender = "Male", joiningDate = joined.ToString("yyyy-MM-dd"), status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var email = $"qasim+{Guid.NewGuid():N}@quota.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Quota-Pass-1234", role = "Employee" }));
        var me = await factory.SignInAsync(email, "Quota-Pass-1234");

        await Ok(await admin.PostAsJsonAsync("/api/expense-setup/types", new
        {
            name = "Fuel", category = "Fuel", calculation = "Amount", yearlyLimit = 120000, monthlyLimit = 4000, proRata = true, carryForward = true, receiptRequired = false, isActive = true,
        }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/expense-setup/types", new
        {
            name = "Maternity", category = "Medical", calculation = "Amount", forGender = "Female", receiptRequired = false, isActive = true,
        }, ApiFactory.Json));

        var types = (await Ok(await me.GetAsync("/api/expense-setup/types/for"))).EnumerateArray().ToList();
        var fuel = Assert.Single(types);
        Assert.Equal("Fuel", fuel.GetProperty("name").GetString());
        var months = joined.Year == today.Year ? 13 - joined.Month : 12;
        Assert.Equal(120000m * months / 12, fuel.GetProperty("yearlyLimit").GetDecimal());
        Assert.Equal(4000m, fuel.GetProperty("monthlyLimit").GetDecimal());

        var fuelId = fuel.GetProperty("id").GetGuid();
        object Claim(decimal amount) => new { title = "Fuel", items = new[] { new { date = today.ToString("yyyy-MM-dd"), category = "Fuel", description = "Fuel", amount, expenseTypeId = fuelId } } };
        await Ok(await me.PostAsJsonAsync("/api/expenses", Claim(3000), ApiFactory.Json));
        var over = await me.PostAsJsonAsync("/api/expenses", Claim(1500), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains("a month", await over.Content.ReadAsStringAsync());
        Assert.Equal(3000m, (await Ok(await me.GetAsync("/api/expense-setup/types/for")))[0].GetProperty("usedThisMonth").GetDecimal());

        var maternity = (await Ok(await admin.GetAsync("/api/expense-setup/types"))).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Maternity").GetProperty("id").GetGuid();
        var wrong = await me.PostAsJsonAsync("/api/expenses", new { title = "M", items = new[] { new { date = today.ToString("yyyy-MM-dd"), category = "Medical", description = "x", amount = 100, expenseTypeId = maternity } } }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }

    [Fact]
    public async Task Taxable_types_are_paid_as_a_taxable_earning()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("ExpTax");
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Tariq", lastName = "T", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 100000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json))
            .EnsureSuccessStatusCode();
        await Ok(await admin.PostAsJsonAsync("/api/expense-setup/types", new { name = "Entertainment", category = "Meals", calculation = "Amount", taxable = true, receiptRequired = false, isActive = true }, ApiFactory.Json));
        var type = (await Ok(await admin.GetAsync("/api/expense-setup/types")))[0];
        Assert.True(type.GetProperty("taxable").GetBoolean());
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        var claim = await Ok(await admin.PostAsJsonAsync("/api/expenses", new
        {
            employeeId = id, title = "Client dinner",
            items = new object[]
            {
                new { date = today.ToString("yyyy-MM-dd"), category = "Meals", description = "Dinner", amount = 5000, expenseTypeId = type.GetProperty("id").GetGuid() },
                new { date = today.ToString("yyyy-MM-dd"), category = "Travel", description = "Taxi", amount = 1000 },
            },
        }, ApiFactory.Json));
        var claimId = claim.GetProperty("id").GetGuid();
        if (claim.GetProperty("status").GetString() == "Pending")
            await Ok(await admin.PostAsJsonAsync($"/api/approvals/Expense/{claimId}/decide", new { approve = true }, ApiFactory.Json));

        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = today.Year, month = today.Month }, ApiFactory.Json));
        var slip = run.GetProperty("payslips").EnumerateArray().Single(p => p.GetProperty("employeeId").GetGuid() == id);
        var slipId = slip.GetProperty("id").GetGuid();
        var detail = await Ok(await admin.GetAsync($"/api/payroll/payslips/{slipId}"));
        var lines = detail.GetProperty("lines").EnumerateArray().Select(l => (l.GetProperty("name").GetString(), l.GetProperty("amount").GetDecimal())).ToList();
        Assert.Contains(("Taxable expenses", 5000m), lines);
        // The taxable part is in taxable income; the taxi stays a tax-free reimbursement.
        Assert.Equal(106000m, detail.GetProperty("grossEarnings").GetDecimal());
        Assert.Equal(105000m, detail.GetProperty("taxableIncome").GetDecimal());
    }
}
