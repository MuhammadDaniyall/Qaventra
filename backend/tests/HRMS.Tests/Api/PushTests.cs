using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Tests.Api;

public class PushTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly object Sub = new
    {
        endpoint = "https://push.example.test/sub/abc123",
        p256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM",
        auth = "tBHItJI5svbpez7KI4CCXg",
    };

    private async Task<int> CountAsync(string endpoint)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<HrmsDbContext>().PushSubscriptions.IgnoreQueryFilters().CountAsync(s => s.Endpoint == endpoint);
    }

    [Fact]
    public async Task Push_key_is_created_once_and_reused()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Push Key Co");
        var first = (await admin.GetFromJsonAsync<JsonElement>("/api/notifications/push-key")).GetProperty("publicKey").GetString();
        var second = (await admin.GetFromJsonAsync<JsonElement>("/api/notifications/push-key")).GetProperty("publicKey").GetString();
        Assert.False(string.IsNullOrEmpty(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task A_browser_subscribes_once_and_can_unsubscribe()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Push Sub Co");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/notifications/push-subscriptions", Sub)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/notifications/push-subscriptions", Sub)).StatusCode);
        Assert.Equal(1, await CountAsync("https://push.example.test/sub/abc123"));

        var res = await admin.DeleteAsync("/api/notifications/push-subscriptions?endpoint=" + Uri.EscapeDataString("https://push.example.test/sub/abc123"));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(0, await CountAsync("https://push.example.test/sub/abc123"));
    }

    [Fact]
    public async Task Subscribing_needs_a_sign_in()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/notifications/push-subscriptions", Sub);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
