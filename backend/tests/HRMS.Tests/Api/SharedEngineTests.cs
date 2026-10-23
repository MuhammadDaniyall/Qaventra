using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Requests on someone's behalf, access templates, suggestions and change history.</summary>
public class SharedEngineTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Func<string, Guid?, string, Task<(Guid Id, Guid UserId, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, Guid, HttpClient)> Make(string first, Guid? manager, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "E", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@engine.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Engine-Pass-1234", role }));
            var userId = (await Ok(await admin.GetAsync($"/api/employees/{id}"))).GetProperty("userId").GetGuid();
            return (id, userId, await factory.SignInAsync(email, "Engine-Pass-1234"));
        }
        return (admin, Make);
    }

    [Fact]
    public async Task Managers_and_hr_file_requests_for_others()
    {
        var (admin, person) = await CompanyAsync("OnBehalf");
        var (lead, _, leadClient) = await person("Lena", null, "Manager");
        var (worker, _, _) = await person("Wali", lead, "Employee");
        var (other, _, otherClient) = await person("Omar", null, "Employee");
        var type = (await Ok(await admin.GetAsync("/api/setup/leave-types"))).EnumerateArray().First(t => t.GetProperty("code").GetString() == "CL").GetProperty("id").GetGuid();
        var day = Today.AddDays(10);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);

        // A manager files for their report; not for someone else's.
        var leave = await Ok(await leadClient.PostAsJsonAsync("/api/leave", new { leaveTypeId = type, fromDate = Iso(day), toDate = Iso(day), reason = "Sick", employeeId = worker }, ApiFactory.Json));
        Assert.Equal(worker, leave.GetProperty("employeeId").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await leadClient.PostAsJsonAsync("/api/leave", new { leaveTypeId = type, fromDate = Iso(day), toDate = Iso(day), employeeId = other }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.PostAsJsonAsync("/api/attendance/requests", new { date = Iso(Today.AddDays(-1)), kind = "MissedPunch", checkIn = "09:00:00", checkOut = "17:00:00", reason = "x", employeeId = worker }, ApiFactory.Json)).StatusCode);
        var timeline = await Ok(await admin.GetAsync($"/api/approvals/Leave/{leave.GetProperty("id").GetGuid()}"));
        Assert.Contains("Lena", timeline.GetProperty("filedBy").GetString());

        // HR files an expense claim for anyone.
        var claim = await Ok(await admin.PostAsJsonAsync("/api/expenses", new
        {
            title = "Taxi", employeeId = other, items = new[] { new { date = Iso(Today.AddDays(-2)), category = "Travel", description = "Taxi", amount = 1500 } },
        }, ApiFactory.Json));
        Assert.Equal(other, claim.GetProperty("employeeId").GetGuid());
    }

    [Fact]
    public async Task Access_templates_limit_hr_and_open_modules_to_others()
    {
        var (admin, person) = await CompanyAsync("Access");
        var (_, recruiterUser, recruiter) = await person("Rida", null, "HrManager");
        var (_, clerkUser, clerk) = await person("Kamal", null, "Employee");
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync("/api/payroll/runs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/api/payroll/runs")).StatusCode);

        await Ok(await admin.PostAsJsonAsync("/api/access/templates", new { name = "Recruiter", modules = new[] { "recruitment", "onboarding" } }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/access/templates", new { name = "Payroll clerk", modules = new[] { "payroll" } }, ApiFactory.Json));
        var templates = (await Ok(await admin.GetAsync("/api/access/templates"))).EnumerateArray().ToList();
        Guid T(string n) => templates.First(t => t.GetProperty("name").GetString() == n).GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/access/users/{recruiterUser}", new { accessTemplateId = T("Recruiter") }, ApiFactory.Json));
        await Ok(await admin.PutAsJsonAsync($"/api/access/users/{clerkUser}", new { accessTemplateId = T("Payroll clerk") }, ApiFactory.Json));

        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync("/api/payroll/runs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync("/api/onboarding/templates")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clerk.GetAsync("/api/payroll/runs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/api/employees")).StatusCode);
        var me = await Ok(await clerk.GetAsync("/api/auth/me"));
        Assert.Equal("payroll", me.GetProperty("modules")[0].GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/access/templates/{T("Recruiter")}")).StatusCode);
    }

    [Fact]
    public async Task Suggestions_reach_reviewers_and_stay_anonymous()
    {
        var (admin, person) = await CompanyAsync("Ideas");
        var (reviewer, _, reviewerClient) = await person("Rafay", null, "Employee");
        var (_, _, author) = await person("Asma", null, "Employee");
        await Ok(await admin.PostAsJsonAsync("/api/suggestions/categories", new { name = "Canteen", reviewerIds = new[] { reviewer } }, ApiFactory.Json));
        var category = (await Ok(await author.GetAsync("/api/suggestions/categories")))[0].GetProperty("id").GetGuid();
        await Ok(await author.PostAsJsonAsync("/api/suggestions", new { title = "Better tea", details = "Green tea please", categoryId = category, isAnonymous = true }, ApiFactory.Json));
        Assert.True((await Ok(await reviewerClient.GetAsync("/api/suggestions/can-review"))).GetBoolean());
        var item = (await Ok(await reviewerClient.GetAsync("/api/suggestions/review"))).EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, item.GetProperty("from").ValueKind);
        await Ok(await reviewerClient.PutAsJsonAsync($"/api/suggestions/{item.GetProperty("id").GetGuid()}/respond", new { status = "Accepted", response = "From Monday" }, ApiFactory.Json));
        var mine = (await Ok(await author.GetAsync("/api/suggestions/my"))).EnumerateArray().Single();
        Assert.Equal("Accepted", mine.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await author.GetAsync("/api/suggestions/review")).StatusCode);

        // Change history of a record.
        var dept = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Ops" }))).GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/departments/{dept}", new { name = "Operations" }));
        var history = await Ok(await admin.GetAsync($"/api/history/Department/{dept}"));
        Assert.True(history.GetArrayLength() >= 2);
    }
}
