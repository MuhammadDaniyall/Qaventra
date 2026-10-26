using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>An account from the company's chart of accounts, as their accounting software knows it.</summary>
public class GlAccount : TenantEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>Which account a payroll amount posts to in the journal voucher. One row per <see cref="PayrollGlLine"/>.</summary>
public class PayrollGlMapping : TenantEntity
{
    public PayrollGlLine Line { get; set; }
    public Guid GlAccountId { get; set; }
}

public enum PayrollGlLine
{
    /// <summary>Debit: gross salaries, split by department.</summary>
    SalaryExpense = 1,
    /// <summary>Debit: employer EOBI, provident fund and social security, split by department.</summary>
    EmployerContributionExpense = 2,
    IncomeTaxPayable = 3,
    EobiPayable = 4,
    ProvidentFundPayable = 5,
    SocialSecurityPayable = 6,
    /// <summary>Loans, advances and other deductions taken from pay.</summary>
    OtherDeductionsPayable = 7,
    /// <summary>Credit: net pay owed to employees.</summary>
    SalaryPayable = 8,
}

/// <summary>
/// A read-only key for another system (accounting, BI, a bank file tool) to pull data from /api/integration.
/// Only a hash of the key is kept.
/// </summary>
public class ApiToken : TenantEntity
{
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string TokenPrefix { get; set; } = "";
    public string? CreatedByName { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
}
