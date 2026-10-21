using HRMS.Domain;
using HRMS.Domain.Services;

namespace HRMS.Tests.Domain;

public class LeavePolicyTests
{
    [Fact]
    public void Joiners_get_a_prorated_year_rounded_to_half_days()
    {
        Assert.Equal(14m, LeavePolicy.YearEntitlement(14, new DateOnly(2024, 3, 1), 2026));
        Assert.Equal(7m, LeavePolicy.YearEntitlement(14, new DateOnly(2026, 7, 15), 2026));
        Assert.Equal(0m, LeavePolicy.YearEntitlement(14, new DateOnly(2027, 1, 1), 2026));
    }

    [Fact]
    public void Monthly_accrual_earns_a_twelfth_per_month_including_the_current_one()
    {
        var joined = new DateOnly(2020, 1, 1);
        Assert.Equal(12m, LeavePolicy.Earned(LeaveAccrual.Upfront, 12, joined, 2026, new DateOnly(2026, 1, 5)));
        Assert.Equal(1m, LeavePolicy.Earned(LeaveAccrual.Monthly, 12, joined, 2026, new DateOnly(2026, 1, 5)));
        Assert.Equal(10m, LeavePolicy.Earned(LeaveAccrual.Monthly, 12, joined, 2026, new DateOnly(2026, 10, 21)));
        Assert.Equal(12m, LeavePolicy.Earned(LeaveAccrual.Monthly, 12, joined, 2026, new DateOnly(2027, 2, 1)));
        // Joined in September: earns from September, never more than the prorated year (4 months = 4 days).
        Assert.Equal(2m, LeavePolicy.Earned(LeaveAccrual.Monthly, 12, new DateOnly(2026, 9, 10), 2026, new DateOnly(2026, 10, 1)));
        Assert.Equal(4m, LeavePolicy.Earned(LeaveAccrual.Monthly, 12, new DateOnly(2026, 9, 10), 2026, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Carry_forward_is_capped_and_never_negative()
    {
        Assert.Equal(5m, LeavePolicy.CarryForward(9, 5));
        Assert.Equal(3m, LeavePolicy.CarryForward(3, 5));
        Assert.Equal(0m, LeavePolicy.CarryForward(-2, 5));
        Assert.Equal(0m, LeavePolicy.CarryForward(9, 0));
    }

    [Theory]
    [InlineData("10:00", "12:00", 0.25)]
    [InlineData("09:00", "12:00", 0.5)]
    [InlineData("14:00", "14:30", 0.25)]
    public void Short_leave_is_a_quarter_day_fraction_of_the_shift(string start, string end, double expected)
    {
        Assert.Equal((decimal)expected, LeavePolicy.ShortLeaveDays(TimeOnly.Parse(start), TimeOnly.Parse(end), new TimeOnly(9, 0), new TimeOnly(18, 0)));
    }
}
