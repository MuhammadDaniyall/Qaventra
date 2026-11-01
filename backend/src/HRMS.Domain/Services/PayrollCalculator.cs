using HRMS.Domain.Entities;

namespace HRMS.Domain.Services;

public record PayrollInput(
    EmployeeSalary Salary,
    TaxYear TaxYear,
    int DaysInMonth,
    decimal UnpaidDays,
    decimal EobiEmployee,
    decimal EobiEmployer,
    decimal Reimbursements = 0m,
    IReadOnlyList<PayItem>? Extras = null,
    Func<decimal, decimal, decimal>? TaxFor = null);

/// <summary>
/// A one-off line for the month (bonus, overtime, deduction, loan installment): never prorated. OneOff earnings
/// (pay requests, piece work) are taxed once at the marginal rate rather than annualised as if paid every period.
/// </summary>
public record PayItem(string Name, SalaryComponentKind Kind, decimal Amount, bool IsTaxable, bool OneOff = false);

public record PayrollLine(string Name, SalaryComponentKind Kind, decimal Amount);

public record PayrollResult(
    decimal PaidDays,
    decimal GrossEarnings,
    decimal TaxableIncome,
    decimal IncomeTax,
    decimal EobiEmployee,
    decimal EobiEmployer,
    decimal OtherDeductions,
    decimal NetPay,
    IReadOnlyList<PayrollLine> Lines);

public static class PayrollCalculator
{
    /// <summary>
    /// Earnings are prorated by paid days over calendar days. Fixed deductions (loan, advance) are not prorated.
    /// Income tax is withheld on the prorated taxable earnings, annualised.
    /// </summary>
    public static PayrollResult Calculate(PayrollInput input)
    {
        if (input.DaysInMonth <= 0)
            throw new ArgumentOutOfRangeException(nameof(input), "DaysInMonth must be positive.");

        var paidDays = Math.Clamp(input.DaysInMonth - input.UnpaidDays, 0, input.DaysInMonth);
        var factor = paidDays / input.DaysInMonth;
        decimal Prorate(decimal amount) => Math.Round(amount * factor, 0, MidpointRounding.AwayFromZero);

        var lines = new List<PayrollLine>();
        var basic = Prorate(input.Salary.BasicSalary);
        lines.Add(new("Basic Salary", SalaryComponentKind.Earning, basic));

        var gross = basic;
        var taxable = basic;
        var oneOffTaxable = 0m;
        var otherDeductions = 0m;

        foreach (var c in input.Salary.Components)
        {
            if (c.Kind == SalaryComponentKind.Earning)
            {
                var amount = Prorate(c.Amount);
                lines.Add(new(c.Name, SalaryComponentKind.Earning, amount));
                gross += amount;
                if (c.IsTaxable)
                    taxable += amount;
            }
            else
            {
                lines.Add(new(c.Name, SalaryComponentKind.Deduction, c.Amount));
                otherDeductions += c.Amount;
            }
        }

        // Approved expense claims: paid in full, never prorated or taxed.
        if (input.Reimbursements > 0)
        {
            lines.Add(new("Expense reimbursement", SalaryComponentKind.Earning, input.Reimbursements));
            gross += input.Reimbursements;
        }

        foreach (var item in input.Extras ?? [])
        {
            lines.Add(new(item.Name, item.Kind, item.Amount));
            if (item.Kind == SalaryComponentKind.Earning)
            {
                gross += item.Amount;
                if (item.IsTaxable)
                {
                    taxable += item.Amount;
                    if (item.OneOff)
                        oneOffTaxable += item.Amount;
                }
            }
            else
            {
                otherDeductions += item.Amount;
            }
        }

        // A projected-year rule (previous employer, tax credits) when given; otherwise the month's regular pay annualised,
        // plus the extra annual tax the one-off earnings cause.
        var regular = taxable - oneOffTaxable;
        var tax = input.TaxFor is { } taxFor
            ? taxFor(regular, oneOffTaxable)
            : TaxCalculator.MonthlyTax(input.TaxYear, regular) + OneOffTax(input.TaxYear, regular * 12m, oneOffTaxable);
        if (tax > 0)
            lines.Add(new("Income Tax", SalaryComponentKind.Deduction, tax));

        var eobiEmployee = input.Salary.EobiApplicable && paidDays > 0 ? input.EobiEmployee : 0m;
        var eobiEmployer = input.Salary.EobiApplicable && paidDays > 0 ? input.EobiEmployer : 0m;
        if (eobiEmployee > 0)
            lines.Add(new("EOBI (Employee)", SalaryComponentKind.Deduction, eobiEmployee));

        var net = gross - tax - eobiEmployee - otherDeductions;

        return new PayrollResult(paidDays, gross, taxable, tax, eobiEmployee, eobiEmployer, otherDeductions, net, lines);
    }

    /// <summary>The extra annual tax a one-off amount adds on top of the given annual income, withheld in full when paid.</summary>
    public static decimal OneOffTax(TaxYear taxYear, decimal annualIncome, decimal oneOff) =>
        oneOff <= 0 ? 0m
            : Math.Round(TaxCalculator.AnnualTax(taxYear, annualIncome + oneOff) - TaxCalculator.AnnualTax(taxYear, annualIncome), 0, MidpointRounding.AwayFromZero);
}
