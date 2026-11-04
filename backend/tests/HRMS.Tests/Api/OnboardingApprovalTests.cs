using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>A template that needs approval: a finished onboarding waits for the Onboarding chain, can be sent back and sent again.</summary>
public class OnboardingApprovalTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Finished_onboarding_waits_for_approval()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("OnbApproval");
        await Ok(await admin.PutAsJsonAsync("/api/approvals/flows/Onboarding", new { steps = new object[] { new { kind = "Hr" } } }, ApiFactory.Json));
        var template = await Ok(await admin.PostAsJsonAsync("/api/onboarding/templates", new
        {
            name = "Checked joiner", isDefault = false, requiresApproval = true,
            tasks = new object[] { new { title = "Collect documents", category = "Paperwork", assigneeKind = "Hr", dueOffsetDays = 0 } },
        }, ApiFactory.Json));
        Assert.True(template.GetProperty("requiresApproval").GetBoolean());
        var emp = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Noor", lastName = "A", joiningDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), status = "Probation" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var started = await Ok(await admin.PostAsJsonAsync("/api/onboarding", new { employeeId = emp, templateId = template.GetProperty("id").GetGuid() }, ApiFactory.Json));
        var id = started.GetProperty("summary").GetProperty("id").GetGuid();
        var task = started.GetProperty("tasks")[0].GetProperty("id").GetGuid();

        async Task<string> Status() => (await Ok(await admin.GetAsync($"/api/onboarding/{id}"))).GetProperty("summary").GetProperty("status").GetString()!;
        await Ok(await admin.PostAsJsonAsync($"/api/onboarding/tasks/{task}", new { done = true }, ApiFactory.Json));
        Assert.Equal("PendingApproval", await Status());
        Assert.Contains((await Ok(await admin.GetAsync("/api/onboarding"))).EnumerateArray(), o => o.GetProperty("id").GetGuid() == id);

        // Sent back: in progress with the note; HR sends it again; approved completes it.
        await Ok(await admin.PostAsJsonAsync($"/api/approvals/Onboarding/{id}/decide", new { approve = false, note = "Bank form missing" }, ApiFactory.Json));
        Assert.Equal("InProgress", await Status());
        Assert.Equal("Bank form missing", (await Ok(await admin.GetAsync($"/api/onboarding/{id}"))).GetProperty("approvalNote").GetString());
        await Ok(await admin.PostAsync($"/api/onboarding/{id}/submit", null));
        Assert.Equal("PendingApproval", await Status());
        await Ok(await admin.PostAsJsonAsync($"/api/approvals/Onboarding/{id}/decide", new { approve = true }, ApiFactory.Json));
        Assert.Equal("Completed", await Status());
    }
}
