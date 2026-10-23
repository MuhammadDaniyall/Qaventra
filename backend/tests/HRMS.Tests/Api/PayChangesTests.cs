using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Salary revisions, recurring pay items, salary holds, leave encashment and payroll approval.</summary>
public class PayChangesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Guid A, Guid B, HttpClient AClient)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "P", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new
            {
                effectiveFrom = "2024-01-01", basicSalary = 100000, eobiApplicable = false,
                components = new object[] { new { name = "House Rent", kind = "Earning", amount = 40000, isTaxable = true } },
            }, ApiFactory.Json));
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@paychanges.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Chg-Pass-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Chg-Pass-1234"));
        }
        var (a, aClient) = await Person("Amna");
        var (b, _) = await Person("Bilal");
        return (admin, a, b, aClient);
    }

    [Fact]
    public async Task Revisions_recurring_items_and_holds_flow_into_payroll()
    {
        var (admin, a, b, aClient) = await CompanyAsync("PayChg");
        var first = new DateOnly(Today.Year, Today.Month, 1);

        // 10% on basic and allowances for Amna, +5,000 basic for Bilal.
        var body = new { effectiveFrom = Iso(first), kind = "Increment", includeAllowances = true, reason = "Annual review",
            lines = new object[] { new { employeeId = a, percent = 10m }, new { employeeId = b, amount = 5000m } } };
        var preview = (await Ok(await admin.PostAsJsonAsync("/api/pay-changes/revisions/preview", body, ApiFactory.Json))).EnumerateArray().ToList();
        var pa = preview.First(p => p.GetProperty("employeeId").GetGuid() == a);
        Assert.Equal(110000m, pa.GetProperty("newBasic").GetDecimal());
        Assert.Equal(154000m, pa.GetProperty("newGross").GetDecimal());
        Assert.Equal(145000m, preview.First(p => p.GetProperty("employeeId").GetGuid() == b).GetProperty("newGross").GetDecimal());
        await Ok(await admin.PostAsJsonAsync("/api/pay-changes/revisions", body, ApiFactory.Json));
        Assert.Equal(110000m, (await Ok(await admin.GetAsync($"/api/employees/{a}/salary"))).GetProperty("basicSalary").GetDecimal());
        Assert.Equal(2, (await Ok(await admin.GetAsync("/api/pay-changes/revisions"))).GetArrayLength());
        Assert.Contains("salary is increased", (await Ok(await aClient.GetAsync("/api/notifications"))).ToString());

        // A car allowance from this month; a canteen deduction that ended last month does not show.
        await Ok(await admin.PostAsJsonAsync("/api/pay-changes/recurring", new { employeeId = a, title = "Car allowance", isEarning = true, amount = 15000, isTaxable = true, startYear = Today.Year, startMonth = Today.Month }, ApiFactory.Json));
        var last = first.AddMonths(-1);
        await Ok(await admin.PostAsJsonAsync("/api/pay-changes/recurring", new { employeeId = a, title = "Canteen", isEarning = false, amount = 2000, isTaxable = false, startYear = last.AddMonths(-3).Year, startMonth = last.AddMonths(-3).Month, endYear = last.Year, endMonth = last.Month }, ApiFactory.Json));

        // Hold Bilal's salary this month.
        await Ok(await admin.PostAsJsonAsync("/api/pay-changes/holds", new { employeeId = b, year = Today.Year, month = Today.Month, reason = "Clearance pending" }, ApiFactory.Json));

        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = Today.Year, month = Today.Month }));
        var slipA = run.GetProperty("payslips").EnumerateArray().First(p => p.GetProperty("employeeId").GetGuid() == a);
        var lines = (await Ok(await admin.GetAsync($"/api/payroll/payslips/{slipA.GetProperty("id").GetGuid()}"))).GetProperty("lines").EnumerateArray().ToList();
        Assert.Single(lines, l => l.GetProperty("name").GetString() == "Car allowance" && l.GetProperty("amount").GetDecimal() == 15000);
        Assert.DoesNotContain(lines, l => l.GetProperty("name").GetString() == "Canteen");
        Assert.Equal("Clearance pending", run.GetProperty("payslips").EnumerateArray().First(p => p.GetProperty("employeeId").GetGuid() == b).GetProperty("heldReason").GetString());
        Assert.Equal(1, run.GetProperty("run").GetProperty("held").GetInt32());

        var bank = await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.bank", new { year = Today.Year, month = Today.Month }));
        Assert.DoesNotContain(bank.GetProperty("rows").EnumerateArray(), r => r[1].GetString() == "Bilal P");

        var hold = Assert.Single((await Ok(await admin.GetAsync("/api/pay-changes/holds"))).EnumerateArray());
        await Ok(await admin.PostAsync($"/api/pay-changes/holds/{hold.GetProperty("id").GetGuid()}/release", null));
        bank = await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.bank", new { year = Today.Year, month = Today.Month }));
        Assert.Contains(bank.GetProperty("rows").EnumerateArray(), r => r[1].GetString() == "Bilal P");
    }

    [Fact]
    public async Task Leave_is_encashed_and_payroll_needs_approval_when_required()
    {
        var (admin, a, _, _) = await CompanyAsync("Encash");
        var types = await Ok(await admin.GetAsync("/api/setup/leave-types"));
        var annual = types.EnumerateArray().First(t => t.GetProperty("isPaid").GetBoolean() && t.GetProperty("annualQuota").GetDecimal() >= 5).GetProperty("id").GetGuid();
        var quote = await Ok(await admin.GetAsync($"/api/pay-changes/encash/quote?employeeId={a}&leaveTypeId={annual}&days=3"));
        Assert.Equal(10000m, quote.GetProperty("amount").GetDecimal()); // basic 100,000 / 30 × 3
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/pay-changes/encash", new { employeeId = a, leaveTypeId = annual, days = 999 }, ApiFactory.Json)).StatusCode);
        Assert.Equal(10000m, (await Ok(await admin.PostAsJsonAsync("/api/pay-changes/encash", new { employeeId = a, leaveTypeId = annual, days = 3 }, ApiFactory.Json))).GetProperty("amount").GetDecimal());

        // Approval required: HR cannot finalize until the admin approves (the company admin here is the same login, as in small companies).
        var settings = await Ok(await admin.GetAsync("/api/payroll/settings"));
        await Ok(await admin.PutAsJsonAsync("/api/payroll/settings", new
        {
            eobiEmployeeContribution = settings.GetProperty("eobiEmployeeContribution").GetDecimal(), eobiEmployerContribution = settings.GetProperty("eobiEmployerContribution").GetDecimal(),
            deductAbsentDays = false, requireApproval = true,
        }, ApiFactory.Json));
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = Today.Year, month = Today.Month }));
        var id = run.GetProperty("run").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/payroll/runs/{id}/finalize", null)).StatusCode);
        await Ok(await admin.PostAsync($"/api/payroll/runs/{id}/submit", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/payroll/runs/{id}/decide", new { approve = false }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/payroll/runs/{id}/decide", new { approve = true }, ApiFactory.Json));
        await Ok(await admin.PostAsync($"/api/payroll/runs/{id}/finalize", null));
        await Ok(await admin.PostAsJsonAsync($"/api/payroll/runs/{id}/disburse", new { via = "Bank transfer" }, ApiFactory.Json));
        var detail = await Ok(await admin.GetAsync($"/api/payroll/runs/{id}"));
        Assert.Equal("Bank transfer", detail.GetProperty("run").GetProperty("disbursedVia").GetString());
        var slip = detail.GetProperty("payslips").EnumerateArray().First(p => p.GetProperty("employeeId").GetGuid() == a).GetProperty("id").GetGuid();
        Assert.Contains((await Ok(await admin.GetAsync($"/api/payroll/payslips/{slip}"))).GetProperty("lines").EnumerateArray(),
            l => l.GetProperty("name").GetString()!.StartsWith("Leave encashment") && l.GetProperty("amount").GetDecimal() == 10000);
    }
}
