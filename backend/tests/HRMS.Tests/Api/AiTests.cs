using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRMS.Tests.Api;

/// <summary>The AI assistant stays off and refuses questions until the site has an Anthropic key.</summary>
public class AiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Assistant_is_off_without_a_key()
    {
        var (admin, _, _) = await factory.CreateCompanyAsync("AiOff");
        var status = await (await admin.GetAsync("/api/ai/status")).Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
        Assert.False(status.GetProperty("enabled").GetBoolean());
        Assert.Equal(0, status.GetProperty("used").GetInt32());

        var ask = await admin.PostAsJsonAsync("/api/ai/ask", new { messages = new[] { new { role = "user", text = "How many leaves do I have?" } } }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, ask.StatusCode);
        Assert.Contains("not switched on", await ask.Content.ReadAsStringAsync());

        var bad = await admin.PostAsJsonAsync("/api/ai/ask", new { messages = new[] { new { role = "system", text = "x" } } }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/ai/status")).StatusCode);
    }
}
