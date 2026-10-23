using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Clearance templates with levels and owners, exit interview templates and panel, settlement approval.</summary>
public class SeparationDepthTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    public async Task Levels_owners_exit_questions_and_settlement_approval()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Exit");
        async Task<(Guid, HttpClient)> Person(string first)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "Q", joiningDate = "2022-01-01", status = "Active" }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@exit.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Exit-Pass-1234", role = "Employee" }));
            return (id, await factory.SignInAsync(email, "Exit-Pass-1234"));
        }
        var (it, itClient) = await Person("Imran");
        var (leaver, leaverClient) = await Person("Laila");

        await Ok(await admin.PostAsJsonAsync("/api/separation-setup/clearance-templates", new
        {
            name = "Standard", isDefault = true,
            items = new object[]
            {
                new { department = "IT", title = "Laptop returned", level = 1, responsibleEmployeeId = it },
                new { department = "Finance", title = "Dues settled", level = 2 },
            },
        }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/separation-setup/exit-templates", new
        {
            name = "Exit", isDefault = true,
            questions = new object[] { new { text = "How was your manager?", kind = "Rating" }, new { text = "Would you come back?", kind = "YesNo" } },
        }, ApiFactory.Json));

        var sep = await Ok(await admin.PostAsJsonAsync("/api/separations", new
        {
            employeeId = leaver, type = "Resignation", noticeDate = Iso(Today.AddDays(-30)), lastWorkingDay = Iso(Today.AddDays(-1)), noticePeriodDays = 30, reason = "Moving",
        }, ApiFactory.Json));
        var id = sep.GetProperty("summary").GetProperty("id").GetGuid();
        var extras = await Ok(await admin.GetAsync($"/api/separation-setup/separations/{id}"));
        var levels = extras.GetProperty("clearance").EnumerateArray().ToList();
        Assert.Equal(2, levels.Count);
        var finance = levels.Single(l => l.GetProperty("level").GetInt32() == 2).GetProperty("itemId").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/separations/{id}/clearance/{finance}", new { isCleared = true }, ApiFactory.Json)).StatusCode);

        // IT clears its own item from "my clearances"; then finance can be cleared.
        var mine = (await Ok(await itClient.GetAsync("/api/separation-setup/my-clearances"))).EnumerateArray().Single();
        await Ok(await itClient.PutAsJsonAsync($"/api/separation-setup/separations/{id}/clearance/{mine.GetProperty("itemId").GetGuid()}/mine", new { isCleared = true }, ApiFactory.Json));
        await Ok(await admin.PutAsJsonAsync($"/api/separations/{id}/clearance/{finance}", new { isCleared = true }, ApiFactory.Json));

        // The leaver answers the exit questions.
        var questions = (await Ok(await leaverClient.GetAsync($"/api/separation-setup/separations/{id}"))).GetProperty("answers").EnumerateArray().ToList();
        Assert.Equal(2, questions.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await leaverClient.PutAsJsonAsync($"/api/separation-setup/separations/{id}/answers",
            new { answers = new[] { new { questionId = questions[0].GetProperty("questionId").GetGuid(), answer = "9" } } }, ApiFactory.Json)).StatusCode);
        await Ok(await leaverClient.PutAsJsonAsync($"/api/separation-setup/separations/{id}/answers", new
        {
            answers = new[]
            {
                new { questionId = questions[0].GetProperty("questionId").GetGuid(), answer = "4" },
                new { questionId = questions[1].GetProperty("questionId").GetGuid(), answer = "Yes" },
            },
        }, ApiFactory.Json));
        Assert.Equal("4", (await Ok(await admin.GetAsync($"/api/separation-setup/separations/{id}"))).GetProperty("answers")[0].GetProperty("answer").GetString());

        // Settlement: saved, sent for approval, completion waits until approved.
        await Ok(await admin.PutAsJsonAsync($"/api/separations/{id}/settlement", new { encashedLeaveDays = 0, recoverNoticeShortfall = false, otherAdditions = 1000, otherDeductions = 0 }, ApiFactory.Json));
        await Ok(await admin.PostAsync($"/api/separation-setup/separations/{id}/settlement/submit", null));
        var completing = await admin.PostAsync($"/api/separations/{id}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, completing.StatusCode);
        await Ok(await admin.PostAsJsonAsync($"/api/approvals/FinalSettlement/{id}/decide", new { approve = true, force = true, note = "OK" }, ApiFactory.Json));
        await Ok(await admin.PostAsync($"/api/separations/{id}/complete", null));
        await Ok(await admin.PostAsJsonAsync($"/api/separation-setup/separations/{id}/settlement/paid", new { via = "Cheque" }, ApiFactory.Json));
        Assert.Equal("Cheque", (await Ok(await admin.GetAsync($"/api/separation-setup/separations/{id}"))).GetProperty("settlementPaidVia").GetString());
    }
}
