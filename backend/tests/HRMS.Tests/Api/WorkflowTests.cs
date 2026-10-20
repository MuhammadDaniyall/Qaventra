using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>HR letters, expense claims and separations end to end.</summary>
public class WorkflowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // A 1x1 PNG.
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private record Team(HttpClient Admin, HttpClient Manager, HttpClient Worker, Guid WorkerId, string WorkerEmail);

    private async Task<Team> CreateTeamAsync(string company)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(company);
        var desig = await Ok(await admin.PostAsJsonAsync("/api/designations", new { title = "Accountant" }));
        var manager = await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Faisal", lastName = "Lead", joiningDate = "2025-01-01", status = "Active" }, ApiFactory.Json));
        var worker = await Ok(await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Rabia", lastName = "Noor", joiningDate = "2025-03-01", status = "Active", cnic = "35202-7654321-2",
            designationId = desig.GetProperty("id").GetGuid(), managerId = manager.GetProperty("id").GetGuid(),
        }, ApiFactory.Json));
        var workerId = worker.GetProperty("id").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"/api/employees/{workerId}/salary", new
        {
            effectiveFrom = "2025-03-01", basicSalary = 80_000, eobiApplicable = false,
            components = new object[] { new { name = "House Rent", kind = "Earning", amount = 40_000, isTaxable = true } },
        }, ApiFactory.Json));

        var domain = company.ToLowerInvariant();
        var mgrEmail = $"faisal+{Guid.NewGuid():N}@{domain}.test";
        var workerEmail = $"rabia+{Guid.NewGuid():N}@{domain}.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{manager.GetProperty("id").GetGuid()}/login", new { email = mgrEmail, password = "Faisal-Pass-1", role = "Manager" }));
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{workerId}/login", new { email = workerEmail, password = "Rabia-Pass-1", role = "Employee" }));
        return new Team(admin, await factory.SignInAsync(mgrEmail, "Faisal-Pass-1"), await factory.SignInAsync(workerEmail, "Rabia-Pass-1"), workerId, workerEmail);
    }

    private static Task<HttpResponseMessage> SubmitClaim(HttpClient client, decimal amount, bool withReceipt = true) =>
        client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Client visit",
            items = new object[]
            {
                new
                {
                    date = Today.ToString("yyyy-MM-dd"), category = "Travel", description = "Careem to client office", amount,
                    receipt = withReceipt ? new { fileName = "ride.png", contentType = "image/png", base64 = Png } : null,
                },
            },
        }, ApiFactory.Json);

    [Fact]
    public async Task Letters_are_rendered_numbered_and_shared_only_with_their_employee()
    {
        var t = await CreateTeamAsync("Lumen");
        var other = await CreateTeamAsync("Orbit");

        var templates = (await Ok(await t.Admin.GetAsync("/api/letters/templates"))).EnumerateArray().ToList();
        Assert.True(templates.Count >= 8);
        var experience = templates.First(x => x.GetProperty("category").GetString() == "Experience");

        var preview = await Ok(await t.Admin.PostAsJsonAsync("/api/letters/preview", new
        {
            employeeId = t.WorkerId, subject = "For {{EmployeeName}}", body = experience.GetProperty("body").GetString(),
        }));
        Assert.Equal("For Rabia Noor", preview.GetProperty("subject").GetString());
        Assert.Contains("35202-7654321-2", preview.GetProperty("body").GetString());

        var bad = await t.Admin.PostAsJsonAsync("/api/letters", new { employeeId = t.WorkerId, category = "Other", subject = "x", body = "{{Bonus}}" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var issued = await Ok(await t.Admin.PostAsJsonAsync("/api/letters", new
        {
            employeeId = t.WorkerId, category = "Experience", subject = experience.GetProperty("subject").GetString(),
            body = experience.GetProperty("body").GetString(), shareWithEmployee = true,
        }, ApiFactory.Json));
        Assert.Equal($"HR/EXP/{Today.Year}/0001", issued.GetProperty("referenceNo").GetString());
        var second = await Ok(await t.Admin.PostAsJsonAsync("/api/letters", new
        {
            employeeId = t.WorkerId, category = "Experience", subject = "Private", body = "HR only", shareWithEmployee = false,
        }, ApiFactory.Json));
        Assert.Equal($"HR/EXP/{Today.Year}/0002", second.GetProperty("referenceNo").GetString());

        var mine = (await Ok(await t.Worker.GetAsync("/api/letters/my"))).EnumerateArray().ToList();
        Assert.Single(mine);
        var doc = await Ok(await t.Worker.GetAsync($"/api/letters/{issued.GetProperty("id").GetGuid()}"));
        Assert.Contains("Rabia Noor", doc.GetProperty("body").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await t.Worker.GetAsync($"/api/letters/{second.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await t.Worker.GetAsync("/api/letters/templates")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/letters/{issued.GetProperty("id").GetGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Approved_expenses_are_paid_through_payroll()
    {
        var t = await CreateTeamAsync("Vega");
        var claim = await Ok(await SubmitClaim(t.Worker, 1_850));
        var claimId = claim.GetProperty("id").GetGuid();
        Assert.Equal(1, claim.GetProperty("receiptCount").GetInt32());

        // The manager sees the claim and its receipt.
        var pending = (await Ok(await t.Manager.GetAsync("/api/expenses/approvals"))).EnumerateArray().ToList();
        Assert.Contains(pending, c => c.GetProperty("id").GetGuid() == claimId);
        var detail = await Ok(await t.Manager.GetAsync($"/api/expenses/{claimId}"));
        Assert.True(detail.GetProperty("canDecide").GetBoolean());
        var receiptId = detail.GetProperty("items")[0].GetProperty("receiptId").GetGuid();
        var receipt = await t.Manager.GetAsync($"/api/expenses/{claimId}/receipts/{receiptId}");
        Assert.Equal("image/png", receipt.Content.Headers.ContentType?.MediaType);

        await Ok(await t.Manager.PostAsJsonAsync($"/api/expenses/{claimId}/approve", new { note = "OK" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await t.Manager.PostAsJsonAsync($"/api/expenses/{claimId}/approve", new { })).StatusCode);

        var run = await Ok(await t.Admin.PostAsJsonAsync("/api/payroll/runs", new { year = Today.Year, month = Today.Month }));
        var runId = run.GetProperty("run").GetProperty("id").GetGuid();

        // Reprocessing keeps the claim on the same run exactly once.
        run = await Ok(await t.Admin.PostAsJsonAsync("/api/payroll/runs", new { year = Today.Year, month = Today.Month }));
        var slipId = run.GetProperty("payslips").EnumerateArray().First(p => p.GetProperty("employeeId").GetGuid() == t.WorkerId).GetProperty("id").GetGuid();
        var slip = await Ok(await t.Admin.GetAsync($"/api/payroll/payslips/{slipId}"));
        Assert.Single(slip.GetProperty("lines").EnumerateArray(), l => l.GetProperty("name").GetString() == "Expense reimbursement" && l.GetProperty("amount").GetDecimal() == 1_850);
        await Ok(await t.Admin.PostAsync($"/api/payroll/runs/{runId}/finalize", null));

        var mine = (await Ok(await t.Worker.GetAsync("/api/expenses/my"))).EnumerateArray().Single();
        Assert.Equal("Paid", mine.GetProperty("status").GetString());
        Assert.StartsWith("Payroll", mine.GetProperty("paidVia").GetString());
    }

    [Fact]
    public async Task Resignation_runs_through_clearance_settlement_and_switches_off_the_login()
    {
        var t = await CreateTeamAsync("Nimbus");
        var claim = await Ok(await SubmitClaim(t.Worker, 1_500, withReceipt: false));
        await Ok(await t.Manager.PostAsJsonAsync($"/api/expenses/{claim.GetProperty("id").GetGuid()}/approve", new { }));

        var today = Today.ToString("yyyy-MM-dd");
        var sep = await Ok(await t.Worker.PostAsJsonAsync("/api/separations/resign", new { proposedLastDay = today, reason = "Moving abroad" }));
        var id = sep.GetProperty("summary").GetProperty("id").GetGuid();
        Assert.Equal("Requested", sep.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await t.Worker.PostAsJsonAsync("/api/separations/resign", new { proposedLastDay = today })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await t.Worker.PostAsJsonAsync($"/api/separations/{id}/accept", new { lastWorkingDay = today, noticePeriodDays = 30 })).StatusCode);

        var accepted = await Ok(await t.Admin.PostAsJsonAsync($"/api/separations/{id}/accept", new { lastWorkingDay = today, noticePeriodDays = 30 }));
        Assert.Equal(6, accepted.GetProperty("clearance").GetArrayLength());
        Assert.Equal(1_500m, accepted.GetProperty("settlement").GetProperty("expenseReimbursement").GetDecimal());

        Assert.Equal(HttpStatusCode.BadRequest, (await t.Admin.PostAsync($"/api/separations/{id}/complete", null)).StatusCode);

        // The employee fills the exit interview but never sees a draft settlement.
        var afterInterview = await Ok(await t.Worker.PutAsJsonAsync($"/api/separations/{id}/exit-interview", new { exitReason = "Relocation", rating = 4, wouldRecommend = true, feedback = "Great team" }));
        Assert.Equal(JsonValueKind.Null, afterInterview.GetProperty("settlement").ValueKind);

        var detail = await Ok(await t.Admin.GetAsync($"/api/separations/{id}"));
        Assert.Contains(detail.GetProperty("clearance").EnumerateArray(), i => i.GetProperty("department").GetString() == "HR" && i.GetProperty("isCleared").GetBoolean());
        foreach (var item in detail.GetProperty("clearance").EnumerateArray().Where(i => !i.GetProperty("isCleared").GetBoolean()))
            await Ok(await t.Admin.PutAsJsonAsync($"/api/separations/{id}/clearance/{item.GetProperty("id").GetGuid()}", new { isCleared = true }));

        var saved = await Ok(await t.Admin.PutAsJsonAsync($"/api/separations/{id}/settlement", new
        {
            encashedLeaveDays = 3, recoverNoticeShortfall = false, otherAdditions = 0, otherDeductions = 0,
        }));
        var s = saved.GetProperty("settlement");
        Assert.Equal(4_000m, s.GetProperty("dailyRate").GetDecimal());
        Assert.Equal(12_000m + 1_500, s.GetProperty("net").GetDecimal());

        var done = await Ok(await t.Admin.PostAsync($"/api/separations/{id}/complete", null));
        Assert.Equal("Completed", done.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal(13_500m, done.GetProperty("settlement").GetProperty("net").GetDecimal());

        var employee = await Ok(await t.Admin.GetAsync($"/api/employees/{t.WorkerId}"));
        Assert.Equal("Resigned", employee.GetProperty("status").GetString());
        var paid = (await Ok(await t.Admin.GetAsync("/api/expenses/approvals?status=Paid"))).EnumerateArray().ToList();
        Assert.Contains(paid, c => c.GetProperty("paidVia").GetString() == "Final settlement");

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = t.WorkerEmail, password = "Rabia-Pass-1" });
        Assert.False(login.IsSuccessStatusCode);
    }
}
