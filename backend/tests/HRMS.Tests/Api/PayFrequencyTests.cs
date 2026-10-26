using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Domain;
using HRMS.Domain.Services;

namespace HRMS.Tests.Api;

/// <summary>Weekly, bi-weekly and semi-monthly pay next to monthly pay.</summary>
public class PayFrequencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public void Periods_follow_the_calendar()
    {
        var oct = new DateOnly(2026, 10, 14);
        Assert.Equal((new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18)), Span(PayPeriods.Containing(PayFrequency.Weekly, oct)));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15)), Span(PayPeriods.Containing(PayFrequency.SemiMonthly, oct)));
        Assert.Equal((new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 31)), Span(PayPeriods.Containing(PayFrequency.SemiMonthly, new DateOnly(2026, 10, 20))));
        // Fortnights count from 5 Jan 2026: 12 Oct is 20 fortnights (280 days) later.
        Assert.Equal((new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25)), Span(PayPeriods.Containing(PayFrequency.BiWeekly, oct)));
        Assert.Equal((new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 11)), Span(PayPeriods.Containing(PayFrequency.BiWeekly, new DateOnly(2026, 10, 11))));

        var weeks = PayPeriods.EndingIn(PayFrequency.Weekly, 2026, 10);
        Assert.Equal(new DateOnly(2026, 10, 4), weeks[0].End);
        Assert.Equal(4, weeks.Count);
        // Only the first period ending in a month takes once-a-month items.
        Assert.True(PayPeriods.TakesMonthlyItems(weeks[0]));
        Assert.False(PayPeriods.TakesMonthlyItems(weeks[1]));
        Assert.Equal("Week 12–18 Oct 2026", PayPeriods.Containing(PayFrequency.Weekly, oct).Label);
        Assert.Equal(12000m, Math.Round(52000m * PayPeriods.ShareOfMonth(PayFrequency.Weekly), 0));
    }

    private static (DateOnly, DateOnly) Span(PayPeriod p) => (p.Start, p.End);

    [Fact]
    public async Task Weekly_paid_people_get_their_own_runs()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Weekly Co");
        async Task<Guid> Person(string first, string frequency)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "W", joiningDate = "2020-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2020-01-01", basicSalary = 52000, eobiApplicable = true, components = Array.Empty<object>() }, ApiFactory.Json));
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/file", new { payFrequency = frequency, allowWebCheckIn = true, allowMobileCheckIn = true }, ApiFactory.Json));
            return id;
        }
        var worker = await Person("Wajid", "Weekly");
        var clerk = await Person("Maha", "Monthly");
        Assert.Equal("Weekly", (await Ok(await admin.GetAsync($"/api/employees/{worker}/file"))).GetProperty("payFrequency").GetString());

        // The monthly run leaves the weekly worker out.
        var monthly = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = 2026, month = 9 }, ApiFactory.Json));
        Assert.Equal(clerk, Assert.Single(monthly.GetProperty("payslips").EnumerateArray()).GetProperty("employeeId").GetGuid());
        Assert.Equal("Monthly", monthly.GetProperty("run").GetProperty("frequency").GetString());

        var periods = (await Ok(await admin.GetAsync("/api/payroll/periods?year=2026&month=9"))).EnumerateArray().ToList();
        Assert.Contains(periods, p => p.GetProperty("frequency").GetString() == "Monthly" && p.GetProperty("runId").ValueKind != JsonValueKind.Null);
        var weeks = periods.Where(p => p.GetProperty("frequency").GetString() == "Weekly").ToList();
        Assert.Equal(4, weeks.Count);
        Assert.All(weeks, w => Assert.Equal(1, w.GetProperty("people").GetInt32()));

        // First week ending in September: a week's share of salary and the month's EOBI.
        var first = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs/period", new { frequency = "Weekly", date = weeks[0].GetProperty("start").GetString() }, ApiFactory.Json));
        var slip = Assert.Single(first.GetProperty("payslips").EnumerateArray());
        Assert.Equal(worker, slip.GetProperty("employeeId").GetGuid());
        Assert.Equal(12000m, slip.GetProperty("grossEarnings").GetDecimal());
        Assert.True(slip.GetProperty("eobiEmployee").GetDecimal() > 0);
        Assert.StartsWith("Week ", first.GetProperty("run").GetProperty("label").GetString());

        // Second week: no EOBI again.
        var second = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs/period", new { frequency = "Weekly", date = weeks[1].GetProperty("start").GetString() }, ApiFactory.Json));
        Assert.Equal(0m, Assert.Single(second.GetProperty("payslips").EnumerateArray()).GetProperty("eobiEmployee").GetDecimal());

        // Reprocessing the same week updates its run instead of adding one.
        await Ok(await admin.PostAsJsonAsync("/api/payroll/runs/period", new { frequency = "Weekly", date = weeks[1].GetProperty("end").GetString() }, ApiFactory.Json));
        var runs = (await Ok(await admin.GetAsync("/api/payroll/runs"))).EnumerateArray().ToList();
        Assert.Equal(3, runs.Count);

        // Finalizing a week does not close the month for the monthly payroll.
        var weekRun = first.GetProperty("run").GetProperty("id").GetGuid();
        await Ok(await admin.PostAsync($"/api/payroll/runs/{weekRun}/finalize", null));
        await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = 2026, month = 9 }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/payroll/runs/period", new { frequency = "Weekly", date = weeks[0].GetProperty("start").GetString() }, ApiFactory.Json)).StatusCode);
    }
}
