using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Deleting a company that has data in many modules removes all of it, whatever links the rows have to each other.</summary>
public class CompanyDeleteTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    [Fact]
    public async Task A_company_with_data_in_every_module_is_deleted()
    {
        var (admin, _, companyId) = await factory.CreateCompanyAsync("Wipe Co");
        await Ok(await admin.PostAsJsonAsync("/api/setup/shifts", new
        {
            name = "All day", startTime = "00:00:00", endTime = "23:59:00", graceMinutes = 0, weeklyOffDays = Array.Empty<string>(), isDefault = true, breakMinutes = 45,
        }, ApiFactory.Json));
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "W", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 40000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json));
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@wipe.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Wipe-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Wipe-Pass-1234"));
        }
        var (lead, leadClient) = await Person("Lena", null, "Manager");
        var (amna, amnaClient) = await Person("Amna", lead, "Employee");
        var (leaver, _) = await Person("Lutf", lead, "Employee");

        // Piece work paid in a finalized payroll run.
        await Ok(await admin.PostAsJsonAsync("/api/piece-work/items", new { name = "Collar stitching", unit = "piece", rate = 12, dailyTarget = 100 }, ApiFactory.Json));
        var item = Assert.Single((await Ok(await admin.GetAsync("/api/piece-work/items"))).EnumerateArray()).GetProperty("id").GetGuid();
        await Ok(await leadClient.PostAsJsonAsync("/api/piece-work/entries", new { itemId = item, date = Iso(Today), lines = new[] { new { employeeId = amna, quantity = 120m } } }, ApiFactory.Json));
        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = Today.Year, month = Today.Month }));
        await Ok(await admin.PostAsync($"/api/payroll/runs/{run.GetProperty("run").GetProperty("id").GetGuid()}/finalize", null));

        // Attendance with a break.
        await Ok(await amnaClient.PostAsJsonAsync("/api/attendance/check-in", new { source = "Web" }));
        await Ok(await amnaClient.PostAsJsonAsync("/api/attendance/break/start", new { kind = "Prayer" }, ApiFactory.Json));
        await Ok(await amnaClient.PostAsync("/api/attendance/break/end", null));

        // Helpdesk ticket with a comment.
        await Ok(await admin.PostAsJsonAsync("/api/helpdesk/categories", new { name = "IT support", targetHours = 8, agentIds = new[] { lead } }));
        var category = Assert.Single((await Ok(await amnaClient.GetAsync("/api/helpdesk/categories"))).EnumerateArray()).GetProperty("id").GetGuid();
        var ticket = await Ok(await amnaClient.PostAsJsonAsync("/api/helpdesk/tickets", new { categoryId = category, subject = "Laptop", description = "Will not charge." }, ApiFactory.Json));
        await Ok(await leadClient.PostAsJsonAsync($"/api/helpdesk/tickets/{ticket.GetProperty("id").GetGuid()}/comments", new { body = "Bring it over." }));

        // An asset handed to an employee.
        await Ok(await admin.PostAsJsonAsync("/api/assets/types", new { name = "Laptop", allowMultiple = false, returnOnExit = true, handlerIds = Array.Empty<Guid>() }));
        var type = Assert.Single((await Ok(await admin.GetAsync("/api/assets/types"))).EnumerateArray()).GetProperty("id").GetGuid();
        var asset = await Ok(await admin.PostAsJsonAsync("/api/assets", new { typeId = type, name = "ThinkPad", serialNumber = "SN-1", condition = "New" }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync($"/api/assets/{asset.GetProperty("id").GetGuid()}/assign", new { employeeId = amna, assignedOn = Iso(Today), condition = "New" }, ApiFactory.Json));

        // A project with a member and a task.
        var project = await Ok(await admin.PostAsJsonAsync("/api/projects", new
        {
            name = "Website", client = "ACME", budgetHours = 100, billable = true, openToAll = false, status = "Active",
            members = new[] { new { employeeId = amna, hourlyRate = 1000 } },
        }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync($"/api/projects/{project.GetProperty("id").GetGuid()}/tasks", new { name = "Design", assigneeEmployeeId = amna, estimatedHours = 10, isDone = false }, ApiFactory.Json));

        // A leave request on working days a few weeks out.
        var monday = Today.AddDays(21 + ((int)DayOfWeek.Monday - (int)Today.DayOfWeek + 7) % 7);
        var annual = (await Ok(await amnaClient.GetAsync("/api/setup/leave-types"))).EnumerateArray().First(t => t.GetProperty("code").GetString() == "AL").GetProperty("id").GetGuid();
        await Ok(await amnaClient.PostAsJsonAsync("/api/leave", new { leaveTypeId = annual, fromDate = Iso(monday), toDate = Iso(monday.AddDays(1)), reason = "Family" }, ApiFactory.Json));

        // A separation.
        await Ok(await admin.PostAsJsonAsync("/api/separations", new
        {
            employeeId = leaver, type = "Resignation", noticeDate = Iso(Today.AddDays(-30)), lastWorkingDay = Iso(Today.AddDays(-1)), noticePeriodDays = 30, reason = "Moving",
        }, ApiFactory.Json));

        // A training session with an enrolment.
        await Ok(await admin.PostAsJsonAsync("/api/training/programs", new { title = "Fire safety", mode = "Classroom", durationHours = 3, isMandatory = true, repeatEveryMonths = 12, completeWithinDays = 30 }, ApiFactory.Json));
        var program = Assert.Single((await Ok(await admin.GetAsync("/api/training/programs"))).EnumerateArray()).GetProperty("id").GetGuid();
        var start = DateTime.UtcNow.AddDays(3);
        var session = await Ok(await admin.PostAsJsonAsync("/api/training/sessions", new
        {
            programId = program, startsAtUtc = start, endsAtUtc = start.AddHours(3), venue = "Board room", trainerEmployeeId = lead, capacity = 5, cost = 1000,
        }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync($"/api/training/sessions/{session.GetProperty("id").GetGuid()}/enroll", new { employeeIds = new[] { amna } }, ApiFactory.Json));

        // A biometric device.
        await Ok(await admin.PostAsJsonAsync("/api/biometric/devices", new { name = "Gate 1", serialNumber = "ZK-01" }, ApiFactory.Json));

        var owner = await factory.SignInAsOwnerAsync();
        var name = (await Ok(await owner.GetAsync($"/api/platform/companies/{companyId}"))).GetProperty("name").GetString()!;
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/platform/companies/{companyId}") { Content = JsonContent.Create(new { confirmName = name }) };
        var res = await owner.SendAsync(delete);
        Assert.True(res.StatusCode == HttpStatusCode.NoContent, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/platform/companies/{companyId}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
        Assert.False(await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.TenantId == companyId));
        Assert.False(await db.Tenants.AnyAsync(t => t.Id == companyId));
    }
}
