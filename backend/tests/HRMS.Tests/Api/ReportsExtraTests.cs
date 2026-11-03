using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Loan, salary revision, separation, hiring and helpdesk reports run and show what was recorded.</summary>
public class ReportsExtraTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task New_reports_run()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Reports2");
        var id = (await (await admin.PostAsJsonAsync("/api/employees", new { firstName = "Rafi", lastName = "R", joiningDate = "2023-01-01", status = "Active" }, ApiFactory.Json))
            .Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2023-01-01", basicSalary = 100000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json))
            .EnsureSuccessStatusCode();
        var next = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1);
        (await admin.PostAsJsonAsync("/api/pay-changes/revisions", new
        {
            effectiveFrom = next.ToString("yyyy-MM-dd"), kind = "Increment", includeAllowances = false, reason = "Review",
            lines = new[] { new { employeeId = id, percent = 10m } },
        }, ApiFactory.Json)).EnsureSuccessStatusCode();

        foreach (var key in new[] { "payroll.loans", "payroll.increments", "employees.separations", "recruitment.pipeline", "helpdesk.tickets", "payroll.overtime", "attendance.summary" })
        {
            var body = key == "payroll.increments" ? (object)new { from = next.ToString("yyyy-MM-dd"), to = next.AddDays(5).ToString("yyyy-MM-dd") } : new { };
            var res = await admin.PostAsJsonAsync($"/api/reports/{key}", body, ApiFactory.Json);
            Assert.True(res.IsSuccessStatusCode, $"{key}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
            if (key == "payroll.increments")
            {
                var row = Assert.Single((await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json)).GetProperty("rows").EnumerateArray());
                Assert.Equal("10.0%", row[7].GetString());
            }
        }
    }
}
