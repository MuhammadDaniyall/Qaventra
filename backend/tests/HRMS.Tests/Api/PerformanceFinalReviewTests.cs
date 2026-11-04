using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>A cycle with a final review: the manager's manager checks the review before HR, who can add grace marks.</summary>
public class PerformanceFinalReviewTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Final_reviewer_checks_before_hr_and_grace_marks_stay_hidden()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("PerfFinal");
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "F", joiningDate = "2024-01-01", status = "Active", managerId = manager }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@final.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Perf-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Perf-Pass-1234"));
        }
        var (head, headClient) = await Person("Hira", null, "Manager");
        var (lead, leadClient) = await Person("Lena", head, "Manager");
        var (_, workerClient) = await Person("Wali", lead, "Employee");

        await Ok(await admin.PostAsJsonAsync("/api/performance/templates", new
        {
            name = "Values only",
            sections = new object[] { new { name = "Values", weight = 100, kind = "Competencies", items = new[] { new { title = "Teamwork", description = (string?)null } } } },
        }, ApiFactory.Json));
        var template = (await Ok(await admin.GetAsync("/api/performance/templates")))[0].GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync("/api/performance/cycles", new { name = "Mid year", periodFrom = "2026-01-01", periodTo = "2026-06-30", templateId = template, finalReview = true }));
        var cycle = (await Ok(await admin.GetAsync("/api/performance/cycles")))[0];
        Assert.True(cycle.GetProperty("finalReview").GetBoolean());
        await Ok(await admin.PostAsync($"/api/performance/cycles/{cycle.GetProperty("id").GetGuid()}/launch", null));

        var id = Assert.Single((await Ok(await workerClient.GetAsync("/api/performance/reviews/my"))).EnumerateArray()).GetProperty("id").GetGuid();
        var r = await Ok(await workerClient.GetAsync($"/api/performance/reviews/{id}"));
        var item = r.GetProperty("items")[0].GetProperty("id").GetGuid();
        if (r.GetProperty("stage").GetString() == "SelfReview")
            await Ok(await workerClient.PutAsJsonAsync($"/api/performance/reviews/{id}/self", new { scores = new[] { new { itemId = item, score = 4 } }, submit = true }));

        var managed = await Ok(await leadClient.PutAsJsonAsync($"/api/performance/reviews/{id}/manager", new { scores = new[] { new { itemId = item, score = 3 } }, potential = 2, submit = true }));
        Assert.Equal("FinalReview", managed.GetProperty("stage").GetString());
        Assert.Equal("Hira F", managed.GetProperty("finalReviewer").GetString());

        // Only the head checks it; sending back needs a reason and returns it to the manager.
        Assert.Equal(HttpStatusCode.Forbidden, (await leadClient.PostAsJsonAsync($"/api/performance/reviews/{id}/final", new { approve = true })).StatusCode);
        Assert.Contains((await Ok(await headClient.GetAsync("/api/performance/reviews/team"))).EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.BadRequest, (await headClient.PostAsJsonAsync($"/api/performance/reviews/{id}/final", new { approve = false })).StatusCode);
        var back = await Ok(await headClient.PostAsJsonAsync($"/api/performance/reviews/{id}/final", new { approve = false, comment = "Rate teamwork higher" }));
        Assert.Equal("ManagerReview", back.GetProperty("stage").GetString());
        await Ok(await leadClient.PutAsJsonAsync($"/api/performance/reviews/{id}/manager", new { scores = new[] { new { itemId = item, score = 4 } }, potential = 2, submit = true }));
        var ok = await Ok(await headClient.PostAsJsonAsync($"/api/performance/reviews/{id}/final", new { approve = true }));
        Assert.Equal("Calibration", ok.GetProperty("stage").GetString());

        // HR adds grace marks (0 to 1); the employee does not see them before release.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/performance/reviews/{id}/calibrate", new { finalRating = 5, graceMarks = 2 })).StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/performance/reviews/{id}/calibrate", new { finalRating = 5, graceMarks = 0.5 }));
        Assert.Equal(0.5m, (await Ok(await admin.GetAsync($"/api/performance/reviews/{id}"))).GetProperty("graceMarks").GetDecimal());
        var mine = await Ok(await workerClient.GetAsync($"/api/performance/reviews/{id}"));
        Assert.Equal(0m, mine.GetProperty("graceMarks").GetDecimal());
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("finalRating").ValueKind);
    }
}
