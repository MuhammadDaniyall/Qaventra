using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class AccountingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Payroll_journal_balances_and_exports_once_every_line_is_mapped()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Ledger Co");
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Sana", lastName = "J", joiningDate = "2026-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new
        {
            effectiveFrom = "2026-01-01",
            basicSalary = 60_000,
            eobiApplicable = true,
            components = new object[] { new { name = "House Rent", kind = "Earning", amount = 27_000, isTaxable = true } },
        }, ApiFactory.Json)).StatusCode);
        var runId = (await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = 2026, month = 9 }, ApiFactory.Json)))
            .GetProperty("run").GetProperty("id").GetGuid();

        var journal = await Ok(await admin.GetAsync($"/api/payroll/runs/{runId}/journal"));
        Assert.Equal(journal.GetProperty("totalDebit").GetDecimal(), journal.GetProperty("totalCredit").GetDecimal());
        Assert.True(journal.GetProperty("totalDebit").GetDecimal() > 0);
        Assert.Equal(8, journal.GetProperty("unmapped").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/payroll/runs/{runId}/journal.csv")).StatusCode);

        var account = await Ok(await admin.PostAsJsonAsync("/api/payroll/gl/accounts", new { code = "5100", name = "Salaries" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/payroll/gl/accounts", new { code = "5100", name = "Again" }, ApiFactory.Json)).StatusCode);
        var lines = (await Ok(await admin.GetAsync("/api/payroll/gl/mapping"))).EnumerateArray()
            .Select(m => new { line = m.GetProperty("line").GetString(), glAccountId = (Guid?)account.GetProperty("id").GetGuid() }).ToList();
        await Ok(await admin.PutAsJsonAsync("/api/payroll/gl/mapping", new { items = lines }, ApiFactory.Json));

        var csv = await admin.GetAsync($"/api/payroll/runs/{runId}/journal.csv");
        Assert.True(csv.IsSuccessStatusCode);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains("Account code", text);
        Assert.Contains("5100", text);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/payroll/gl/accounts/{account.GetProperty("id").GetGuid()}")).StatusCode);
    }
}
