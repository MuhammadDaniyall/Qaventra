using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;

namespace HRMS.Api.Services;

/// <summary>"Payments" settings. Real keys live only in the server's secret settings, never in the repository.</summary>
public class PaymentOptions
{
    public BankTransferOptions Bank { get; set; } = new();
    public SafepayOptions Safepay { get; set; } = new();
}

/// <summary>The account companies transfer to. Shown on the billing page when they choose bank transfer.</summary>
public class BankTransferOptions
{
    public string? BankName { get; set; }
    public string? AccountTitle { get; set; }
    public string? AccountNumber { get; set; }
    public string? Iban { get; set; }
    public string? Instructions { get; set; }
}

/// <summary>
/// Safepay (getsafepay.com) hosted checkout. Off until Enabled is true and the keys are set. Sandbox by default:
/// switch BaseUrl to https://api.getsafepay.com and Environment to "production" only with live keys.
/// </summary>
public class SafepayOptions
{
    public bool Enabled { get; set; }
    public string Environment { get; set; } = "sandbox";
    public string BaseUrl { get; set; } = "https://sandbox.api.getsafepay.com";
    /// <summary>Public "client" key from the Safepay dashboard.</summary>
    public string? ApiKey { get; set; }
    /// <summary>Webhook shared secret from the Safepay dashboard; every webhook is checked against it.</summary>
    public string? WebhookSecret { get; set; }
}

/// <summary>What the company does next: open the provider's checkout page, or follow the bank transfer instructions.</summary>
public record PaymentStart(string? RedirectUrl, string? Instructions, string? Reference);

/// <summary>A verified provider notification. EventKey is unique per event, so repeats can be ignored.</summary>
public record WebhookNotice(string EventKey, string? ProviderReference, string? InvoiceNumber, bool Paid, decimal? Amount);

/// <summary>One way of paying an invoice. New providers (PayFast, JazzCash) plug in here.</summary>
public interface IPaymentGateway
{
    string Key { get; }
    string Label { get; }
    bool IsEnabled { get; }
    /// <summary>Online providers return a checkout address; bank transfer returns instructions.</summary>
    Task<PaymentStart> StartAsync(TenantInvoice invoice, string companyName, string returnUrl, CancellationToken ct = default);
    /// <summary>Checks the signature and reads the notification. Null when the signature is missing or wrong.</summary>
    WebhookNotice? ReadWebhook(string body, IHeaderDictionary headers);
}

/// <summary>Bank transfer: the company pays into the account in settings, uploads the receipt, and the platform owner approves it.</summary>
public class ManualBankGateway(PaymentOptions options) : IPaymentGateway
{
    public const string ProviderKey = "manual";
    public string Key => ProviderKey;
    public string Label => "Bank transfer";
    public bool IsEnabled => true;

    public Task<PaymentStart> StartAsync(TenantInvoice invoice, string companyName, string returnUrl, CancellationToken ct = default)
    {
        var b = options.Bank;
        var lines = new List<string> { $"Transfer PKR {invoice.Amount:N0} and write {invoice.Number} as the reference." };
        if (!string.IsNullOrWhiteSpace(b.BankName)) lines.Add($"Bank: {b.BankName}");
        if (!string.IsNullOrWhiteSpace(b.AccountTitle)) lines.Add($"Account title: {b.AccountTitle}");
        if (!string.IsNullOrWhiteSpace(b.AccountNumber)) lines.Add($"Account number: {b.AccountNumber}");
        if (!string.IsNullOrWhiteSpace(b.Iban)) lines.Add($"IBAN: {b.Iban}");
        if (string.IsNullOrWhiteSpace(b.AccountNumber) && string.IsNullOrWhiteSpace(b.Iban))
            lines.Add("Bank details are not set up yet. Ask your HR software provider for them.");
        if (!string.IsNullOrWhiteSpace(b.Instructions)) lines.Add(b.Instructions);
        lines.Add("Then upload the receipt here. Your subscription is extended once it is checked.");
        return Task.FromResult(new PaymentStart(null, string.Join("\n", lines), invoice.Number));
    }

