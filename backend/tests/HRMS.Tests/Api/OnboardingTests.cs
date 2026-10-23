using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Onboarding templates, starting for a new joiner, task clearance, the welcome page and the team introduction.</summary>
public class OnboardingTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    public async Task A_new_joiner_is_onboarded_by_everyone_involved()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Onboard");
        var dept = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Engineering" }))).GetProperty("id").GetGuid();
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, string joined = "2024-01-01")
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "O", joiningDate = joined, status = "Probation", managerId = manager, departmentId = dept }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@onboard.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Onboard-Pass-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Onboard-Pass-1234"));
        }
        var (lead, leadClient) = await Person("Lena", null);
        var (buddy, buddyClient) = await Person("Bilal", lead);

        // A default template: tasks for HR, the manager, the buddy and the joiner, relative to the joining date.
        var template = await Ok(await admin.PostAsJsonAsync("/api/onboarding/templates", new
        {
            name = "Engineering joiner", isDefault = true, welcomeMessage = "Bring your CNIC.",
            tasks = new object[]
            {
                new { title = "Prepare offer pack", category = "Paperwork", assigneeKind = "Hr", dueOffsetDays = -2 },
                new { title = "Plan first week", category = "Training", assigneeKind = "LineManager", dueOffsetDays = 0 },
                new { title = "Office tour", category = "Introduction", assigneeKind = "Buddy", dueOffsetDays = 0 },
                new { title = "Upload CNIC copy", category = "Paperwork", assigneeKind = "NewJoiner", dueOffsetDays = 1 },
            },
        }, ApiFactory.Json));
        Assert.Equal(4, template.GetProperty("tasks").GetArrayLength());

        // Adding an employee starts the default template automatically.
        var joining = Today.AddDays(3);
        var (nida, nidaClient) = await Person("Nida", lead, Iso(joining));
        var list = await Ok(await admin.GetAsync("/api/onboarding"));
        var item = list.EnumerateArray().First(o => o.GetProperty("employeeId").GetGuid() == nida);
        Assert.Equal(4, item.GetProperty("total").GetInt32());
        var id = item.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/onboarding", new { employeeId = nida }, ApiFactory.Json)).StatusCode);

        // Each person sees only their own tasks. The buddy task falls to the manager when no buddy was named.
        var detail = await Ok(await admin.GetAsync($"/api/onboarding/{id}"));
        var tasks = detail.GetProperty("tasks").EnumerateArray().ToList();
        Assert.Equal(Iso(joining.AddDays(-2)), tasks.First(t => t.GetProperty("title").GetString() == "Prepare offer pack").GetProperty("dueOn").GetString());
        Assert.Equal(lead, tasks.First(t => t.GetProperty("title").GetString() == "Office tour").GetProperty("assigneeEmployeeId").GetGuid());
        Assert.Equal(2, (await Ok(await leadClient.GetAsync("/api/onboarding/my-tasks"))).GetArrayLength());
        Assert.Empty((await Ok(await buddyClient.GetAsync("/api/onboarding/my-tasks"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await buddyClient.GetAsync($"/api/onboarding/{id}")).StatusCode);

        // The welcome page shows first-day details and the joiner's own task.
        var welcome = await Ok(await nidaClient.GetAsync("/api/onboarding/welcome"));
        Assert.Equal("Bring your CNIC.", welcome.GetProperty("onboarding").GetProperty("instructions").GetString());
        Assert.Equal("Lena O", welcome.GetProperty("onboarding").GetProperty("manager").GetString());
        Assert.Contains("Welcome to the team", (await Ok(await nidaClient.GetAsync("/api/notifications"))).ToString());
        var mine = welcome.GetProperty("onboarding").GetProperty("tasks").EnumerateArray().First(t => t.GetProperty("canDo").GetBoolean());
        Assert.Equal("Upload CNIC copy", mine.GetProperty("title").GetString());

        // Clearing tasks: others' tasks are refused; when all are done the onboarding completes.
        var hrTask = tasks.First(t => t.GetProperty("title").GetString() == "Prepare offer pack").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await nidaClient.PostAsJsonAsync($"/api/onboarding/tasks/{hrTask}", new { done = true })).StatusCode);
        foreach (var t in tasks)
        {
            var who = t.GetProperty("assigneeEmployeeId").ValueKind == JsonValueKind.Null ? admin
                : t.GetProperty("assigneeEmployeeId").GetGuid() == nida ? nidaClient : leadClient;
            await Ok(await who.PostAsJsonAsync($"/api/onboarding/tasks/{t.GetProperty("id").GetGuid()}", new { done = true, note = "Done" }));
        }
        Assert.Equal("Completed", (await Ok(await admin.GetAsync($"/api/onboarding/{id}"))).GetProperty("summary").GetProperty("status").GetString());

        // The introduction becomes a welcome announcement for the department.
        Assert.Equal(HttpStatusCode.BadRequest, (await nidaClient.PostAsJsonAsync("/api/onboarding/introduce", new { answers = new Dictionary<string, string?>() })).StatusCode);
        await Ok(await nidaClient.PostAsJsonAsync("/api/onboarding/introduce", new { answers = new Dictionary<string, string?> { ["about"] = "Backend developer from Lahore", ["funFact"] = "I have climbed K2 base camp" } }));
        Assert.Equal(HttpStatusCode.BadRequest, (await nidaClient.PostAsJsonAsync("/api/onboarding/introduce", new { answers = new Dictionary<string, string?> { ["about"] = "again" } })).StatusCode);
        var news = await Ok(await buddyClient.GetAsync("/api/announcements"));
        Assert.Contains(news.EnumerateArray(), a => a.GetProperty("title").GetString() == "Welcome Nida O!" && a.GetProperty("body").GetString()!.Contains("K2"));
        _ = buddy;
    }

    [Fact]
    public async Task HR_starts_onboarding_by_hand_with_a_buddy_and_extra_tasks()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Onboard2");
        async Task<Guid> Person(string first) =>
            (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "O", joiningDate = Iso(Today), status = "Probation" }, ApiFactory.Json))).GetProperty("id").GetGuid();
        var joiner = await Person("Omar");
        var buddy = await Person("Sana");
        Assert.Empty((await Ok(await admin.GetAsync("/api/onboarding"))).EnumerateArray());

        var started = await Ok(await admin.PostAsJsonAsync("/api/onboarding", new { employeeId = joiner, reportingTime = "09:30:00", reportingPlace = "3rd floor reception", buddyEmployeeId = buddy, instructions = "Ask for Sana" }, ApiFactory.Json));
        Assert.Equal("Sana O", started.GetProperty("buddy").GetString());
        var id = started.GetProperty("summary").GetProperty("id").GetGuid();
        var withTask = await Ok(await admin.PostAsJsonAsync($"/api/onboarding/{id}/tasks", new { title = "Lunch with the team", category = "Introduction", assigneeKind = "Buddy", dueOn = Iso(Today) }, ApiFactory.Json));
        Assert.Equal(buddy, Assert.Single(withTask.GetProperty("tasks").EnumerateArray()).GetProperty("assigneeEmployeeId").GetGuid());
        await Ok(await admin.PostAsync($"/api/onboarding/{id}/cancel", null));
        Assert.Single((await Ok(await admin.GetAsync("/api/onboarding?status=Cancelled"))).EnumerateArray());
    }
}
