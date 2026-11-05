using System.Text.Json;
using System.Text.Json.Nodes;
using HRMS.Domain.Common;

namespace HRMS.Api.Services;

public partial class AiAssistantService
{
    /// <summary>
    /// One JSON answer from the free models (OpenRouter or Gemini, whichever answers), for the robots and the writing
    /// helpers. Null when no free model is set up, the company's monthly AI allowance is used up, or no model answered,
    /// so callers always have a plain fallback.
    /// </summary>
    public async Task<T?> FreeJsonAsync<T>(string system, string data) where T : class
    {
        if (!UseGemini && !HasOpenRouter) return null;
        var (limit, used) = await AllowanceAsync();
        if (used >= limit) return null;
        if (data.Length > 60_000) data = data[..60_000];
        try
        {
            if (HasOpenRouter && (options.OpenRouterFirst || !UseGemini))
            {
                try { return await OpenRouterJsonAsync<T>(system, data); }
                catch (DomainException) when (UseGemini) { }
                return await GeminiJsonAsync<T>(system, new JsonArray(Part(data)));
            }
            try { return await GeminiJsonAsync<T>(system, new JsonArray(Part(data))); }
            catch (DomainException) when (HasOpenRouter) { }
            return await OpenRouterJsonAsync<T>(system, data);
        }
        catch (DomainException ex)
        {
            log.LogInformation("Free AI JSON call gave no answer: {Message}", ex.Message);
            return null;
        }
    }

    async Task<T> OpenRouterJsonAsync<T>(string system, string data)
    {
        var entry = new HRMS.Domain.Entities.AiMessageLog { UserId = me.UserId ?? Guid.Empty };
        var message = await OpenRouterCallAsync(new JsonObject
        {
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system + "\nAnswer with JSON only." },
                new JsonObject { ["role"] = "user", ["content"] = data }),
        }, entry);
        db.AiMessageLogs.Add(entry);
        await db.SaveChangesAsync();
        var text = message["content"]?.GetValue<string>() ?? "";
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        try
        {
            return JsonSerializer.Deserialize<T>(start >= 0 && end > start ? text[start..(end + 1)] : text, GeminiJson) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new DomainException("The AI's answer could not be read. Please try again.");
        }
    }
}
