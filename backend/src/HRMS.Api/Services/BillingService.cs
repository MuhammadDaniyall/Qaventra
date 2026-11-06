using System.Security.Cryptography;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>Invoices, paying them through a gateway, and packages (plans and modules) for companies.</summary>
public class BillingService(HrmsDbContext db, SubscriptionService subscriptions, TenantAccessChecker access, IEnumerable<IPaymentGateway> gateways,
    ILogger<BillingService> log)
{
    public IEnumerable<IPaymentGateway> Enabled => gateways.Where(g => g.IsEnabled);

    public IPaymentGateway Gateway(string key) =>
        gateways.FirstOrDefault(g => g.Key == key && g.IsEnabled) ?? throw new DomainException("This way of paying is not available.");

    /// <summary>Active employees, the headcount billed when a company has no employee limit.</summary>
    public Task<int> HeadcountAsync(Guid tenantId) =>
        db.Employees.IgnoreQueryFilters().CountAsync(e => e.TenantId == tenantId && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation));

    /// <summary>An invoice for the given number of billing cycles at the company's current price. An open invoice for the same cycles is reused.</summary>
    public async Task<TenantInvoice> CreateInvoiceAsync(Guid tenantId, int periods)
    {
        if (periods is < 1 or > 12)
            throw new DomainException("Pay for 1 to 12 billing cycles at a time.");
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId) ?? throw new NotFoundException("Company");
        var perCycle = tenant.AmountPerCycle(await HeadcountAsync(tenantId));
        if (perCycle is not > 0)
            throw new DomainException("Your package has no price set yet. Ask your HR software provider to set it up.");
        var amount = perCycle.Value * periods;
        var open = await db.TenantInvoices.FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Status == InvoiceStatus.Unpaid && i.Periods == periods && i.Amount == amount);
        if (open is not null)
            return open;
        var today = access.LocalToday(tenant.TimeZoneId);
        var invoice = new TenantInvoice
        {
            TenantId = tenantId,
            Number = $"INV-{today:yyyyMM}-{RandomNumberGenerator.GetHexString(6)}",
            Amount = amount,
            Periods = periods,
            IssuedOn = today,
            Description = $"{tenant.BillingCycle} subscription x {periods}",
            Status = InvoiceStatus.Unpaid,
        };
        db.TenantInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    /// <summary>Marks the invoice paid, records the payment and extends the subscription by its cycles. Safe to call twice.</summary>
    public async Task MarkPaidAsync(TenantInvoice invoice, string method, string? reference)
    {
        if (invoice.Status == InvoiceStatus.Paid)
            return;
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new DomainException("This invoice was cancelled.");
        var tenant = await db.Tenants.FirstAsync(t => t.Id == invoice.TenantId);
        var payment = await subscriptions.RecordPaymentAsync(invoice.TenantId, new NewPayment(invoice.Amount, access.LocalToday(tenant.TimeZoneId), invoice.Periods,
            method, reference ?? invoice.Number, $"Invoice {invoice.Number}"));
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAtUtc = DateTime.UtcNow;
        invoice.PaymentId = payment.Id;
        await db.SaveChangesAsync();
    }

    public enum WebhookOutcome { Applied, Duplicate, Ignored, BadSignature, UnknownProvider }

    /// <summary>
    /// Applies a provider's webhook once: the signature must check out, the event is remembered (a repeat is ignored),
    /// and the invoice is paid only when the amount matches.
    /// </summary>
    public async Task<WebhookOutcome> HandleWebhookAsync(string provider, string body, IHeaderDictionary headers)
    {
        var gateway = gateways.FirstOrDefault(g => g.Key == provider && g.IsEnabled);
        if (gateway is null)
            return WebhookOutcome.UnknownProvider;
        var notice = gateway.ReadWebhook(body, headers);
        if (notice is null)
            return WebhookOutcome.BadSignature;

        var key = notice.EventKey.Length > 300 ? notice.EventKey[..300] : notice.EventKey;
        if (await db.PaymentWebhookEvents.AnyAsync(e => e.Provider == provider && e.EventKey == key))
            return WebhookOutcome.Duplicate;
        var entry = new PaymentWebhookEvent { Provider = provider, EventKey = key };
        db.PaymentWebhookEvents.Add(entry);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The same event arrived twice at once; the other delivery handles it.
            db.ChangeTracker.Clear();
            return WebhookOutcome.Duplicate;
        }

        var invoice = notice.ProviderReference is { } r
            ? await db.TenantInvoices.FirstOrDefaultAsync(i => i.Provider == provider && i.ProviderReference == r)
            : null;
        invoice ??= notice.InvoiceNumber is { } n ? await db.TenantInvoices.FirstOrDefaultAsync(i => i.Number == n) : null;
        entry.InvoiceId = invoice?.Id;

        string result;
        if (invoice is null) result = "No matching invoice.";
        else if (!notice.Paid) result = "Not a successful payment.";
        else if (invoice.Status == InvoiceStatus.Paid) result = "Invoice already paid.";
        else if (invoice.Status == InvoiceStatus.Cancelled) result = "Invoice was cancelled.";
        else if (notice.Amount is { } paid && paid != invoice.Amount) result = $"Amount {paid} does not match the invoice ({invoice.Amount}).";
        else
        {
            invoice.Provider = provider;
            invoice.ProviderReference ??= notice.ProviderReference;
            await MarkPaidAsync(invoice, gateway.Label, notice.ProviderReference);
            result = "Paid.";
        }
        entry.Result = result;
        await db.SaveChangesAsync();
        if (result != "Paid.")
            log.LogWarning("Payment webhook {Provider} {Event}: {Result}", provider, key, result);
        return result == "Paid." ? WebhookOutcome.Applied : WebhookOutcome.Ignored;
    }

    /// <summary>
    /// Puts a company on a package: its prices, employee limit and modules are copied from the plan (modules can be
    /// adjusted per company), and a trial end date can be given.
    /// </summary>
    public async Task AssignPlanAsync(Guid tenantId, Guid? planId, DateOnly? trialEndsOn, List<string>? modules)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId) ?? throw new NotFoundException("Company");
        SubscriptionPlan? plan = null;
        if (planId is { } id)
        {
            plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Package");
            var months = SubscriptionService.Months(tenant.BillingCycle ?? BillingCycle.Monthly);
            tenant.BillingCycle ??= BillingCycle.Monthly;
            tenant.PlanPrice = plan.FlatPrice * months;
            tenant.PricePerEmployee = plan.PricePerEmployee;
            tenant.MaxEmployees = plan.MaxEmployees;
        }
        tenant.PlanId = planId;
        tenant.EnabledModules = modules is not null ? AccessModules.ToCompanyCsv(modules)
            : plan is not null ? AccessModules.ToCompanyCsv(AccessModules.Parse(plan.Modules))
            : null;
        tenant.TrialEndsOn = trialEndsOn;
        if (trialEndsOn is { } trial && (tenant.SubscriptionEndsOn is null || tenant.SubscriptionEndsOn < trial))
            tenant.SubscriptionEndsOn = trial;
        await db.SaveChangesAsync();
        access.Invalidate(tenantId);
    }
}
