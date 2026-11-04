using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Api.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

/// <summary>A company's own mail server: the password is encrypted and never returned, and the test send goes through it.</summary>
public class CompanySmtpTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    [Fact]
    public async Task Own_mail_server_is_saved_encrypted_and_tested()
    {
        var sender = new CapturingSender();
        var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IEmailSender>(sender)));
        var (_, adminEmail, _) = await factory.CreateCompanyAsync("OwnSmtp");
        var admin = app.CreateClient();
        var session = await (await admin.PostAsJsonAsync("/api/auth/login", new { email = adminEmail, password = "Company-Pass-123" })).Content.ReadFromJsonAsync<JsonElement>();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("token").GetString());

        object Body(string host, int port, string? password) => new { host, port, user = "hr@acme.test", password, from = "hr@acme.test", fromName = "Acme HR", enableSsl = true, isEnabled = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/setup/email/smtp", Body("localhost", 587, "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/setup/email/smtp", Body("10.0.0.5", 587, "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/setup/email/smtp", Body("203.0.113.5", 8080, "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/setup/email/smtp", Body("203.0.113.5", 587, "s3cret-Pass"))).StatusCode);

        var saved = await admin.GetStringAsync("/api/setup/email/smtp");
        Assert.DoesNotContain("s3cret", saved);
        Assert.True(JsonDocument.Parse(saved).RootElement.GetProperty("hasPassword").GetBoolean());

        // Saving again without a password keeps it.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/setup/email/smtp", Body("203.0.113.5", 587, null))).StatusCode);
        var test = await (await admin.PostAsJsonAsync("/api/setup/email/smtp/test", new { to = "someone@acme.test" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(test.GetProperty("ok").GetBoolean());
        var sent = Assert.Single(sender.Sent);
        Assert.Equal("203.0.113.5", sent.Smtp!.Host);
        Assert.Equal("s3cret-Pass", sent.Smtp.Password);
    }
}
