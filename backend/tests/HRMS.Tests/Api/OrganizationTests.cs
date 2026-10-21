using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Announcements, polls, custom fields and department heads in approval chains.</summary>
public class OrganizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private record Org(HttpClient Admin, Guid Eng, Guid Sales, Func<string, Guid, string, Task<(Guid Id, HttpClient Client)>> Person);

    private async Task<Org> OrgAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        var eng = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Engineering" }))).GetProperty("id").GetGuid();
        var sales = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Sales" }))).GetProperty("id").GetGuid();
        async Task<(Guid, HttpClient)> Person(string first, Guid dept, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "O", joiningDate = "2024-01-01", status = "Active", departmentId = dept }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Org-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Org-Pass-1234"));
        }
        return new Org(admin, eng, sales, Person);
    }

    [Fact]
    public async Task Announcements_reach_their_audience_and_count_reads()
    {
        var o = await OrgAsync("News");
        var (_, dev) = await o.Person("Dana", o.Eng, "Employee");
        var (_, seller) = await o.Person("Sami", o.Sales, "Employee");

        var a = await Ok(await o.Admin.PostAsJsonAsync("/api/announcements", new { title = "Code freeze Friday", body = "No deploys after 3pm.", kind = "News", publishFrom = Iso(Today), departmentIds = new[] { o.Eng }, isPinned = true }, ApiFactory.Json));
        await Ok(await o.Admin.PostAsJsonAsync("/api/announcements", new { title = "Leave policy 2027", body = "Starts next month.", kind = "Policy", publishFrom = Iso(Today.AddDays(10)) }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await o.Admin.PostAsJsonAsync("/api/announcements", new { title = "x", body = "y", kind = "News", publishFrom = Iso(Today), linkUrl = "javascript:alert(1)" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dev.PostAsJsonAsync("/api/announcements", new { title = "x", body = "y", kind = "News", publishFrom = Iso(Today) }, ApiFactory.Json)).StatusCode);

        var devFeed = (await Ok(await dev.GetAsync("/api/announcements"))).EnumerateArray().ToList();
        Assert.Equal("Code freeze Friday", Assert.Single(devFeed).GetProperty("title").GetString());
        Assert.Empty((await Ok(await seller.GetAsync("/api/announcements"))).EnumerateArray());
        var bell = await Ok(await dev.GetAsync("/api/notifications"));
        Assert.Contains("Code freeze Friday", bell.ToString());

        var id = a.GetProperty("id").GetGuid();
        await Ok(await dev.PostAsync($"/api/announcements/{id}/read", null));
        await Ok(await dev.PostAsync($"/api/announcements/{id}/read", null));
        Assert.True(Assert.Single((await Ok(await dev.GetAsync("/api/announcements"))).EnumerateArray()).GetProperty("isRead").GetBoolean());
        var managed = (await Ok(await o.Admin.GetAsync("/api/announcements/manage"))).EnumerateArray().ToList();
        var stats = managed.First(x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(1, stats.GetProperty("readCount").GetInt32());
        Assert.Equal(1, stats.GetProperty("audienceCount").GetInt32());
        Assert.Equal(2, managed.Count);

        await Ok(await o.Admin.DeleteAsync($"/api/announcements/{id}"));
        Assert.Empty((await Ok(await dev.GetAsync("/api/announcements"))).EnumerateArray());
    }

    [Fact]
    public async Task Polls_take_one_vote_each_and_show_results_by_the_rules()
    {
        var o = await OrgAsync("Polls");
        var (_, dev) = await o.Person("Dana", o.Eng, "Employee");
        var (_, dev2) = await o.Person("Ehsan", o.Eng, "Employee");
        var (_, seller) = await o.Person("Sami", o.Sales, "Employee");

        Assert.Equal(HttpStatusCode.BadRequest, (await o.Admin.PostAsJsonAsync("/api/announcements/polls", new { question = "Lunch?", opensOn = Iso(Today), closesOn = Iso(Today.AddDays(3)), resultsVisibility = "AfterVoting", options = new[] { "Only one" } }, ApiFactory.Json)).StatusCode);
        var poll = await Ok(await o.Admin.PostAsJsonAsync("/api/announcements/polls", new
        {
            question = "Team dinner venue?", opensOn = Iso(Today), closesOn = Iso(Today.AddDays(3)), departmentIds = new[] { o.Eng },
            resultsVisibility = "AfterVoting", options = new[] { "BBQ Tonight", "Monal", "Cafe Aylanto" },
        }, ApiFactory.Json));
        var id = poll.GetProperty("id").GetGuid();
        var options = poll.GetProperty("options").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();

        // Before voting the results are hidden; afterwards they show. Votes can change while open.
        var before = Assert.Single((await Ok(await dev.GetAsync("/api/announcements/polls"))).EnumerateArray());
        Assert.False(before.GetProperty("resultsShown").GetBoolean());
        var after = await Ok(await dev.PostAsJsonAsync($"/api/announcements/polls/{id}/vote", new { optionId = options[0] }));
        Assert.True(after.GetProperty("resultsShown").GetBoolean());
        await Ok(await dev.PostAsJsonAsync($"/api/announcements/polls/{id}/vote", new { optionId = options[1] }));
        await Ok(await dev2.PostAsJsonAsync($"/api/announcements/polls/{id}/vote", new { optionId = options[1] }));
        Assert.Equal(HttpStatusCode.NotFound, (await seller.PostAsJsonAsync($"/api/announcements/polls/{id}/vote", new { optionId = options[0] })).StatusCode);
        Assert.Empty((await Ok(await seller.GetAsync("/api/announcements/polls"))).EnumerateArray());

        var hr = Assert.Single((await Ok(await o.Admin.GetAsync("/api/announcements/polls/manage"))).EnumerateArray());
        Assert.Equal(2, hr.GetProperty("totalVotes").GetInt32());
        Assert.Equal(2, hr.GetProperty("audienceCount").GetInt32());
        var monal = hr.GetProperty("options").EnumerateArray().First(x => x.GetProperty("text").GetString() == "Monal");
        Assert.Equal(2, monal.GetProperty("votes").GetInt32());
        Assert.Equal(2, monal.GetProperty("voters").GetArrayLength());

        // Anonymous: HR sees counts but never names.
        var anon = await Ok(await o.Admin.PostAsJsonAsync("/api/announcements/polls", new { question = "Rate your manager", opensOn = Iso(Today), closesOn = Iso(Today), isAnonymous = true, resultsVisibility = "HrOnly", options = new[] { "Good", "Bad" } }, ApiFactory.Json));
        var voted = await Ok(await seller.PostAsJsonAsync($"/api/announcements/polls/{anon.GetProperty("id").GetGuid()}/vote", new { optionId = anon.GetProperty("options")[1].GetProperty("id").GetGuid() }));
        Assert.False(voted.GetProperty("resultsShown").GetBoolean());
        var anonHr = (await Ok(await o.Admin.GetAsync("/api/announcements/polls/manage"))).EnumerateArray().First(p => p.GetProperty("isAnonymous").GetBoolean());
        Assert.All(anonHr.GetProperty("options").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.GetProperty("voters").ValueKind));
        Assert.Equal(1, anonHr.GetProperty("totalVotes").GetInt32());
    }

    [Fact]
    public async Task Custom_fields_are_validated_and_shown_to_employees_when_visible()
    {
        var o = await OrgAsync("Fields");
        var (id, dev) = await o.Person("Dana", o.Eng, "Employee");
        Assert.Equal(HttpStatusCode.BadRequest, (await o.Admin.PostAsJsonAsync("/api/setup/custom-fields", new { label = "T-shirt size", type = "Dropdown", options = new[] { "M" }, visibleToEmployee = true }, ApiFactory.Json)).StatusCode);
        await Ok(await o.Admin.PostAsJsonAsync("/api/setup/custom-fields", new { label = "T-shirt size", type = "Dropdown", options = new[] { "S", "M", "L" }, visibleToEmployee = true }, ApiFactory.Json));
        await Ok(await o.Admin.PostAsJsonAsync("/api/setup/custom-fields", new { label = "Laptop asset no", type = "Number", isRequired = true, visibleToEmployee = false }, ApiFactory.Json));
        var fields = (await Ok(await o.Admin.GetAsync("/api/setup/custom-fields"))).EnumerateArray().ToDictionary(f => f.GetProperty("label").GetString()!, f => f.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.BadRequest, (await o.Admin.PutAsJsonAsync($"/api/employees/{id}/custom-fields", new { values = new Dictionary<Guid, string> { [fields["T-shirt size"]] = "XXL", [fields["Laptop asset no"]] = "12" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await o.Admin.PutAsJsonAsync($"/api/employees/{id}/custom-fields", new { values = new Dictionary<Guid, string> { [fields["T-shirt size"]] = "m" } })).StatusCode);
        var saved = await Ok(await o.Admin.PutAsJsonAsync($"/api/employees/{id}/custom-fields", new { values = new Dictionary<Guid, string> { [fields["T-shirt size"]] = "m", [fields["Laptop asset no"]] = "4410" } }));
        Assert.Contains(saved.EnumerateArray(), v => v.GetProperty("value").GetString() == "M");

        var mine = Assert.Single((await Ok(await dev.GetAsync("/api/employees/me/custom-fields"))).EnumerateArray());
        Assert.Equal("M", mine.GetProperty("value").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await dev.PutAsJsonAsync($"/api/employees/{id}/custom-fields", new { values = new Dictionary<Guid, string>() })).StatusCode);
    }

    [Fact]
    public async Task Department_head_can_be_an_approval_step()
    {
        var o = await OrgAsync("Heads");
        var (headId, head) = await o.Person("Hamid", o.Eng, "Manager");
        var (_, dev) = await o.Person("Dana", o.Eng, "Employee");
        await Ok(await o.Admin.PutAsJsonAsync($"/api/departments/{o.Eng}", new { name = "Engineering", headEmployeeId = headId }));
        Assert.Equal("Hamid O", (await Ok(await o.Admin.GetAsync("/api/departments"))).EnumerateArray().First(d => d.GetProperty("id").GetGuid() == o.Eng).GetProperty("head").GetString());
        await Ok(await o.Admin.PutAsJsonAsync("/api/approvals/flows/Attendance", new { steps = new[] { new { kind = "DepartmentHead", employeeId = (Guid?)null } } }, ApiFactory.Json));

        var day = Today.AddDays(-2);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(-1);
        await Ok(await dev.PostAsJsonAsync("/api/attendance/requests", new { date = Iso(day), kind = "MissedPunch", checkIn = "09:00:00", checkOut = "18:00:00", reason = "Forgot" }, ApiFactory.Json));
        var item = Assert.Single((await Ok(await head.GetAsync("/api/approvals/inbox"))).EnumerateArray());
        Assert.True(item.GetProperty("assignedToMe").GetBoolean());
    }
}
