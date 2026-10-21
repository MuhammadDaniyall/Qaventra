namespace HRMS.Domain.Services;

/// <summary>How much leave an employee has: yearly entitlement, monthly accrual, carry forward and partial days.</summary>
public static class LeavePolicy
{
    /// <summary>The year's quota, prorated by whole months for someone who joins during the year, rounded to half days.</summary>
    public static decimal YearEntitlement(decimal quota, DateOnly joining, int year)
    {
        if (joining.Year > year)
            return 0;
        if (joining.Year < year)
            return quota;
        var monthsLeft = 12 - joining.Month + 1;
        return RoundHalf(quota * monthsLeft / 12m);
    }

    /// <summary>
    /// Days earned by a date. Upfront: the whole entitlement. Monthly: one twelfth per month from January (or the joining
    /// month), counting the current month, never more than the entitlement.
    /// </summary>
    public static decimal Earned(LeaveAccrual accrual, decimal quota, DateOnly joining, int year, DateOnly asOf)
    {
        var entitlement = YearEntitlement(quota, joining, year);
        if (accrual == LeaveAccrual.Upfront || asOf.Year > year)
            return entitlement;
        if (asOf.Year < year)
            return 0;
        var firstMonth = joining.Year == year ? joining.Month : 1;
        var months = Math.Max(0, asOf.Month - firstMonth + 1);
        return Math.Min(entitlement, RoundHalf(quota * months / 12m));
    }

    /// <summary>Unused days moved into the next year, capped by the leave type.</summary>
    public static decimal CarryForward(decimal unused, decimal max) => Math.Clamp(unused, 0, Math.Max(0, max));

    /// <summary>Short leave as a fraction of the shift, in quarter days (minimum a quarter).</summary>
    public static decimal ShortLeaveDays(TimeOnly start, TimeOnly end, TimeOnly shiftStart, TimeOnly shiftEnd)
    {
        var shiftHours = (decimal)(shiftEnd - shiftStart).TotalHours;
        if (shiftHours <= 0)
            shiftHours = 8;
        var hours = (decimal)(end - start).TotalHours;
        var quarters = Math.Ceiling(hours / shiftHours * 4);
        return Math.Clamp(quarters / 4, 0.25m, 1m);
    }

    private static decimal RoundHalf(decimal days) => Math.Round(days * 2, MidpointRounding.AwayFromZero) / 2;
}
