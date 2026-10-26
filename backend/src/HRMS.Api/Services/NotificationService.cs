using System.Net;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>
/// Creates in-app notifications and queues the matching emails and phone/browser pushes. Rows are added to the context and saved with the
/// caller's change; emails go out only after that save succeeds, so nobody is told about something that was rolled back.
/// The person who caused the event is never notified about it.
/// </summary>
public class NotificationService
{
    private readonly HrmsDbContext _db;
    private readonly ICurrentUser _me;
    private readonly EmailQueue _queue;
    private readonly PushQueue _push;
    private readonly string? _baseUrl;
    private readonly List<EmailMessage> _pending = [];
    private readonly List<PushMessage> _pendingPush = [];

    public NotificationService(HrmsDbContext db, ICurrentUser me, EmailQueue queue, PushQueue push, IHttpContextAccessor http)
    {
        _db = db;
        _me = me;
        _queue = queue;
        _push = push;
        var request = http.HttpContext?.Request;
        _baseUrl = request is null ? null : $"{request.Scheme}://{request.Host}";
        _db.SavedChanges += (_, _) => Flush();
    }

    /// <param name="includeMe">Also notify the signed-in user, for reminders that are not about their own action.</param>
    public async Task ToEmployeesAsync(IEnumerable<Guid> employeeIds, NotificationKind kind, string title, string? body, string? link, bool includeMe = false)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0)
            return;
        await ToUsersAsync(_db.Users.Where(u => u.EmployeeId != null && ids.Contains(u.EmployeeId.Value)), kind, title, body, link, includeMe);
    }

    /// <summary>Everyone with HR rights in the company: HR managers and company admins.</summary>
    public Task ToHrAsync(NotificationKind kind, string title, string? body, string? link, bool includeMe = false) =>
        ToUsersAsync(_db.Users.Where(u => u.Role == UserRole.HrManager || u.Role == UserRole.CompanyAdmin), kind, title, body, link, includeMe);

    private async Task ToUsersAsync(IQueryable<User> users, NotificationKind kind, string title, string? body, string? link, bool includeMe)
    {
        var me = includeMe ? null : _me.UserId;
        var recipients = await users.Where(u => u.IsActive && u.Id != me)
            .Select(u => new { u.Id, u.Email, u.EmailNotifications, u.FullName }).ToListAsync();
        title = Trim(title, 200)!;
        body = Trim(body, 1000);
        var template = await TemplateAsync(kind);
        var tenant = _db.CurrentTenantId;
        foreach (var r in recipients)
        {
            _db.Notifications.Add(new Notification { UserId = r.Id, Kind = kind, Title = title, Body = body, Link = link });
            _pendingPush.Add(new PushMessage(r.Id, title, body, link));
            if (!r.EmailNotifications || template is { Enabled: false })
                continue;
            string Fill(string text) => text.Replace("{name}", r.FullName).Replace("{company}", _company ?? "")
                .Replace("{title}", title).Replace("{body}", body ?? "");
            var subject = template is null ? title : Trim(Fill(template.Subject), 200)!;
            var intro = template?.Intro is { Length: > 0 } i ? Fill(i) : null;
            _pending.Add(new EmailMessage(r.Email, subject, Html(title, body, link, intro: intro),
                TenantId: tenant == Guid.Empty ? null : tenant, Kind: kind.ToString()));
        }
    }

    /// <summary>A one-off email that is not a notification, such as sign-in details. Sent after the next save.</summary>
    public void EmailOnly(string to, string title, string body, string? link, string? footer = null) =>
        _pending.Add(new EmailMessage(to, title, Html(title, body, link,
            footer ?? "Sent by your company's HR team through Qaventra. If you did not expect it, tell HR.")));

    private void Flush()
    {
        foreach (var m in _pending)
            _queue.Enqueue(m);
        _pending.Clear();
        foreach (var p in _pendingPush)
            _push.Enqueue(p);
        _pendingPush.Clear();
    }

    private readonly Dictionary<NotificationKind, EmailTemplate?> _templates = [];
    private string? _company;

    /// <summary>The company's wording for this kind of email, if it set one (looked up once per request).</summary>
    private async Task<EmailTemplate?> TemplateAsync(NotificationKind kind)
    {
        if (_db.CurrentTenantId == Guid.Empty)
            return null;
        _company ??= await _db.Tenants.Where(t => t.Id == _db.CurrentTenantId).Select(t => t.Name).FirstOrDefaultAsync();
        if (!_templates.TryGetValue(kind, out var t))
            _templates[kind] = t = await _db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Kind == kind);
        return t;
    }

    private string Html(string title, string? body, string? link,
        string footer = "You get this because notifications are on for your Qaventra account. Turn emails off under My profile.",
        string? intro = null)
    {
        static string e(string? s) => WebUtility.HtmlEncode(s ?? "");
        var button = link is not null && _baseUrl is not null
            ? $"<p style=\"margin:24px 0\"><a href=\"{e(_baseUrl + link)}\" style=\"background:#4f46e5;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none;font-weight:600\">Open in Qaventra</a></p>"
            : "";
        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:auto;color:#18181b">
              {(intro is null ? "" : $"<p style=\"font-size:14px;line-height:1.6;color:#3f3f46;white-space:pre-line\">{e(intro)}</p>")}
              <h2 style="font-size:18px;margin:0 0 8px">{e(title)}</h2>
              {(body is null ? "" : $"<p style=\"font-size:14px;line-height:1.6;color:#3f3f46;white-space:pre-line\">{e(body)}</p>")}
              {button}
              <p style="font-size:12px;color:#a1a1aa">{e(footer)}</p>
            </div>
            """;
    }

    private static string? Trim(string? s, int max) => s is { Length: > 0 } && s.Length > max ? s[..max] : s;
}
