using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>Bonuses, deductions, overtime, advance salary and loans flowing into payroll.</summary>
public class PayRequestTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private async Task<(HttpClient Admin, HttpClient Lead, HttpClient Worker, Guid WorkerId)> TeamAsync(string company)
    {
        var (admin, _, _) = await factory.CreateCompanyAsync(company);
        async Task<(Guid, HttpClient)> Person(string first, Guid? managerId, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "P", joiningDate = "2024-01-01", status = "Active", managerId }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            await Ok(await admin.PutAsJsonAsync($"/api/employees/{id}/salary", new { effectiveFrom = "2024-01-01", basicSalary = 120000, eobiApplicable = false, components = new object[] { new { name = "House Rent", kind = "Earning", amount = 54000, isTaxable = true } } }, ApiFactory.Json));
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{company.ToLowerInvariant()}.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Pay-Pass-1234", role }));
            return (id, await factory.SignInAsync(email, "Pay-Pass-1234"));
        }
        var (leadId, lead) = await Person("Lena", null, "Manager");
        var (workerId, worker) = await Person("Wali", leadId, "Employee");
        return (admin, lead, worker, workerId);
    }

    private static async Task<JsonElement> SlipLines(HttpClient admin, JsonElement run, Guid employeeId)
    {
        var slipId = run.GetProperty("payslips").EnumerateArray().First(p => p.GetProperty("employeeId").GetGuid() == employeeId).GetProperty("id").GetGuid();
        return (await Ok(await admin.GetAsync($"/api/payroll/payslips/{slipId}"))).GetProperty("lines");
    }

    [Fact]
    public async Task One_off_items_and_overtime_land_on_the_payslip_once()
    {
        var (admin, lead, worker, workerId) = await TeamAsync("Bonus");
        var (y, m) = (Today.Year, Today.Month);

        // HR enters a taxable bonus and a deduction; they are approved as entered. Employees cannot enter bonuses.
        var bonus = await Ok(await admin.PostAsJsonAsync("/api/pay/requests", new { employeeId = workerId, kind = "Bonus", title = "Performance bonus", amount = 25000, isTaxable = true, year = y, month = m }, ApiFactory.Json));
        Assert.Equal("Approved", bonus.GetProperty("status").GetString());
        await Ok(await admin.PostAsJsonAsync("/api/pay/requests", new { employeeId = workerId, kind = "Deduction", title = "Damaged laptop charger", amount = 6000, year = y, month = m }, ApiFactory.Json));
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.PostAsJsonAsync("/api/pay/requests", new { kind = "Bonus", amount = 99999, isTaxable = false, year = y, month = m }, ApiFactory.Json)).StatusCode);

        // Overtime: basic 120,000 / 30 / 8 x 1.5 x 4 h = 3,000.
        var day = Today.AddDays(-2).ToString("yyyy-MM-dd");
        var quote = await Ok(await worker.GetAsync($"/api/pay/overtime-quote?date={day}&hours=4"));
        Assert.Equal(3000m, quote.GetProperty("amount").GetDecimal());
        var ot = await Ok(await worker.PostAsJsonAsync("/api/pay/requests", new { kind = "Overtime", workDate = day, hours = 4, year = y, month = m, reason = "Release night" }, ApiFactory.Json));
        Assert.Equal("Pending", ot.GetProperty("status").GetString());
        await Ok(await lead.PostAsJsonAsync($"/api/pay/requests/{ot.GetProperty("id").GetGuid()}/approve", new { }));

        // Advance capped at one month's gross (174,000).
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/pay/requests", new { kind = "AdvanceSalary", amount = 500000, year = y, month = m }, ApiFactory.Json)).StatusCode);
        var adv = await Ok(await worker.PostAsJsonAsync("/api/pay/requests", new { kind = "AdvanceSalary", amount = 40000, year = y, month = m, reason = "Medical bill" }, ApiFactory.Json));
        await Ok(await lead.PostAsJsonAsync($"/api/pay/requests/{adv.GetProperty("id").GetGuid()}/approve", new { }));

        var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = y, month = m }));
        run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = y, month = m })); // reprocess: still once
        var lines = (await SlipLines(admin, run, workerId)).EnumerateArray().ToList();
        Assert.Single(lines, l => l.GetProperty("name").GetString() == "Performance bonus" && l.GetProperty("amount").GetDecimal() == 25000);
        Assert.Single(lines, l => l.GetProperty("name").GetString() == "Overtime (4 h)" && l.GetProperty("amount").GetDecimal() == 3000);
        Assert.Single(lines, l => l.GetProperty("name").GetString() == "Damaged laptop charger" && l.GetProperty("kind").GetString() == "Deduction");
        Assert.Single(lines, l => l.GetProperty("name").GetString() == "Advance salary recovery" && l.GetProperty("amount").GetDecimal() == 40000);

        await Ok(await admin.PostAsync($"/api/payroll/runs/{run.GetProperty("run").GetProperty("id").GetGuid()}/finalize", null));
        var mine = await Ok(await worker.GetAsync("/api/pay/requests/my"));
        Assert.All(mine.EnumerateArray().Where(r => r.GetProperty("status").GetString() == "Approved"), r => Assert.NotEqual(JsonValueKind.Null, r.GetProperty("paidAtUtc").ValueKind));

        // Paid items cannot be cancelled; the finalized month takes no new items.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/pay/requests/{bonus.GetProperty("id").GetGuid()}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/pay/requests", new { employeeId = workerId, kind = "Bonus", amount = 1000, isTaxable = true, year = y, month = m }, ApiFactory.Json)).StatusCode);
    }

    [Fact]
    public async Task Loans_are_repaid_one_installment_per_payroll()
    {
        var (admin, lead, worker, workerId) = await TeamAsync("Loans");
        var start = new DateOnly(Today.Year, Today.Month, 1);
        var loan = await Ok(await worker.PostAsJsonAsync("/api/pay/loans", new { title = "Motorbike loan", amount = 30000, installments = 3, startYear = start.Year, startMonth = start.Month, reason = "Commute" }));
        Assert.Equal(10000m, loan.GetProperty("installmentAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/pay/loans", new { amount = 1000, installments = 1, startYear = start.Year, startMonth = start.Month })).StatusCode);
        await Ok(await lead.PostAsJsonAsync($"/api/pay/loans/{loan.GetProperty("id").GetGuid()}/approve", new { }));

        for (var i = 1; i <= 3; i++)
        {
            var month = start.AddMonths(i - 1);
            var run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = month.Year, month = month.Month }));
            run = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = month.Year, month = month.Month }));
            var lines = (await SlipLines(admin, run, workerId)).EnumerateArray().ToList();
            Assert.Single(lines, l => l.GetProperty("name").GetString() == $"Motorbike loan installment ({i}/3)" && l.GetProperty("amount").GetDecimal() == 10000);
            await Ok(await admin.PostAsync($"/api/payroll/runs/{run.GetProperty("run").GetProperty("id").GetGuid()}/finalize", null));
        }

        var mine = (await Ok(await worker.GetAsync("/api/pay/loans/my"))).EnumerateArray().Single();
        Assert.Equal(30000m, mine.GetProperty("repaid").GetDecimal());
        Assert.Equal(0m, mine.GetProperty("remaining").GetDecimal());

        // A fourth month takes nothing.
        var after = start.AddMonths(3);
        var fourth = await Ok(await admin.PostAsJsonAsync("/api/payroll/runs", new { year = after.Year, month = after.Month }));
        Assert.DoesNotContain((await SlipLines(admin, fourth, workerId)).EnumerateArray(), l => l.GetProperty("name").GetString()!.Contains("Motorbike"));
    }
}
