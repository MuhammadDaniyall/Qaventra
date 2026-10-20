using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;

namespace HRMS.Tests.Domain;

public class TaxCalculatorTests
{
    /// <summary>Tax year 2025-26 salaried slabs, same as the seeded data.</summary>
    internal static TaxYear TaxYear2025_26() => new()
    {
        Code = "2025-26",
        SurchargeThreshold = 10_000_000m,
        SurchargeRate = 0.09m,
        Slabs =
        [
            new() { FromAmount = 0, ToAmount = 600_000, FixedTax = 0, Rate = 0 },
            new() { FromAmount = 600_000, ToAmount = 1_200_000, FixedTax = 0, Rate = 0.01m },
            new() { FromAmount = 1_200_000, ToAmount = 2_200_000, FixedTax = 6_000, Rate = 0.11m },
            new() { FromAmount = 2_200_000, ToAmount = 3_200_000, FixedTax = 116_000, Rate = 0.23m },
            new() { FromAmount = 3_200_000, ToAmount = 4_100_000, FixedTax = 346_000, Rate = 0.30m },
            new() { FromAmount = 4_100_000, ToAmount = null, FixedTax = 616_000, Rate = 0.35m },
        ],
    };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(600_000, 0)]
    [InlineData(1_200_000, 6_000)]
    [InlineData(2_000_000, 94_000)]
    [InlineData(3_200_000, 346_000)]
    [InlineData(5_000_000, 931_000)]
    [InlineData(10_000_000, 2_681_000)]
    public void AnnualTax_follows_the_slabs(decimal income, decimal expected)
    {
        Assert.Equal(expected, TaxCalculator.AnnualTax(TaxYear2025_26(), income));
    }

    [Fact]
    public void AnnualTax_adds_surcharge_above_ten_million()
    {
        // 616,000 + 35% of 7,900,000 = 3,381,000; plus 9% surcharge.
        Assert.Equal(3_685_290m, TaxCalculator.AnnualTax(TaxYear2025_26(), 12_000_000m));
    }

    [Fact]
    public void MonthlyTax_is_annualised_tax_over_twelve()
    {
        Assert.Equal(500m, TaxCalculator.MonthlyTax(TaxYear2025_26(), 100_000m));
    }
}

public class PayrollCalculatorTests
{
    private static EmployeeSalary Salary() => new()
    {
        BasicSalary = 60_000,
        EobiApplicable = true,
        Components =
        [
            new() { Name = "House Rent", Kind = SalaryComponentKind.Earning, Amount = 27_000, IsTaxable = true },
            new() { Name = "Medical", Kind = SalaryComponentKind.Earning, Amount = 6_000, IsTaxable = false },
            new() { Name = "Loan Installment", Kind = SalaryComponentKind.Deduction, Amount = 5_000 },
        ],
    };

    [Fact]
    public void Full_month_pays_everything()
    {
        var r = PayrollCalculator.Calculate(new(Salary(), TaxCalculatorTests.TaxYear2025_26(), 30, 0, 400, 2_000));

        Assert.Equal(93_000m, r.GrossEarnings);
        Assert.Equal(87_000m, r.TaxableIncome);
        // 87,000 x 12 = 1,044,000 -> 1% of 444,000 = 4,440 / 12 = 370
        Assert.Equal(370m, r.IncomeTax);
        Assert.Equal(400m, r.EobiEmployee);
        Assert.Equal(93_000m - 370 - 400 - 5_000, r.NetPay);
    }

    [Fact]
    public void Unpaid_days_prorate_earnings_but_not_fixed_deductions()
    {
        var r = PayrollCalculator.Calculate(new(Salary(), TaxCalculatorTests.TaxYear2025_26(), 30, 3, 400, 2_000));

        Assert.Equal(27m, r.PaidDays);
        Assert.Equal(54_000m + 24_300 + 5_400, r.GrossEarnings);
        Assert.Equal(78_300m, r.TaxableIncome);
        // 78,300 x 12 = 939,600 -> 1% of 339,600 = 3,396 / 12 = 283
        Assert.Equal(283m, r.IncomeTax);
        Assert.Equal(5_000m, r.OtherDeductions);
        Assert.Equal(83_700m - 283 - 400 - 5_000, r.NetPay);
    }

    [Fact]
    public void No_paid_days_means_no_eobi()
    {
        var r = PayrollCalculator.Calculate(new(Salary(), TaxCalculatorTests.TaxYear2025_26(), 30, 30, 400, 2_000));

        Assert.Equal(0m, r.GrossEarnings);
        Assert.Equal(0m, r.EobiEmployee);
        Assert.Equal(0m, r.EobiEmployer);
    }

    [Fact]
    public void Eobi_is_skipped_when_not_applicable()
    {
        var salary = Salary();
        salary.EobiApplicable = false;
        var r = PayrollCalculator.Calculate(new(salary, TaxCalculatorTests.TaxYear2025_26(), 30, 0, 400, 2_000));

        Assert.Equal(0m, r.EobiEmployee);
        Assert.DoesNotContain(r.Lines, l => l.Name.StartsWith("EOBI"));
    }
}

public class WorkCalendarTests
{
    [Fact]
    public void Counts_working_days_skipping_weekends_and_holidays()
    {
        // Mon 2026-10-19 .. Sun 2026-10-25 with a holiday on Wednesday.
        var days = WorkCalendar.CountWorkingDays(
            new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25),
            [DayOfWeek.Saturday, DayOfWeek.Sunday],
            new HashSet<DateOnly> { new(2026, 10, 21) });

        Assert.Equal(4, days);
    }

    [Fact]
    public void Overlap_returns_null_for_disjoint_ranges()
    {
        Assert.Null(WorkCalendar.Overlap(new(2026, 1, 1), new(2026, 1, 5), new(2026, 1, 6), new(2026, 1, 9)));
        Assert.Equal((new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 5)),
            WorkCalendar.Overlap(new(2026, 1, 1), new(2026, 1, 5), new(2026, 1, 3), new(2026, 1, 9)));
    }

    [Fact]
    public void GeoFence_distance_is_close_to_known_value()
    {
        // Lahore (Liberty Market) to Karachi (Saddar) is roughly 1,030 km.
        var meters = GeoFence.DistanceMeters(31.5102, 74.3441, 24.8556, 67.0225);
        Assert.InRange(meters, 1_000_000, 1_060_000);
    }
}
