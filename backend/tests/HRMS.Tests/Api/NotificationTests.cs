using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Api.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>In-app notifications and their emails for approvals.</summary>
public class NotificationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private class CapturingSender : IEmailSender
    {
        public readonly ConcurrentQueue<EmailMessage> Sent = new();
        public Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            Sent.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    [Fact]
    public async Task Approvers_and_requesters_are_notified_in_app_and_by_email()
    {
        var sender = new CapturingSender();
        var app = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Smtp:Host", "smtp.test.invalid");
            b.UseSetting("Smtp:From", "noreply@test.invalid");
            b.ConfigureTestServices(s => s.AddSingleton<IEmailSender>(sender));
        });

        async Task<HttpClient> SignIn(string email, string password)
        {
            var client = app.CreateClient();
            var session = await Ok(await client.PostAsJsonAsync("/api/auth/login", new { email, password }));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("token").GetString());
            return client;
        }

        var (_, adminEmail, _) = await factory.CreateCompanyAsync("Notify");
        var admin = await SignIn(adminEmail, "Company-Pass-123");
        async Task<(Guid Id, HttpClient Client, string Email)> Person(string first, Guid? managerId, string role)
        {
            var id = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = first, lastName = "N", joiningDate = "2025-01-01", status = "Active", managerId }, ApiFactory.Json)))
                .GetProperty("id").GetGuid();
            var email = $"{first.ToLowerInvariant()}+{Guid.NewGuid():N}@notify.test";
            await Ok(await admin.PostAsJsonAsync($"/api/employees/{id}/login", new { email, password = "Notify-Pass-1", role }));
            return (id, await SignIn(email, "Notify-Pass-1"), email);
        }
        var lead = await Person("Lena", null, "Manager");
        var worker = await Person("Wali", lead.Id, "Employee");

        var claim = await Ok(await worker.Client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Taxi", items = new object[] { new { date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).ToString("yyyy-MM-dd"), category = "Travel", description = "Airport", amount = 700 } },
        }, ApiFactory.Json));

        var leadFeed = await Ok(await lead.Client.GetAsync("/api/notifications"));
        Assert.Equal(1, leadFeed.GetProperty("unread").GetInt32());
        var item = leadFeed.GetProperty("items")[0];
        Assert.Equal("ApprovalNeeded", item.GetProperty("kind").GetString());
        Assert.Contains("Wali N", item.GetProperty("title").GetString());
        Assert.Equal("/approvals", item.GetProperty("link").GetString());
        Assert.Equal(0, (await Ok(await worker.Client.GetAsync("/api/notifications"))).GetProperty("unread").GetInt32());

        // The email goes out in the background.
        for (var i = 0; i < 50 && !sender.Sent.Any(m => m.To == lead.Email); i++)
            await Task.Delay(100);
        var mail = Assert.Single(sender.Sent, m => m.To == lead.Email);
        Assert.Contains("needs your approval", mail.Subject);

        await Ok(await lead.Client.PostAsJsonAsync($"/api/expenses/{claim.GetProperty("id").GetGuid()}/approve", new { note = "Fine" }));
        var workerFeed = await Ok(await worker.Client.GetAsync("/api/notifications"));
        var decided = workerFeed.GetProperty("items")[0];
        Assert.Equal("Your expense claim was approved", decided.GetProperty("title").GetString());
        Assert.Contains("Fine", decided.GetProperty("body").GetString());

        await Ok(await worker.Client.PostAsync($"/api/notifications/{decided.GetProperty("id").GetGuid()}/read", null));
        Assert.Equal(0, (await Ok(await worker.Client.GetAsync("/api/notifications"))).GetProperty("unread").GetInt32());

        // Email can be switched off; the in-app notification still arrives.
        await Ok(await lead.Client.PutAsJsonAsync("/api/notifications/preferences", new { emailEnabled = false }));
        await Ok(await lead.Client.PostAsync("/api/notifications/read-all", null));
        var before = sender.Sent.Count(m => m.To == lead.Email);
        await Ok(await worker.Client.PostAsJsonAsync("/api/expenses", new
        {
            title = "Lunch", items = new object[] { new { date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).ToString("yyyy-MM-dd"), category = "Meals", description = "Client lunch", amount = 1500 } },
        }, ApiFactory.Json));
        await Task.Delay(500);
        Assert.Equal(before, sender.Sent.Count(m => m.To == lead.Email));
        var after = await Ok(await lead.Client.GetAsync("/api/notifications"));
        Assert.Equal(1, after.GetProperty("unread").GetInt32());
        Assert.False(after.GetProperty("emailEnabled").GetBoolean());
    }
}
