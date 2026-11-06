using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record ModuleInfoDto(string Key, string Label, bool Core);

public record PaymentProviderDto(string Key, string Label, bool Online);

public record InvoiceDto(Guid Id, Guid TenantId, string? Company, string Number, decimal Amount, int Periods, DateOnly IssuedOn, string? Description,
    InvoiceStatus Status, string? Provider, string? ProviderReference, bool HasProof, string? ReviewNote, DateTime? PaidAtUtc, RecordStampsDto Stamps);

public record BillingDto(
    string Company, Guid? PlanId, string? PlanName, BillingCycle? BillingCycle, decimal? AmountPerCycle, int Employees, int? MaxEmployees,
    DateOnly? SubscriptionEndsOn, DateOnly? TrialEndsOn, int? DaysLeft, TenantAccess Access, List<string>? EnabledModules, List<ModuleInfoDto> Modules,
    List<InvoiceDto> Invoices, List<PaymentProviderDto> Providers);

public record CreateInvoiceRequest([Range(1, 12)] int Periods = 1);

public record PayInvoiceRequest([Required, StringLength(30)] string Provider, [StringLength(500)] string? ReturnUrl);

public class InvoiceProofForm
{
    public IFormFile? File { get; set; }
    [StringLength(200)] public string? Reference { get; set; }
}

/// <summary>The company admin's billing page: package, invoices and paying them. Open even after the subscription has run out.</summary>
[ApiController]
[Route("api/billing")]
[Authorize(Policy = Policies.Admin)]
public class BillingController(HrmsDbContext db, BillingService billing, TenantAccessChecker access, ICurrentUser me) : ControllerBase
{
    private const long MaxProofBytes = 5 * 1024 * 1024;
    private static readonly string[] ProofTypes = ["application/pdf", "image/png", "image/jpeg", "image/webp"];

    private Guid TenantId => me.TenantId ?? throw new DomainException("Open a company first.");

