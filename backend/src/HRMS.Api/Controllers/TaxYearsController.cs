using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

/// <summary>Rates are fractions (0.11 = 11%). ToAmount is null for the top slab.</summary>
public record TaxSlabDto(decimal FromAmount, decimal? ToAmount, decimal FixedTax, decimal Rate);

public record TaxYearDto(Guid Id, string Code, DateOnly StartDate, DateOnly EndDate, decimal SurchargeThreshold, decimal SurchargeRate,
    List<TaxSlabDto> Slabs, int Payrolls, RecordStampsDto Stamps);

public record SaveTaxYearRequest(
    [Required, StringLength(10)] string Code,
    DateOnly StartDate,
    DateOnly EndDate,
    [Range(0, 1_000_000_000)] decimal SurchargeThreshold,
    [Range(0, 1)] decimal SurchargeRate,
    [Required] List<TaxSlabDto> Slabs);

/// <summary>
/// Platform owner (SuperAdmin) only: the salaried income tax years and slabs every company's payroll uses. They are
/// shared by all companies, so no company user may change them. Add each year after the federal budget.
/// </summary>
[ApiController]
[Route("api/platform/tax-years")]
[Authorize(Policy = Policies.SuperAdmin)]
public class TaxYearsController(HrmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<TaxYearDto>> List()
    {
        var years = await db.TaxYears.Include(t => t.Slabs).AsNoTracking().OrderByDescending(t => t.StartDate).ToListAsync();
        var used = await UsageAsync();
        return years.Select(t => ToDto(t, used.GetValueOrDefault(t.Code))).ToList();
    }

    [HttpPost]
    public async Task<TaxYearDto> Create(SaveTaxYearRequest req)
    {
        var year = new TaxYear();
        await ApplyAsync(year, req);
        db.TaxYears.Add(year);
        await db.SaveChangesAsync();
        return ToDto(year, 0);
    }

    /// <summary>Corrects a year. Finalized payslips keep the tax they were worked out with; draft payrolls pick it up on reprocess.</summary>
    [HttpPut("{id:guid}")]
    public async Task<TaxYearDto> Update(Guid id, SaveTaxYearRequest req)
    {
        var year = await db.TaxYears.Include(t => t.Slabs).FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Tax year");
        var used = (await UsageAsync()).GetValueOrDefault(year.Code);
        if (used > 0 && req.Code.Trim() != year.Code)
            throw new DomainException($"Payrolls already use {year.Code}, so its code can't change.");
        await ApplyAsync(year, req);
        await db.SaveChangesAsync();
        return ToDto(year, used);
    }

    /// <summary>Only a year no payroll or tax adjustment uses, e.g. one added by mistake.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var year = await db.TaxYears.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Tax year");
        if ((await UsageAsync()).GetValueOrDefault(year.Code) > 0
            || await db.TaxAdjustments.IgnoreQueryFilters().AnyAsync(a => a.TaxYearCode == year.Code))
            throw new DomainException($"{year.Code} is already used by payrolls or tax adjustments.");
        db.TaxYears.Remove(year);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(TaxYear year, SaveTaxYearRequest req)
    {
        var code = req.Code.Trim();
        if (!Regex.IsMatch(code, @"^\d{4}-\d{2}$"))
            throw new DomainException("Write the code as 2026-27.");
        if (await db.TaxYears.AnyAsync(t => t.Code == code && t.Id != year.Id))
            throw new DomainException($"Tax year {code} already exists.");
        if (req.EndDate <= req.StartDate || req.EndDate > req.StartDate.AddYears(1))
            throw new DomainException("A tax year ends after it starts and lasts at most a year.");
        if (await db.TaxYears.AnyAsync(t => t.Id != year.Id && t.StartDate <= req.EndDate && t.EndDate >= req.StartDate))
            throw new DomainException("These dates overlap another tax year.");

        // Slabs run from zero upwards without gaps; only the top one is open-ended.
        var slabs = (req.Slabs ?? []).OrderBy(s => s.FromAmount).ToList();
        if (slabs.Count == 0)
            throw new DomainException("Add the slabs.");
        if (slabs.Count > 30)
            throw new DomainException("At most 30 slabs.");
        if (slabs[0].FromAmount != 0)
            throw new DomainException("The first slab starts at 0.");
        for (var i = 0; i < slabs.Count; i++)
        {
            var s = slabs[i];
            var last = i == slabs.Count - 1;
            if (s.Rate is < 0 or > 1 || s.FixedTax < 0)
                throw new DomainException("Rates are between 0% and 100%, and fixed tax can't be negative.");
            if (last ? s.ToAmount is not null : s.ToAmount != slabs[i + 1].FromAmount)
                throw new DomainException(last ? "The top slab has no upper limit." : $"The slab from {s.FromAmount:N0} must end where the next one starts.");
            if (s.ToAmount is { } to && to <= s.FromAmount)
                throw new DomainException($"The slab from {s.FromAmount:N0} ends before it starts.");
        }

        year.Code = code;
        year.StartDate = req.StartDate;
        year.EndDate = req.EndDate;
        year.SurchargeThreshold = req.SurchargeThreshold;
        year.SurchargeRate = req.SurchargeRate;
        db.Set<TaxSlab>().RemoveRange(year.Slabs);
        year.Slabs.Clear();
        foreach (var s in slabs)
        {
            var slab = new TaxSlab { TaxYearId = year.Id, FromAmount = s.FromAmount, ToAmount = s.ToAmount, FixedTax = s.FixedTax, Rate = s.Rate };
            year.Slabs.Add(slab);
            // Added explicitly: a new row with its id already set would otherwise be taken for an existing one.
            db.Entry(slab).State = EntityState.Added;
        }
    }

    /// <summary>Payroll runs per tax year code, across every company.</summary>
    private Task<Dictionary<string, int>> UsageAsync() =>
        db.PayrollRuns.IgnoreQueryFilters().GroupBy(r => r.TaxYearCode).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

    private static TaxYearDto ToDto(TaxYear t, int payrolls) => new(t.Id, t.Code, t.StartDate, t.EndDate, t.SurchargeThreshold, t.SurchargeRate,
        t.Slabs.OrderBy(s => s.FromAmount).Select(s => new TaxSlabDto(s.FromAmount, s.ToAmount, s.FixedTax, s.Rate)).ToList(), payrolls,
        RecordStampsDto.Of(t));
}