    public WebhookNotice? ReadWebhook(string body, IHeaderDictionary headers) => null;
}

/// <summary>
/// Safepay hosted checkout: the server asks Safepay for a payment token (tracker), sends the company to Safepay's page,
/// and Safepay calls the webhook when the payment is made. The webhook is trusted only with a valid HMAC-SHA256
/// signature of the raw body (header X-SFPY-SIGNATURE, hex) made with the webhook secret.
/// </summary>
public class SafepayGateway(PaymentOptions options, IHttpClientFactory http) : IPaymentGateway
{
    public const string ProviderKey = "safepay";
    public const string SignatureHeader = "X-SFPY-SIGNATURE";
    private SafepayOptions O => options.Safepay;

    public string Key => ProviderKey;
    public string Label => "Card / bank account (Safepay)";
    public bool IsEnabled => O.Enabled && !string.IsNullOrWhiteSpace(O.ApiKey) && !string.IsNullOrWhiteSpace(O.WebhookSecret);

    public async Task<PaymentStart> StartAsync(TenantInvoice invoice, string companyName, string returnUrl, CancellationToken ct = default)
    {
        var client = http.CreateClient();
        var res = await client.PostAsJsonAsync($"{O.BaseUrl.TrimEnd('/')}/order/v1/init",
            new { client = O.ApiKey, amount = invoice.Amount, currency = "PKR", environment = O.Environment }, ct);
        if (!res.IsSuccessStatusCode)
            throw new DomainException("The online payment service did not answer. Please try again, or pay by bank transfer.");
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var token = json.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new DomainException("The online payment service did not answer. Please try again, or pay by bank transfer.");
        var query = new Dictionary<string, string>
        {
            ["env"] = O.Environment,
            ["beacon"] = token,
            ["source"] = "custom",
            ["order_id"] = invoice.Number,
            ["redirect_url"] = returnUrl,
            ["cancel_url"] = returnUrl,
        };
        var url = $"{O.BaseUrl.TrimEnd('/')}/components?" + string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return new PaymentStart(url, null, token);
    }

    public WebhookNotice? ReadWebhook(string body, IHeaderDictionary headers)
    {
        if (!IsEnabled || !Verify(body, headers[SignatureHeader].ToString(), O.WebhookSecret!))
            return null;
        JsonElement root;
        try { root = JsonDocument.Parse(body).RootElement; }
        catch (JsonException) { return null; }

        // Safepay sends the payment under "data" (sometimes "data.notification"); read both, and the top level.
        var scopes = new List<JsonElement> { root };
        if (root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
        {
            scopes.Insert(0, d);
            if (d.TryGetProperty("notification", out var n) && n.ValueKind == JsonValueKind.Object)
                scopes.Insert(0, n);
        }
        string? Str(string name) => scopes.Select(s => s.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString() : null)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var tracker = Str("tracker") ?? Str("token");
        var order = Str("order_id") ?? Str("reference");
        var state = (Str("state") ?? Str("status") ?? Str("type") ?? "").ToUpperInvariant();
        decimal? amount = decimal.TryParse(Str("amount"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : null;
        var paid = state is "PAID" or "TRACKER_ENDED" or "PAYMENT:CREATED" or "PAYMENT.SUCCEEDED" or "SUCCESS";
        var eventKey = headers["X-SFPY-EVENT-ID"].ToString() is { Length: > 0 } id ? id : $"{tracker ?? order}:{state}";
        return new WebhookNotice(eventKey, tracker, order, paid, amount);
    }

    /// <summary>Constant-time check of the hex HMAC-SHA256 of the body.</summary>
    public static bool Verify(string body, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;
        var expected = Sign(body, secret);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public static string Sign(string body, string secret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
}
