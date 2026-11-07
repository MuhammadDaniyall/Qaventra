using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Domain;
using HRMS.Domain.Entities;

namespace HRMS.Tests.Api;

public class SubscriptionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Read(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static object Plan(string name, BillingCycle? cycle = null, int? maxEmployees = null, decimal? basePrice = null,
        decimal? perEmployee = null, string? endsOn = null) => new
    {
        name, phone = (string?)null, city = (string?)null, address = (string?)null, maxEmployees,
        billingCycle = cycle?.ToString(), planPrice = basePrice, pricePerEmployee = perEmployee, subscriptionEndsOn = endsOn,
    };

    [Fact]
    public async Task Inactive_company_blocks_already_signed_in_users_immediately()
    {
        var (admin, email, id) = await factory.CreateCompanyAsync("Mu");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/dashboard")).StatusCode);

        var owner = await factory.SignInAsOwnerAsync();
        await Read(await owner.PutAsJsonAsync($"/api/platform/companies/{id}/status", new { isActive = false }));

        var res = await admin.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("CompanyInactive", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // The owner can still open the company while it is inactive.
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/platform/companies/{id}/enter", null)).StatusCode);

        await Read(await owner.PutAsJsonAsync($"/api/platform/companies/{id}/status", new { isActive = true }));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/dashboard")).StatusCode);
        await factory.SignInAsync(email, "Company-Pass-123");
    }

    [Fact]
    public async Task Expired_subscription_blocks_sign_in_and_payment_renews_it_by_the_billing_cycle()
    {
        var (admin, email, id) = await factory.CreateCompanyAsync("Nu");
        var owner = await factory.SignInAsOwnerAsync();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-1);

        var company = await Read(await owner.PutAsJsonAsync($"/api/platform/companies/{id}",
            Plan($"Nu Renewed {Guid.NewGuid():N}", BillingCycle.Quarterly, maxEmployees: 50, basePrice: 1_000, perEmployee: 200, endsOn: yesterday.ToString("yyyy-MM-dd"))));
        Assert.Equal("Expired", company.GetProperty("access").GetString());
        // Base 1,000 + 50 seats x 200 x 3 months.
        Assert.Equal(31_000m, company.GetProperty("amountPerCycle").GetDecimal());

        var blocked = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "Company-Pass-123" });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("SubscriptionExpired", (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/dashboard")).StatusCode);

        var today = yesterday.AddDays(1);
        var renewed = await Read(await owner.PostAsJsonAsync($"/api/platform/companies/{id}/payments",
            new { amount = 31_000, paidOn = today.ToString("yyyy-MM-dd"), periods = 1, method = "Bank transfer", reference = "TXN-1" }));
        Assert.Equal("Allowed", renewed.GetProperty("access").GetString());
        // A lapsed subscription restarts today and runs one quarter.
        Assert.Equal(today.AddMonths(3).AddDays(-1).ToString("yyyy-MM-dd"), renewed.GetProperty("subscriptionEndsOn").GetString());

        // Paying again before expiry extends from the current end date, not from today.
        var again = await Read(await owner.PostAsJsonAsync($"/api/platform/companies/{id}/payments",
            new { amount = 31_000, paidOn = today.ToString("yyyy-MM-dd"), periods = 1 }));
        Assert.Equal(today.AddMonths(6).AddDays(-1).ToString("yyyy-MM-dd"), again.GetProperty("subscriptionEndsOn").GetString());

        var payments = await Read(await owner.GetAsync($"/api/platform/companies/{id}/payments"));
        Assert.Equal(2, payments.GetArrayLength());
        await factory.SignInAsync(email, "Company-Pass-123");
    }

    [Fact]
    public async Task Disabled_login_is_blocked_on_the_next_request()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Xi");
        var emp = await Read(await admin.PostAsJsonAsync("/api/employees",
            new { firstName = "Rabia", lastName = "Test", joiningDate = "2026-01-01", status = "Active" }));
        var empId = emp.GetProperty("id").GetGuid();
        var email = $"rabia+{Guid.NewGuid():N}@xi.test";
        await Read(await admin.PostAsJsonAsync($"/api/employees/{empId}/login", new { email, password = "Rabia-Pass-1", role = "Employee" }));
        var worker = await factory.SignInAsync(email, "Rabia-Pass-1");
        Assert.Equal(HttpStatusCode.OK, (await worker.GetAsync("/api/dashboard")).StatusCode);

        await Read(await admin.PutAsJsonAsync($"/api/employees/{empId}/login", new { role = "Employee", isActive = false }));
        var res = await worker.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("UserInactive", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_requires_the_exact_name_and_removes_all_company_data()
    {
        var (admin, email, id) = await factory.CreateCompanyAsync("Omicron");
        await Read(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Zara", lastName = "Test", joiningDate = "2026-01-01", status = "Active" }));
        var owner = await factory.SignInAsOwnerAsync();
        var company = await Read(await owner.GetAsync($"/api/platform/companies/{id}"));
        var name = company.GetProperty("name").GetString()!;

        var wrong = new HttpRequestMessage(HttpMethod.Delete, $"/api/platform/companies/{id}") { Content = JsonContent.Create(new { confirmName = "wrong" }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.SendAsync(wrong)).StatusCode);

        var ok = new HttpRequestMessage(HttpMethod.Delete, $"/api/platform/companies/{id}") { Content = JsonContent.Create(new { confirmName = name }) };
        Assert.Equal(HttpStatusCode.NoContent, (await owner.SendAsync(ok)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/platform/companies/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "Company-Pass-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/dashboard")).StatusCode);

        var logs = await Read(await owner.GetAsync($"/api/audit-logs?tenantId={id}&action=CompanyDeleted"));
        Assert.Equal(1, logs.GetProperty("total").GetInt32());
    }

    [Fact]
    public void Amount_per_cycle_uses_seats_or_headcount()
    {
        var tenant = new Tenant { BillingCycle = BillingCycle.Yearly, PlanPrice = 0, PricePerEmployee = 150 };
        Assert.Equal(150m * 20 * 12, tenant.AmountPerCycle(currentEmployees: 20));
        tenant.MaxEmployees = 30;
        Assert.Equal(150m * 30 * 12, tenant.AmountPerCycle(currentEmployees: 20));
        tenant.BillingCycle = null;
        Assert.Null(tenant.AmountPerCycle(20));
    }

    [Theory]
    [InlineData(30, null)]
    [InlineData(8, null)]
    [InlineData(7, 7)]
    [InlineData(5, 7)]
    [InlineData(3, 3)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    [InlineData(-6, -1)]
    [InlineData(-40, -7)]
    public void Reminder_stage_is_the_latest_one_reached(int daysLeft, int? stage) =>
        Assert.Equal(stage, HRMS.Api.Services.SubscriptionReminders.StageFor(daysLeft));

    [Theory]
    [InlineData("2026-01-30", "2026-01-15", BillingCycle.Monthly, 1, "2026-01-31", "2026-02-28")]
    [InlineData("2026-03-31", "2026-03-10", BillingCycle.Monthly, 1, "2026-04-01", "2026-04-30")]
    [InlineData("2026-06-30", "2026-06-01", BillingCycle.Quarterly, 2, "2026-07-01", "2026-12-31")]
    [InlineData("2025-12-31", "2026-02-10", BillingCycle.Yearly, 1, "2026-02-10", "2027-02-09")]
    public void Next_period_follows_the_end_date_or_restarts_today(string endsOn, string today, BillingCycle cycle, int periods, string from, string to)
    {
        var (f, t) = HRMS.Api.Services.SubscriptionService.NextPeriod(DateOnly.Parse(endsOn), DateOnly.Parse(today), cycle, periods);
        Assert.Equal((DateOnly.Parse(from), DateOnly.Parse(to)), (f, t));
    }

    [Fact]
    public async Task Ending_subscription_reminds_the_admin_once_and_the_owner_sees_it()
    {
        var (admin, _, id) = await factory.CreateCompanyAsync("Ri");
        var owner = await factory.SignInAsOwnerAsync();
        var inThreeDays = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(3);
        await Read(await owner.PutAsJsonAsync($"/api/platform/companies/{id}",
            Plan($"Ri Reminded {Guid.NewGuid():N}", BillingCycle.Monthly, maxEmployees: 10, basePrice: 0, perEmployee: 500, endsOn: inThreeDays.ToString("yyyy-MM-dd"))));

        using (var scope = factory.Services.CreateScope())
        {
            var reminders = scope.ServiceProvider.GetRequiredService<HRMS.Api.Services.SubscriptionReminders>();
            Assert.True(await reminders.RunAsync(id));
        }
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<HRMS.Api.Services.SubscriptionReminders>().RunAsync(id));

        var notes = (await Read(await admin.GetAsync("/api/notifications"))).ToString();
        Assert.Contains("ends in 3 days", notes);
        Assert.Contains("Rs 5,000 per monthly cycle", notes);
        var company = await Read(await owner.GetAsync($"/api/platform/companies/{id}"));
        Assert.Contains("ends in 3 days", company.GetProperty("lastReminder").GetString());

        // The owner can send one more by hand, once a day.
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/platform/companies/{id}/remind", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/api/platform/companies/{id}/remind", null)).StatusCode);
    }

    [Fact]
    public async Task Invoice_names_the_package_and_period_and_replaces_a_stale_one()
    {
        var (admin, _, id) = await factory.CreateCompanyAsync("Iv");
        var owner = await factory.SignInAsOwnerAsync();
        var endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(10);
        await Read(await owner.PutAsJsonAsync($"/api/platform/companies/{id}",
            Plan($"Iv Billed {Guid.NewGuid():N}", BillingCycle.Monthly, maxEmployees: 10, basePrice: 1_000, perEmployee: 100, endsOn: endsOn.ToString("yyyy-MM-dd"))));

        var first = await Read(await admin.PostAsJsonAsync("/api/billing/invoices", new { periods = 2 }));
        Assert.Equal(4_000m, first.GetProperty("amount").GetDecimal());
        var from = endsOn.AddDays(1);
        var (_, to) = HRMS.Api.Services.SubscriptionService.NextPeriod(endsOn, endsOn.AddDays(-10), BillingCycle.Monthly, 2);
        Assert.Contains($"{from:d MMM yyyy} to {to:d MMM yyyy}, 10 employees", first.GetProperty("description").GetString());

        // The price changes: a new invoice replaces the old unpaid one.
        await Read(await owner.PutAsJsonAsync($"/api/platform/companies/{id}",
            Plan($"Iv Billed {Guid.NewGuid():N}", BillingCycle.Monthly, maxEmployees: 20, basePrice: 1_000, perEmployee: 100, endsOn: endsOn.ToString("yyyy-MM-dd"))));
        var second = await Read(await admin.PostAsJsonAsync("/api/billing/invoices", new { periods = 2 }));
        Assert.Equal(6_000m, second.GetProperty("amount").GetDecimal());
        var billing = (await Read(await admin.GetAsync("/api/billing"))).ToString();
        Assert.Contains("Cancelled", billing);

        // The owner records the bank transfer: the open invoice is closed with it.
        await Read(await owner.PostAsJsonAsync($"/api/platform/companies/{id}/payments",
            new { amount = 6_000, paidOn = endsOn.AddDays(-10).ToString("yyyy-MM-dd"), periods = 2, method = "Bank transfer" }));
        var invoices = (await Read(await admin.GetAsync("/api/billing"))).GetProperty("invoices");
        Assert.Equal("Paid", invoices.EnumerateArray().First(i => i.GetProperty("amount").GetDecimal() == 6_000m).GetProperty("status").GetString());

        // A payment dated in the future is refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/platform/companies/{id}/payments",
            new { amount = 6_000, paidOn = endsOn.AddDays(30).ToString("yyyy-MM-dd"), periods = 1 })).StatusCode);
    }
}
