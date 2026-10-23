using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Job requests, posts on the careers page, the pipeline, interviews with feedback, offers and the hire.</summary>
public class RecruitmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private async Task<(HttpClient Admin, Func<string, Guid?, Task<(Guid Id, HttpClient Client)>> Person)> CompanyAsync(string name)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(name);
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "A", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{name.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Hire-Pass-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Hire-Pass-1234"));
        }
        return (admin, Person);
    }

    private static MultipartFormDataContent Form(string first, string email, bool cv = true, string? phone = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(first), "firstName" },
            { new StringContent("Khan"), "lastName" },
            { new StringContent(email), "email" },
            { new StringContent(phone ?? "0300-1234567"), "phone" },
            { new StringContent("4"), "experienceYears" },
        };
        if (cv)
            form.Add(new ByteArrayContent("%PDF-1.4 cv"u8.ToArray()), "cv", "cv.pdf");
        return form;
    }

    private static async Task<Guid> OpenPostAsync(HttpClient admin, string title, Guid? recruiter, Guid? manager, int positions = 1)
    {
        var post = await Ok(await admin.PostAsJsonAsync("/api/recruitment/jobs", new
        {
            title, description = "Build things.", employmentType = "FullTime", positions, recruiterEmployeeId = recruiter, hiringManagerEmployeeId = manager,
            salaryMin = 150000, salaryMax = 200000, showSalary = true,
        }, ApiFactory.Json));
        var id = post.GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/jobs/{id}/status", new { status = "Open" }, ApiFactory.Json));
        return id;
    }

    [Fact]
    public async Task A_hire_goes_from_request_to_careers_page_to_employee()
    {
        var (admin, person) = await CompanyAsync("Hiring Co");
        var (mona, monaClient) = await person("Mona", null);
        await person("Tariq", mona); // Mona manages someone, so she may ask to hire.
        var (rina, rinaClient) = await person("Rina", null);
        var (ivan, ivanClient) = await person("Ivan", null);
        var (omar, omarClient) = await person("Omar", null);

        // The manager asks; the request goes through approval.
        var request = await Ok(await monaClient.PostAsJsonAsync("/api/recruitment/requests", new { title = "Backend developer", positions = 1, reason = "New product team" }, ApiFactory.Json));
        Assert.Equal("Pending", request.GetProperty("status").GetString());
        var inbox = await Ok(await admin.GetAsync("/api/approvals/inbox"));
        Assert.Contains(inbox.EnumerateArray(), i => i.GetProperty("requestType").GetString() == "JobRequest");
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/requests/{request.GetProperty("id").GetGuid()}/approve", new { }));

        var post = await Ok(await admin.PostAsJsonAsync("/api/recruitment/jobs", new
        {
            title = "Backend developer", jobRequestId = request.GetProperty("id").GetGuid(), description = "Build our APIs.", employmentType = "FullTime",
            positions = 1, recruiterEmployeeId = rina, hiringManagerEmployeeId = mona, salaryMin = 150000, salaryMax = 200000, showSalary = false,
        }, ApiFactory.Json));
        var postId = post.GetProperty("id").GetGuid();
        var slug = post.GetProperty("slug").GetString();
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/jobs/{postId}/status", new { status = "Open" }, ApiFactory.Json));
        var me = await Ok(await admin.GetAsync("/api/recruitment/me"));
        var company = me.GetProperty("careersSlug").GetString();
        Assert.StartsWith("hiring-co", company);

        // The public careers page, no sign-in; the salary stays hidden.
        var anon = factory.CreateClient();
        var page = await Ok(await anon.GetAsync($"/api/careers/{company}"));
        var job = Assert.Single(page.GetProperty("jobs").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, job.GetProperty("salaryMin").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsync($"/api/careers/{company}/{slug}/apply", Form("Sara", "sara@mail.test", cv: false))).StatusCode);
        await Ok(await anon.PostAsync($"/api/careers/{company}/{slug}/apply", Form("Sara", "sara@mail.test")));
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsync($"/api/careers/{company}/{slug}/apply", Form("Sara", "SARA@mail.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/careers/no-such-company")).StatusCode);

        // The recruiter sees the pipeline; outsiders do not.
        var card = Assert.Single((await Ok(await rinaClient.GetAsync($"/api/recruitment/jobs/{postId}/pipeline"))).EnumerateArray());
        Assert.Equal("CareersPage", card.GetProperty("source").GetString());
        var appId = card.GetProperty("id").GetGuid();
        Assert.Contains("New candidate for Backend developer", (await Ok(await rinaClient.GetAsync("/api/notifications"))).ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await omarClient.GetAsync($"/api/recruitment/applications/{appId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await omarClient.GetAsync("/api/recruitment/candidates")).StatusCode);
        var candidateId = card.GetProperty("candidateId").GetGuid();
        var files = await Ok(await rinaClient.GetAsync($"/api/attachments/Candidate/{candidateId}"));
        Assert.Equal("cv.pdf", Assert.Single(files.GetProperty("items").EnumerateArray()).GetProperty("fileName").GetString());

        await Ok(await rinaClient.PostAsJsonAsync($"/api/recruitment/applications/{appId}/move", new { stage = "Shortlisted" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await rinaClient.PostAsJsonAsync($"/api/recruitment/applications/{appId}/move", new { stage = "Hired" }, ApiFactory.Json)).StatusCode);

        // Two interviewers; each sees the other's verdict only after giving their own.
        await Ok(await rinaClient.PostAsJsonAsync($"/api/recruitment/applications/{appId}/interviews", new
        {
            title = "Technical", startsAtUtc = DateTime.UtcNow.AddMinutes(-30), durationMinutes = 60, mode = "Online", where = "meet.link", interviewerIds = new[] { ivan, omar },
        }, ApiFactory.Json));
        var mine = Assert.Single((await Ok(await ivanClient.GetAsync("/api/recruitment/interviews/mine"))).EnumerateArray());
        var interviewId = mine.GetProperty("id").GetGuid();
        await Ok(await omarClient.PostAsJsonAsync($"/api/recruitment/interviews/{interviewId}/feedback", new { rating = 4, recommendation = "Yes", strengths = "Clear thinker" }, ApiFactory.Json));
        var ivanView = await Ok(await ivanClient.GetAsync($"/api/recruitment/applications/{appId}"));
        var iv = Assert.Single(ivanView.GetProperty("interviews").EnumerateArray());
        Assert.Empty(iv.GetProperty("feedback").EnumerateArray());
        Assert.Equal(1, iv.GetProperty("feedbackHidden").GetInt32());
        Assert.Equal(JsonValueKind.Null, ivanView.GetProperty("candidate").GetProperty("expectedSalary").ValueKind);
        await Ok(await ivanClient.PostAsJsonAsync($"/api/recruitment/interviews/{interviewId}/feedback", new { rating = 5, recommendation = "StrongYes" }, ApiFactory.Json));
        iv = (await Ok(await ivanClient.GetAsync($"/api/recruitment/applications/{appId}"))).GetProperty("interviews")[0];
        Assert.Equal(2, iv.GetProperty("feedback").GetArrayLength());
        Assert.Equal("Completed", iv.GetProperty("status").GetString());

        // Offer, acceptance, hire.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/hire", new { }, ApiFactory.Json)).StatusCode);
        await Ok(await rinaClient.PostAsJsonAsync($"/api/recruitment/applications/{appId}/offer", new { salary = 180000, joiningDate = Iso(Today.AddDays(14)), emailCandidate = true }, ApiFactory.Json));
        await Ok(await rinaClient.PostAsJsonAsync($"/api/recruitment/applications/{appId}/offer-response", new { accepted = true }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.Forbidden, (await rinaClient.PostAsJsonAsync($"/api/recruitment/applications/{appId}/hire", new { }, ApiFactory.Json)).StatusCode);
        var hired = await Ok(await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/hire", new { }, ApiFactory.Json));
        var employeeId = hired.GetProperty("employeeId").GetGuid();

        var employee = await Ok(await admin.GetAsync($"/api/employees/{employeeId}"));
        Assert.Equal("Sara", employee.GetProperty("firstName").GetString());
        Assert.Equal(mona, employee.GetProperty("managerId").GetGuid());
        var docs = await Ok(await admin.GetAsync($"/api/attachments/EmployeeDocument/{employeeId}"));
        Assert.Equal("cv.pdf", Assert.Single(docs.GetProperty("items").EnumerateArray()).GetProperty("fileName").GetString());
        Assert.Equal("Filled", (await Ok(await admin.GetAsync($"/api/recruitment/jobs/{postId}"))).GetProperty("status").GetString());
        Assert.Contains("joins your team", (await Ok(await monaClient.GetAsync("/api/notifications"))).ToString());
        Assert.Empty((await Ok(await anon.GetAsync($"/api/careers/{company}"))).GetProperty("jobs").EnumerateArray());

        var report = await Ok(await admin.GetAsync("/api/recruitment/report"));
        Assert.Equal(1, report.GetProperty("offersAccepted").GetInt32());
        Assert.Equal(1, report.GetProperty("hiredBySource").GetProperty("CareersPage").GetInt32());
    }

    [Fact]
    public async Task Rules_for_requests_referrals_rejections_and_the_do_not_hire_list()
    {
        var (admin, person) = await CompanyAsync("Rules Hiring");
        var (wali, waliClient) = await person("Wali", null);

        // Only managers and HR ask to hire.
        Assert.Equal(HttpStatusCode.Forbidden, (await waliClient.PostAsJsonAsync("/api/recruitment/requests", new { title = "Clerk", positions = 1, reason = "Busy" }, ApiFactory.Json)).StatusCode);
        Assert.False((await Ok(await waliClient.GetAsync("/api/recruitment/me"))).GetProperty("canRequest").GetBoolean());

        var postA = await OpenPostAsync(admin, "Sales executive", null, null);
        var postB = await OpenPostAsync(admin, "Sales lead", null, null);

        // Anyone can see openings and refer a friend.
        Assert.Equal(2, (await Ok(await waliClient.GetAsync("/api/recruitment/openings"))).GetArrayLength());
        await Ok(await waliClient.PostAsync($"/api/recruitment/openings/{postA}/refer", Form("Fahad", "fahad@mail.test")));
        var referral = Assert.Single((await Ok(await waliClient.GetAsync("/api/recruitment/referrals"))).EnumerateArray());
        Assert.Equal("Applied", referral.GetProperty("stage").GetString());

        var card = Assert.Single((await Ok(await admin.GetAsync($"/api/recruitment/jobs/{postA}/pipeline"))).EnumerateArray());
        Assert.Equal("Referral", card.GetProperty("source").GetString());
        var candidateId = card.GetProperty("candidateId").GetGuid();
        var candidate = await Ok(await admin.GetAsync($"/api/recruitment/candidates/{candidateId}"));
        Assert.Equal("Wali A", candidate.GetProperty("referredBy").GetString());

        // The same person in another job's pipeline; a near-duplicate is flagged.
        var appB = (await Ok(await admin.PostAsync($"/api/recruitment/jobs/{postB}/candidates/{candidateId}", null))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/recruitment/jobs/{postB}/candidates/{candidateId}", null)).StatusCode);
        await Ok(await admin.PostAsync($"/api/recruitment/jobs/{postB}/candidates", Form("Fahad", "fahad.k@other.test", cv: false, phone: "+92 300 1234567")));
        Assert.Single((await Ok(await admin.GetAsync($"/api/recruitment/candidates/{candidateId}"))).GetProperty("possibleDuplicates").EnumerateArray());

        // Rejection needs a reason and cancels scheduled interviews.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/recruitment/applications/{card.GetProperty("id").GetGuid()}/move", new { stage = "Rejected" }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/applications/{card.GetProperty("id").GetGuid()}/move", new { stage = "Rejected", reason = "Not enough experience", emailCandidate = true }, ApiFactory.Json));

        // A blacklisted person cannot be offered a job.
        await Ok(await admin.PostAsJsonAsync($"/api/recruitment/candidates/{candidateId}/blacklist", new { on = true, reason = "Fake degree" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/recruitment/applications/{appB}/offer", new { salary = 100000, joiningDate = Iso(Today.AddDays(7)) }, ApiFactory.Json)).StatusCode);
        Assert.Single((await Ok(await admin.GetAsync("/api/recruitment/candidates?blacklisted=true"))).EnumerateArray());

        // A post with applicants cannot be deleted.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/recruitment/jobs/{postA}")).StatusCode);
        _ = wali;
    }
}
