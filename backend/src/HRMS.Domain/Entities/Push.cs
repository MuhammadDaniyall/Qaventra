using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>One browser or installed app that asked for push notifications for a user.</summary>
public class PushSubscription : TenantEntity
{
    public Guid UserId { get; set; }
    /// <summary>The push service URL the browser gave us; unique per browser.</summary>
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
}

/// <summary>
/// The platform's VAPID key pair for Web Push. Created once on first use and kept in the database, so deploys
/// need no extra secret. Changing it makes every existing subscription stop working.
/// </summary>
public class PushKeys : BaseEntity
{
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
}
