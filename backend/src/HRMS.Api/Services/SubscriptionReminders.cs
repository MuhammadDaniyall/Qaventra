using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>
/// Tells a company's admins, in the app and by email, that its subscription is about to end or has ended, so they can
/// renew from Billing; the platform owner gets a copy by email. One reminder per stage per end date: 7, 3 and 1 day
/// before, on the last day, the day access stops, and a week later. A stage missed while the server slept is sent late,
/// once (only the latest stage reached). Each reminder is kept in the audit log, which the owner's company list shows.
/// </summary>
public class SubscriptionReminders(HrmsDbContext db, TenantAccessChecker access, NotificationService notify)
{
    public const string Action = "SubscriptionReminder";

    /// <summary>Days left at which a reminder goes out, earliest first; 0 is the last usable day, -1 the first day without access.</summary>
    public static readonly int[] Stages = [7, 3, 1, 0, -1, -7];

    /// <summary>The latest stage a company with this many days left has reached, or null when it is not yet due.</summary>
    public static int? StageFor(int daysLeft) => Stages.Where(s => daysLeft <= s).Cast<int?>().LastOrDefault();

    public static string Key(DateOnly endsOn, int stage) => $"{endsOn:yyyy-MM-dd}|{stage}";

    /// <summary>Sends what is due for one company. Returns true when a reminder went out.</summary>
    public async Task<bool> RunAsync(Guid tenantId, bool force = false)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is not { IsActive: true, SubscriptionEndsOn: { } endsOn })
            return false;
        var today = access.LocalToday(tenant.TimeZoneId);
        var daysLeft = endsOn.DayNumber - today.DayNumber;
        var stage = StageFor(daysLeft);
        if (stage is null && !force)
            return false;
        var key = force ? $"{endsOn:yyyy-MM-dd}|manual|{today:yyyy-MM-dd}" : Key(endsOn, stage!.Value);
        if (await db.AuditLogs.AnyAsync(a => a.TenantId == tenantId && a.Action == Action && a.EntityId == key))
            return false;

        var amount = tenant.AmountPerCycle(await db.Employees.IgnoreQueryFilters()
            .CountAsync(e => e.TenantId == tenantId && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)));
        var price = amount is > 0 ? $" Renewal is Rs {amount.Value:N0} per {(tenant.BillingCycle ?? BillingCycle.Monthly).ToString().ToLowerInvariant()} cycle." : "";
        var (title, body) = daysLeft switch
        {
            > 1 => ($"Your Qaventra subscription ends in {daysLeft} days", $"Your subscription is paid until {endsOn:d MMM yyyy}. Renew from Billing so your team keeps access.{price}"),
            1 => ("Your Qaventra subscription ends tomorrow", $"Your subscription is paid until {endsOn:d MMM yyyy}. Renew today from Billing so nobody loses access.{price}"),
            0 => ("Your Qaventra subscription ends today", $"Today is the last paid day. From tomorrow only the company admin can sign in, to pay.{price}"),
            _ => ("Your Qaventra subscription has expired", $"It ended on {endsOn:d MMM yyyy}, so your team cannot use Qaventra. Sign in as the company admin and pay from Billing to restore access at once.{price}"),
        };

        db.UseTenant(tenantId);
        await notify.ToUsersAsync(db.Users.Where(u => u.TenantId == tenantId && u.Role == UserRole.CompanyAdmin), NotificationKind.Alert,
            title, body, "/billing", includeMe: true, email: true);
        var admins = await db.Users.IgnoreQueryFilters().Where(u => u.TenantId == tenantId && u.Role == UserRole.CompanyAdmin && u.IsActive)
            .Select(u => u.Email).ToListAsync();
        foreach (var owner in await db.Users.IgnoreQueryFilters().Where(u => u.Role == UserRole.SuperAdmin && u.IsActive).Select(u => u.Email).ToListAsync())
            notify.EmailOnly(owner, $"{tenant.Name}: {title.Replace("Your Qaventra subscription", "subscription")}",
                $"{body} Reminder sent to {(admins.Count > 0 ? string.Join(", ", admins) : "nobody: the company has no active admin")}.", "/platform/companies",
                footer: "Sent by Qaventra to the platform owner.");
        db.AuditLogs.Add(new AuditLog
        {
            Category = AuditCategory.Data,
            Action = Action,
            TenantId = tenantId,
            EntityType = nameof(Tenant),
            EntityId = key,
            Changes = $"{title} (to {string.Join(", ", admins)})",
        });
        await db.SaveChangesAsync();
        return true;
    }
}

/// <summary>Checks every company with an end date a few times a day and sends the subscription reminders that are due.</summary>
public class SubscriptionReminderService(IServiceProvider services, IHostEnvironment env, ILogger<SubscriptionReminderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (env.IsEnvironment("Testing"))
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                List<Guid> tenants;
                using (var scope = services.CreateScope())
                    tenants = await scope.ServiceProvider.GetRequiredService<HrmsDbContext>().Tenants.IgnoreQueryFilters()
                        .Where(t => t.IsActive && t.SubscriptionEndsOn != null).Select(t => t.Id).ToListAsync(stoppingToken);
                foreach (var tenant in tenants)
                {
                    try
                    {
                        using var scope = services.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<SubscriptionReminders>().RunAsync(tenant);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        log.LogWarning(ex, "Subscription reminder failed for company {Tenant}.", tenant);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Subscription reminders failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
