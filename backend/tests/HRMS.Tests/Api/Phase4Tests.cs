using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HRMS.Api.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>Quizzes and certificates, packages with module switches, paying invoices (bank transfer and webhook), and the free calculators.</summary>
public class Phase4Tests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    private static async Task<string?> Code(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("code", out var c) ? c.GetString() : null;

    private async Task<(Guid Id, HttpClient Client)> PersonAsync(HttpClient admin, string company, string first)
    {
        var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "Khan", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@{company}.test";
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Quiz-Pass-1234", role = "Employee" }));
        return (id, await factory.SignInAsync(email, "Quiz-Pass-1234"));
    }

    private static object Quiz(Guid programId, int passPercent = 60, int maxAttempts = 2, int? minutes = null) => new
    {
        programId, title = "Fire safety check", instructions = "Pick one answer for each question.", passPercent, timeLimitMinutes = minutes, maxAttempts, isActive = true,
        questions = new[]
        {
            new { text = "Which number do you call for the fire brigade?", options = new[] { "15", "16", "1122" }, correctIndex = 1 },
            new { text = "Where do you meet after leaving the building?", options = new[] { "Car park", "Assembly point" }, correctIndex = 1 },
            new { text = "Which extinguisher is for electrical fires?", options = new[] { "Water", "CO2", "Foam", "Sand" }, correctIndex = 1 },
        },
    };

    [Fact]
    public async Task Quiz_hides_answers_until_submitted_scores_on_the_server_and_issues_a_verifiable_certificate()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Quiz Co");
        await Ok(await admin.PostAsJsonAsync("/api/training/programs", new { title = "Fire safety", mode = "Classroom", isMandatory = false }, ApiFactory.Json));
        var programId = (await Ok(await admin.GetAsync("/api/training/programs"))).EnumerateArray().First().GetProperty("id").GetGuid();
        var quiz = await Ok(await admin.PostAsJsonAsync("/api/training/quizzes", Quiz(programId), ApiFactory.Json));
        var quizId = quiz.GetProperty("id").GetGuid();

        // A wrong right-answer index is refused.
        var bad = Quiz(programId);
        var badJson = JsonSerializer.Serialize(bad, ApiFactory.Json).Replace("\"correctIndex\":1}]", "\"correctIndex\":9}]");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/training/quizzes", new StringContent(badJson, Encoding.UTF8, "application/json"))).StatusCode);

        var (_, sana) = await PersonAsync(admin, "quizco", "Sana");
        // Employees cannot open HR's copy with the answers.
        Assert.Equal(HttpStatusCode.Forbidden, (await sana.GetAsync($"/api/training/quizzes/{quizId}")).StatusCode);

        var startRes = await sana.PostAsync($"/api/training/quizzes/{quizId}/start", null);
        var raw = await startRes.Content.ReadAsStringAsync();
        Assert.True(startRes.IsSuccessStatusCode, raw);
        Assert.DoesNotContain("correct", raw, StringComparison.OrdinalIgnoreCase);
        var paper = JsonDocument.Parse(raw).RootElement;
        var attemptId = paper.GetProperty("attemptId").GetGuid();
        Assert.Equal(3, paper.GetProperty("questions").GetArrayLength());

        // Answer two of three right (by question text, since the order is shuffled): 66% passes a 60% mark.
        var answers = new Dictionary<string, int>();
        foreach (var q in paper.GetProperty("questions").EnumerateArray())
        {
            var text = q.GetProperty("text").GetString()!;
            answers[q.GetProperty("id").GetString()!] = text.StartsWith("Which number") ? 1 : text.StartsWith("Where") ? 1 : 0;
        }
        var result = await Ok(await sana.PostAsJsonAsync($"/api/training/quizzes/attempts/{attemptId}/submit", new { answers }));
        Assert.Equal(2, result.GetProperty("correct").GetInt32());
        Assert.Equal(66, result.GetProperty("scorePercent").GetInt32());
        Assert.True(result.GetProperty("passed").GetBoolean());
        // The answers are shown only after submitting.
        Assert.All(result.GetProperty("review").EnumerateArray(), r => Assert.Equal(1, r.GetProperty("correctIndex").GetInt32()));
        var code = result.GetProperty("certificateCode").GetString()!;

        // Submitting the same attempt again is refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await sana.PostAsJsonAsync($"/api/training/quizzes/attempts/{attemptId}/submit", new { answers })).StatusCode);

        var cert = await Ok(await sana.GetAsync($"/api/training/certificates/{result.GetProperty("certificateId").GetGuid()}"));
        Assert.Equal("Sana Khan", cert.GetProperty("employeeName").GetString());
        Assert.Equal("Fire safety", cert.GetProperty("courseTitle").GetString());
        Assert.Equal($"/verify/{code}", cert.GetProperty("verifyPath").GetString());

        // The public check needs no sign-in and shows only the name, course and date.
        var verify = await Ok(await factory.CreateClient().GetAsync($"/api/public/certificates/{code.ToLowerInvariant()}"));
        Assert.Equal(["courseTitle", "employeeName", "issuedOn"], verify.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("Sana Khan", verify.GetProperty("employeeName").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/api/public/certificates/NOPE123456")).StatusCode);

        // Attempts run out: one wrong attempt, then the limit of two is reached.
        var (_, omar) = await PersonAsync(admin, "quizco", "Omar");
        var first = await Ok(await omar.PostAsync($"/api/training/quizzes/{quizId}/start", null));
        var failed = await Ok(await omar.PostAsJsonAsync($"/api/training/quizzes/attempts/{first.GetProperty("attemptId").GetGuid()}/submit",
            new { answers = new Dictionary<string, int>() }));
        Assert.False(failed.GetProperty("passed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, failed.GetProperty("certificateId").ValueKind);
        var second = await Ok(await omar.PostAsync($"/api/training/quizzes/{quizId}/start", null));

        // Past the time limit (plus grace) the attempt scores nothing, even with right answers.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            var attempt = await db.TrainingQuizAttempts.IgnoreQueryFilters().FirstAsync(a => a.Id == second.GetProperty("attemptId").GetGuid());
            attempt.DeadlineUtc = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }
        var all = second.GetProperty("questions").EnumerateArray().ToDictionary(q => q.GetProperty("id").GetString()!, _ => 1);
        var late = await Ok(await omar.PostAsJsonAsync($"/api/training/quizzes/attempts/{second.GetProperty("attemptId").GetGuid()}/submit", new { answers = all }));
        Assert.True(late.GetProperty("timedOut").GetBoolean());
        Assert.False(late.GetProperty("passed").GetBoolean());
        var third = await omar.PostAsync($"/api/training/quizzes/{quizId}/start", null);
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);

        var list = await Ok(await admin.GetAsync("/api/training/quizzes"));
        var row = list.EnumerateArray().Single();
        Assert.Equal(2, row.GetProperty("takers").GetInt32());
        Assert.Equal(50, row.GetProperty("passRate").GetInt32());
        Assert.Equal(3, (await Ok(await admin.GetAsync($"/api/training/quizzes/{quizId}/attempts"))).GetArrayLength());
        // A quiz people have taken cannot be deleted.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/training/quizzes/{quizId}")).StatusCode);
    }

    [Fact]
    public async Task Packages_switch_modules_off_with_a_clear_403_and_existing_companies_keep_everything()
    {
        var (admin, email, id) = await factory.CreateCompanyAsync("Module Co");
        var owner = await factory.SignInAsOwnerAsync();

        // A company without a package has every module.
        var me = await Ok(await admin.GetAsync("/api/auth/me"));
        Assert.Equal(JsonValueKind.Null, me.GetProperty("companyModules").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/training/programs")).StatusCode);

        var plan = await Ok(await owner.PostAsJsonAsync("/api/platform/plans", new
        {
            name = $"Starter {Guid.NewGuid():N}", pricePerEmployee = 150, flatPrice = 0, maxEmployees = 25, modules = new[] { "leave", "attendance" }, trialDays = 14,
        }));
        var planModules = plan.GetProperty("modules").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("employees", planModules); // core modules are always in
        Assert.DoesNotContain("training", planModules);

        await Ok(await owner.PutAsJsonAsync($"/api/platform/companies/{id}/plan", new { planId = plan.GetProperty("id").GetGuid(), trialEndsOn = "2099-01-31" }));

        var blocked = await admin.GetAsync("/api/training/programs");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("ModuleDisabled", await Code(blocked));
        Assert.NotEqual(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/leave/balances")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/employees")).StatusCode);

        var after = await Ok(await (await factory.SignInAsync(email, "Company-Pass-123")).GetAsync("/api/auth/me"));
        var modules = after.GetProperty("companyModules").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("leave", modules);
        Assert.DoesNotContain("training", modules);

        var billing = await Ok(await admin.GetAsync("/api/billing"));
        Assert.Equal(plan.GetProperty("name").GetString(), billing.GetProperty("planName").GetString());
        Assert.Equal("2099-01-31", billing.GetProperty("trialEndsOn").GetString());
        Assert.Equal(25, billing.GetProperty("maxEmployees").GetInt32());

        // The package is in use, so it cannot be deleted; taking the company off it turns everything back on.
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync($"/api/platform/plans/{plan.GetProperty("id").GetGuid()}")).StatusCode);
        await Ok(await owner.PutAsJsonAsync($"/api/platform/companies/{id}/plan", new { planId = (Guid?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/training/programs")).StatusCode);
    }

    private async Task<(HttpClient Admin, Guid Id, decimal Amount, JsonElement Invoice)> InvoiceAsync(string name)
    {
        var (admin, _, id) = await factory.CreateCompanyAsync(name);
        var owner = await factory.SignInAsOwnerAsync();
        await Ok(await owner.PutAsJsonAsync($"/api/platform/companies/{id}", new
        {
            name = $"{name} {Guid.NewGuid():N}", maxEmployees = 10, billingCycle = "Monthly", planPrice = 2000, pricePerEmployee = 100,
            subscriptionEndsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(5).ToString("yyyy-MM-dd"),
        }));
        var invoice = await Ok(await admin.PostAsJsonAsync("/api/billing/invoices", new { periods = 2 }));
        // (2,000 + 10 seats x 100) x 2 months.
        Assert.Equal(6000m, invoice.GetProperty("amount").GetDecimal());
        // Asking again reuses the open invoice.
        Assert.Equal(invoice.GetProperty("id").GetGuid(), (await Ok(await admin.PostAsJsonAsync("/api/billing/invoices", new { periods = 2 }))).GetProperty("id").GetGuid());
        return (admin, id, 6000m, invoice);
    }

    private static HttpRequestMessage Webhook(string body, string? signature, string? eventId = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhooks/safepay") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (signature is not null) req.Headers.Add(SafepayGateway.SignatureHeader, signature);
        if (eventId is not null) req.Headers.Add("X-SFPY-EVENT-ID", eventId);
        return req;
    }

    [Fact]
    public async Task Payment_webhook_needs_a_valid_signature_and_is_applied_only_once()
    {
        var (admin, id, amount, invoice) = await InvoiceAsync("Hook Co");
        var number = invoice.GetProperty("number").GetString();
        var owner = await factory.SignInAsOwnerAsync();
        var endsBefore = (await Ok(await admin.GetAsync("/api/billing"))).GetProperty("subscriptionEndsOn").GetString();
        var anon = factory.CreateClient();

        var body = JsonSerializer.Serialize(new { type = "payment:created", data = new { tracker = $"track_{Guid.NewGuid():N}", state = "PAID", amount, order_id = number } });
        var sig = SafepayGateway.Sign(body, ApiFactory.SafepayTestSecret);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.SendAsync(Webhook(body, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.SendAsync(Webhook(body, SafepayGateway.Sign(body, "wrong-secret")))).StatusCode);
        // A signature for a different body (amount changed) does not pass.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.SendAsync(Webhook(body.Replace("6000", "1"), sig))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhooks/nobody") { Content = new StringContent(body) })).StatusCode);

        var applied = await Ok(await anon.SendAsync(Webhook(body, sig)));
        Assert.Equal("Applied", applied.GetProperty("result").GetString());
        var repeat = await Ok(await anon.SendAsync(Webhook(body, sig)));
        Assert.Equal("Duplicate", repeat.GetProperty("result").GetString());
        // Even as a new event, a paid invoice is not paid twice.
        var other = await Ok(await anon.SendAsync(Webhook(body, sig, eventId: "evt-2")));
        Assert.Equal("Ignored", other.GetProperty("result").GetString());

        var billing = await Ok(await admin.GetAsync("/api/billing"));
        Assert.Equal("Paid", billing.GetProperty("invoices").EnumerateArray().Single(i => i.GetProperty("number").GetString() == number).GetProperty("status").GetString());
        Assert.Equal(DateOnly.Parse(endsBefore!).AddMonths(2).ToString("yyyy-MM-dd"), billing.GetProperty("subscriptionEndsOn").GetString());
        Assert.Single((await Ok(await owner.GetAsync($"/api/platform/companies/{id}/payments"))).EnumerateArray());

        // A webhook with the wrong amount does not pay the invoice.
        var next = await Ok(await admin.PostAsJsonAsync("/api/billing/invoices", new { periods = 1 }));
        var cheap = JsonSerializer.Serialize(new { data = new { tracker = "track_cheap", state = "PAID", amount = 1, order_id = next.GetProperty("number").GetString() } });
        Assert.Equal("Ignored", (await Ok(await anon.SendAsync(Webhook(cheap, SafepayGateway.Sign(cheap, ApiFactory.SafepayTestSecret))))).GetProperty("result").GetString());
        Assert.Single((await Ok(await owner.GetAsync($"/api/platform/companies/{id}/payments"))).EnumerateArray());
    }

    [Fact]
    public async Task Bank_transfer_receipt_is_approved_by_the_owner_and_extends_the_subscription()
    {
        var (admin, id, _, invoice) = await InvoiceAsync("Bank Co");
        var invoiceId = invoice.GetProperty("id").GetGuid();
        var billing = await Ok(await admin.GetAsync("/api/billing"));
        var providers = billing.GetProperty("providers").EnumerateArray().Select(p => p.GetProperty("key").GetString()).ToList();
        Assert.Contains("manual", providers);
        Assert.Contains("safepay", providers);

        var start = await Ok(await admin.PostAsJsonAsync($"/api/billing/invoices/{invoiceId}/pay", new { provider = "manual" }));
        Assert.Contains(invoice.GetProperty("number").GetString()!, start.GetProperty("instructions").GetString());

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 receipt"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "receipt.pdf");
        form.Add(new StringContent("IBFT-778899"), "reference");
        var proof = await Ok(await admin.PostAsync($"/api/billing/invoices/{invoiceId}/proof", form));
        Assert.Equal("AwaitingApproval", proof.GetProperty("status").GetString());

        var owner = await factory.SignInAsOwnerAsync();
        var waiting = await Ok(await owner.GetAsync("/api/platform/invoices"));
        Assert.Contains(waiting.EnumerateArray(), i => i.GetProperty("id").GetGuid() == invoiceId && i.GetProperty("hasProof").GetBoolean());
        Assert.Equal("application/pdf", (await owner.GetAsync($"/api/platform/invoices/{invoiceId}/proof")).Content.Headers.ContentType?.MediaType);
        // A company admin cannot approve their own payment.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/platform/invoices/{invoiceId}/approve", new { })).StatusCode);

        // Turned down with a reason, then uploaded again and approved.
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/platform/invoices/{invoiceId}/reject", new { note = "" })).StatusCode);
        await Ok(await owner.PostAsJsonAsync($"/api/platform/invoices/{invoiceId}/reject", new { note = "Not received yet" }));
        Assert.Equal("Unpaid", (await Ok(await admin.GetAsync("/api/billing"))).GetProperty("invoices").EnumerateArray().First().GetProperty("status").GetString());
        await Ok(await admin.PostAsync($"/api/billing/invoices/{invoiceId}/proof", form));
        await Ok(await owner.PostAsJsonAsync($"/api/platform/invoices/{invoiceId}/approve", new { note = "Received" }));

        var payments = await Ok(await owner.GetAsync($"/api/platform/companies/{id}/payments"));
        Assert.Equal("IBFT-778899", payments.EnumerateArray().Single().GetProperty("reference").GetString());
        Assert.Equal("Paid", (await Ok(await admin.GetAsync("/api/billing"))).GetProperty("invoices").EnumerateArray().First().GetProperty("status").GetString());
    }

    [Fact]
    public async Task Free_calculators_use_the_payroll_tax_slabs_and_need_no_sign_in()
    {
        var anon = factory.CreateClient();
        // 2025-26: Rs 3.6m a year is in the 30% slab: 346,000 + 30% of 400,000 = 466,000 a year.
        var tax = await Ok(await anon.GetAsync("/api/public/tools/salary-tax?monthlySalary=300000&taxYear=2025-26"));
        Assert.Equal("2025-26", tax.GetProperty("taxYear").GetString());
        Assert.Equal(466_000m, tax.GetProperty("annualTax").GetDecimal());
        Assert.Equal(38_833m, tax.GetProperty("monthlyTax").GetDecimal());
        Assert.Equal(300_000m - 38_833m, tax.GetProperty("monthlyTakeHome").GetDecimal());
        Assert.Equal(0.30m, tax.GetProperty("slab").GetProperty("rate").GetDecimal());
        Assert.Contains("2026-27", tax.GetProperty("taxYears").EnumerateArray().Select(y => y.GetString()));
        // Below Rs 600,000 a year there is no tax.
        Assert.Equal(0m, (await Ok(await anon.GetAsync("/api/public/tools/salary-tax?monthlySalary=50000&taxYear=2025-26"))).GetProperty("monthlyTax").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.GetAsync("/api/public/tools/salary-tax?monthlySalary=-5")).StatusCode);

        var eobi = await Ok(await anon.GetAsync("/api/public/tools/eobi?minimumWage=37000"));
        Assert.Equal(370m, eobi.GetProperty("employeeMonthly").GetDecimal());
        Assert.Equal(1850m, eobi.GetProperty("employerMonthly").GetDecimal());

        // 60,000 / 30 x 30 days x 5 years.
        var gratuity = await Ok(await anon.GetAsync("/api/public/tools/gratuity?lastMonthlySalary=60000&serviceYears=5"));
        Assert.Equal(300_000m, gratuity.GetProperty("gratuity").GetDecimal());
        var tooShort = await Ok(await anon.GetAsync("/api/public/tools/gratuity?lastMonthlySalary=60000&serviceYears=0.5"));
        Assert.False(tooShort.GetProperty("eligible").GetBoolean());
        Assert.Equal(0m, tooShort.GetProperty("gratuity").GetDecimal());

        // 90,000 / 30 = 3,000 a day x 12 days.
        var leave = await Ok(await anon.GetAsync("/api/public/tools/leave-encashment?monthlySalary=90000&days=12"));
        Assert.Equal(3000m, leave.GetProperty("dailyRate").GetDecimal());
        Assert.Equal(36_000m, leave.GetProperty("amount").GetDecimal());
    }
}
