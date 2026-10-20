using HRMS.Domain.Entities;

namespace HRMS.Domain.Services;

public static class TaxCalculator
{
    /// <summary>Annual income tax on salaried income for the given tax year, including surcharge.</summary>
    public static decimal AnnualTax(TaxYear taxYear, decimal annualTaxableIncome)
    {
        if (annualTaxableIncome <= 0)
            return 0m;

        var slab = taxYear.Slabs
            .OrderByDescending(s => s.FromAmount)
            .FirstOrDefault(s => annualTaxableIncome > s.FromAmount);

        if (slab is null)
            return 0m;

        var tax = slab.FixedTax + (annualTaxableIncome - slab.FromAmount) * slab.Rate;

        if (taxYear.SurchargeRate > 0 && annualTaxableIncome > taxYear.SurchargeThreshold)
            tax += tax * taxYear.SurchargeRate;

        return Math.Round(tax, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Monthly withholding: the annualised monthly taxable income's tax, spread over 12 months.</summary>
    public static decimal MonthlyTax(TaxYear taxYear, decimal monthlyTaxableIncome)
        => Math.Round(AnnualTax(taxYear, monthlyTaxableIncome * 12m) / 12m, 0, MidpointRounding.AwayFromZero);
}
