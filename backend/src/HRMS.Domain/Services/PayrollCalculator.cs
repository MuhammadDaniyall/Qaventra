using HRMS.Domain.Entities;

namespace HRMS.Domain.Services;

public record PayrollInput(
    EmployeeSalary Salary,
    TaxYear TaxYear,
    int DaysInMonth,
    decimal UnpaidDays,
    decimal EobiEmployee,
    decimal EobiEmployer,
    decimal Reimbursements = 0m);

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

        var tax = TaxCalculator.MonthlyTax(input.TaxYear, taxable);
        if (tax > 0)
            lines.Add(new("Income Tax", SalaryComponentKind.Deduction, tax));

        var eobiEmployee = input.Salary.EobiApplicable && paidDays > 0 ? input.EobiEmployee : 0m;
        var eobiEmployer = input.Salary.EobiApplicable && paidDays > 0 ? input.EobiEmployer : 0m;
        if (eobiEmployee > 0)
            lines.Add(new("EOBI (Employee)", SalaryComponentKind.Deduction, eobiEmployee));

        var net = gross - tax - eobiEmployee - otherDeductions;

        return new PayrollResult(paidDays, gross, taxable, tax, eobiEmployee, eobiEmployer, otherDeductions, net, lines);
    }
}
