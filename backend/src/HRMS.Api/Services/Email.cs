using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record EmailAttachment(string FileName, string ContentType, byte[] Data);

/// <param name="TenantId">The company it is sent for; set to log it and count it against the company's monthly quota.</param>
/// <param name="ReplyTo">Where replies go, when not the From address (ticket emails point it at the helpdesk inbox).</param>
public record EmailMessage(string To, string Subject, string HtmlBody, IReadOnlyList<EmailAttachment>? Attachments = null, Guid? TenantId = null, string? Kind = null,
    string? ReplyTo = null);

/// <summary>SMTP settings ("Smtp" section). Leave Host empty to switch email off; in-app notifications still work.</summary>
public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "Qaventra";
    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public class SmtpEmailSender(SmtpOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            Credentials = string.IsNullOrEmpty(options.User) ? null : new NetworkCredential(options.User, options.Password),
        };
        using var mail = new MailMessage
        {
            From = new MailAddress(options.From, options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(message.To);
        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
            mail.ReplyToList.Add(message.ReplyTo);
        foreach (var a in message.Attachments ?? [])
            mail.Attachments.Add(new System.Net.Mail.Attachment(new MemoryStream(a.Data), a.FileName, a.ContentType));
        await client.SendMailAsync(mail, ct);
    }
}

/// <summary>Emails waiting to be sent. Requests only enqueue, so a slow mail server never slows the app.</summary>
public class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Sends queued emails one by one. Failures are logged and dropped; the in-app notification remains. Company emails
/// are written to the company's email log and stop for the month once its quota is used up.
/// </summary>
public class EmailBackgroundService(EmailQueue queue, IServiceProvider services, SmtpOptions options, ILogger<EmailBackgroundService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            if (message.TenantId is { } tenantId && tenantId != Guid.Empty)
            {
                await SendForCompanyAsync(message, tenantId, stoppingToken);
                continue;
            }
            if (!options.IsConfigured)
            {
                log.LogDebug("Email is not configured; skipped \"{Subject}\".", message.Subject);
                continue;
            }
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Could not send email \"{Subject}\".", message.Subject);
            }
        }
    }

    private async Task SendForCompanyAsync(EmailMessage message, Guid tenantId, CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            db.UseTenant(tenantId);
            var status = EmailLogStatus.Sent;
            string? error = null;
            if (!options.IsConfigured)
                status = EmailLogStatus.NotConfigured;
            else
            {
                var quota = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.EmailQuotaPerMonth).FirstOrDefaultAsync(ct);
                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                if (quota is { } q && await db.EmailLogs.CountAsync(l => l.Status == EmailLogStatus.Sent && l.CreatedAtUtc >= monthStart, ct) >= q)
                    status = EmailLogStatus.OverQuota;
                else
                {
                    try
                    {
                        await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(message, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        status = EmailLogStatus.Failed;
                        error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                        log.LogWarning(ex, "Could not send email \"{Subject}\".", message.Subject);
                    }
                }
            }
            db.EmailLogs.Add(new EmailLog
            {
                TenantId = tenantId, To = message.To, Subject = message.Subject.Length > 300 ? message.Subject[..300] : message.Subject,
                Kind = message.Kind, Status = status, Error = error,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Could not send or log email \"{Subject}\".", message.Subject);
        }
    }
}
