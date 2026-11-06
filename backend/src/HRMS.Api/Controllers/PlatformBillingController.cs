using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record PlanDto(Guid Id, string Name, string? Description, decimal PricePerEmployee, decimal FlatPrice, int? MaxEmployees, List<string> Modules,
    int TrialDays, bool IsActive, int SortOrder, int Companies, RecordStampsDto Stamps);

public record SavePlanRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name, [StringLength(1000)] string? Description,
    [Range(0, 1_000_000)] decimal PricePerEmployee, [Range(0, 100_000_000)] decimal FlatPrice, [Range(1, 1_000_000)] int? MaxEmployees,
    List<string>? Modules, [Range(0, 365)] int TrialDays, bool IsActive = true, int SortOrder = 0);

public record CompanyPlanDto(Guid? PlanId, string? PlanName, DateOnly? TrialEndsOn, List<string>? EnabledModules, List<ModuleInfoDto> Modules);

/// <summary>Modules null = the plan's modules (or every module when no plan).</summary>
public record SetCompanyPlanRequest(Guid? PlanId, DateOnly? TrialEndsOn, List<string>? Modules);

public record ReviewInvoiceRequest([StringLength(1000)] string? Note);

/// <summary>Platform owner: packages, which modules each company has, and approving bank transfer payments.</summary>
[ApiController]
[Route("api/platform")]
[Authorize(Policy = Policies.SuperAdmin)]
public class PlatformBillingController(HrmsDbContext db, BillingService billing) : ControllerBase
{
    [HttpGet("modules")]
    public List<ModuleInfoDto> Modules() => BillingController.Modules();

    [HttpGet("plans")]
    public async Task<List<PlanDto>> Plans()
    {
        var rows = await db.SubscriptionPlans.OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new { P = p, Companies = db.Tenants.Count(t => t.PlanId == p.Id) }).ToListAsync();
        return rows.Select(r => ToDto(r.P, r.Companies)).ToList();
    }

    [HttpPost("plans")]
    public async Task<PlanDto> CreatePlan(SavePlanRequest req)
    {
        var plan = new SubscriptionPlan();
        await ApplyAsync(plan, req);
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();
        return ToDto(plan, 0);
    }

    /// <summary>Changes the package. Companies already on it keep their prices and modules until they are put on it again.</summary>
    [HttpPut("plans/{id:guid}")]
    public async Task<PlanDto> UpdatePlan(Guid id, SavePlanRequest req)
    {
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Package");
        await ApplyAsync(plan, req);
        await db.SaveChangesAsync();
        return ToDto(plan, await db.Tenants.CountAsync(t => t.PlanId == id));
    }

    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Package");
        if (await db.Tenants.AnyAsync(t => t.PlanId == id))
            throw new DomainException("Companies are on this package. Turn it off instead.");
        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("companies/{id:guid}/plan")]
    public async Task<CompanyPlanDto> CompanyPlan(Guid id)
    {
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Company");
        var name = t.PlanId is { } p ? await db.SubscriptionPlans.Where(x => x.Id == p).Select(x => x.Name).FirstOrDefaultAsync() : null;
        return new CompanyPlanDto(t.PlanId, name, t.TrialEndsOn, AccessModules.CompanyModules(t.EnabledModules)?.ToList(), BillingController.Modules());
    }

    [HttpPut("companies/{id:guid}/plan")]
    public async Task<CompanyPlanDto> SetCompanyPlan(Guid id, SetCompanyPlanRequest req)
    {
        await billing.AssignPlanAsync(id, req.PlanId, req.TrialEndsOn, req.Modules);
        return await CompanyPlan(id);
    }

    /// <summary>Every company's invoices; by default those waiting for a bank transfer to be checked.</summary>
    [HttpGet("invoices")]
    public async Task<List<InvoiceDto>> Invoices([FromQuery] InvoiceStatus? status = null, [FromQuery] bool all = false, [FromQuery] Guid? companyId = null)
    {
        var q = db.TenantInvoices.AsQueryable();
        if (!all) { var s = status ?? InvoiceStatus.AwaitingApproval; q = q.Where(i => i.Status == s); }
        if (companyId is { } c) q = q.Where(i => i.TenantId == c);
        var list = await BillingController.InvoiceQuery(q.OrderByDescending(i => i.CreatedAtUtc).Take(300)).ToListAsync();
        var ids = list.Select(i => i.TenantId).Distinct().ToList();
        var names = await db.Tenants.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);
        return list.Select(i => i with { Company = names.GetValueOrDefault(i.TenantId) }).ToList();
    }

    [HttpGet("invoices/{id:guid}/proof")]
    public async Task<IActionResult> Proof(Guid id)
    {
        var i = await db.TenantInvoices.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Invoice");
        if (i.ProofData is null)
            throw new NotFoundException("Receipt");
        return File(i.ProofData, i.ProofContentType ?? "application/octet-stream", i.ProofFileName ?? $"{i.Number}-receipt");
    }

    /// <summary>The money has arrived: records the payment and extends the company's subscription.</summary>
    [HttpPost("invoices/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ReviewInvoiceRequest req)
    {
        var i = await db.TenantInvoices.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Invoice");
        if (i.Status == InvoiceStatus.Paid)
            throw new DomainException("This invoice is already paid.");
        i.ReviewNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        await billing.MarkPaidAsync(i, "Bank transfer", i.ProviderReference);
        return NoContent();
    }

    /// <summary>The transfer could not be found: the invoice goes back to unpaid with the reason.</summary>
    [HttpPost("invoices/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, ReviewInvoiceRequest req)
    {
        var i = await db.TenantInvoices.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Invoice");
        if (i.Status != InvoiceStatus.AwaitingApproval)
            throw new DomainException("Only a receipt waiting for approval can be turned down.");
        if (string.IsNullOrWhiteSpace(req.Note))
            throw new DomainException("Say why, so the company knows what to fix.");
        i.Status = InvoiceStatus.Unpaid;
        i.ReviewNote = req.Note.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(SubscriptionPlan plan, SavePlanRequest req)
    {
        var name = req.Name.Trim();
        if (await db.SubscriptionPlans.AnyAsync(p => p.Name == name && p.Id != plan.Id))
            throw new DomainException("A package with this name already exists.");
        if (req.PricePerEmployee == 0 && req.FlatPrice == 0)
            throw new DomainException("Set a price per employee, a flat monthly price, or both.");
        plan.Name = name;
        plan.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        plan.PricePerEmployee = req.PricePerEmployee;
        plan.FlatPrice = req.FlatPrice;
        plan.MaxEmployees = req.MaxEmployees;
        plan.Modules = string.Join(",", AccessModules.All.Select(m => m.Key)
            .Where(k => AccessModules.Core.Contains(k) || (req.Modules ?? AccessModules.All.Select(m => m.Key).ToList()).Contains(k)));
        plan.TrialDays = req.TrialDays;
        plan.IsActive = req.IsActive;
        plan.SortOrder = req.SortOrder;
    }

    private static PlanDto ToDto(SubscriptionPlan p, int companies) =>
        new(p.Id, p.Name, p.Description, p.PricePerEmployee, p.FlatPrice, p.MaxEmployees, AccessModules.Parse(p.Modules), p.TrialDays, p.IsActive, p.SortOrder,
            companies, RecordStampsDto.Of(p));
}
