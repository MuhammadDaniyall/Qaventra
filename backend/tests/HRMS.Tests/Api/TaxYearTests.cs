using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>The platform owner's tax years and slabs, and payroll for a month whose tax year is not set up yet.</summary>
public class TaxYearTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static string Code(int startYear) => $"{startYear}-{(startYear + 1) % 100:D2}";

    /// <summary>
    /// The July–June tax year the month falls in, added by the owner with the latest year's slabs when it is not set up
    /// (so tests that put tax adjustments on the current year keep working before and after a year is seeded).
    /// </summary>
    public static async Task<string> EnsureTaxYearAsync(ApiFactory factory, DateOnly month)
    {
        var start = month.Month >= 7 ? month.Year : month.Year - 1;
        var owner = await factory.SignInAsOwnerAsync();
        var years = (await Ok(await owner.GetAsync("/api/platform/tax-years"))).EnumerateArray().ToList();
        if (years.Any(y => y.GetProperty("code").GetString() == Code(start)))
            return Code(start);
        var latest = years[0];
        await Ok(await owner.PostAsJsonAsync("/api/platform/tax-years", new
        {
            code = Code(start), startDate = $"{start}-07-01", endDate = $"{start + 1}-06-30",
            surchargeThreshold = latest.GetProperty("surchargeThreshold").GetDecimal(), surchargeRate = latest.GetProperty("surchargeRate").GetDecimal(),
            slabs = latest.GetProperty("slabs"),
        }, ApiFactory.Json));
        return Code(start);
    }

    private static object Year(string code, int start, params object[] slabs) =>
        new { code, startDate = $"{start}-07-01", endDate = $"{start + 1}-06-30", surchargeThreshold = 10_000_000m, surchargeRate = 0.09m, slabs };

    [Fact]
    public async Task Only_the_owner_adds_and_corrects_tax_years()
    {
        var owner = await factory.SignInAsOwnerAsync();
        var (admin, _, _) = await factory.CreateCompanyAsync("Tax Years");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/platform/tax-years")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/platform/tax-years", Year("2040-41", 2040, new { fromAmount = 0, toAmount = (decimal?)null, fixedTax = 0, rate = 0 }), ApiFactory.Json)).StatusCode);

        // Slabs start at 0, run without gaps, and only the top one is open.
        var gap = Year("2040-41", 2040, new { fromAmount = 0, toAmount = (decimal?)600_000, fixedTax = 0, rate = 0 }, new { fromAmount = 700_000, toAmount = (decimal?)null, fixedTax = 0, rate = 0.1 });
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/platform/tax-years", gap, ApiFactory.Json)).StatusCode);
        var open = Year("2040-41", 2040, new { fromAmount = 0, toAmount = (decimal?)null, fixedTax = 0, rate = 0 }, new { fromAmount = 600_000, toAmount = (decimal?)null, fixedTax = 0, rate = 0.1 });
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/platform/tax-years", open, ApiFactory.Json)).StatusCode);

        var good = Year("2040-41", 2040, new { fromAmount = 0, toAmount = (decimal?)600_000, fixedTax = 0, rate = 0 }, new { fromAmount = 600_000, toAmount = (decimal?)null, fixedTax = 0, rate = 0.05 });
        var created = await Ok(await owner.PostAsJsonAsync("/api/platform/tax-years", good, ApiFactory.Json));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/platform/tax-years", good, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/platform/tax-years", Year("2040-42", 2040, new { fromAmount = 0, toAmount = (decimal?)null, fixedTax = 0, rate = 0 }), ApiFactory.Json)).StatusCode);

        // A correction replaces the slabs; companies see the year in their list.
        var fixedYear = Year("2040-41", 2040, new { fromAmount = 0, toAmount = (decimal?)800_000, fixedTax = 0, rate = 0 }, new { fromAmount = 800_000, toAmount = (decimal?)null, fixedTax = 0, rate = 0.05 });
        var updated = await Ok(await owner.PutAsJsonAsync($"/api/platform/tax-years/{id}", fixedYear, ApiFactory.Json));
        Assert.Equal(800_000m, updated.GetProperty("slabs")[1].GetProperty("fromAmount").GetDecimal());
        Assert.Contains((await Ok(await admin.GetAsync("/api/funds/tax-years"))).EnumerateArray(), c => c.GetString() == "2040-41");

        // Unused, so it can be removed; the seeded year that payrolls use cannot.
        await Ok(await owner.DeleteAsync($"/api/platform/tax-years/{id}"));
        Assert.DoesNotContain((await Ok(await owner.GetAsync("/api/platform/tax-years"))).EnumerateArray(), y => y.GetProperty("code").GetString() == "2040-41");
    }

    [Fact]
    public async Task A_month_with_no_tax_year_does_not_use_the_old_years_adjustments()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("TaxGap");
        async Task<Guid> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "T", joiningDate = "2020-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2020-01-01", basicSalary = 300000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json));
            return id;
        }
        var a = await Person("Amna");
        await Person("Bilal");

        // The latest set-up year's adjustment (tax already paid) belongs to that year only. A month after it falls in the
        // next July–June year: same slabs, but none of last year's adjustments or payslips.
        var years = (await Ok(await (await factory.SignInAsOwnerAsync()).GetAsync("/api/platform/tax-years"))).EnumerateArray().ToList();
        var latest = years[0];
        await Ok(await admin.PostAsJsonAsync("/api/funds/tax-adjustments", new { employeeId = a, taxYearCode = latest.GetProperty("code").GetString(), kind = "PreviousTax", amount = 5_000_000 }, ApiFactory.Json));
        var after = DateOnly.Parse(latest.GetProperty("endDate").GetString()!).AddMonths(4);
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = after.Year, month = after.Month }, ApiFactory.Json));
        Assert.Contains(run.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains($"Tax year {Code(after.Year)} is not set up"));
        var taxes = run.GetProperty("payslips").EnumerateArray().Select(p => p.GetProperty("incomeTax").GetDecimal()).Distinct().ToList();
        Assert.True(Assert.Single(taxes) > 0);
    }
}
