using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;
using DbPushSubscription = HRMS.Domain.Entities.PushSubscription;

namespace HRMS.Api.Services;

public record PushMessage(Guid UserId, string Title, string? Body, string? Link);

/// <summary>"Push" section. Subject is the contact the push services see: a mailto: or https: address.</summary>
public class PushOptions
{
    public string Subject { get; set; } = "mailto:owner@qaventra.com";
}

/// <summary>Push messages waiting to be sent, filled by <see cref="NotificationService"/> after a successful save.</summary>
public class PushQueue
{
    private readonly Channel<PushMessage> _channel = Channel.CreateBounded<PushMessage>(
        new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(PushMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<PushMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>The VAPID key pair, created on first use and stored in the database.</summary>
public class PushKeyStore(HrmsDbContext db)
{
    public async Task<PushKeys> GetAsync()
    {
        var keys = await db.PushKeys.AsNoTracking().OrderBy(k => k.CreatedAtUtc).FirstOrDefaultAsync();
        if (keys is not null)
            return keys;
        var pair = VapidHelper.GenerateVapidKeys();
        keys = new PushKeys { PublicKey = pair.PublicKey, PrivateKey = pair.PrivateKey };
        db.PushKeys.Add(keys);
        try
        {
            await db.SaveChangesAsync();
            return keys;
        }
        catch (DbUpdateException)
        {
            // Another request created the keys at the same moment; use theirs.
            db.ChangeTracker.Clear();
            return await db.PushKeys.AsNoTracking().OrderBy(k => k.CreatedAtUtc).FirstAsync();
        }
    }
}

/// <summary>Sends queued pushes to every browser the user subscribed. Gone subscriptions (404/410) are removed.</summary>
public class PushBackgroundService(PushQueue queue, IServiceProvider services, PushOptions options, ILogger<PushBackgroundService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var client = new WebPushClient();
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
                // No signed-in user here, so the tenant filter is bypassed and rows are picked by user id.
                var subs = await db.PushSubscriptions.IgnoreQueryFilters().Where(s => s.UserId == message.UserId).ToListAsync(stoppingToken);
                if (subs.Count == 0)
                    continue;
                var keys = await scope.ServiceProvider.GetRequiredService<PushKeyStore>().GetAsync();
                var vapid = new VapidDetails(options.Subject, keys.PublicKey, keys.PrivateKey);
                var payload = JsonSerializer.Serialize(new { title = message.Title, body = message.Body, link = message.Link });
                var gone = new List<DbPushSubscription>();
                foreach (var s in subs)
                {
                    try
                    {
                        await client.SendNotificationAsync(new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, vapid, stoppingToken);
                    }
                    catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                    {
                        gone.Add(s);
                    }
                }
                if (gone.Count > 0)
                {
                    db.PushSubscriptions.RemoveRange(gone);
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Push to user {UserId} failed.", message.UserId);
            }
        }
    }
}
