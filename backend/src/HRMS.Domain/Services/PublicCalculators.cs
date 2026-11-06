namespace HRMS.Domain.Services;

/// <summary>Gratuity as the payroll works it out: last salary / 30 x days per year x years of service (none before the minimum service).</summary>
public static class GratuityCalculator
{
    public static decimal Accrued(decimal monthlyBase, decimal daysPerYear, decimal serviceYears, decimal minYears)
    {
        if (monthlyBase <= 0 || serviceYears <= 0 || serviceYears < minYears)
            return 0m;
        return Math.Round(monthlyBase / 30m * daysPerYear * serviceYears, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>Years of service between two dates (inclusive), to two decimals, as the funds screen counts them.</summary>
    public static decimal ServiceYears(DateOnly joined, DateOnly until) =>
        Math.Max(0, Math.Round((until.DayNumber - joined.DayNumber + 1) / 365.25m, 2));
}

/// <summary>
/// EOBI contributions: the employee pays 1% and the employer 5% of the minimum wage each month
/// (EOBI Act 1976, contributions on the notified minimum wage).
/// </summary>
public static class EobiCalculator
{
    public const decimal EmployeeRate = 0.01m;
    public const decimal EmployerRate = 0.05m;
    /// <summary>Federal minimum wage per month used when none is given (2025-26).</summary>
    public const decimal DefaultMinimumWage = 40_000m;

    public static (decimal Employee, decimal Employer) Monthly(decimal minimumWage) =>
        (Math.Round(minimumWage * EmployeeRate, 0, MidpointRounding.AwayFromZero), Math.Round(minimumWage * EmployerRate, 0, MidpointRounding.AwayFromZero));
}
