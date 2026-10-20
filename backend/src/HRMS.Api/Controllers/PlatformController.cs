using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

/// <summary>Access is computed: Allowed, Inactive (switched off by the owner) or Expired (subscription end date passed).</summary>
public record CompanyDto(
    Guid Id, string Name, bool IsActive, TenantAccess Access, DateTime CreatedAtUtc,
    string? Phone, string? City, string? Address, int? MaxEmployees,
    BillingCycle? BillingCycle, decimal? PlanPrice, decimal? PricePerEmployee, decimal? AmountPerCycle, DateOnly? SubscriptionEndsOn, int? DaysLeft,
    int Employees, int Users, string? AdminName, string? AdminEmail, DateTime? LastActivityUtc, DateOnly? LastPaymentOn);

public record PlatformStatsDto(
    int Companies, int ActiveCompanies, int InactiveCompanies, int ExpiredCompanies, int ExpiringSoon,
    int Employees, int Users, decimal RevenueThisMonth, decimal RevenueThisYear,
    int LoginsToday, int FailedLoginsToday, int EventsToday,
    List<CompanyDto> ExpiringCompanies);

public record SetCompanyStatusRequest(bool IsActive);

public record CreateCompanyRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    /// <summary>Optional: defaults to the company name, so the admin shows as "&lt;Company&gt; Super Admin".</summary>
    [StringLength(100)] string? AdminFirstName,
    [StringLength(100)] string? AdminLastName,
    [Required, EmailAddress] string AdminEmail,
    [StringLength(100, MinimumLength = 8)] string? AdminPassword,
    [StringLength(30)] string? Phone,
    [StringLength(100)] string? City,
    [StringLength(500)] string? Address,
    [Range(1, 1_000_000)] int? MaxEmployees,
    BillingCycle? BillingCycle,
    [Range(0, 100_000_000)] decimal? PlanPrice,
    [Range(0, 1_000_000)] decimal? PricePerEmployee,
    DateOnly? SubscriptionEndsOn);

public record UpdateCompanyRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [StringLength(30)] string? Phone,
    [StringLength(100)] string? City,
    [StringLength(500)] string? Address,
    [Range(1, 1_000_000)] int? MaxEmployees,
    BillingCycle? BillingCycle,
    [Range(0, 100_000_000)] decimal? PlanPrice,
    [Range(0, 1_000_000)] decimal? PricePerEmployee,
    DateOnly? SubscriptionEndsOn);

/// <summary>GeneratedPassword is returned only when the system created it; show it once and never store it.</summary>
public record CompanyAdminCredentialsDto(CompanyDto Company, string AdminEmail, string? GeneratedPassword);

public record ResetAdminPasswordRequest([StringLength(100, MinimumLength = 8)] string? Password);

public record RecordPaymentRequest(
    [Range(1, 100_000_000)] decimal Amount,
    DateOnly PaidOn,
    [Range(1, 36)] int Periods = 1,
    [StringLength(50)] string? Method = null,
    [StringLength(100)] string? Reference = null,
    [StringLength(1000)] string? Notes = null);

public record PaymentDto(Guid Id, decimal Amount, DateOnly PaidOn, DateOnly PeriodFrom, DateOnly PeriodTo, string? Method, string? Reference, string? Notes, DateTime CreatedAtUtc);

public record DeleteCompanyRequest([Required] string ConfirmName);

