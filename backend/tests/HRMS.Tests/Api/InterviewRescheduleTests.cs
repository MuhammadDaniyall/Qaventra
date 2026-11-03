using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>A scheduled interview moves to a new time with a reason; past times and finished interviews cannot move.</summary>
public class InterviewRescheduleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Interview_moves_to_a_new_time()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Resched");
        async Task<JsonElement> Json(HttpResponseMessage r)
        {
            Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
            return r.StatusCode == HttpStatusCode.NoContent ? default : await r.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        }
        var interviewer = (await Json(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Iqbal", lastName = "I", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var post = (await Json(await admin.PostAsJsonAsync("/api/recruitment/jobs", new { title = "Analyst", employmentType = "FullTime", description = "Numbers", positions = 1, showSalary = false }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var form = new MultipartFormDataContent
        {
            { new StringContent("Nadia"), "FirstName" }, { new StringContent("N"), "LastName" }, { new StringContent($"nadia+{Guid.NewGuid():N}@cand.test"), "Email" },
        };
        await Json(await admin.PostAsync($"/api/recruitment/jobs/{post}/candidates", form));
        var appId = (await Json(await admin.GetAsync($"/api/recruitment-extras/posts/{post}/scores"))).EnumerateArray().Single().GetProperty("applicationId").GetGuid();
        await Json(await admin.PostAsJsonAsync($"/api/recruitment/applications/{appId}/interviews", new
        {
            title = "First round", startsAtUtc = DateTime.UtcNow.AddDays(2), durationMinutes = 45, mode = "Online", where = "Meet", interviewerIds = new[] { interviewer }, emailCandidate = false,
        }, ApiFactory.Json));
        var interview = (await Json(await admin.GetAsync($"/api/recruitment/applications/{appId}"))).GetProperty("interviews")[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/recruitment/interviews/{interview}/reschedule",
            new { startsAtUtc = DateTime.UtcNow.AddHours(-2), reason = "x", emailCandidate = false }, ApiFactory.Json)).StatusCode);
        var newTime = DateTime.UtcNow.AddDays(5);
        await Json(await admin.PostAsJsonAsync($"/api/recruitment/interviews/{interview}/reschedule",
            new { startsAtUtc = newTime, where = "Head office", reason = "Interviewer travelling", emailCandidate = false }, ApiFactory.Json));
        var moved = (await Json(await admin.GetAsync($"/api/recruitment/applications/{appId}"))).GetProperty("interviews")[0];
        Assert.Equal(newTime.Date, moved.GetProperty("startsAtUtc").GetDateTime().Date);
        Assert.Equal("Head office", moved.GetProperty("where").GetString());

        await Json(await admin.PostAsJsonAsync($"/api/recruitment/interviews/{interview}/status", new { status = "Cancelled" }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/recruitment/interviews/{interview}/reschedule",
            new { startsAtUtc = DateTime.UtcNow.AddDays(6), reason = "Again", emailCandidate = false }, ApiFactory.Json)).StatusCode);
    }
}
