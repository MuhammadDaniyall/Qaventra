using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

public class EmailSettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        var text = await res.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement;
    }

    /// <summary>The sender runs in the background; wait a little for its log rows.</summary>
    private static async Task<List<JsonElement>> LogAsync(HttpClient admin, int atLeast)
    {
        for (var i = 0; i < 40; i++)
        {
            var items = (await Ok(await admin.GetAsync("/api/setup/email/log"))).GetProperty("items").EnumerateArray().ToList();
            if (items.Count >= atLeast) return items;
            await Task.Delay(150);
        }
        return [];
    }

    [Fact]
    public async Task Company_wording_is_used_and_every_email_is_logged()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("Mail Co");
        var templates = await Ok(await admin.GetAsync("/api/setup/email/templates"));
        Assert.Contains(templates.EnumerateArray(), t => t.GetProperty("kind").GetString() == "Celebration" && !t.GetProperty("isCustom").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/setup/email/templates/Announcement",
            new { subject = "{company} news for {name}", intro = "Hello {name},", enabled = true }, ApiFactory.Json)).StatusCode);

        var emp = (await Ok(await admin.PostAsJsonAsync("/api/employees", new { firstName = "Mail", lastName = "Reader", joiningDate = "2024-01-01", status = "Active" }, ApiFactory.Json)))
            .GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync($"/api/employees/{emp}/login", new { email = $"reader+{Guid.NewGuid():N}@mail.test", password = "Mail-Pass-123", role = "Employee" }));
        await Ok(await admin.PostAsJsonAsync("/api/announcements", new { title = "Office closed Friday", body = "Maintenance.", kind = "News", publishFrom = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-dd"), isPinned = false }, ApiFactory.Json));

        var log = await LogAsync(admin, 1);
        var row = Assert.Single(log, l => l.GetProperty("kind").GetString() == "Announcement");
        var subject = row.GetProperty("subject").GetString()!;
        Assert.StartsWith("Mail Co", subject); // the test factory adds a unique suffix to company names
        Assert.EndsWith("news for Mail Reader", subject);
        Assert.Equal("NotConfigured", row.GetProperty("status").GetString()); // no SMTP in tests, but it is still logged

        var usage = await Ok(await admin.GetAsync("/api/setup/email/usage"));
        Assert.Equal(0, usage.GetProperty("sentThisMonth").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/setup/email/templates/Announcement")).StatusCode);
        Assert.DoesNotContain((await Ok(await admin.GetAsync("/api/setup/email/templates"))).EnumerateArray(),
            t => t.GetProperty("kind").GetString() == "Announcement" && t.GetProperty("isCustom").GetBoolean());
    }
}
