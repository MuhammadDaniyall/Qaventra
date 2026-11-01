using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Added/Modified stamps on every record, and the employee name and code on every audit entry.</summary>
public class RecordStampTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static DateOnly NextWorkday(int days)
    {
        var day = Today.AddDays(days);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);
        return day;
    }

    private record Person(Guid Id, Guid UserId, string Display, string Code, HttpClient Client);

    private async Task<(HttpClient Admin, Guid CompanyId, Func<string, string, Task<Person>> Hire)> CompanyAsync(string name)
    {
        var (admin, _, companyId) = await factory.CreateCompanyAsync(name);
        async Task<Person> Hire(string first, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "Stamp", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@stamps.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Stamp-Pass-1234", role }));
            var employee = await Ok(await admin.GetAsync($"/api/employees/{id}"));
            var code = employee.GetProperty("employeeCode").GetString()!;
            return new Person(id, employee.GetProperty("userId").GetGuid(), $"{first} Stamp ({code})", code, await factory.SignInAsync(email, "Stamp-Pass-1234"));
        }
        return (admin, companyId, Hire);
    }

    private async Task<Guid> CasualLeaveAsync(HttpClient admin) =>
        (await Ok(await admin.GetAsync("/api/setup/leave-types"))).EnumerateArray().First(t => t.GetProperty("code").GetString() == "CL").GetProperty("id").GetGuid();

    private async Task<T> DbAsync<T>(Func<HrmsDbContext, Task<T>> read)
    {
        using var scope = factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<HrmsDbContext>());
    }

    [Fact]
    public async Task Leave_is_stamped_by_the_employee_who_applied_and_the_hr_who_approved()
    {
        var (admin, _, hire) = await CompanyAsync("Stamps");
        var worker = await hire("Ayesha", "Employee");
        var hr = await hire("Hamid", "HrManager");
        var type = await CasualLeaveAsync(admin);
        var day = NextWorkday(12);

        var applied = await Ok(await worker.Client.PostAsJsonAsync("/api/leave", new { leaveTypeId = type, fromDate = Iso(day), toDate = Iso(day), reason = "Family" }, ApiFactory.Json));
        var stamps = applied.GetProperty("stamps");
        Assert.Equal(worker.Display, stamps.GetProperty("createdBy").GetString());
        Assert.Equal(worker.Display, stamps.GetProperty("modifiedBy").GetString());
        Assert.Equal(stamps.GetProperty("createdAtUtc").GetDateTime(), stamps.GetProperty("modifiedAtUtc").GetDateTime());
        var id = applied.GetProperty("id").GetGuid();

        await Ok(await hr.Client.PostAsJsonAsync($"/api/leave/{id}/approve", new { note = "Enjoy" }));
        var approved = (await Ok(await hr.Client.GetAsync("/api/leave/approvals?status=Approved"))).EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == id);
        stamps = approved.GetProperty("stamps");
        Assert.Equal(worker.Display, stamps.GetProperty("createdBy").GetString());
        Assert.Equal(hr.Display, stamps.GetProperty("modifiedBy").GetString());
        Assert.True(stamps.GetProperty("modifiedAtUtc").GetDateTime() > stamps.GetProperty("createdAtUtc").GetDateTime());

        var row = await DbAsync(db => db.LeaveRequests.IgnoreQueryFilters().SingleAsync(r => r.Id == id));
        Assert.Equal(worker.UserId, row.CreatedById);
        Assert.Equal(hr.UserId, row.ModifiedById);

        // The Data log keeps the change itself; the stamp columns are not repeated in it.
        var logs = await Ok(await admin.GetAsync($"/api/audit-logs?category=Data&entityType=LeaveRequest&pageSize=50"));
        Assert.All(logs.GetProperty("items").EnumerateArray(), l => Assert.DoesNotContain("ModifiedBy", l.GetProperty("changes").GetString()));
    }

    [Fact]
    public async Task Owner_inside_a_company_is_stamped_as_the_owner_and_background_work_as_system()
    {
        var (_, companyId, hire) = await CompanyAsync("OwnerStamps");
        var worker = await hire("Bilal", "Employee");
        var owner = await factory.SignInAsOwnerAsync();
        var session = await Ok(await owner.PostAsync($"/api/platform/companies/{companyId}/enter", null));
        var inside = factory.CreateClient();
        inside.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("token").GetString());

        // HR rights inside the company: the owner files leave for an employee.
        var type = await CasualLeaveAsync(inside);
        var day = NextWorkday(15);
        var filed = await Ok(await inside.PostAsJsonAsync("/api/leave", new { leaveTypeId = type, fromDate = Iso(day), toDate = Iso(day), reason = "Filed by owner", employeeId = worker.Id }, ApiFactory.Json));
        Assert.Equal("Super Admin (Owner)", filed.GetProperty("stamps").GetProperty("createdBy").GetString());

        // A context with nobody signed in (background jobs, startup) stamps "System".
        Guid deptId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            db.UseTenant(companyId);
            var dept = new Department { Name = "Night shift" };
            db.Departments.Add(dept);
            await db.SaveChangesAsync();
            deptId = dept.Id;
            Assert.Equal(HrmsDbContext.SystemActor, dept.CreatedBy);
            Assert.Null(dept.CreatedById);
        }

        // Someone with an employee record changes it later: only Modified moves.
        var hr = await hire("Huma", "HrManager");
        await Ok(await hr.Client.PutAsJsonAsync($"/api/departments/{deptId}", new { name = "Night operations" }));
        var row = await DbAsync(db => db.Departments.IgnoreQueryFilters().SingleAsync(d => d.Id == deptId));
        Assert.Equal(HrmsDbContext.SystemActor, row.CreatedBy);
        Assert.Equal(hr.Display, row.ModifiedBy);
        Assert.Equal(hr.UserId, row.ModifiedById);
        Assert.True(row.ModifiedAtUtc > row.CreatedAtUtc);
    }

    [Fact]
    public async Task Every_audit_entry_names_the_employee_and_can_be_searched_by_name_or_code()
    {
        var (admin, _, hire) = await CompanyAsync("AuditNames");
        var worker = await hire("Zainab", "Employee");
        var type = await CasualLeaveAsync(admin);
        var day = NextWorkday(20);
        await Ok(await worker.Client.PostAsJsonAsync("/api/leave", new { leaveTypeId = type, fromDate = Iso(day), toDate = Iso(day), reason = "Rest" }, ApiFactory.Json));

        var byCode = (await Ok(await admin.GetAsync($"/api/audit-logs?employee={worker.Code}&pageSize=200"))).GetProperty("items").EnumerateArray().ToList();
        Assert.All(byCode, l =>
        {
            Assert.Equal("Zainab Stamp", l.GetProperty("employeeName").GetString());
            Assert.Equal(worker.Code, l.GetProperty("employeeCode").GetString());
            Assert.Equal(worker.Id, l.GetProperty("employeeId").GetGuid());
        });
        // Sign-in, the API calls and the leave row itself.
        Assert.Contains(byCode, l => l.GetProperty("category").GetString() == "Auth" && l.GetProperty("action").GetString() == "LoginSucceeded");
        Assert.Contains(byCode, l => l.GetProperty("category").GetString() == "Request" && l.GetProperty("path").GetString() == "/api/leave");
        Assert.Contains(byCode, l => l.GetProperty("category").GetString() == "Data" && l.GetProperty("entityType").GetString() == "LeaveRequest");

        var byName = (await Ok(await admin.GetAsync("/api/audit-logs?employee=Zainab&pageSize=200"))).GetProperty("items").EnumerateArray();
        Assert.Equal(byCode.Count, byName.Count());

        // The record's change history names them too.
        var leave = byCode.First(l => l.GetProperty("entityType").GetString() == "LeaveRequest");
        var history = await Ok(await admin.GetAsync($"/api/history/LeaveRequest/{leave.GetProperty("entityId").GetString()}"));
        Assert.Equal(worker.Code, history[0].GetProperty("employeeCode").GetString());

        // The owner has no employee record, so their entries carry none.
        var owner = await factory.SignInAsOwnerAsync();
        await Ok(await owner.GetAsync("/api/platform/companies"));
        var ownerLogs = (await Ok(await owner.GetAsync("/api/audit-logs?user=" + Uri.EscapeDataString(ApiFactory.SuperAdminEmail) + "&pageSize=20"))).GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(ownerLogs);
        Assert.All(ownerLogs, l => Assert.Equal(JsonValueKind.Null, l.GetProperty("employeeName").ValueKind));
    }

    [Fact]
    public async Task Anonymous_suggestions_and_poll_votes_still_name_nobody()
    {
        var (admin, _, hire) = await CompanyAsync("AnonStamps");
        var author = await hire("Sana", "Employee");
        var suggestion = await Ok(await author.Client.PostAsJsonAsync("/api/suggestions", new { title = "Quiet room", details = "Please", isAnonymous = true }, ApiFactory.Json));

        var created = (await Ok(await admin.GetAsync("/api/audit-logs?entityType=Suggestion&action=Created"))).GetProperty("items").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, created.GetProperty("employeeName").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("employeeCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("employeeId").ValueKind);
        Assert.DoesNotContain("Sana", created.GetProperty("changes").GetString());
        var sent = (await Ok(await admin.GetAsync("/api/audit-logs?category=Request&action=POST&pageSize=200"))).GetProperty("items").EnumerateArray()
            .Single(l => l.GetProperty("path").GetString() == "/api/suggestions");
        Assert.Equal(JsonValueKind.Null, sent.GetProperty("employeeName").ValueKind);
        Assert.Empty((await Ok(await admin.GetAsync($"/api/audit-logs?employee={author.Code}&entityType=Suggestion"))).GetProperty("items").EnumerateArray());

        var suggestionId = suggestion.ValueKind == JsonValueKind.Object && suggestion.TryGetProperty("id", out var sid)
            ? sid.GetGuid()
            : await DbAsync(db => db.Suggestions.IgnoreQueryFilters().Where(s => s.EmployeeId == author.Id).Select(s => s.Id).SingleAsync());
        var row = await DbAsync(db => db.Suggestions.IgnoreQueryFilters().SingleAsync(s => s.Id == suggestionId));
        Assert.Equal(HrmsDbContext.AnonymousActor, row.CreatedBy);
        Assert.Equal(HrmsDbContext.AnonymousActor, row.ModifiedBy);
        Assert.Null(row.CreatedById);
        Assert.Null(row.ModifiedById);

        // Anonymous poll: the vote row keeps no name, and no audit entry mentions it.
        var poll = await Ok(await admin.PostAsJsonAsync("/api/announcements/polls", new
        {
            question = "Friday off?", opensOn = Iso(Today), closesOn = Iso(Today.AddDays(3)), isAnonymous = true,
            resultsVisibility = "AfterVoting", options = new[] { "Yes", "No" },
        }, ApiFactory.Json));
        var pollId = poll.GetProperty("id").GetGuid();
        var option = poll.GetProperty("options")[0].GetProperty("id").GetGuid();
        await Ok(await author.Client.PostAsJsonAsync($"/api/announcements/polls/{pollId}/vote", new { optionId = option }));
        var vote = await DbAsync(db => db.PollVotes.IgnoreQueryFilters().SingleAsync(v => v.PollId == pollId));
        Assert.Null(vote.CreatedBy);
        Assert.Null(vote.CreatedById);
        Assert.Null(vote.ModifiedBy);
        Assert.Empty((await Ok(await admin.GetAsync("/api/audit-logs?entityType=PollVote"))).GetProperty("items").EnumerateArray());
    }
}
