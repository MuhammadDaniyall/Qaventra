using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Data;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace HRMS.Api.Services;

/// <summary>
/// The mailbox that receives replies to ticket emails ("HelpdeskInbox" section). Any IMAP mailbox works, e.g. a Gmail
/// account with an app password. Leave Host empty to switch email replies off.
/// </summary>
public class HelpdeskInboxOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>The address replies are sent to (the Reply-To of ticket emails). Defaults to User.</summary>
    public string? ReplyAddress { get; set; }
    public string Folder { get; set; } = "INBOX";
    public int PollSeconds { get; set; } = 120;

    public string Address => string.IsNullOrWhiteSpace(ReplyAddress) ? User : ReplyAddress;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(User);
}

/// <param name="Authentication">The Authentication-Results header, when the mail server adds one.</param>
public record InboundEmail(string From, string Subject, string? Text, string? Html, string? Authentication, bool IsAutomatic);

public enum InboundResult
{
    Added,
    NoTicketCode,
    UnknownSender,
    FailedAuthentication,
    Automatic,
    TicketNotFound,
    Empty,
    Refused,
}

/// <summary>Turns an email reply into a reply on its ticket, written as the person who sent it.</summary>
public class HelpdeskInbox(IServiceProvider services, ILogger<HelpdeskInbox> log)
{
    private static readonly Regex Code = new(@"\bHD-(\d{1,9})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The ticket code (HD-0042) in a subject or title, if any.</summary>
    public static string? TicketCode(string? text) => text is not null && Code.Match(text) is { Success: true } m ? m.Value.ToUpperInvariant() : null;

    public async Task<InboundResult> ProcessAsync(InboundEmail mail)
    {
        if (mail.IsAutomatic || Regex.IsMatch(mail.Subject, @"^\s*(out of office|automatic reply|auto(matic)?[- ]?reply|undeliverable|delivery status notification)", RegexOptions.IgnoreCase))
            return InboundResult.Automatic;
        // Mail servers that check the sender say so; a failed check means the From address may be forged.
        if (mail.Authentication is { } auth && Regex.IsMatch(auth, @"\b(dmarc|spf|dkim)=fail\b", RegexOptions.IgnoreCase))
            return InboundResult.FailedAuthentication;
        if (Code.Match(mail.Subject) is not { Success: true } match || !int.TryParse(match.Groups[1].Value, out var number))
            return InboundResult.NoTicketCode;
        var body = ReplyText(mail.Text ?? HtmlToText(mail.Html));
        if (body.Length == 0)
            return InboundResult.Empty;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
        var email = mail.From.Trim().ToLowerInvariant();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email && u.IsActive && u.TenantId != Guid.Empty);
        if (user is null)
            return InboundResult.UnknownSender;

        // Everything below runs as the sender, with their rights in their own company.
        ((CurrentUser)scope.ServiceProvider.GetRequiredService<ICurrentUser>())
            .ActAs(new ClaimsPrincipal(new ClaimsIdentity(JwtTokenService.ClaimsFor(user), "email")));
        db.UseTenant(user.TenantId);
        var ticket = await db.Tickets.Where(t => t.Number == number).Select(t => (Guid?)t.Id).FirstOrDefaultAsync();
        if (ticket is null)
            return InboundResult.TicketNotFound;
        try
        {
            await scope.ServiceProvider.GetRequiredService<HelpdeskService>().CommentAsync(ticket.Value, body, isInternal: false);
            return InboundResult.Added;
        }
        catch (Exception ex) when (ex is DomainException or ForbiddenException or NotFoundException)
        {
            log.LogInformation("Email reply from {From} on HD-{Number} refused: {Reason}", email, number, ex.Message);
            return InboundResult.Refused;
        }
    }

    /// <summary>The new text of a reply: everything above the quoted message, without the signature dashes.</summary>
    public static string ReplyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var kept = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith('>')
                || Regex.IsMatch(line, @"^On .{4,200} wrote:$")
                || Regex.IsMatch(line, @"^-{2,}\s*Original Message\s*-{2,}", RegexOptions.IgnoreCase)
                || Regex.IsMatch(line, @"^_{5,}$")
                || Regex.IsMatch(line, @"^From:\s.+", RegexOptions.IgnoreCase) && kept.Count > 0
                || raw.TrimEnd('\r') == "-- " || line == "--")
                break;
            kept.Add(line);
        }
        var result = string.Join('\n', kept).Trim();
        return result.Length > 8000 ? result[..8000] : result;
    }

    private static string? HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        // Gmail and Outlook put the quoted message in these blocks.
        var s = Regex.Replace(html, @"<(blockquote|div class=""gmail_quote"")[\s\S]*", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<(br|/p|/div)\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<[^>]+>", "");
        return WebUtility.HtmlDecode(s);
    }
}

/// <summary>Checks the helpdesk inbox every few minutes and adds new replies to their tickets.</summary>
public class HelpdeskInboxService(HelpdeskInboxOptions options, HelpdeskInbox inbox, IHostEnvironment env, ILogger<HelpdeskInboxService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.IsConfigured || env.IsEnvironment("Testing"))
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, options.PollSeconds)));
        do
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Could not read the helpdesk inbox.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(options.Host, options.Port, options.UseSsl, ct);
        await client.AuthenticateAsync(options.User, options.Password, ct);
        var folder = await client.GetFolderAsync(options.Folder, ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);
        // Unread mail from the last two weeks; each one is marked read once handled, whatever the outcome.
        var ids = await folder.SearchAsync(SearchQuery.NotSeen.And(SearchQuery.DeliveredAfter(DateTime.UtcNow.AddDays(-14))), ct);
        foreach (var id in ids.Take(200))
        {
            var message = await folder.GetMessageAsync(id, ct);
            var from = message.From.Mailboxes.FirstOrDefault()?.Address ?? "";
            var automatic = message.Headers["Auto-Submitted"] is { } a && !a.Equals("no", StringComparison.OrdinalIgnoreCase)
                || message.Headers.Contains("X-Autoreply") || message.Headers.Contains("X-Autorespond")
                || message.Headers["Precedence"] is "bulk" or "auto_reply" or "junk"
                || from.Equals(options.Address, StringComparison.OrdinalIgnoreCase);
            var result = await inbox.ProcessAsync(new InboundEmail(from, message.Subject ?? "", message.TextBody, message.HtmlBody,
                message.Headers["Authentication-Results"], automatic));
            log.LogInformation("Helpdesk email from {From} \"{Subject}\": {Result}", from, message.Subject, result);
            await folder.AddFlagsAsync(id, MessageFlags.Seen, true, ct);
        }
        await client.DisconnectAsync(true, ct);
    }
}
