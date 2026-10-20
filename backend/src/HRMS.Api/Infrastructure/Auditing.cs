using System.Diagnostics;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;

namespace HRMS.Api.Infrastructure;

/// <summary>Writes Auth and Request audit rows. Uses its own scope so a failed request's tracked changes are never saved by accident.</summary>
public class AuditWriter(IServiceScopeFactory scopes, ICurrentUser me, ILogger<AuditWriter> logger)
{
    public async Task AuthAsync(string action, User? user, string? attemptedEmail = null)
    {
        await WriteAsync(new AuditLog
        {
            Category = AuditCategory.Auth,
            Action = action,
            TenantId = user is { TenantId: var t } && t != Guid.Empty ? t : null,
            UserId = user?.Id,
            UserEmail = user?.Email ?? attemptedEmail,
            Role = user?.Role,
            EntityType = nameof(User),
            EntityId = user?.Id.ToString(),
            IpAddress = me.IpAddress,
            UserAgent = Cut(me.UserAgent, 512),
            CorrelationId = me.CorrelationId,
        });
    }

    public async Task WriteAsync(AuditLog log)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
            db.AuditLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // An audit failure must not break the user's request, but it must be visible to operators.
            logger.LogError(ex, "Failed to write audit log {Category} {Action} {Path}", log.Category, log.Action, log.Path);
        }
    }

    public static string? Cut(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}

/// <summary>Logs every API request: who, what, from where, result and duration.</summary>
public class RequestAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, AuditWriter audit, ICurrentUser me)
    {
        if (!http.Request.Path.StartsWithSegments("/api"))
        {
            await next(http);
            return;
        }

        var watch = Stopwatch.StartNew();
        try
        {
            await next(http);
        }
        finally
        {
            watch.Stop();
            await audit.WriteAsync(new AuditLog
            {
                Category = AuditCategory.Request,
                Action = http.Request.Method,
                TenantId = me.TenantId,
                UserId = me.UserId,
                UserEmail = me.Email,
                Role = me.Role,
                Path = AuditWriter.Cut(http.Request.Path + http.Request.QueryString, 500),
                StatusCode = http.Response.StatusCode,
                DurationMs = (int)watch.ElapsedMilliseconds,
                IpAddress = me.IpAddress,
                UserAgent = AuditWriter.Cut(me.UserAgent, 512),
                CorrelationId = me.CorrelationId,
            });
        }
    }
}
