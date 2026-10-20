using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Calls every read endpoint and the main write flows as each role, so no screen in the app hits a server error.</summary>
public class SmokeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static async Task NoServerError(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        Assert.True((int)res.StatusCode < 500, $"GET {url} -> {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Every_screen_loads_for_every_role()
    {
        var (admin, _, companyId) = await factory.CreateCompanyAsync("Sigma");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

        // Setup data so lists are not empty.
        var dept = await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Engineering", code = "ENG" }));
        var desig = await Ok(await admin.PostAsJsonAsync("/api/designations", new { title = "Software Engineer" }));
        var shift = await Ok(await admin.PostAsJsonAsync("/api/setup/shifts", new
        {
            name = "Morning", startTime = "08:00:00", endTime = "16:00:00", graceMinutes = 10,
            weeklyOffDays = new[] { "Sunday" }, isDefault = false,
        }, ApiFactory.Json));
        var holiday = await Ok(await admin.PostAsJsonAsync("/api/setup/holidays", new { date = $"{today.Year}-12-25", name = "Quaid-e-Azam Day" }));
        await Ok(await admin.PutAsJsonAsync($"/api/setup/holidays/{holiday.GetProperty("id").GetGuid()}", new { date = $"{today.Year}-12-25", name = "Quaid Day" }));

        var manager = await Ok(await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Faisal", lastName = "Test", joiningDate = "2026-01-01", status = "Active",
            departmentId = dept.GetProperty("id").GetGuid(), designationId = desig.GetProperty("id").GetGuid(),
        }, ApiFactory.Json));
        var worker = await Ok(await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Amna", lastName = "Test", joiningDate = "2026-01-01", status = "Active", cnic = "35202-1234567-1",
            departmentId = dept.GetProperty("id").GetGuid(), designationId = desig.GetProperty("id").GetGuid(),
            managerId = manager.GetProperty("id").GetGuid(), shiftId = shift.GetProperty("id").GetGuid(),
        }, ApiFactory.Json));
        var workerId = worker.GetProperty("id").GetGuid();

        var mgrEmail = $"faisal+{Guid.NewGuid():N}@sigma.test";
        var workerEmail = $"amna+{Guid.NewGuid():N}@sigma.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{manager.GetProperty("id").GetGuid()}/login", new { email = mgrEmail, password = "Faisal-Pass-1", role = "Manager" }));
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{workerId}/login", new { email = workerEmail, password = "Amna-Pass-1", role = "Employee" }));
        var mgr = await factory.SignInAsync(mgrEmail, "Faisal-Pass-1");
        var emp = await factory.SignInAsync(workerEmail, "Amna-Pass-1");

        // Attendance: no office locations yet, so no location is required.
        await Ok(await emp.PostAsJsonAsync("/api/attendance/check-in", new { source = "Web" }));
        await Ok(await emp.PostAsJsonAsync("/api/attendance/check-out", new { source = "Web" }));
        await Ok(await admin.PostAsJsonAsync("/api/attendance/manual", new { employeeId = workerId, date = today.AddDays(-1).ToString("yyyy-MM-dd"), checkIn = "09:05:00", checkOut = "17:00:00" }));
        await Ok(await admin.PostAsJsonAsync("/api/setup/office-locations", new { name = "Head Office", latitude = 31.5204, longitude = 74.3587, radiusMeters = 200 }));

        await Ok(await admin.PutAsJsonAsync($"/api/employees/{workerId}/salary", new
        {
            effectiveFrom = "2026-01-01", basicSalary = 80_000, eobiApplicable = true,
            components = new object[] { new { name = "House Rent", kind = "Earning", amount = 36_000, isTaxable = true } },
        }, ApiFactory.Json));
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = today.Year, month = today.Month }));
        var runId = run.GetProperty("run").GetProperty("id").GetGuid();
        await Ok(await admin.PostAsync($"/api/payroll/runs/{runId}/finalize", null));
        var slipId = run.GetProperty("payslips").EnumerateArray().First(p => p.GetProperty("employeeId").GetGuid() == workerId).GetProperty("id").GetGuid();

        var y = today.Year;
        var m = today.Month;
        string[] common =
        [
            "/api/auth/me", "/api/dashboard", "/api/departments", "/api/designations",
            "/api/setup/shifts", "/api/setup/office-locations", $"/api/setup/holidays?year={y}", "/api/setup/leave-types",
            "/api/attendance/today", $"/api/attendance/my?year={y}&month={m}",
            "/api/leave/balances", "/api/leave/my", $"/api/leave/count-days?from={y}-11-02&to={y}-11-06",
            "/api/employees/me", "/api/payroll/my-payslips", $"/api/payroll/payslips/{slipId}",
        ];
        string[] approver =
        [
            "/api/employees", "/api/employees/lookup", $"/api/employees/{workerId}",
            "/api/attendance/daily", $"/api/attendance/daily?date={today.AddDays(-1):yyyy-MM-dd}",
            "/api/leave/approvals", "/api/leave/approvals?status=Approved",
        ];
        string[] hr =
        [
            $"/api/employees/{workerId}/salary", "/api/payroll/runs", $"/api/payroll/runs/{runId}", "/api/payroll/settings",
            "/api/audit-logs", "/api/audit-logs?category=Data&pageSize=10",
        ];

        foreach (var url in common.Concat(approver).Concat(hr)) await NoServerError(admin, url);
        foreach (var url in common.Concat(approver)) await NoServerError(mgr, url);
        foreach (var url in common) await NoServerError(emp, url);

        // The employee really sees their own finalized payslip and today's punch.
        Assert.Equal(1, (await Ok(await emp.GetAsync("/api/payroll/my-payslips"))).GetArrayLength());
        Assert.NotEqual(JsonValueKind.Null, (await Ok(await emp.GetAsync("/api/attendance/today"))).GetProperty("record").ValueKind);

        // Owner: platform screens, then the same company screens from inside the company.
        var owner = await factory.SignInAsOwnerAsync();
        foreach (var url in new[] { "/api/platform/stats", "/api/platform/companies", $"/api/platform/companies/{companyId}", $"/api/platform/companies/{companyId}/payments", "/api/audit-logs" })
            await NoServerError(owner, url);
        var session = await Ok(await owner.PostAsync($"/api/platform/companies/{companyId}/enter", null));
        var inside = factory.CreateClient();
        inside.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("token").GetString());
        foreach (var url in approver.Concat(hr).Concat(["/api/dashboard", "/api/departments", "/api/setup/shifts", $"/api/payroll/payslips/{slipId}"]))
            await NoServerError(inside, url);
    }
}
