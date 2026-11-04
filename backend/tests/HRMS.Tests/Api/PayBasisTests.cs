using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Daily and hourly paid people earn their rate times the days attended or hours worked; monthly pay is unchanged.</summary>
public class PayBasisTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Daily_and_hourly_pay_follow_attendance()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("PayBasis");
        async Task<Guid> Person(string first, object salary)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "B", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", salary, ApiFactory.Json));
            return id;
        }
        var daily = await Person("Dina", new { effectiveFrom = "2024-01-01", basicSalary = 0, eobiApplicable = false, payBasis = "Daily", rate = 2000, components = Array.Empty<object>() });
        var hourly = await Person("Hamid", new { effectiveFrom = "2024-01-01", basicSalary = 0, eobiApplicable = false, payBasis = "Hourly", rate = 500, components = Array.Empty<object>() });
        var monthly = await Person("Mona", new { effectiveFrom = "2024-01-01", basicSalary = 60000, eobiApplicable = false, components = Array.Empty<object>() });
        var saved = await Ok(await admin.GetAsync($"/api/employees/{daily}/salary"));
        Assert.Equal("Daily", saved.GetProperty("payBasis").GetString());
        Assert.Equal(2000m, saved.GetProperty("rate").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/employees/{daily}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 0, payBasis = "Hourly", components = Array.Empty<object>() }, ApiFactory.Json)).StatusCode);

        // September 2026: Dina attends two full days and one short one; Hamid two full days.
        decimal dinaDays = 0;
        foreach (var (day, outAt) in new[] { ("2026-09-07", "17:00:00"), ("2026-09-08", "17:00:00"), ("2026-09-09", "11:00:00") })
        {
            var a = await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = daily, date = day, checkIn = "09:00:00", checkOut = outAt }, ApiFactory.Json));
            dinaDays += a.GetProperty("status").GetString() == "HalfDay" ? 0.5m : 1m;
        }
        var hamidMinutes = 0;
        foreach (var day in new[] { "2026-09-07", "2026-09-08" })
            hamidMinutes += (await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = hourly, date = day, checkIn = "09:00:00", checkOut = "17:00:00" }, ApiFactory.Json)))
                .GetProperty("workedMinutes").GetInt32();

        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = 2026, month = 9 }, ApiFactory.Json));
        decimal Gross(Guid id) => run.GetProperty("payslips").EnumerateArray().Single(p => p.GetProperty("employeeId").GetGuid() == id).GetProperty("grossEarnings").GetDecimal();
        Assert.Equal(Math.Round(2000 * dinaDays), Gross(daily));
        Assert.Equal(Math.Round(500 * hamidMinutes / 60m), Gross(hourly));
        Assert.True(Gross(hourly) > 0);
        Assert.Equal(60000m, Gross(monthly));
    }
}
