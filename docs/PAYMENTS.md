# Packages and online payment

## How it works

- The platform owner creates **packages** under *Packages and payments*: a price per employee per month, a flat monthly price (or both), an employee limit, a free trial length and the modules included.
- On a company's page the owner chooses a package, the trial end date and, if needed, different modules for that company. The package's prices and employee limit are copied onto the company.
- **Modules outside the package** are hidden from the menus and the API refuses them with `403` and `code: "ModuleDisabled"`. Employees, organization settings and approvals are always on.
- **Companies from before packages keep every module**: `Tenants.EnabledModules` is `NULL` for them, which means "all modules".
- The company admin opens **Billing**, picks how many billing cycles to pay and presses **Pay**. That creates an invoice, which is paid in one of two ways:
  - **Bank transfer** (always available): the page shows the bank details from settings. The company uploads the receipt, and the owner approves it under *Payments to check* (or turns it down with a reason).
  - **Safepay** (card or bank account, online): the company is sent to Safepay's hosted checkout. Safepay calls our webhook, the invoice is marked paid and the subscription is extended.
- Every payment, by either route, is recorded in the company's payment history and extends the subscription by the invoice's billing cycles.

## Why Safepay

The provider sits behind `IPaymentGateway` (`backend/src/HRMS.Api/Services/Payments.cs`), so PayFast or JazzCash can be added later without other changes. Safepay came first because it has:

- the simplest hosted checkout: one server call returns a token, then the company is redirected to Safepay's page, so no card data ever reaches our server;
- a sandbox with self-service keys;
- one webhook signed with a shared secret (HMAC-SHA256).

## Settings (no real keys in the repository)

Put these in `deploy/secrets/production.json` (git-ignored). `deploy/build-package.ps1` copies the `Payments` section into the site's `appsettings.Production.json`.

```json
"Payments": {
  "Bank": {
    "BankName": "…",
    "AccountTitle": "…",
    "AccountNumber": "…",
    "Iban": "PK…",
    "Instructions": "Optional extra line"
  },
  "Safepay": {
    "Enabled": true,
    "Environment": "sandbox",
    "BaseUrl": "https://sandbox.api.getsafepay.com",
    "ApiKey": "<Safepay public/client key>",
    "WebhookSecret": "<Safepay webhook secret>"
  }
}
```

- Safepay stays switched off until `Enabled` is true **and** both keys are set. Bank transfer works without any keys; until the bank details are filled in, the page tells the company to ask for them.
- In the Safepay dashboard, set the webhook URL to `https://<your-site>/api/billing/webhooks/safepay`.
- To go live, use the live keys, `"Environment": "production"` and `"BaseUrl": "https://api.getsafepay.com"`.

## Webhook safety

- The webhook is accepted only with a valid `X-SFPY-SIGNATURE` header: the hex HMAC-SHA256 of the raw body, made with `WebhookSecret`. Anything else gets `401`.
- Every event is stored in `PaymentWebhookEvents` (unique on provider and event). A repeated delivery is answered `Duplicate` and changes nothing.
- An invoice that is already paid is never paid twice, and a webhook whose amount differs from the invoice is ignored and logged.
- **Check before going live:** confirm the exact webhook field names (`tracker`, `state`, `amount`, `order_id`) and the signature header against Safepay's current documentation, using a sandbox payment. The parser reads `data`, `data.notification` and the top level, so small layout differences are handled. If Safepay names a field differently, change only `SafepayGateway.ReadWebhook`.

## Known limits

- An **expired** company cannot sign in (unchanged from before), so it must pay before expiry, or the owner records the payment. The API already lets the company admin use `/api/billing` on an expired subscription, so a "pay to reactivate" screen on the sign-in page can be added later.
- There is no self sign-up yet. The "Start free trial" buttons on the free calculator pages go to the sign-in page.
