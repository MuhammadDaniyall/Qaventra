using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Leave reasons, sandwich counting, document rule, opening balances, and attendance exemptions.</summary>
public class LeaveAttendanceRulesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Guid Worker, HttpClient WorkerClient, HttpClient Lead)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "L", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@rules.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Rule-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Rule-Pass-1234"));
        }
        var (lead, leadClient) = await Person("Lena", null, "Manager");
        var (worker, workerClient) = await Person("Wali", lead, "Employee");
        return (admin, worker, workerClient, leadClient);
    }

    private static async Task<JsonElement> TypeAsync(HttpClient admin, string code) =>
        (await Ok(await admin.GetAsync("/api/setup/leave-types"))).EnumerateArray().First(t => t.GetProperty("code").GetString() == code);

    [Fact]
    public async Task Reasons_sandwich_and_documents_shape_leave()
    {
        var (admin, worker, workerClient, lead) = await CompanyAsync("LeaveRules");
        await Ok(await admin.PostAsJsonAsync("/api/setup/leave-types", new
        {
            name = "Medical", code = "MED", annualQuota = 30, isPaid = true, isActive = true, accrual = "Upfront", sandwich = true, documentAfterDays = 2,
        }, ApiFactory.Json));
        var med = (await TypeAsync(admin, "MED")).GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync("/api/leave-rules/reasons", new { leaveTypeId = med, name = "Fever", maxDays = 3, requiresDocument = false }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/leave-rules/reasons", new { leaveTypeId = med, name = "Surgery", requiresDocument = true }, ApiFactory.Json));
        var reasons = (await Ok(await workerClient.GetAsync("/api/leave-rules/reasons"))).EnumerateArray().ToList();
        Guid R(string n) => reasons.First(r => r.GetProperty("name").GetString() == n).GetProperty("id").GetGuid();

        // Next Monday to the Monday after: a sandwich counts every day in between.
        var monday = Today.AddDays(((int)DayOfWeek.Monday - (int)Today.DayOfWeek + 7) % 7 + 7);
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync("/api/leave", new { leaveTypeId = med, fromDate = Iso(monday), toDate = Iso(monday.AddDays(7)) }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync("/api/leave", new { leaveTypeId = med, fromDate = Iso(monday), toDate = Iso(monday.AddDays(7)), reasonId = R("Fever") }, ApiFactory.Json)).StatusCode);
        var leave = await Ok(await workerClient.PostAsJsonAsync("/api/leave", new { leaveTypeId = med, fromDate = Iso(monday), toDate = Iso(monday.AddDays(7)), reasonId = R("Surgery") }, ApiFactory.Json));
        Assert.Equal(8m, leave.GetProperty("days").GetDecimal());

        // More than 2 days needs a document before the manager can approve.
        var id = leave.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await lead.PostAsJsonAsync($"/api/leave/{id}/approve", new { }, ApiFactory.Json)).StatusCode);
        var file = new MultipartFormDataContent { { new ByteArrayContent("%PDF med"u8.ToArray()), "file", "certificate.pdf" } };
        await Ok(await workerClient.PostAsync($"/api/attachments/Leave/{id}", file));
        await Ok(await lead.PostAsJsonAsync($"/api/leave/{id}/approve", new { }, ApiFactory.Json));

        // Opening balances: bring 4 days forward.
        var rows = (await Ok(await admin.GetAsync($"/api/leave-rules/balances?year={Today.Year}"))).EnumerateArray().ToList();
        var row = rows.First(r => r.GetProperty("employeeId").GetGuid() == worker && r.GetProperty("leaveTypeId").GetGuid() == med);
        await Ok(await admin.PutAsJsonAsync("/api/leave-rules/balances", new { year = Today.Year, lines = new[] { new { employeeId = worker, leaveTypeId = med, carriedForward = 4m, adjustment = 0m } } }, ApiFactory.Json));
        var after = (await Ok(await admin.GetAsync($"/api/leave-rules/balances?year={Today.Year}"))).EnumerateArray()
            .First(r => r.GetProperty("employeeId").GetGuid() == worker && r.GetProperty("leaveTypeId").GetGuid() == med);
        Assert.Equal(row.GetProperty("remaining").GetDecimal() + 4, after.GetProperty("remaining").GetDecimal());
    }

    [Fact]
    public async Task A_late_arrival_is_excused_within_the_monthly_quota()
    {
        var (admin, worker, workerClient, lead) = await CompanyAsync("Exempt");
        await Ok(await admin.PutAsJsonAsync("/api/exemptions/settings", new { exemptionsPerMonth = 1, exemptionWithinDays = 7 }, ApiFactory.Json));
        var day = Today.AddDays(-1).Month == Today.Month ? Today.AddDays(-1) : Today;
        var other = day.AddDays(-1).Month == day.Month ? day.AddDays(-1) : day.AddDays(1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var tenant = await db.Employees.IgnoreQueryFilters().Where(e => e.Id == worker).Select(e => e.TenantId).FirstAsync();
            foreach (var d in new[] { day, other })
                db.AttendanceRecords.Add(new HRMS.Domain.Entities.AttendanceRecord
                {
                    TenantId = tenant, EmployeeId = worker, Date = d, CheckInUtc = d.ToDateTime(new TimeOnly(5, 40), DateTimeKind.Utc), Status = HRMS.Domain.AttendanceStatus.Late, WorkedMinutes = 400,
                });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync("/api/exemptions", new { date = Iso(Today.AddDays(-20)), reason = "Traffic" }, ApiFactory.Json)).StatusCode);
        await Ok(await workerClient.PostAsJsonAsync("/api/exemptions", new { date = Iso(day), reason = "Road blocked" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync("/api/exemptions", new { date = Iso(other), reason = "Again" }, ApiFactory.Json)).StatusCode);

        var waiting = Assert.Single((await Ok(await lead.GetAsync("/api/exemptions/approvals"))).EnumerateArray());
        Assert.Contains((await Ok(await lead.GetAsync("/api/approvals/inbox"))).EnumerateArray(), i => i.GetProperty("requestType").GetString() == "Exemption");
        await Ok(await lead.PostAsJsonAsync($"/api/exemptions/{waiting.GetProperty("id").GetGuid()}/approve", new { }, ApiFactory.Json));
        Assert.Equal("Approved", (await Ok(await workerClient.GetAsync("/api/exemptions/my")))[0].GetProperty("status").GetString());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var rec = await db.AttendanceRecords.IgnoreQueryFilters().FirstAsync(r => r.EmployeeId == worker && r.Date == day);
            Assert.Equal(HRMS.Domain.AttendanceStatus.Present, rec.Status);
        }
    }
}
