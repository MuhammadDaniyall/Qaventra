using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Course catalog, sessions with nominations and attendance, feedback, requests and mandatory-course compliance.</summary>
public class TrainingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private async Task<(HttpClient Admin, Func<string, Guid?, Task<(Guid Id, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "A", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant().Replace(" ", "")}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Learn-Pass-1234", role = manager is null ? "Manager" : "Employee" }));
            return (id, await factory.SignInAsync(email, "Learn-Pass-1234"));
        }
        return (admin, Person);
    }

    private static async Task<Guid> ProgramAsync(HttpClient admin, string title, bool mandatory = false, int? repeat = null, int? within = null)
    {
        await Ok(await admin.PostAsJsonAsync("/api/training/programs", new { title, mode = "Classroom", durationHours = 3, isMandatory = mandatory, repeatEveryMonths = repeat, completeWithinDays = within }, ApiFactory.Json));
        return (await Ok(await admin.GetAsync("/api/training/programs"))).EnumerateArray().First(p => p.GetProperty("title").GetString() == title).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Sessions_take_nominations_attendance_and_feedback_and_keep_people_compliant()
    {
        var (admin, person) = await CompanyAsync("Learning Co");
        var (manager, managerClient) = await person("Maha", null);
        var (trainer, trainerClient) = await person("Tariq", null);
        var (sana, sanaClient) = await person("Sana", manager);
        var (omar, _) = await person("Omar", null);

        var fire = await ProgramAsync(admin, "Fire safety", mandatory: true, repeat: 12, within: 30);
        var start = DateTime.UtcNow.AddDays(3);
        var session = await Ok(await admin.PostAsJsonAsync("/api/training/sessions", new
        {
            programId = fire, startsAtUtc = start, endsAtUtc = start.AddHours(3), venue = "Board room", trainerEmployeeId = trainer, capacity = 2, cost = 30000,
        }, ApiFactory.Json));
        var sid = session.GetProperty("id").GetGuid();

        // Everyone starts out not compliant; the window counts from when the course was added, so nobody is overdue yet.
        // All five are due within 30 days: the four people above and the company admin's own employee record.
        var before = await Ok(await admin.GetAsync("/api/training/compliance"));
        Assert.Equal(0, before.GetProperty("overdue").GetInt32());
        Assert.Equal(5, before.GetProperty("dueSoon").GetInt32());

        // A manager nominates only their own team; seats are limited.
        Assert.Equal(HttpStatusCode.Forbidden, (await managerClient.PostAsJsonAsync($"/api/training/sessions/{sid}/enroll", new { employeeIds = new[] { omar } }, ApiFactory.Json)).StatusCode);
        Assert.Equal(1, (await Ok(await managerClient.PostAsJsonAsync($"/api/training/sessions/{sid}/enroll", new { employeeIds = new[] { sana } }, ApiFactory.Json))).GetProperty("added").GetInt32());
        Assert.Contains("You are booked on Fire safety", (await Ok(await sanaClient.GetAsync("/api/notifications"))).ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/training/sessions/{sid}/enroll", new { employeeIds = new[] { omar, manager } }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/training/sessions/{sid}/enroll", new { employeeIds = new[] { omar } }, ApiFactory.Json));
        Assert.True((await Ok(await admin.GetAsync("/api/training/compliance"))).GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("employeeId").GetGuid() == sana).GetProperty("cells")[0].GetProperty("scheduled").GetBoolean());

        // The session happens; the trainer marks it.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var s = await db.TrainingSessions.IgnoreQueryFilters().FirstAsync(x => x.Id == sid);
            // Just finished, so it ended today in Pakistan even shortly after midnight.
            s.StartsAtUtc = DateTime.UtcNow.AddMinutes(-2);
            s.EndsAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var detail = await Ok(await trainerClient.GetAsync($"/api/training/sessions/{sid}"));
        var rows = detail.GetProperty("enrollments").EnumerateArray().ToList();
        Assert.Equal(HttpStatusCode.Forbidden, (await sanaClient.PostAsJsonAsync($"/api/training/sessions/{sid}/attendance", new { marks = Array.Empty<object>() }, ApiFactory.Json)).StatusCode);
        await Ok(await trainerClient.PostAsJsonAsync($"/api/training/sessions/{sid}/attendance", new
        {
            marks = rows.Select(r => new { enrollmentId = r.GetProperty("id").GetGuid(), status = r.GetProperty("employeeId").GetGuid() == sana ? "Completed" : "NoShow", score = (int?)85 }),
        }, ApiFactory.Json));
        Assert.Equal("Completed", (await Ok(await admin.GetAsync($"/api/training/sessions/{sid}"))).GetProperty("session").GetProperty("status").GetString());

        // Sana gives feedback; she is now compliant for a year, Omar still has it to do.
        var mine = Assert.Single((await Ok(await sanaClient.GetAsync("/api/training/my"))).EnumerateArray());
        await Ok(await sanaClient.PostAsJsonAsync($"/api/training/enrollments/{mine.GetProperty("enrollmentId").GetGuid()}/feedback", new { rating = 5, trainerRating = 4, wouldRecommend = true, comment = "Useful" }, ApiFactory.Json));
        var myCompliance = await Ok(await sanaClient.GetAsync("/api/training/my/compliance"));
        var cell = myCompliance.GetProperty("rows")[0].GetProperty("cells")[0];
        Assert.Equal("Done", cell.GetProperty("state").GetString());
        Assert.Equal(Today.AddMonths(12).ToString("yyyy-MM-dd"), cell.GetProperty("dueOn").GetString());
        var team = await Ok(await managerClient.GetAsync("/api/training/compliance"));
        Assert.Single(team.GetProperty("rows").EnumerateArray());

        var report = await Ok(await admin.GetAsync("/api/training/report"));
        Assert.Equal(1, report.GetProperty("peopleTrained").GetInt32());
        Assert.Equal(5.0, report.GetProperty("averageRating").GetDouble());
        Assert.Equal(30000m, report.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Requests_go_through_approval_and_outside_courses_are_recorded()
    {
        var (admin, person) = await CompanyAsync("Learning Rules");
        var (manager, managerClient) = await person("Maha", null);
        var (sana, sanaClient) = await person("Sana", manager);

        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsJsonAsync("/api/training/requests", new { reason = "Grow" }, ApiFactory.Json)).StatusCode);
        await Ok(await sanaClient.PostAsJsonAsync("/api/training/requests", new { courseName = "AWS Solutions Architect", provider = "Udemy", cost = 15000, reason = "Our cloud move" }, ApiFactory.Json));
        var req = Assert.Single((await Ok(await sanaClient.GetAsync("/api/training/requests"))).EnumerateArray());
        var id = req.GetProperty("id").GetGuid();
        Assert.Contains((await Ok(await managerClient.GetAsync("/api/approvals/inbox"))).EnumerateArray(), i => i.GetProperty("requestType").GetString() == "Training");
        await Ok(await managerClient.PostAsJsonAsync($"/api/training/requests/{id}/approve", new { }));
        Assert.Contains("Training approved for Sana A", (await Ok(await admin.GetAsync("/api/notifications"))).ToString());

        // The certificate goes on the request; HR records it done.
        var file = new MultipartFormDataContent { { new ByteArrayContent("%PDF cert"u8.ToArray()), "file", "certificate.pdf" } };
        await Ok(await sanaClient.PostAsync($"/api/attachments/TrainingRequest/{id}", file));
        Assert.Equal(HttpStatusCode.Forbidden, (await sanaClient.PostAsJsonAsync($"/api/training/requests/{id}/complete", new { completedOn = Today.ToString("yyyy-MM-dd") }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/training/requests/{id}/complete", new { completedOn = Today.ToString("yyyy-MM-dd") }, ApiFactory.Json));
        Assert.Equal(Today.ToString("yyyy-MM-dd"), (await Ok(await sanaClient.GetAsync("/api/training/requests")))[0].GetProperty("completedOn").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await sanaClient.PostAsync($"/api/training/requests/{id}/cancel", null)).StatusCode);
        _ = sana;
    }

    [Fact]
    public async Task Hr_without_training_in_their_template_gets_no_hr_rights_in_training()
    {
        var (admin, person) = await CompanyAsync("Learning Access");
        var (_, sanaClient) = await person("Sana", null);
        var hrId = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Rida", lastName = "A", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var email = $"rida+{Guid.NewGuid():N}@learningaccess.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{hrId}/login", new { email, password = "Learn-Pass-1234", role = "HrManager" }));
        var hrUser = (await Ok(await admin.GetAsync($"/api/employees/{hrId}"))).GetProperty("userId").GetGuid();
        var hr = await factory.SignInAsync(email, "Learn-Pass-1234");

        await Ok(await sanaClient.PostAsJsonAsync("/api/training/requests", new { courseName = "Excel", reason = "Reports" }, ApiFactory.Json));
        var id = Assert.Single((await Ok(await admin.GetAsync("/api/training/requests"))).EnumerateArray()).GetProperty("id").GetGuid();
        Assert.Single((await Ok(await hr.GetAsync("/api/training/requests"))).EnumerateArray());
        Assert.Empty((await Ok(await admin.GetAsync("/api/training/requests?mine=true"))).EnumerateArray());

        await Ok(await admin.PostAsJsonAsync("/api/access/templates", new { name = "Recruiter", modules = new[] { "recruitment" } }, ApiFactory.Json));
        var template = (await Ok(await admin.GetAsync("/api/access/templates"))).EnumerateArray().First(t => t.GetProperty("name").GetString() == "Recruiter").GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/access/users/{hrUser}", new { accessTemplateId = template }, ApiFactory.Json));

        Assert.False((await Ok(await hr.GetAsync("/api/training/me"))).GetProperty("isHr").GetBoolean());
        Assert.Empty((await Ok(await hr.GetAsync("/api/training/requests"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.PostAsync($"/api/training/requests/{id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task The_completion_window_counts_from_when_a_course_became_mandatory()
    {
        var (admin, person) = await CompanyAsync("Learning Since");
        await person("Sana", null);
        var course = await ProgramAsync(admin, "Data protection");
        // The course has been in the catalog for a year before HR makes it mandatory.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HRMS.Infrastructure.Data.HrmsDbContext>();
            var p = await db.TrainingPrograms.IgnoreQueryFilters().FirstAsync(x => x.Id == course);
            p.CreatedAtUtc = DateTime.UtcNow.AddYears(-1);
            await db.SaveChangesAsync();
        }
        await Ok(await admin.PutAsJsonAsync($"/api/training/programs/{course}", new { title = "Data protection", mode = "Classroom", durationHours = 3, isMandatory = true, completeWithinDays = 30 }, ApiFactory.Json));

        var compliance = await Ok(await admin.GetAsync("/api/training/compliance"));
        Assert.Equal(0, compliance.GetProperty("overdue").GetInt32());
        var cell = compliance.GetProperty("rows")[0].GetProperty("cells")[0];
        Assert.Equal(Today.AddDays(30).ToString("yyyy-MM-dd"), cell.GetProperty("dueOn").GetString());
    }
}
