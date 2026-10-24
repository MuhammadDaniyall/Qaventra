using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record NotificationDto(Guid Id, NotificationKind Kind, string Title, string? Body, string? Link, DateTime CreatedAtUtc, DateTime? ReadAtUtc);

public record NotificationFeedDto(int Unread, bool EmailEnabled, List<NotificationDto> Items);

public record NotificationPreferencesRequest(bool EmailEnabled);

public record PushKeyDto(string PublicKey);

public record PushSubscribeRequest(
    [Required, Url, StringLength(800)] string Endpoint,
    [Required, StringLength(200)] string P256dh,
    [Required, StringLength(100)] string Auth);

/// <summary>The signed-in user's own notifications. Each user only ever sees their own.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Policy = Policies.CompanyUser)]
public class NotificationsController(HrmsDbContext db, ICurrentUser me, PushKeyStore pushKeys) : ControllerBase
{
    [HttpGet]
    public async Task<NotificationFeedDto> Feed([FromQuery] int take = 30)
    {
        var mine = db.Notifications.Where(n => n.UserId == me.UserId);
        var items = await mine.OrderByDescending(n => n.CreatedAtUtc).Take(Math.Clamp(take, 1, 100))
            .Select(n => new NotificationDto(n.Id, n.Kind, n.Title, n.Body, n.Link, n.CreatedAtUtc, n.ReadAtUtc)).ToListAsync();
        var unread = await mine.CountAsync(n => n.ReadAtUtc == null);
        var email = await db.Users.IgnoreQueryFilters().Where(u => u.Id == me.UserId).Select(u => u.EmailNotifications).FirstOrDefaultAsync();
        return new NotificationFeedDto(unread, email, items);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id)
    {
        await db.Notifications.Where(n => n.Id == id && n.UserId == me.UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, DateTime.UtcNow));
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        await db.Notifications.Where(n => n.UserId == me.UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, DateTime.UtcNow));
        return NoContent();
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> Preferences(NotificationPreferencesRequest req)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == me.UserId);
        user.EmailNotifications = req.EmailEnabled;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>The public VAPID key the browser needs to subscribe.</summary>
    [HttpGet("push-key")]
    public async Task<PushKeyDto> PushKey() => new((await pushKeys.GetAsync()).PublicKey);

    /// <summary>Saves this browser for push. The same browser signing in as someone else moves to that user.</summary>
    [HttpPost("push-subscriptions")]
    public async Task<IActionResult> Subscribe(PushSubscribeRequest req)
    {
        var userId = me.UserId ?? throw new DomainException("Sign in first.");
        var existing = await db.PushSubscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);
        if (existing is not null && (existing.UserId != userId || existing.TenantId != me.TenantId))
        {
            db.PushSubscriptions.Remove(existing);
            existing = null;
        }
        if (existing is null)
            db.PushSubscriptions.Add(new PushSubscription { UserId = userId, Endpoint = req.Endpoint, P256dh = req.P256dh, Auth = req.Auth });
        else
        {
            existing.P256dh = req.P256dh;
            existing.Auth = req.Auth;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("push-subscriptions")]
    public async Task<IActionResult> Unsubscribe([FromQuery] string endpoint)
    {
        await db.PushSubscriptions.Where(s => s.Endpoint == endpoint && s.UserId == me.UserId).ExecuteDeleteAsync();
        return NoContent();
    }
}
