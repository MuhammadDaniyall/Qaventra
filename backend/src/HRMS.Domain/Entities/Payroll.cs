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
    /// <summary>Overtime pay per hour = basic salary / 30 / 8 x this multiplier.</summary>
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    /// <summary>Late arrivals that make one penalty (e.g. 3); 0 switches late penalties off.</summary>
    public int LatesPerPenalty { get; set; }
    /// <summary>Days of pay deducted per penalty (e.g. 1 or 0.5).</summary>
    public decimal LatePenaltyDays { get; set; } = 1m;
    /// <summary>A day marked half day (worked under half the shift) costs half a day's pay.</summary>
    public bool DeductHalfDays { get; set; }
    /// <summary>When true, the company admin approves each month's payroll before HR can finalize it.</summary>
    public bool RequireApproval { get; set; }
    /// <summary>Leave encashment pays basic (false) or gross (true) salary per day, over 30 days.</summary>
    public bool EncashOnGross { get; set; }

    // ---------- Provident fund ----------
    public bool ProvidentFundEnabled { get; set; }
    /// <summary>Percent of basic (or gross) taken from the member each month.</summary>
    public decimal PfEmployeePercent { get; set; } = 8.33m;
    /// <summary>Percent of basic (or gross) the company adds each month.</summary>
    public decimal PfEmployerPercent { get; set; } = 8.33m;
    public bool PfOnGross { get; set; }
    /// <summary>Years of service before the employer share belongs to the person (0 = at once).</summary>
    public int PfVestingYears { get; set; }

    // ---------- Voluntary pension scheme (VPS) ----------
    /// <summary>Members' own contributions are deducted and earn the section 63 tax credit; the company may add a share.</summary>
    public bool VpsEnabled { get; set; }
    /// <summary>Contributions above this percent of taxable pay get no tax credit (the law's limit is 20%).</summary>
    public decimal VpsCreditCapPercent { get; set; } = 20m;

    // ---------- Gratuity ----------
    public bool GratuityEnabled { get; set; }
    /// <summary>Days of salary per year of service (e.g. 30).</summary>
    public decimal GratuityDaysPerYear { get; set; } = 30;
    /// <summary>Service needed before any gratuity is due.</summary>
    public decimal GratuityMinYears { get; set; } = 1;
    public bool GratuityOnGross { get; set; }

    // ---------- Social security (PESSI / SESSI) ----------
    /// <summary>Employer contribution as a percent of wages for people with a social security number (0 = off).</summary>
    public decimal SocialSecurityPercent { get; set; }
    /// <summary>Wages above this are not counted; null = no ceiling.</summary>
    public decimal? SocialSecurityWageCeiling { get; set; }

    // ---------- Overtime ----------
    /// <summary>Multiplier for overtime on a public holiday.</summary>
    public decimal HolidayOvertimeMultiplier { get; set; } = 2m;
    /// <summary>Multiplier for overtime on a weekly day off.</summary>
    public decimal OffDayOvertimeMultiplier { get; set; } = 2m;
    /// <summary>Claims under this many minutes are refused (0 = any).</summary>
    public int OvertimeMinMinutes { get; set; }
    /// <summary>Hours are rounded down to this many minutes (0 = exact).</summary>
    public int OvertimeRoundMinutes { get; set; }
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
    public DateTime? SubmittedAtUtc { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    /// <summary>The approver's note, or why it was sent back.</summary>
    public string? ApprovalNote { get; set; }
    public DateTime? DisbursedAtUtc { get; set; }
    public string? DisbursedVia { get; set; }
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
    /// <summary>Provident fund: member's share (also a deduction line) and the company's share.</summary>
    public decimal PfEmployee { get; set; }
    public decimal PfEmployer { get; set; }
    /// <summary>Voluntary pension: member's share (a deduction line), the company's share, and the tax it saved the member.</summary>
    public decimal VpsEmployee { get; set; }
    public decimal VpsEmployer { get; set; }
    public decimal VpsTaxCredit { get; set; }
    public decimal SocialSecurityEmployer { get; set; }
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
