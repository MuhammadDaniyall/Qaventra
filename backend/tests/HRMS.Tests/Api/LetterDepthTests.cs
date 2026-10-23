using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Self-service letters through approval, letters needing approval, automatic letters on events.</summary>
public class LetterDepthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Requests_approvals_and_automatic_letters()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Letters");
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Rabia", lastName = "L", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 100000, eobiApplicable = false, components = Array.Empty<object>() }, ApiFactory.Json));
        var email = $"rabia+{Guid.NewGuid():N}@letters.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Letter-Pass-1234", role = "Employee" }));
        var rabia = await factory.SignInAsync(email, "Letter-Pass-1234");

        await Ok(await admin.PostAsJsonAsync("/api/letters/templates", new
        {
            name = "Salary certificate", category = "SalaryCertificate", subject = "Salary certificate", body = "{{EmployeeName}} earns {{GrossSalary}}.",
            isActive = true, selfService = true, marginTopMm = 40,
        }, ApiFactory.Json));
        await Ok(await admin.PostAsJsonAsync("/api/letters/templates", new
        {
            name = "Increment", category = "Increment", subject = "Salary revision", body = "From {{EffectiveDate}} your gross is {{GrossSalary}}.",
            isActive = true, autoUse = "SalaryRevision",
        }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/letters/templates", new
        {
            name = "Increment 2", category = "Increment", subject = "x", body = "y", isActive = true, autoUse = "SalaryRevision",
        }, ApiFactory.Json)).StatusCode);

        // Self-service: Rabia asks, HR approves through the letter chain, then she sees it.
        var template = (await Ok(await rabia.GetAsync("/api/letters/self-service"))).EnumerateArray().Single().GetProperty("id").GetGuid();
        var req = await Ok(await rabia.PostAsJsonAsync("/api/letters/request", new { templateId = template, reason = "Bank loan" }, ApiFactory.Json));
        Assert.Equal("Pending", req.GetProperty("status").GetString());
        Assert.Empty((await Ok(await rabia.GetAsync("/api/letters/my"))).EnumerateArray());
        await Ok(await admin.PostAsJsonAsync($"/api/approvals/Letter/{req.GetProperty("id").GetGuid()}/decide", new { approve = true }, ApiFactory.Json));
        var mine = (await Ok(await rabia.GetAsync("/api/letters/my"))).EnumerateArray().Single();
        var doc = await Ok(await rabia.GetAsync($"/api/letters/{mine.GetProperty("id").GetGuid()}"));
        Assert.Contains("Rs 100,000", doc.GetProperty("body").GetString());
        Assert.Equal(40, doc.GetProperty("marginTopMm").GetInt32());

        // A salary revision issues the increment letter with the new salary.
        var first = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1);
        await Ok(await admin.PostAsJsonAsync("/api/pay-changes/revisions", new
        {
            effectiveFrom = first.ToString("yyyy-MM-dd"), kind = "Increment", includeAllowances = false, reason = "Review",
            lines = new[] { new { employeeId = id, percent = 10m } },
        }, ApiFactory.Json));
        var letters = (await Ok(await rabia.GetAsync("/api/letters/my"))).EnumerateArray().ToList();
        var increment = letters.Single(l => l.GetProperty("subject").GetString() == "Salary revision");
        var body = (await Ok(await rabia.GetAsync($"/api/letters/{increment.GetProperty("id").GetGuid()}"))).GetProperty("body").GetString();
        Assert.Contains("Rs 110,000", body);
    }
}
