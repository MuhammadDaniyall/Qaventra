namespace HRMS.Domain.Services;

public record SettlementInput(
    decimal MonthlyGross,
    decimal EncashedLeaveDays,
    int NoticePeriodDays,
    DateOnly NoticeDate,
    DateOnly LastWorkingDay,
    bool RecoverNoticeShortfall,
    decimal ExpenseReimbursement,
    decimal OtherAdditions,
    decimal OtherDeductions);

public record SettlementResult(
    decimal DailyRate,
    decimal LeaveEncashment,
    int NoticeServedDays,
    int NoticeShortfallDays,
    decimal NoticeRecovery,
    decimal ExpenseReimbursement,
    decimal OtherAdditions,
    decimal OtherDeductions,
    decimal Net);

/// <summary>
/// Final settlement on top of the last month's salary (which payroll pays, prorated to the leaving date).
/// Daily rate is gross monthly salary / 30. Notice served counts calendar days from notice to last day, inclusive.
/// </summary>
public static class SettlementCalculator
{
    public static SettlementResult Calculate(SettlementInput i)
    {
        if (i.EncashedLeaveDays < 0 || i.OtherAdditions < 0 || i.OtherDeductions < 0)
            throw new ArgumentOutOfRangeException(nameof(i), "Settlement amounts cannot be negative.");

        var daily = Math.Round(i.MonthlyGross / 30m, 2, MidpointRounding.AwayFromZero);
        var encashment = Math.Round(daily * i.EncashedLeaveDays, 0, MidpointRounding.AwayFromZero);

        var served = Math.Max(0, i.LastWorkingDay.DayNumber - i.NoticeDate.DayNumber + 1);
        var shortfall = Math.Max(0, i.NoticePeriodDays - served);
        var recovery = i.RecoverNoticeShortfall ? Math.Round(daily * shortfall, 0, MidpointRounding.AwayFromZero) : 0m;

        var net = encashment + i.ExpenseReimbursement + i.OtherAdditions - recovery - i.OtherDeductions;
        return new SettlementResult(daily, encashment, served, shortfall, recovery, i.ExpenseReimbursement, i.OtherAdditions, i.OtherDeductions, net);
    }
}
