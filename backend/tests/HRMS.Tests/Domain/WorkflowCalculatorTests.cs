using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;

namespace HRMS.Tests.Domain;

public class LetterRendererTests
{
    [Fact]
    public void Fills_known_placeholders_and_keeps_unknown_ones()
    {
        var values = new Dictionary<string, string?> { ["EmployeeName"] = "Ayesha Khan", ["Designation"] = null };
        var text = LetterRenderer.Render("Dear {{ EmployeeName }}, you are {{Designation}}. {{Mystery}}", values);
        Assert.Equal("Dear Ayesha Khan, you are —. {{Mystery}}", text);
    }

    [Fact]
    public void Reports_unknown_placeholders_once()
    {
        Assert.Equal(["Salary"], LetterRenderer.UnknownKeys("{{EmployeeName}} {{Salary}} {{Salary}}"));
    }
}

public class SettlementCalculatorTests
{
    private static SettlementInput Input(bool recover = true, decimal encash = 0, int notice = 30, int servedDays = 30) => new(
        MonthlyGross: 90_000, EncashedLeaveDays: encash, NoticePeriodDays: notice,
        NoticeDate: new DateOnly(2026, 9, 1), LastWorkingDay: new DateOnly(2026, 9, 1).AddDays(servedDays - 1),
        RecoverNoticeShortfall: recover, ExpenseReimbursement: 2_500, OtherAdditions: 1_000, OtherDeductions: 500);

    [Fact]
    public void Full_notice_served_means_no_recovery()
    {
        var r = SettlementCalculator.Calculate(Input(encash: 5));
        Assert.Equal(3_000m, r.DailyRate);
        Assert.Equal(15_000m, r.LeaveEncashment);
        Assert.Equal(0, r.NoticeShortfallDays);
        Assert.Equal(15_000m + 2_500 + 1_000 - 500, r.Net);
    }

    [Fact]
    public void Short_notice_is_recovered_at_the_daily_rate_unless_waived()
    {
        var recovered = SettlementCalculator.Calculate(Input(servedDays: 20));
        Assert.Equal(10, recovered.NoticeShortfallDays);
        Assert.Equal(30_000m, recovered.NoticeRecovery);
        Assert.Equal(2_500m + 1_000 - 500 - 30_000, recovered.Net);

        var waived = SettlementCalculator.Calculate(Input(recover: false, servedDays: 20));
        Assert.Equal(0m, waived.NoticeRecovery);
    }
}

public class PayrollReimbursementTests
{
    [Fact]
    public void Reimbursements_are_paid_in_full_and_not_taxed()
    {
        var salary = new EmployeeSalary { BasicSalary = 150_000 };
        var taxYear = TaxCalculatorTests.TaxYear2025_26();
        var without = PayrollCalculator.Calculate(new PayrollInput(salary, taxYear, 30, 15, 0, 0));
        var with = PayrollCalculator.Calculate(new PayrollInput(salary, taxYear, 30, 15, 0, 0, 4_200));

        Assert.Equal(without.IncomeTax, with.IncomeTax);
        Assert.Equal(without.NetPay + 4_200, with.NetPay);
        Assert.Contains(with.Lines, l => l.Name == "Expense reimbursement" && l.Amount == 4_200 && l.Kind == SalaryComponentKind.Earning);
    }
}
