using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Domain;
using HRMS.Domain.Services;

namespace HRMS.Tests.Api;

/// <summary>A review cycle from goals to sign-off, with weighted scores, calibration, the bell curve and raises.</summary>
public class PerformanceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public void Scores_are_weighted_by_item_and_section()
    {
        List<ReviewSection> sections = [new("Goals", 60, ReviewSectionKind.Goals, []), new("Values", 40, ReviewSectionKind.Competencies, [])];
        var score = PerformanceMath.Score(sections, [new(0, 70, 4), new(0, 30, 5), new(1, 50, 3), new(1, 50, 4)]);
        Assert.Equal(3.98m, score);
        Assert.Equal(4, PerformanceMath.Rating(3.98m));
        Assert.Equal(3m, PerformanceMath.Score(sections, [new(1, 50, 3), new(1, 50, 3)]));
        Assert.Null(PerformanceMath.Score(sections, [new(0, 100, null)]));
        Assert.Equal(10, PerformanceMath.Raise(4.2m, PerformanceMath.DefaultRaises));
        Assert.Equal(0, PerformanceMath.Raise(1.5m, PerformanceMath.DefaultRaises));
        Assert.Equal(3, PerformanceMath.PerformanceBand(5));
    }

    [Fact]
    public async Task A_review_goes_from_goals_to_sign_off()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Perf");
        var dept = (await Ok(await admin.PostAsJsonAsync("/api/departments", new { name = "Engineering" }))).GetProperty("id").GetGuid();
        async Task<(Guid, HttpClient)> Person(string first, Guid? manager)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "P", joiningDate = "2024-01-01", status = "Active", managerId = manager, departmentId = dept }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@perf.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Perf-Pass-1234", role = first == "Lena" ? "Manager" : "Employee" }));
            return (id, await factory.SignInAsync(email, "Perf-Pass-1234"));
        }
        var (lead, leadClient) = await Person("Lena", null);
        var (worker, workerClient) = await Person("Wali", lead);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/performance/templates", new { name = "Bad", sections = new[] { new { name = "Goals", weight = 50, kind = "Goals", items = Array.Empty<object>() } } }, ApiFactory.Json)).StatusCode);
        await Ok(await admin.PostAsJsonAsync("/api/performance/templates", new
        {
            name = "Annual",
            sections = new object[]
            {
                new { name = "Goals", weight = 60, kind = "Goals", items = Array.Empty<object>() },
                new { name = "Values", weight = 40, kind = "Competencies", items = new[] { new { title = "Teamwork", description = (string?)null }, new { title = "Communication", description = (string?)null } } },
            },
        }, ApiFactory.Json));
        var template = (await Ok(await admin.GetAsync("/api/performance/templates")))[0].GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync("/api/performance/cycles", new { name = "Annual review 2026", periodFrom = "2026-01-01", periodTo = "2026-12-31", templateId = template, departmentIds = new[] { dept } }));
        var cycle = (await Ok(await admin.GetAsync("/api/performance/cycles")))[0].GetProperty("id").GetGuid();
        Assert.Equal(2, (await Ok(await admin.PostAsync($"/api/performance/cycles/{cycle}/launch", null))).GetProperty("reviews").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/performance/cycles/{cycle}/launch", null)).StatusCode);

        // Goals: weights must add up to 100; only the manager approves.
        var mine = Assert.Single((await Ok(await workerClient.GetAsync("/api/performance/reviews/my"))).EnumerateArray());
        Assert.Equal("Goals", mine.GetProperty("stage").GetString());
        var id = mine.GetProperty("id").GetGuid();
        await Ok(await workerClient.PutAsJsonAsync($"/api/performance/reviews/{id}/goals", new { goals = new[] { new { sectionIndex = 0, title = "Ship the payments API", target = "Live by June", weight = 70 }, new { sectionIndex = 0, title = "Mentor a junior", target = (string?)null, weight = 20 } } }));
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsync($"/api/performance/reviews/{id}/goals/submit", null)).StatusCode);
        var withGoals = await Ok(await workerClient.PutAsJsonAsync($"/api/performance/reviews/{id}/goals", new { goals = new[] { new { sectionIndex = 0, title = "Ship the payments API", target = "Live by June", weight = 70 }, new { sectionIndex = 0, title = "Mentor a junior", target = (string?)null, weight = 30 } } }));
        Assert.Equal(4, withGoals.GetProperty("items").GetArrayLength());
        await Ok(await workerClient.PostAsync($"/api/performance/reviews/{id}/goals/submit", null));
        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.PostAsJsonAsync($"/api/performance/reviews/{id}/goals/decide", new { approve = true })).StatusCode);
        Assert.Single((await Ok(await leadClient.GetAsync("/api/performance/reviews/team"))).EnumerateArray());
        var approved = await Ok(await leadClient.PostAsJsonAsync($"/api/performance/reviews/{id}/goals/decide", new { approve = true }));
        Assert.Equal("SelfReview", approved.GetProperty("stage").GetString());

        // Self review: every item rated before sending.
        var items = approved.GetProperty("items").EnumerateArray().Select(i => (Id: i.GetProperty("id").GetGuid(), Title: i.GetProperty("title").GetString()!)).ToList();
        object Scores(params int[] s) => items.Select((x, i) => new { itemId = x.Id, score = (int?)s[i], comment = (string?)"ok" }).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PutAsJsonAsync($"/api/performance/reviews/{id}/self", new { scores = new[] { new { itemId = items[0].Id, score = 5 } }, submit = true })).StatusCode);
        var self = await Ok(await workerClient.PutAsJsonAsync($"/api/performance/reviews/{id}/self", new { scores = Scores(5, 5, 4, 4), comment = "Good year", submit = true }));
        Assert.Equal("ManagerReview", self.GetProperty("stage").GetString());
        Assert.Equal(4.6m, self.GetProperty("selfScore").GetDecimal());

        // Manager review: potential is required; scores give 3.98 → rating 4. The employee does not see them yet.
        Assert.Equal(HttpStatusCode.BadRequest, (await leadClient.PutAsJsonAsync($"/api/performance/reviews/{id}/manager", new { scores = Scores(4, 5, 3, 4), submit = true })).StatusCode);
        var managed = await Ok(await leadClient.PutAsJsonAsync($"/api/performance/reviews/{id}/manager", new { scores = Scores(4, 5, 3, 4), comment = "Strong delivery", strengths = "Ownership", improvements = "Estimates", developmentPlan = "Lead a project", potential = 3, submit = true }));
        Assert.Equal(3.98m, managed.GetProperty("managerScore").GetDecimal());
        Assert.Equal(4, managed.GetProperty("finalRating").GetInt32());
        var hidden = await Ok(await workerClient.GetAsync($"/api/performance/reviews/{id}"));
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("managerScore").ValueKind);
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("items")[0].GetProperty("managerScore").ValueKind);

        // Calibration: HR sees the bell curve and raises, can adjust and release.
        var cal = await Ok(await admin.GetAsync($"/api/performance/cycles/{cycle}/calibration"));
        var row = cal.GetProperty("reviews").EnumerateArray().First(r => r.GetProperty("employeeId").GetGuid() == worker);
        Assert.Equal(10m, row.GetProperty("suggestedRaise").GetDecimal());
        Assert.Equal(100, cal.GetProperty("bellActual")[3].GetInt32());
        await Ok(await admin.PostAsJsonAsync($"/api/performance/reviews/{id}/calibrate", new { finalRating = 5, note = "Top performer across teams" }));
        Assert.Equal(1, (await Ok(await admin.PostAsJsonAsync($"/api/performance/cycles/{cycle}/release", new { }))).GetProperty("released").GetInt32());

        // Sign-off: the employee now sees everything and signs.
        var released = await Ok(await workerClient.GetAsync($"/api/performance/reviews/{id}"));
        Assert.Equal(5, released.GetProperty("finalRating").GetInt32());
        Assert.Equal("Outstanding", released.GetProperty("finalLabel").GetString());
        Assert.Equal(JsonValueKind.Null, released.GetProperty("calibrationNote").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.PostAsJsonAsync($"/api/performance/reviews/{id}/signoff", new { agree = false })).StatusCode);
        var done = await Ok(await workerClient.PostAsJsonAsync($"/api/performance/reviews/{id}/signoff", new { agree = true, comment = "Thanks" }));
        Assert.Equal("Done", done.GetProperty("stage").GetString());

        // HR can send it back one stage at a time.
        await Ok(await admin.PostAsync($"/api/performance/reviews/{id}/revert", null));
        Assert.Equal("Calibration", (await Ok(await admin.GetAsync($"/api/performance/reviews/{id}"))).GetProperty("stage").GetString());
        await Ok(await admin.PostAsync($"/api/performance/cycles/{cycle}/close", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await workerClient.GetAsync($"/api/performance/reviews/{id}")).StatusCode);
        _ = lead;
    }
}
