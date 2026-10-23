using System.Net;
using System.Net.Mail;
using System.Threading.Channels;

namespace HRMS.Api.Services;

public record EmailAttachment(string FileName, string ContentType, byte[] Data);

public record EmailMessage(string To, string Subject, string HtmlBody, IReadOnlyList<EmailAttachment>? Attachments = null);

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
        foreach (var a in message.Attachments ?? [])
            mail.Attachments.Add(new Attachment(new MemoryStream(a.Data), a.FileName, a.ContentType));
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

/// <summary>Sends queued emails one by one. Failures are logged and dropped; the in-app notification remains.</summary>
public class EmailBackgroundService(EmailQueue queue, IServiceProvider services, SmtpOptions options, ILogger<EmailBackgroundService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
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
}
