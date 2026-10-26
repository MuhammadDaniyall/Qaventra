using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Voluntary pension scheme: deduction, company share, the section 63 tax credit, report and journal.</summary>
public class VoluntaryPensionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private static List<List<string>> Rows(JsonElement report) =>
        report.GetProperty("rows").EnumerateArray().Select(r => r.EnumerateArray().Select(c => c.GetString() ?? "").ToList()).ToList();

    private static decimal Money(string s) => decimal.Parse(s.Replace(",", ""), CultureInfo.InvariantCulture);

    [Fact]
    public async Task Members_pay_into_their_pension_and_pay_less_tax()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Pension Co");
        async Task<Guid> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "P", joiningDate = "2020-01-01", status = "Active", cnic = "35202-1234567-1" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2020-01-01", basicSalary = 300000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json));
            return id;
        }
        var member = await Person("Maryam");
        var other = await Person("Omar");

        // No contribution given: refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/employees/{member}/file", new { vpsMember = true, allowWebCheckIn = true, allowMobileCheckIn = true }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{member}/file", new
        {
            vpsMember = true, vpsProvider = "Meezan Tahaffuz Pension Fund", vpsAccountNo = "MTPF-0042", vpsEmployeePercent = 10, vpsEmployerPercent = 5,
            allowWebCheckIn = true, allowMobileCheckIn = true,
        }, ApiFactory.Json));
        var file = await Ok(await admin.GetAsync($"/api/employees/{member}/file"));
        Assert.True(file.GetProperty("vpsMember").GetBoolean());
        Assert.Equal(10, file.GetProperty("vpsEmployeePercent").GetDecimal());

        var month = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        var q = new { year = month.Year, month = month.Month };

        // While VPS is off in payroll settings, nothing is deducted.
        await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", q, ApiFactory.Json));
        Assert.Empty(Rows(await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.vps", q, ApiFactory.Json))));

        var s = JsonSerializer.Deserialize<Dictionary<string, object?>>((await Ok(await admin.GetAsync("/api/payroll/settings"))).GetRawText())!;
        s["vpsEnabled"] = true;
        await Ok(await admin.PutAsJsonAsync("/api/payroll/settings", s, ApiFactory.Json));
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", q, ApiFactory.Json));
        var runId = run.GetProperty("run").GetProperty("id").GetGuid();
        var slips = run.GetProperty("payslips").EnumerateArray().ToList();
        var mine = slips.Single(p => p.GetProperty("employeeId").GetGuid() == member);
        var theirs = slips.Single(p => p.GetProperty("employeeId").GetGuid() == other);

        // 10% of 300,000 basic is deducted; tax drops by the average rate on that 30,000.
        var taxPlain = theirs.GetProperty("incomeTax").GetDecimal();
        var taxMember = mine.GetProperty("incomeTax").GetDecimal();
        Assert.True(taxPlain > 0);
        Assert.Equal(Math.Round(taxPlain - Math.Round(taxPlain / 300000m * 30000m, 0, MidpointRounding.AwayFromZero), 0), taxMember);
        Assert.Equal(theirs.GetProperty("netPay").GetDecimal() - 30000m + (taxPlain - taxMember), mine.GetProperty("netPay").GetDecimal());

        var detail = await Ok(await admin.GetAsync($"/api/payroll/payslips/{mine.GetProperty("id").GetGuid()}"));
        Assert.Contains(detail.GetProperty("lines").EnumerateArray(), l => l.GetProperty("name").GetString()!.StartsWith("Voluntary pension") && l.GetProperty("amount").GetDecimal() == 30000m);

        var row = Assert.Single(Rows(await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.vps", q, ApiFactory.Json))));
        Assert.Equal("Meezan Tahaffuz Pension Fund", row[3]);
        Assert.Equal(30000m, Money(row[5]));
        Assert.Equal(15000m, Money(row[6]));
        Assert.Equal(taxPlain - taxMember, Money(row[8]));

        // The journal owes both shares to the pension fund and still balances.
        var journal = await Ok(await admin.GetAsync($"/api/payroll/runs/{runId}/journal"));
        Assert.Equal(journal.GetProperty("totalDebit").GetDecimal(), journal.GetProperty("totalCredit").GetDecimal());
        Assert.Contains(journal.GetProperty("lines").EnumerateArray(), l => l.GetProperty("description").GetString()!.StartsWith("Voluntary pension") && l.GetProperty("credit").GetDecimal() == 45000m);
    }
}
