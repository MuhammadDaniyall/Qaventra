using System.Globalization;
using HRMS.Domain.Entities;

namespace HRMS.Domain.Services;

/// <summary>A stretch of days one payroll run pays for. Year and Month are those of its last day.</summary>
public record PayPeriod(PayFrequency Frequency, DateOnly Start, DateOnly End)
{
    public int Year => End.Year;
    public int Month => End.Month;
    public int Days => End.DayNumber - Start.DayNumber + 1;

    public string Label => PayPeriods.Label(Frequency, Start, End);
}

public static class PayPeriods
{
    /// <summary>The Monday the bi-weekly count starts from when the company has not set one.</summary>
    public static readonly DateOnly DefaultAnchor = new(2026, 1, 5);

    public static int PerYear(PayFrequency f) => f switch
    {
        PayFrequency.SemiMonthly => 24,
        PayFrequency.BiWeekly => 26,
        PayFrequency.Weekly => 52,
        _ => 12,
    };

    /// <summary>The part of a monthly salary one period pays: 12 months spread over the periods in a year.</summary>
    public static decimal ShareOfMonth(PayFrequency f) => 12m / PerYear(f);

    /// <summary>The period that contains a day.</summary>
    /// <param name="weekStartsOn">First day of a weekly or bi-weekly period.</param>
    /// <param name="anchor">A day on which some bi-weekly period starts.</param>
    public static PayPeriod Containing(PayFrequency f, DateOnly day, DayOfWeek weekStartsOn = DayOfWeek.Monday, DateOnly? anchor = null)
    {
        switch (f)
        {
            case PayFrequency.SemiMonthly:
                return day.Day <= 15
                    ? new(f, new DateOnly(day.Year, day.Month, 1), new DateOnly(day.Year, day.Month, 15))
                    : new(f, new DateOnly(day.Year, day.Month, 16), new DateOnly(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month)));
            case PayFrequency.Weekly:
            {
                var start = day.AddDays(-(((int)day.DayOfWeek - (int)weekStartsOn + 7) % 7));
                return new(f, start, start.AddDays(6));
            }
            case PayFrequency.BiWeekly:
            {
                var a = anchor ?? DefaultAnchor;
                var offset = ((day.DayNumber - a.DayNumber) % 14 + 14) % 14;
                var start = day.AddDays(-offset);
                return new(f, start, start.AddDays(13));
            }
            default:
                return new(f, new DateOnly(day.Year, day.Month, 1), new DateOnly(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month)));
        }
    }

    /// <summary>The periods that end in a month, oldest first.</summary>
    public static List<PayPeriod> EndingIn(PayFrequency f, int year, int month, DayOfWeek weekStartsOn = DayOfWeek.Monday, DateOnly? anchor = null)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var list = new List<PayPeriod>();
        for (var p = Containing(f, first, weekStartsOn, anchor); p.Start <= last; p = Containing(f, p.End.AddDays(1), weekStartsOn, anchor))
            if (p.End >= first && p.End <= last)
                list.Add(p);
        return list;
    }

    /// <summary>
    /// Once-a-month amounts (EOBI, recurring allowances, loan installments) go in the first period that ends in each
    /// month, so they are paid once whatever the frequency.
    /// </summary>
    public static bool TakesMonthlyItems(PayPeriod p) => p.Frequency == PayFrequency.Monthly || p.End.AddDays(-p.Days).Month != p.End.Month;

    public static string Label(PayFrequency f, DateOnly start, DateOnly end)
    {
        var c = CultureInfo.InvariantCulture;
        if (f == PayFrequency.Monthly)
            return start.ToString("MMMM yyyy", c);
        var range = start.Month == end.Month
            ? $"{start.Day}–{end.Day} {end.ToString("MMM yyyy", c)}"
            : $"{start.ToString("d MMM", c)} – {end.ToString("d MMM yyyy", c)}";
        return f switch
        {
            PayFrequency.Weekly => $"Week {range}",
            PayFrequency.BiWeekly => $"Fortnight {range}",
            _ => $"Half month {range}",
        };
    }

    /// <summary>Withholding for one period: the period's taxable pay annualised, its tax spread over the periods in a year.</summary>
    public static decimal PeriodTax(TaxYear taxYear, decimal periodTaxable, PayFrequency f) =>
        Math.Round(TaxCalculator.AnnualTax(taxYear, periodTaxable * PerYear(f)) / PerYear(f), 0, MidpointRounding.AwayFromZero);
}