/// <summary>Platform owner (SuperAdmin) only: creates and manages every company, its plan and payments.</summary>
[ApiController]
[Route("api/platform")]
[Authorize(Policy = Policies.SuperAdmin)]
public class PlatformController(
    HrmsDbContext db,
    CompanySetupService setup,
    SubscriptionService subscriptions,
    SessionService sessions,
    TenantAccessChecker access,
    AuditWriter audit,
    ICurrentUser me) : ControllerBase
{
    private const int ExpiringWithinDays = 7;

    [HttpGet("stats")]
    public async Task<PlatformStatsDto> Stats()
    {
        var since = DateTime.UtcNow.Date;
        var logs = db.AuditLogs.Where(l => l.TimestampUtc >= since);
        var companies = await LoadCompanies(db.Tenants);
        var today = access.LocalToday("Asia/Karachi");
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var yearStart = new DateOnly(today.Year, 1, 1);

        var expiring = companies
            .Where(c => c.Access == TenantAccess.Allowed && c.DaysLeft is >= 0 and <= ExpiringWithinDays)
            .OrderBy(c => c.DaysLeft).ToList();

        return new PlatformStatsDto(
            companies.Count,
            companies.Count(c => c.Access == TenantAccess.Allowed),
            companies.Count(c => c.Access == TenantAccess.Inactive),
            companies.Count(c => c.Access == TenantAccess.Expired),
            expiring.Count,
            await db.Employees.IgnoreQueryFilters().CountAsync(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation),
            await db.Users.IgnoreQueryFilters().CountAsync(u => u.Role != UserRole.SuperAdmin),
            await db.TenantPayments.Where(p => p.PaidOn >= monthStart).SumAsync(p => (decimal?)p.Amount) ?? 0,
            await db.TenantPayments.Where(p => p.PaidOn >= yearStart).SumAsync(p => (decimal?)p.Amount) ?? 0,
            await logs.CountAsync(l => l.Category == AuditCategory.Auth && l.Action == "LoginSucceeded"),
            await logs.CountAsync(l => l.Category == AuditCategory.Auth && l.Action == "LoginFailed"),
            await logs.CountAsync(),
            expiring);
    }

    [HttpGet("companies")]
    public Task<List<CompanyDto>> Companies() => LoadCompanies(db.Tenants.OrderByDescending(t => t.CreatedAtUtc));

    [HttpGet("companies/{id:guid}")]
    public async Task<CompanyDto> Company(Guid id) =>
        (await LoadCompanies(db.Tenants.Where(t => t.Id == id))).FirstOrDefault() ?? throw new NotFoundException("Company");

    /// <summary>Creates the company and its Company Super Admin login in one step.</summary>
    [HttpPost("companies")]
    public async Task<CompanyAdminCredentialsDto> Create(CreateCompanyRequest req)
    {
        // The Company Super Admin is named after the company unless a person's name is given.
        var firstName = string.IsNullOrWhiteSpace(req.AdminFirstName) ? req.Name.Trim() : req.AdminFirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(req.AdminFirstName) && string.IsNullOrWhiteSpace(req.AdminLastName)
            ? "Super Admin" : (req.AdminLastName ?? "").Trim();
        if (firstName.Length > 100)
            firstName = firstName[..100];

        var created = await setup.CreateAsync(new NewCompany(
            req.Name, firstName, lastName, req.AdminEmail, req.AdminPassword,
            req.Phone, req.City, req.Address, req.MaxEmployees));

        created.Company.BillingCycle = req.BillingCycle;
        created.Company.PlanPrice = req.PlanPrice;
        created.Company.PricePerEmployee = req.PricePerEmployee;
        created.Company.SubscriptionEndsOn = req.SubscriptionEndsOn;
        await db.SaveChangesAsync();

        return new CompanyAdminCredentialsDto(await Company(created.Company.Id), created.Admin.Email, created.GeneratedPassword);
    }

    [HttpPut("companies/{id:guid}")]
    public async Task<CompanyDto> Update(Guid id, UpdateCompanyRequest req)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Company");
        if (await db.Tenants.AnyAsync(t => t.Id != id && t.Name == req.Name.Trim()))
            throw new DomainException("A company with this name already exists.");
        tenant.Name = req.Name.Trim();
        tenant.Phone = req.Phone;
        tenant.City = req.City;
        tenant.Address = req.Address;
        tenant.MaxEmployees = req.MaxEmployees;
        tenant.BillingCycle = req.BillingCycle;
        tenant.PlanPrice = req.PlanPrice;
        tenant.PricePerEmployee = req.PricePerEmployee;
        tenant.SubscriptionEndsOn = req.SubscriptionEndsOn;
        await db.SaveChangesAsync();
        access.Invalidate(id);
        return await Company(id);
    }

    /// <summary>Inactive companies are blocked immediately: no user of that company can sign in or make any request.</summary>
    [HttpPut("companies/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetCompanyStatusRequest req)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Company");
        tenant.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        access.Invalidate(id);
        return NoContent();
    }

    /// <summary>Permanently deletes the company and all its data. The request must repeat the exact company name.</summary>
    [HttpDelete("companies/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteCompanyRequest req)
    {
        var company = await Company(id);
        await subscriptions.DeleteCompanyAsync(id, req.ConfirmName);
        await audit.WriteAsync(new AuditLog
        {
            Category = AuditCategory.Data,
            Action = "CompanyDeleted",
            TenantId = id,
            UserId = me.UserId,
            UserEmail = me.Email,
            Role = me.Role,
            EntityType = nameof(Tenant),
            EntityId = id.ToString(),
            Changes = JsonSerializer.Serialize(new
            {
                Name = new { old = company.Name },
                Employees = new { old = company.Employees },
                Users = new { old = company.Users },
                AdminEmail = new { old = company.AdminEmail },
            }),
            IpAddress = me.IpAddress,
            UserAgent = AuditWriter.Cut(me.UserAgent, 512),
            CorrelationId = me.CorrelationId,
        });
        return NoContent();
    }

    [HttpGet("companies/{id:guid}/payments")]
    public Task<List<PaymentDto>> Payments(Guid id) =>
        db.TenantPayments.Where(p => p.TenantId == id).OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.CreatedAtUtc)
            .Select(p => new PaymentDto(p.Id, p.Amount, p.PaidOn, p.PeriodFrom, p.PeriodTo, p.Method, p.Reference, p.Notes, p.CreatedAtUtc))
            .ToListAsync();

    /// <summary>Records a payment and extends the subscription by the given number of billing cycles.</summary>
    [HttpPost("companies/{id:guid}/payments")]
    public async Task<CompanyDto> RecordPayment(Guid id, RecordPaymentRequest req)
    {
        await subscriptions.RecordPaymentAsync(id, new NewPayment(req.Amount, req.PaidOn, req.Periods, req.Method, req.Reference, req.Notes));
        return await Company(id);
    }

    [HttpPost("companies/{id:guid}/reset-admin-password")]
    public async Task<CompanyAdminCredentialsDto> ResetAdminPassword(Guid id, ResetAdminPasswordRequest req)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == id))
            throw new NotFoundException("Company");
        var (admin, generated) = await setup.ResetAdminPasswordAsync(id, req.Password);
        access.InvalidateUser(admin.Id);
        await audit.AuthAsync("CompanyAdminPasswordReset", admin);
        return new CompanyAdminCredentialsDto(await Company(id), admin.Email, generated);
    }

    /// <summary>
    /// Returns a token scoped to the company so the owner can see and do everything inside it, with HR and admin rights.
    /// Works for inactive or expired companies too. Every action is logged under the owner's own account.
    /// </summary>
    [HttpPost("companies/{id:guid}/enter")]
    public async Task<SessionDto> Enter(Guid id)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Company");
        var owner = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == me.UserId);
        await audit.WriteAsync(new AuditLog
        {
            Category = AuditCategory.Auth,
            Action = "OwnerEnteredCompany",
            TenantId = tenant.Id,
            UserId = owner.Id,
            UserEmail = owner.Email,
            Role = owner.Role,
            EntityType = nameof(Tenant),
            EntityId = tenant.Id.ToString(),
            IpAddress = me.IpAddress,
            UserAgent = AuditWriter.Cut(me.UserAgent, 512),
            CorrelationId = me.CorrelationId,
        });
        return await sessions.CreateAsync(owner, tenant.Id);
    }

    private async Task<List<CompanyDto>> LoadCompanies(IQueryable<Tenant> tenants)
    {
        var rows = await tenants.Select(t => new
        {
            Tenant = t,
            Employees = db.Employees.IgnoreQueryFilters().Count(e => e.TenantId == t.Id && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)),
            Users = db.Users.IgnoreQueryFilters().Count(u => u.TenantId == t.Id),
            Admin = db.Users.IgnoreQueryFilters().Where(u => u.TenantId == t.Id && u.Role == UserRole.CompanyAdmin)
                .OrderBy(u => u.CreatedAtUtc).Select(u => new { u.FullName, u.Email }).FirstOrDefault(),
            LastActivity = db.AuditLogs.Where(l => l.TenantId == t.Id).Max(l => (DateTime?)l.TimestampUtc),
            LastPayment = db.TenantPayments.Where(p => p.TenantId == t.Id).Max(p => (DateOnly?)p.PaidOn),
        }).ToListAsync();

        return rows.Select(r =>
        {
            var t = r.Tenant;
            var today = access.LocalToday(t.TimeZoneId);
            return new CompanyDto(
                t.Id, t.Name, t.IsActive, t.AccessOn(today), t.CreatedAtUtc, t.Phone, t.City, t.Address, t.MaxEmployees,
                t.BillingCycle, t.PlanPrice, t.PricePerEmployee, t.AmountPerCycle(r.Employees), t.SubscriptionEndsOn,
                t.SubscriptionEndsOn is { } end ? end.DayNumber - today.DayNumber : null,
                r.Employees, r.Users, r.Admin?.FullName, r.Admin?.Email, r.LastActivity, r.LastPayment);
        }).ToList();
    }
}
