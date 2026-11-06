using System.ComponentModel.DataAnnotations;
using HRMS.Api.Infrastructure;
using HRMS.Domain.Common;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record TaxSlabInfo(decimal FromAmount, decimal? ToAmount, decimal FixedTax, decimal Rate);

public record SalaryTaxResult(string TaxYear, List<string> TaxYears, decimal MonthlySalary, decimal AnnualSalary, decimal AnnualTax, decimal MonthlyTax,
    decimal MonthlyTakeHome, decimal AnnualTakeHome, decimal EffectiveRate, TaxSlabInfo? Slab, decimal SurchargeRate, decimal SurchargeThreshold);

public record EobiResult(decimal MinimumWage, decimal EmployeeRate, decimal EmployerRate, decimal EmployeeMonthly, decimal EmployerMonthly,
    decimal TotalMonthly, decimal EmployeeYearly, decimal EmployerYearly);

public record GratuityResult(decimal LastMonthlySalary, decimal ServiceYears, decimal DaysPerYear, decimal MinYears, bool Eligible, decimal Gratuity);

public record LeaveEncashmentResult(decimal MonthlySalary, decimal Days, decimal DailyRate, decimal Amount);

public record VerifyCertificateDto(string EmployeeName, string CourseTitle, DateOnly IssuedOn);

/// <summary>
/// Free tools for anyone, no sign-in: Pakistan salary income tax (the same FBR slabs the payroll uses), EOBI, gratuity
/// and leave encashment, plus certificate verification. Rate-limited per visitor.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public class PublicToolsController(HrmsDbContext db, TenantAccessChecker access) : ControllerBase
{
    /// <summary>Monthly salary tax for a tax year (code like "2025-26"); the current year when none is given.</summary>
    [HttpGet("tools/salary-tax")]
    public async Task<SalaryTaxResult> SalaryTax([FromQuery, Range(0, 1_000_000_000)] decimal monthlySalary, [FromQuery] string? taxYear = null)
    {
        var years = await db.TaxYears.AsNoTracking().Include(t => t.Slabs).OrderByDescending(t => t.StartDate).ToListAsync();
        if (years.Count == 0)
            throw new DomainException("Tax rates are not set up yet.");
        var today = access.LocalToday("Asia/Karachi");
        var year = years.FirstOrDefault(t => t.Code == taxYear)
                   ?? years.FirstOrDefault(t => t.StartDate <= today && t.EndDate >= today)
                   ?? years[0];
        var annual = monthlySalary * 12m;
        var annualTax = TaxCalculator.AnnualTax(year, annual);
        var monthlyTax = TaxCalculator.MonthlyTax(year, monthlySalary);
        var slab = year.Slabs.OrderByDescending(s => s.FromAmount).FirstOrDefault(s => annual > s.FromAmount)
                   ?? year.Slabs.OrderBy(s => s.FromAmount).FirstOrDefault();
        return new SalaryTaxResult(year.Code, years.Select(t => t.Code).ToList(), monthlySalary, annual, annualTax, monthlyTax,
            monthlySalary - monthlyTax, annual - annualTax, annual > 0 ? Math.Round(annualTax / annual * 100m, 2) : 0m,
            slab is null ? null : new TaxSlabInfo(slab.FromAmount, slab.ToAmount, slab.FixedTax, slab.Rate), year.SurchargeRate, year.SurchargeThreshold);
    }

    [HttpGet("tools/eobi")]
    public EobiResult Eobi([FromQuery, Range(0, 10_000_000)] decimal? minimumWage = null)
    {
        var wage = minimumWage is > 0 ? minimumWage.Value : EobiCalculator.DefaultMinimumWage;
        var (employee, employer) = EobiCalculator.Monthly(wage);
        return new EobiResult(wage, EobiCalculator.EmployeeRate, EobiCalculator.EmployerRate, employee, employer, employee + employer, employee * 12, employer * 12);
    }

    /// <summary>Service as dates (joined / left) or as years; last salary is basic (or gross, if the company pays gratuity on gross).</summary>
    [HttpGet("tools/gratuity")]
    public GratuityResult Gratuity([FromQuery, Range(0, 100_000_000)] decimal lastMonthlySalary, [FromQuery, Range(0, 80)] decimal? serviceYears = null,
        [FromQuery] DateOnly? joinedOn = null, [FromQuery] DateOnly? leftOn = null, [FromQuery, Range(0, 60)] decimal daysPerYear = 30,
        [FromQuery, Range(0, 10)] decimal minYears = 1)
    {
        var years = serviceYears
                    ?? (joinedOn is { } j ? GratuityCalculator.ServiceYears(j, leftOn ?? access.LocalToday("Asia/Karachi")) : throw new DomainException("Give the years of service or the joining date."));
        var amount = GratuityCalculator.Accrued(lastMonthlySalary, daysPerYear, years, minYears);
        return new GratuityResult(lastMonthlySalary, years, daysPerYear, minYears, years >= minYears, amount);
    }

    /// <summary>Unused leave paid out at the monthly salary / 30 per day, as the final settlement does.</summary>
    [HttpGet("tools/leave-encashment")]
    public LeaveEncashmentResult LeaveEncashment([FromQuery, Range(0, 100_000_000)] decimal monthlySalary, [FromQuery, Range(0, 365)] decimal days)
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var r = SettlementCalculator.Calculate(new SettlementInput(monthlySalary, days, 0, day, day, false, 0, 0, 0));
        return new LeaveEncashmentResult(monthlySalary, days, r.DailyRate, r.LeaveEncashment);
    }

    /// <summary>Public certificate check: only the name, course and date, nothing else about the person or company.</summary>
    [HttpGet("certificates/{code}")]
    public async Task<VerifyCertificateDto> Verify(string code)
    {
        var clean = (code ?? "").Trim().ToUpperInvariant();
        if (clean.Length is < 6 or > 20)
            throw new NotFoundException("Certificate");
        return await db.TrainingCertificates.IgnoreQueryFilters().Where(c => c.Code == clean)
                   .Select(c => new VerifyCertificateDto(c.EmployeeName, c.CourseTitle, c.IssuedOn)).FirstOrDefaultAsync()
               ?? throw new NotFoundException("Certificate");
    }
}
