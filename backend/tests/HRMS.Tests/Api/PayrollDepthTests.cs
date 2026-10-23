using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Provident fund, social security, tax adjustments, cash payments, fund withdrawals and overtime rules.</summary>
public class PayrollDepthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static List<List<string>> Rows(JsonElement report) =>
        report.GetProperty("rows").EnumerateArray().Select(r => r.EnumerateArray().Select(c => c.GetString() ?? "").ToList()).ToList();

    [Fact]
    public async Task Funds_social_security_tax_adjustments_and_cash_pay()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("PayDepth");
        async Task<(Guid, HttpClient)> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "D", joiningDate = "2020-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2020-01-01", basicSalary = 200000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json));
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@paydepth.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Depth-Pay-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Depth-Pay-1234"));
        }
        var (a, aClient) = await Person("Amna");
        var (b, _) = await Person("Bilal");

        var settings = await Ok(await admin.GetAsync("/api/payroll/settings"));
        var s = JsonSerializer.Deserialize<Dictionary<string, object?>>(settings.GetRawText())!;
        s["providentFundEnabled"] = true;
        s["pfEmployeePercent"] = 10;
        s["pfEmployerPercent"] = 10;
        s["socialSecurityPercent"] = 6;
        s["socialSecurityWageCeiling"] = 50000;
        s["gratuityEnabled"] = true;
        await Ok(await admin.PutAsJsonAsync("/api/payroll/settings", s, ApiFactory.Json));
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{a}/file", new { providentFundMember = true, paymentMethod = "Cash", socialSecurityNo = "PESSI-1", allowWebCheckIn = true, allowMobileCheckIn = true }, ApiFactory.Json));

        // Amna's previous employer already took a lot of tax this year: no more tax for her.
        var month = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        var taxYear = (await Ok(await admin.GetAsync("/api/funds/tax-years")))[0].GetString();
        await Ok(await admin.PostAsJsonAsync("/api/funds/tax-adjustments", new { employeeId = a, taxYearCode = taxYear, kind = "PreviousTax", amount = 5_000_000 }, ApiFactory.Json));

        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = month.Year, month = month.Month }, ApiFactory.Json));
        var runId = run.GetProperty("run").GetProperty("id").GetGuid();
        var q = new { year = month.Year, month = month.Month };
        var tax = Rows(await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.tax", q, ApiFactory.Json)));
        Assert.Equal("0", tax.First(r => r[1].StartsWith("Amna"))[4]);
        Assert.NotEqual("0", tax.First(r => r[1].StartsWith("Bilal"))[4]);
        var cash = Rows(await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.cash", q, ApiFactory.Json)));
        Assert.Single(cash);
        Assert.DoesNotContain(Rows(await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.bank", q, ApiFactory.Json))), r => r[1].StartsWith("Amna"));
        var ss = Rows(await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.socialsecurity", q, ApiFactory.Json)));
        Assert.Equal("3,000", ss.Single()[5]);

        await Ok(await admin.PostAsync($"/api/payroll/runs/{runId}/finalize", null));
        await Ok(await admin.PostAsJsonAsync($"/api/payroll/runs/{runId}/disburse", new { via = "Bank" }, ApiFactory.Json));
        await Ok(await admin.PostAsync($"/api/payroll/runs/{runId}/undisburse", null));
        var mine = await Ok(await aClient.GetAsync("/api/funds/me"));
        Assert.Equal(20000m, mine.GetProperty("pfEmployeeShare").GetDecimal());
        Assert.Equal(40000m, mine.GetProperty("pfBalance").GetDecimal());
        Assert.True(mine.GetProperty("gratuityBalance").GetDecimal() > 0);

        // Withdraw part of the fund; HR approves; it rides on the next payroll as a tax-free addition.
        Assert.Equal(HttpStatusCode.BadRequest, (await aClient.PostAsJsonAsync("/api/funds/withdrawals", new { fund = "ProvidentFund", amount = 50000, reason = "Too much" }, ApiFactory.Json)).StatusCode);
        await Ok(await aClient.PostAsJsonAsync("/api/funds/withdrawals", new { fund = "ProvidentFund", amount = 15000, reason = "Medical" }, ApiFactory.Json));
        var w = (await Ok(await admin.GetAsync("/api/funds/withdrawals?status=Pending"))).EnumerateArray().Single().GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync($"/api/approvals/FundWithdrawal/{w}/decide", new { approve = true }, ApiFactory.Json));
        Assert.Equal(25000m, (await Ok(await aClient.GetAsync("/api/funds/me"))).GetProperty("pfBalance").GetDecimal());
        var pay = await Ok(await admin.GetAsync($"/api/pay/requests?employeeId={a}"));
        Assert.Contains(pay.EnumerateArray(), r => r.GetProperty("title").GetString() == "Provident fund withdrawal" && r.GetProperty("status").GetString() == "Approved");
        var pfReport = Rows(await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.pf", new { }, ApiFactory.Json)));
        Assert.Equal("25,000", pfReport.Single()[6]);
        _ = b;
    }
}
