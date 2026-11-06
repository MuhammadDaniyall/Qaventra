using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Domain.Services;

namespace HRMS.Tests.Api;

/// <summary>Gulf payroll: GOSI, GPSSA, UAE and Saudi gratuity, WPS files, and Pakistan payroll left as it was.</summary>
public class GulfPayrollTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateOnly Years(DateOnly joined, int years) => joined.AddDays(365 * years - 1);

    // ---------- GOSI ----------

    [Fact]
    public void Gosi_for_saudis_is_annuities_and_saned_on_basic_plus_housing()
    {
        var r = new GosiRates();
        var g = GosiCalculator.Calculate(r, 10000m, 2500m, isSaudi: true, newEntrant: false, new DateOnly(2026, 10, 31));
        Assert.Equal(12500m, g.WageBase);
        Assert.Equal(1125m, g.AnnuityEmployee);
        Assert.Equal(93.75m, g.SanedEmployee);
        Assert.Equal(1218.75m, g.Employee);                 // 9.75%
        Assert.Equal(1468.75m, g.Employer);                 // 9% + 0.75% + 2% hazards
    }

    [Fact]
    public void Gosi_for_non_saudis_is_occupational_hazards_paid_by_the_employer()
    {
        var g = GosiCalculator.Calculate(new GosiRates(), 8000m, 2000m, isSaudi: false, newEntrant: false, new DateOnly(2026, 10, 31));
        Assert.Equal(0m, g.Employee);
        Assert.Equal(200m, g.Employer);
        Assert.Equal(200m, g.HazardEmployer);
    }

    [Fact]
    public void Gosi_wage_is_capped_at_45000_and_floored_at_1500_for_saudis()
    {
        var on = new DateOnly(2026, 10, 31);
        var high = GosiCalculator.Calculate(new GosiRates(), 50000m, 10000m, true, false, on);
        Assert.Equal(45000m, high.WageBase);
        Assert.Equal(4387.5m, high.Employee);
        var low = GosiCalculator.Calculate(new GosiRates(), 1000m, 0m, true, false, on);
        Assert.Equal(1500m, low.WageBase);
        // The floor is for Saudis; a non-Saudi's hazards are on the actual wage.
        Assert.Equal(1000m, GosiCalculator.Calculate(new GosiRates(), 1000m, 0m, false, false, on).WageBase);
    }

    [Fact]
    public void New_saudi_entrants_after_the_2024_reform_pay_a_rising_annuity_rate()
    {
        var r = new GosiRates();
        Assert.Equal(9m, GosiCalculator.AnnuityPercent(r, true, new DateOnly(2025, 6, 30), false));
        Assert.Equal(9.5m, GosiCalculator.AnnuityPercent(r, true, new DateOnly(2025, 7, 31), false));
        Assert.Equal(10m, GosiCalculator.AnnuityPercent(r, true, new DateOnly(2026, 11, 30), true));
        Assert.Equal(11m, GosiCalculator.AnnuityPercent(r, true, new DateOnly(2028, 7, 31), false));
        Assert.Equal(11m, GosiCalculator.AnnuityPercent(r, true, new DateOnly(2035, 1, 31), false));
        // Registered before July 2024: stays on 9%.
        Assert.Equal(9m, GosiCalculator.AnnuityPercent(r, false, new DateOnly(2030, 1, 31), false));
        var g = GosiCalculator.Calculate(r, 10000m, 0m, true, true, new DateOnly(2026, 11, 30));
        Assert.Equal(1075m, g.Employee);                    // 10% + 0.75%
        Assert.Equal(1275m, g.Employer);                    // 10% + 0.75% + 2%
    }

    [Fact]
    public void Gpssa_pension_is_for_uae_nationals_only()
    {
        var (wage, employee, employer) = PensionCalculator.Calculate(new PensionRates(), 20000m, true);
        Assert.Equal((20000m, 2200m, 2500m), (wage, employee, employer));
        Assert.Equal(70000m, PensionCalculator.Calculate(new PensionRates(), 90000m, true).Wage);
        Assert.Equal((0m, 0m, 0m), PensionCalculator.Calculate(new PensionRates(), 20000m, false));
    }

    // ---------- Gratuity ----------

    [Fact]
    public void Uae_gratuity_is_21_days_for_five_years_then_30_capped_at_two_years()
    {
        var joined = new DateOnly(2010, 1, 1);
        Assert.Equal(0m, GulfGratuity.Uae(10000m, joined, joined.AddDays(300)).Amount);                 // under a year
        Assert.Equal(21000m, GulfGratuity.Uae(10000m, joined, Years(joined, 3)).Amount);               // 63 days
        Assert.Equal(55000m, GulfGratuity.Uae(10000m, joined, Years(joined, 7)).Amount);               // 105 + 60 days
        Assert.Equal(240000m, GulfGratuity.Uae(10000m, joined, Years(joined, 30)).Amount);             // capped at 24 months
        // Pro-rata: 2.2 years = 46.2 days.
        Assert.Equal(15400m, GulfGratuity.Uae(10000m, joined, joined.AddDays(802)).Amount);
    }

    [Fact]
    public void Ksa_gratuity_is_half_a_month_then_a_month_and_resignation_reduces_it()
    {
        var joined = new DateOnly(2010, 1, 1);
        Assert.Equal(15000m, GulfGratuity.Saudi(10000m, joined, Years(joined, 3), resigned: false).Amount);
        Assert.Equal(5000m, GulfGratuity.Saudi(10000m, joined, Years(joined, 3), resigned: true).Amount);       // one third
        Assert.Equal(45000m, GulfGratuity.Saudi(10000m, joined, Years(joined, 7), resigned: false).Amount);
        Assert.Equal(30000m, GulfGratuity.Saudi(10000m, joined, Years(joined, 7), resigned: true).Amount);      // two thirds
        Assert.Equal(0m, GulfGratuity.Saudi(10000m, joined, joined.AddDays(500), resigned: true).Amount);      // under 2 years
        Assert.Equal(95000m, GulfGratuity.Saudi(10000m, joined, Years(joined, 12), resigned: true).Amount);    // 10+ years: all
    }

    // ---------- WPS ----------

    [Fact]
    public void Uae_sif_has_an_edr_per_person_and_an_scr_with_the_totals()
    {
        var er = new WpsEmployer("1234567890123", "808610001", null, "200110001", "PAYROLL202610", "AED");
        var lines = new List<WpsLine>
        {
            new("E1", "Ali", "10000000000001", "601710101", "AE07 0331 2345 6789 0123 456", 9000m, 500m, 9500m, 6000m, 3000m, 500m, 0m, 0),
            new("E2", "Sara", "10000000000002", null, "AE070331234567890123457", 12000m, 0m, 12000m, 8000m, 4000m, 0m, 0m, 2),
        };
        var (name, content) = WpsFiles.UaeSif(er, lines, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), new DateTime(2026, 11, 2, 9, 30, 15));
        Assert.Equal("1234567890123261102093015.SIF", name);
        var rows = content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, rows.Length);
        Assert.Equal("EDR,10000000000001,601710101,AE070331234567890123456,2026-10-01,2026-10-31,31,9000.00,500.00,0", rows[0]);
        // No routing code on the person: the agent from settings.
        Assert.Equal("EDR,10000000000002,200110001,AE070331234567890123457,2026-10-01,2026-10-31,31,12000.00,0.00,2", rows[1]);
        Assert.Equal("SCR,1234567890123,808610001,2026-11-02,0930,102026,2,21500.00,AED,PAYROLL202610", rows[2]);
    }

    // ---------- Through the API ----------

    private static async Task<JsonElement> SetCountryAsync(HttpClient admin, string country, Action<Dictionary<string, object?>>? more = null)
    {
        var s = JsonSerializer.Deserialize<Dictionary<string, object?>>((await Ok(await admin.GetAsync("/api/payroll/settings"))).GetRawText())!;
        s["payrollCountry"] = country;
        more?.Invoke(s);
        await Ok(await admin.PutAsJsonAsync("/api/payroll/settings", s, ApiFactory.Json));
        return await Ok(await admin.GetAsync("/api/payroll/settings"));
    }

    private static async Task<Guid> PersonAsync(HttpClient admin, string first, string joined, decimal basic, decimal housing, object file)
    {
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "G", joiningDate = joined, status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new
        {
            effectiveFrom = joined, basicSalary = basic, eobiApplicable = true,
            components = new[] { new { name = "Housing allowance", kind = "Earning", amount = housing, isTaxable = true } },
        }, ApiFactory.Json));
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/file", file, ApiFactory.Json));
        return id;
    }

    [Fact]
    public async Task Saudi_company_pays_gosi_no_tax_and_downloads_a_bank_file()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Riyadh Trading");
        var settings = await SetCountryAsync(admin, "SA", s => { s["wpsEmployerId"] = "7001234567"; s["wpsEmployerIban"] = "SA0380000000608010167519"; });
        Assert.Equal("SAR", settings.GetProperty("currency").GetString());
        Assert.Equal("SAR", (await Ok(await admin.GetAsync("/api/auth/me"))).GetProperty("currency").GetString());

        var saudi = await PersonAsync(admin, "Faisal", "2015-01-01", 10000m, 2500m, new
        {
            gccNationality = "SA", iqamaNo = "1012345678", iban = "SA44 2000 0001 2345 6789 1234", allowWebCheckIn = true, allowMobileCheckIn = true,
        });
        var expat = await PersonAsync(admin, "Imran", "2023-01-01", 8000m, 2000m, new
        {
            iqamaNo = "2087654321", iban = "SA4420000001234567891235", allowWebCheckIn = true, allowMobileCheckIn = true,
        });

        var month = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = month.Year, month = month.Month }, ApiFactory.Json));
        Assert.Equal("SAR", run.GetProperty("run").GetProperty("currency").GetString());
        var slips = run.GetProperty("payslips").EnumerateArray().ToList();
        var mine = slips.Single(p => p.GetProperty("employeeId").GetGuid() == saudi);
        Assert.Equal(0m, mine.GetProperty("incomeTax").GetDecimal());
        Assert.Equal(0m, mine.GetProperty("eobiEmployee").GetDecimal());
        Assert.Equal(12500m, mine.GetProperty("grossEarnings").GetDecimal());
        Assert.Equal(12500m - 1218.75m, mine.GetProperty("netPay").GetDecimal());

        var detail = await Ok(await admin.GetAsync($"/api/payroll/payslips/{mine.GetProperty("id").GetGuid()}"));
        Assert.Equal(1218.75m, detail.GetProperty("socialInsuranceEmployee").GetDecimal());
        Assert.Equal(1468.75m, detail.GetProperty("socialInsuranceEmployer").GetDecimal());
        Assert.Contains(detail.GetProperty("lines").EnumerateArray(), l => l.GetProperty("name").GetString()!.StartsWith("GOSI"));

        var theirs = slips.Single(p => p.GetProperty("employeeId").GetGuid() == expat);
        Assert.Equal(10000m, theirs.GetProperty("netPay").GetDecimal());
        var theirDetail = await Ok(await admin.GetAsync($"/api/payroll/payslips/{theirs.GetProperty("id").GetGuid()}"));
        Assert.Equal(200m, theirDetail.GetProperty("socialInsuranceEmployer").GetDecimal());

        var insurance = await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.gulfinsurance", new { year = month.Year, month = month.Month }, ApiFactory.Json));
        Assert.Equal(2, insurance.GetProperty("rows").GetArrayLength());

        // The bank file only for a finalized run.
        var runId = run.GetProperty("run").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/payroll/runs/{runId}/wps")).StatusCode);
        await Ok(await admin.PostAsync($"/api/payroll/runs/{runId}/finalize", null));
        var file = await admin.GetAsync($"/api/payroll/runs/{runId}/wps");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        var csv = (await file.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Employee Number,Employee Name,National ID / Iqama,Employee IBAN", csv[0]);
        Assert.Equal(3, csv.Length);
        Assert.Contains(csv, l => l.Contains("1012345678,SA4420000001234567891234,") && l.Contains(",11281.25,10000.00,2500.00,0.00,1218.75,SAR,"));

        // End-of-service provision: Faisal has served over ten years.
        var provision = await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.gratuityprovision", new { }, ApiFactory.Json));
        var faisal = provision.GetProperty("rows").EnumerateArray().Single(r => r[1].GetString() == "Faisal G");
        var years = decimal.Parse(faisal[3].GetString()!, CultureInfo.InvariantCulture);
        // Half a month for five years, a full month after; the same if he resigned (10+ years).
        var due = decimal.Parse(faisal[5].GetString()!.Replace(",", ""), CultureInfo.InvariantCulture);
        Assert.InRange(due, 25000m + (years - 5.01m) * 10000m, 25000m + (years - 4.99m) * 10000m);
        Assert.Equal(faisal[5].GetString(), faisal[6].GetString());
        Assert.True(years > 10);
    }

    [Fact]
    public async Task Saudi_resignation_settlement_includes_the_reduced_gratuity()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Jeddah Builders");
        await SetCountryAsync(admin, "SA");
        var joined = Today.AddDays(-(365 * 3) + 20);       // three years by the last day
        var leaver = await PersonAsync(admin, "Nora", Iso(joined), 9000m, 3000m, new { gccNationality = "SA", allowWebCheckIn = true, allowMobileCheckIn = true });
        var sep = await Ok(await admin.PostAsJsonAsync("/api/separations", new
        {
            employeeId = leaver, type = "Resignation", noticeDate = Iso(Today), lastWorkingDay = Iso(joined.AddDays(365 * 3 - 1)), noticePeriodDays = 0, reason = "Moving",
        }, ApiFactory.Json));
        var settlement = sep.GetProperty("settlement");
        // Basic 9,000 x 0.5 x 3 years = 13,500; resigned between 2 and 5 years: one third.
        Assert.Equal(4500m, settlement.GetProperty("gratuity").GetDecimal());
        Assert.Contains("one third", settlement.GetProperty("gratuityNote").GetString());
        Assert.Equal(4500m, settlement.GetProperty("net").GetDecimal());
    }

    [Fact]
    public async Task Uae_company_deducts_gpssa_for_emiratis_and_writes_a_sif()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Dubai Logistics");
        await SetCountryAsync(admin, "AE", s => { s["wpsEmployerId"] = "1000000012345"; s["wpsBankRoutingCode"] = "808610001"; s["wpsAgentId"] = "808610001"; });
        var emirati = await PersonAsync(admin, "Ahmed", "2020-01-01", 15000m, 5000m, new
        {
            gccNationality = "AE", labourCardNo = "12345678901234", iban = "AE070331234567890123456", allowWebCheckIn = true, allowMobileCheckIn = true,
        });
        await PersonAsync(admin, "Ravi", "2020-01-01", 6000m, 2000m, new
        {
            labourCardNo = "98765432109876", iban = "AE070331234567890123457", allowWebCheckIn = true, allowMobileCheckIn = true,
        });
        var month = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = month.Year, month = month.Month }, ApiFactory.Json));
        var slips = run.GetProperty("payslips").EnumerateArray().ToList();
        var mine = slips.Single(p => p.GetProperty("employeeId").GetGuid() == emirati);
        Assert.Equal(20000m - 2200m, mine.GetProperty("netPay").GetDecimal());   // 11% of the 20,000 gross
        Assert.All(slips, p => Assert.Equal(0m, p.GetProperty("incomeTax").GetDecimal()));

        var runId = run.GetProperty("run").GetProperty("id").GetGuid();
        await Ok(await admin.PostAsync($"/api/payroll/runs/{runId}/finalize", null));
        var file = await admin.GetAsync($"/api/payroll/runs/{runId}/wps");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.EndsWith(".SIF", file.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        var rows = (await file.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, rows.Length);
        Assert.StartsWith("EDR,12345678901234,808610001,AE070331234567890123456,", rows[0]);
        Assert.StartsWith("SCR,1000000012345,808610001,", rows[2]);
        Assert.Contains(",2,25800.00,AED,", rows[2]);       // 17,800 + 8,000
    }

    [Fact]
    public async Task Pakistan_stays_the_default_and_its_payroll_is_unchanged()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Lahore Textiles");
        var settings = await Ok(await admin.GetAsync("/api/payroll/settings"));
        Assert.Equal("PK", settings.GetProperty("payrollCountry").GetString());
        Assert.Equal("PKR", settings.GetProperty("currency").GetString());
        Assert.Equal("PKR", (await Ok(await admin.GetAsync("/api/auth/me"))).GetProperty("currency").GetString());

        // A Saudi passport or a housing allowance changes nothing under Pakistan payroll.
        var id = await PersonAsync(admin, "Bilal", "2020-01-01", 200000m, 50000m, new { gccNationality = "SA", allowWebCheckIn = true, allowMobileCheckIn = true });
        var month = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-1);
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = month.Year, month = month.Month }, ApiFactory.Json));
        Assert.Equal("PKR", run.GetProperty("run").GetProperty("currency").GetString());
        var slip = run.GetProperty("payslips").EnumerateArray().Single(p => p.GetProperty("employeeId").GetGuid() == id);
        Assert.True(slip.GetProperty("incomeTax").GetDecimal() > 0);
        Assert.Equal(400m, slip.GetProperty("eobiEmployee").GetDecimal());
        var detail = await Ok(await admin.GetAsync($"/api/payroll/payslips/{slip.GetProperty("id").GetGuid()}"));
        Assert.Equal(0m, detail.GetProperty("socialInsuranceEmployee").GetDecimal());
        Assert.DoesNotContain(detail.GetProperty("lines").EnumerateArray(), l => l.GetProperty("name").GetString()!.StartsWith("GOSI"));
        Assert.Equal(250000m - slip.GetProperty("incomeTax").GetDecimal() - 400m, slip.GetProperty("netPay").GetDecimal());

        // No WPS file and no Gulf provision for Pakistan payroll.
        var runId = run.GetProperty("run").GetProperty("id").GetGuid();
        await Ok(await admin.PostAsync($"/api/payroll/runs/{runId}/finalize", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/payroll/runs/{runId}/wps")).StatusCode);
    }
}
