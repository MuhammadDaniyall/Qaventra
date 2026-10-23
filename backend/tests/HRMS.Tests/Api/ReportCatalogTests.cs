using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>The report catalog: every report runs, payroll is HR-only, managers see only their team, Excel and signatures.</summary>
public class ReportCatalogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    [Fact]
    public async Task Every_report_runs_and_access_is_scoped()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Reports Catalog");
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "R", joiningDate = "2024-01-01", status = "Active", managerId = manager, gender = "Female" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 90000, eobiApplicable = true, components = Array.Empty<object>() }, ApiFactory.Json));
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@reports.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Rep-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Rep-Pass-1234"));
        }
        var (lead, leadClient) = await Person("Lena", null, "Manager");
        await Person("Wali", lead, "Employee");
        await Person("Omar", null, "Employee");
        await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = Today.Year, month = Today.Month }));

        var list = (await Ok(await admin.GetAsync("/api/reports"))).EnumerateArray().ToList();
        Assert.True(list.Count >= 18);
        var body = new { from = Today.AddDays(-30).ToString("yyyy-MM-dd"), to = Today.ToString("yyyy-MM-dd"), year = Today.Year, month = Today.Month };
        foreach (var r in list)
        {
            var result = await Ok(await admin.PostAsJsonAsync($"/api/reports/{r.GetProperty("key").GetString()}", body));
            Assert.Equal(result.GetProperty("headers").GetArrayLength(), result.GetProperty("numeric").GetArrayLength());
        }

        var sheet = await Ok(await admin.PostAsJsonAsync("/api/reports/payroll.sheet", body));
        Assert.Equal("Total", sheet.GetProperty("totals")[0].GetString());
        Assert.True(sheet.GetProperty("rows").GetArrayLength() >= 3);

        // Managers: no pay reports, and only their own team.
        Assert.DoesNotContain((await Ok(await leadClient.GetAsync("/api/reports"))).EnumerateArray(), r => r.GetProperty("key").GetString()!.StartsWith("payroll"));
        Assert.Equal(HttpStatusCode.Forbidden, (await leadClient.PostAsJsonAsync("/api/reports/payroll.sheet", body)).StatusCode);
        var team = await Ok(await leadClient.PostAsJsonAsync("/api/reports/employees.list", body));
        Assert.Equal("Wali R", Assert.Single(team.GetProperty("rows").EnumerateArray())[1].GetString());

        var excel = await admin.PostAsJsonAsync("/api/reports/attendance.register/excel", body);
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);

        await Ok(await admin.PutAsJsonAsync("/api/reports/settings", new { signatures = new[] { "Prepared by", "HR Manager", "" } }));
        Assert.Equal(2, (await Ok(await leadClient.GetAsync("/api/reports/settings"))).GetProperty("signatures").GetArrayLength());
    }
}
