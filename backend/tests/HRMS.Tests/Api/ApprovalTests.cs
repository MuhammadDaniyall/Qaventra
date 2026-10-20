using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Configurable approval chains for leave and expense claims.</summary>
public class ApprovalTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private record Org(HttpClient Admin, HttpClient Lead, HttpClient Director, HttpClient Worker, Guid DirectorId, Guid WorkerId);

    /// <summary>Worker reports to Lead, who reports to Director. Lead and Director are Managers.</summary>
    private async Task<Org> CreateOrgAsync(string company)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(company);
        async Task<Guid> Employee(string first, Guid? managerId) =>
            (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = company, joiningDate = "2025-01-01", status = "Active", managerId }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        async Task<HttpClient> Login(Guid id, string name, string role)
        {
            var email = $"{name}+{Guid.NewGuid():N}@{company.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Approval-Pass-1", role }));
            return await factory.SignInAsync(email, "Approval-Pass-1");
        }

        var director = await Employee("Dana", null);
        var lead = await Employee("Lena", director);
        var worker = await Employee("Wali", lead);
        return new Org(admin, await Login(lead, "lena", "Manager"), await Login(director, "dana", "Manager"), await Login(worker, "wali", "Employee"), director, worker);
    }

    private static async Task<Guid> ApplyLeaveAsync(HttpClient worker, int daysAhead = 14)
    {
        var types = await Ok(await worker.GetAsync("/api/setup/leave-types"));
        var annual = types.EnumerateArray().First(t => t.GetProperty("isPaid").GetBoolean() && t.GetProperty("annualQuota").GetDecimal() >= 2);
        var day = Today.AddDays(daysAhead);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        var leave = await Ok(await worker.PostAsJsonAsync("/api/leave", new
        {
            leaveTypeId = annual.GetProperty("id").GetGuid(), fromDate = day.ToString("yyyy-MM-dd"), toDate = day.ToString("yyyy-MM-dd"), reason = "Family event",
        }));
        return leave.GetProperty("id").GetGuid();
    }

    private static async Task<string> LeaveStatusAsync(HttpClient worker, Guid id) =>
        (await Ok(await worker.GetAsync("/api/leave/my"))).EnumerateArray().First(r => r.GetProperty("id").GetGuid() == id).GetProperty("status").GetString()!;

    [Fact]
    public async Task Default_chain_is_the_line_manager_only()
    {
        var o = await CreateOrgAsync("Default");
        var id = await ApplyLeaveAsync(o.Worker);

        Assert.Equal(HttpStatusCode.Forbidden, (await o.Director.PostAsJsonAsync($"/api/leave/{id}/approve", new { })).StatusCode);
        Assert.DoesNotContain((await Ok(await o.Director.GetAsync("/api/leave/approvals"))).EnumerateArray(), r => r.GetProperty("id").GetGuid() == id);
        Assert.Contains((await Ok(await o.Lead.GetAsync("/api/approvals/inbox"))).EnumerateArray(), r => r.GetProperty("subjectId").GetGuid() == id);

        await Ok(await o.Lead.PostAsJsonAsync($"/api/leave/{id}/approve", new { note = "Enjoy" }));
        Assert.Equal("Approved", await LeaveStatusAsync(o.Worker, id));
    }

    [Fact]
    public async Task Multi_level_chain_moves_step_by_step_and_rejection_stops_it()
    {
        var o = await CreateOrgAsync("Chain");
        await Ok(await o.Admin.PutAsJsonAsync("/api/approvals/flows/Leave", new
        {
            steps = new object[] { new { kind = "LineManager" }, new { kind = "SecondLevelManager" }, new { kind = "Hr" } },
        }, ApiFactory.Json));
        var flows = await Ok(await o.Admin.GetAsync("/api/approvals/flows"));
        Assert.Equal(3, flows.EnumerateArray().First(f => f.GetProperty("requestType").GetString() == "Leave").GetProperty("steps").GetArrayLength());

        var id = await ApplyLeaveAsync(o.Worker);
        await Ok(await o.Lead.PostAsJsonAsync($"/api/leave/{id}/approve", new { }));
        Assert.Equal("Pending", await LeaveStatusAsync(o.Worker, id));
        // Step 2 belongs to the director; the lead cannot act on it.
        Assert.Equal(HttpStatusCode.Forbidden, (await o.Lead.PostAsJsonAsync($"/api/leave/{id}/approve", new { })).StatusCode);

        var inbox = (await Ok(await o.Director.GetAsync("/api/approvals/inbox"))).EnumerateArray().Single(r => r.GetProperty("subjectId").GetGuid() == id);
        Assert.Equal(2, inbox.GetProperty("step").GetInt32());
        Assert.Equal(3, inbox.GetProperty("totalSteps").GetInt32());
        await Ok(await o.Director.PostAsJsonAsync($"/api/leave/{id}/approve", new { note = "Fine by me" }));
        Assert.Equal("Pending", await LeaveStatusAsync(o.Worker, id));

        await Ok(await o.Admin.PostAsJsonAsync($"/api/leave/{id}/approve", new { }));
        Assert.Equal("Approved", await LeaveStatusAsync(o.Worker, id));

        var timeline = await Ok(await o.Worker.GetAsync($"/api/approvals/Leave/{id}"));
        Assert.Equal("Approved", timeline.GetProperty("status").GetString());
        Assert.All(timeline.GetProperty("steps").EnumerateArray(), s => Assert.Equal("Approved", s.GetProperty("status").GetString()));
        Assert.Equal("Fine by me", timeline.GetProperty("steps")[1].GetProperty("note").GetString());

        // A rejection at the first step ends the chain.
        var second = await ApplyLeaveAsync(o.Worker, daysAhead: 21);
        await Ok(await o.Lead.PostAsJsonAsync($"/api/leave/{second}/reject", new { note = "Busy week" }));
        Assert.Equal("Rejected", await LeaveStatusAsync(o.Worker, second));
        var rejected = await Ok(await o.Worker.GetAsync($"/api/approvals/Leave/{second}"));
        Assert.Equal(["Rejected", "Skipped", "Skipped"], rejected.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Specific_person_and_automatic_approval()
    {
        var o = await CreateOrgAsync("Rules");
        await Ok(await o.Admin.PutAsJsonAsync("/api/approvals/flows/Expense", new { steps = new object[] { new { kind = "SpecificEmployee", employeeId = o.DirectorId } } }, ApiFactory.Json));
        var claim = await Ok(await o.Worker.PostAsJsonAsync("/api/expenses", new
        {
            title = "Taxi", items = new object[] { new { date = Today.ToString("yyyy-MM-dd"), category = "Travel", description = "Airport", amount = 900 } },
        }, ApiFactory.Json));
        var claimId = claim.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await o.Lead.PostAsJsonAsync($"/api/expenses/{claimId}/approve", new { })).StatusCode);
        await Ok(await o.Director.PostAsJsonAsync($"/api/expenses/{claimId}/approve", new { }));

        // No steps: approved the moment it is submitted.
        await Ok(await o.Admin.PutAsJsonAsync("/api/approvals/flows/Expense", new { steps = Array.Empty<object>() }, ApiFactory.Json));
        var auto = await Ok(await o.Worker.PostAsJsonAsync("/api/expenses", new
        {
            title = "Tea", items = new object[] { new { date = Today.ToString("yyyy-MM-dd"), category = "Meals", description = "Client tea", amount = 300 } },
        }, ApiFactory.Json));
        Assert.Equal("Approved", auto.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await o.Admin.PutAsJsonAsync("/api/approvals/flows/Expense", new { steps = new object[] { new { kind = "SpecificEmployee" } } }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await o.Lead.GetAsync("/api/approvals/flows")).StatusCode);
    }
}