    [HttpGet]
    public async Task<BillingDto> Get()
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantId) ?? throw new NotFoundException("Company");
        var plan = tenant.PlanId is { } p ? await db.SubscriptionPlans.Where(x => x.Id == p).Select(x => x.Name).FirstOrDefaultAsync() : null;
        var employees = await billing.HeadcountAsync(tenant.Id);
        var today = access.LocalToday(tenant.TimeZoneId);
        var invoices = await InvoiceQuery(db.TenantInvoices.Where(i => i.TenantId == tenant.Id).OrderByDescending(i => i.CreatedAtUtc).Take(100)).ToListAsync();
        return new BillingDto(tenant.Name, tenant.PlanId, plan, tenant.BillingCycle, tenant.AmountPerCycle(employees), employees, tenant.MaxEmployees,
            tenant.SubscriptionEndsOn, tenant.TrialEndsOn, tenant.SubscriptionEndsOn is { } end ? end.DayNumber - today.DayNumber : null, tenant.AccessOn(today),
            AccessModules.CompanyModules(tenant.EnabledModules)?.ToList(), Modules(), invoices,
            billing.Enabled.Select(g => new PaymentProviderDto(g.Key, g.Label, g.Key != ManualBankGateway.ProviderKey)).ToList());
    }

    [HttpPost("invoices")]
    public async Task<InvoiceDto> CreateInvoice(CreateInvoiceRequest req)
    {
        var invoice = await billing.CreateInvoiceAsync(TenantId, req.Periods);
        return await One(invoice.Id);
    }

    /// <summary>Starts paying: online providers return a checkout address to open; bank transfer returns the account details.</summary>
    [HttpPost("invoices/{id:guid}/pay")]
    public async Task<PaymentStart> Pay(Guid id, PayInvoiceRequest req)
    {
        var invoice = await Mine(id);
        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            throw new DomainException("This invoice is already settled.");
        var gateway = billing.Gateway(req.Provider);
        var company = await db.Tenants.Where(t => t.Id == invoice.TenantId).Select(t => t.Name).FirstAsync();
        var returnUrl = Uri.TryCreate(req.ReturnUrl, UriKind.Absolute, out var u) && u.Scheme is "https" or "http"
            ? u.ToString() : $"{Request.Scheme}://{Request.Host}/billing";
        var start = await gateway.StartAsync(invoice, company, returnUrl, HttpContext.RequestAborted);
        invoice.Provider = gateway.Key;
        if (gateway.Key != ManualBankGateway.ProviderKey)
            invoice.ProviderReference = start.Reference;
        await db.SaveChangesAsync();
        return start;
    }

    /// <summary>Bank transfer: the receipt (PDF or image) and the bank's reference. The platform owner then approves it.</summary>
    [HttpPost("invoices/{id:guid}/proof")]
    [RequestSizeLimit(MaxProofBytes + 64 * 1024)]
    public async Task<InvoiceDto> UploadProof(Guid id, [FromForm] InvoiceProofForm form)
    {
        var invoice = await Mine(id);
        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            throw new DomainException("This invoice is already settled.");
        if (form.File is not { Length: > 0 } file)
            throw new DomainException("Choose the payment receipt to upload.");
        if (file.Length > MaxProofBytes)
            throw new DomainException("The receipt must be 5 MB or smaller.");
        if (!ProofTypes.Contains(file.ContentType))
            throw new DomainException("Upload the receipt as a PDF or an image (PNG, JPEG).");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        invoice.ProofData = ms.ToArray();
        invoice.ProofFileName = Path.GetFileName(file.FileName);
        invoice.ProofContentType = file.ContentType;
        invoice.Provider = ManualBankGateway.ProviderKey;
        invoice.ProviderReference = string.IsNullOrWhiteSpace(form.Reference) ? null : form.Reference.Trim();
        invoice.Status = InvoiceStatus.AwaitingApproval;
        invoice.ReviewNote = null;
        await db.SaveChangesAsync();
        return await One(id);
    }

    [HttpDelete("invoices/{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var invoice = await Mine(id);
        if (invoice.Status != InvoiceStatus.Unpaid)
            throw new DomainException("Only an unpaid invoice can be cancelled.");
        invoice.Status = InvoiceStatus.Cancelled;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Called by the payment provider. Trusted only with a valid signature; each event is applied once.</summary>
    [HttpPost("webhooks/{provider}"), AllowAnonymous, EnableRateLimiting("webhook")]
    public async Task<IActionResult> Webhook(string provider)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (body.Length > 64 * 1024)
            return BadRequest();
        return await billing.HandleWebhookAsync(provider, body, Request.Headers) switch
        {
            BillingService.WebhookOutcome.UnknownProvider => NotFound(),
            BillingService.WebhookOutcome.BadSignature => Unauthorized(),
            var outcome => Ok(new { result = outcome.ToString() }),
        };
    }

    public static List<ModuleInfoDto> Modules() =>
        AccessModules.All.Select(m => new ModuleInfoDto(m.Key, m.Label, AccessModules.Core.Contains(m.Key))).ToList();

    public static IQueryable<InvoiceDto> InvoiceQuery(IQueryable<TenantInvoice> q) =>
        q.Select(i => new InvoiceDto(i.Id, i.TenantId, null,
            i.Number, i.Amount, i.Periods, i.IssuedOn, i.Description, i.Status, i.Provider, i.ProviderReference, i.ProofFileName != null, i.ReviewNote, i.PaidAtUtc,
            new RecordStampsDto(i.CreatedAtUtc, i.CreatedBy, i.ModifiedAtUtc, i.ModifiedBy)));

    private async Task<TenantInvoice> Mine(Guid id) =>
        await db.TenantInvoices.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId) ?? throw new NotFoundException("Invoice");

    private async Task<InvoiceDto> One(Guid id) => await InvoiceQuery(db.TenantInvoices.Where(i => i.Id == id)).FirstAsync();
}
