using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>Per-company payroll settings.</summary>
public class PayrollSettings : TenantEntity
{
    /// <summary>EOBI employee share per month (1% of the notified minimum wage). Verify against the current EOBI notification.</summary>
    public decimal EobiEmployeeContribution { get; set; } = 400m;
    /// <summary>EOBI employer share per month (5% of the notified minimum wage).</summary>
    public decimal EobiEmployerContribution { get; set; } = 2000m;
    /// <summary>When true, working days with no attendance and no approved leave are unpaid.</summary>
    public bool DeductAbsentDays { get; set; }
}

/// <summary>A Pakistani tax year (July–June) with its salaried-income slabs. Shared by all companies.</summary>
public class TaxYear : BaseEntity
{
    public string Code { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    /// <summary>Annual taxable income above which the surcharge applies.</summary>
    public decimal SurchargeThreshold { get; set; }
    /// <summary>Surcharge as a fraction of the computed tax (0.09 = 9%).</summary>
    public decimal SurchargeRate { get; set; }
    public List<TaxSlab> Slabs { get; set; } = [];
}

public class TaxSlab : BaseEntity
{
    public Guid TaxYearId { get; set; }
    public decimal FromAmount { get; set; }
    /// <summary>Upper bound, inclusive; null for the top slab.</summary>
    public decimal? ToAmount { get; set; }
    public decimal FixedTax { get; set; }
    /// <summary>Rate applied to income above <see cref="FromAmount"/>, as a fraction.</summary>
    public decimal Rate { get; set; }
}

public class EmployeeSalary : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal BasicSalary { get; set; }
    public bool EobiApplicable { get; set; } = true;
    public List<SalaryComponent> Components { get; set; } = [];
}

public class SalaryComponent : BaseEntity
{
    public Guid EmployeeSalaryId { get; set; }
    public string Name { get; set; } = "";
    public SalaryComponentKind Kind { get; set; }
    public decimal Amount { get; set; }
    public bool IsTaxable { get; set; } = true;
}

public class PayrollRun : TenantEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public string TaxYearCode { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public List<Payslip> Payslips { get; set; } = [];
}

public class Payslip : TenantEntity
{
    public Guid PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int DaysInMonth { get; set; }
    public decimal UnpaidDays { get; set; }
    public decimal PaidDays { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal EobiEmployee { get; set; }
    public decimal EobiEmployer { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal NetPay { get; set; }
    public List<PayslipLine> Lines { get; set; } = [];
}

public class PayslipLine : BaseEntity
{
    public Guid PayslipId { get; set; }
    public string Name { get; set; } = "";
    public SalaryComponentKind Kind { get; set; }
    public decimal Amount { get; set; }
}
