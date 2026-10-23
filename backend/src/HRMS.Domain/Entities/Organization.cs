using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = "";
    /// <summary>IANA time zone used for attendance dates and shift times.</summary>
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public bool IsActive { get; set; } = true;
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    /// <summary>Public careers page address part, e.g. "systems-limited"; set when first needed.</summary>
    public string? CareersSlug { get; set; }
    /// <summary>Licensed employee limit set by the platform owner; null means unlimited.</summary>
    public int? MaxEmployees { get; set; }

    public BillingCycle? BillingCycle { get; set; }
    /// <summary>Fixed base fee per billing cycle, in PKR (optional).</summary>
    public decimal? PlanPrice { get; set; }
    /// <summary>Price per licensed employee per month, in PKR (optional).</summary>
    public decimal? PricePerEmployee { get; set; }

    /// <summary>
    /// Bill for one cycle: base fee + licensed seats x per-employee monthly rate x months in the cycle.
    /// Seats are MaxEmployees, or the current headcount when there is no limit. Null when no cycle is set.
    /// </summary>
    public decimal? AmountPerCycle(int currentEmployees)
    {
        if (BillingCycle is not { } cycle)
            return null;
        var seats = MaxEmployees ?? currentEmployees;
        return (PlanPrice ?? 0) + (PricePerEmployee ?? 0) * seats * (int)cycle;
    }
    /// <summary>Last day the company may use the system (inclusive, company-local). Null means no expiry.</summary>
    public DateOnly? SubscriptionEndsOn { get; set; }

    /// <summary>Whether the company's users may sign in and use the system on the given local date.</summary>
    public TenantAccess AccessOn(DateOnly today) =>
        !IsActive ? TenantAccess.Inactive
        : SubscriptionEndsOn is { } end && end < today ? TenantAccess.Expired
        : TenantAccess.Allowed;
}

/// <summary>A subscription payment recorded by the platform owner. Only the owner sees these.</summary>
public class TenantPayment : BaseEntity
{
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public string? Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid? RecordedByUserId { get; set; }
}

public class User : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; }
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Also send notifications by email (in-app ones always appear).</summary>
    public bool EmailNotifications { get; set; } = true;
    /// <summary>Set when HR issues a temporary password; the user must pick their own after signing in.</summary>
    public bool MustChangePassword { get; set; }
    /// <summary>Limits (HR) or opens (others) HR modules; null = the role's usual access.</summary>
    public Guid? AccessTemplateId { get; set; }
}

public class Department : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    /// <summary>Can be a step in approval chains.</summary>
    public Guid? HeadEmployeeId { get; set; }
}

public class Designation : TenantEntity
{
    public string Title { get; set; } = "";
}

public class Employee : TenantEntity
{
    public string EmployeeCode { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Cnic { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly JoiningDate { get; set; }
    public DateOnly? LeavingDate { get; set; }
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Probation;
    public string? Address { get; set; }
    public string? BankName { get; set; }
    public string? BankAccount { get; set; }
    public string? PersonalEmail { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? BloodGroup { get; set; }

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? DesignationId { get; set; }
    public Designation? Designation { get; set; }
    public Guid? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public Guid? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    // ---------- Personal file ----------
    public string? FatherName { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? Nationality { get; set; }
    public string? Religion { get; set; }
    public DateOnly? CnicIssuedOn { get; set; }
    public DateOnly? CnicExpiresOn { get; set; }
    public string? PassportNo { get; set; }
    public DateOnly? PassportIssuedOn { get; set; }
    public DateOnly? PassportExpiresOn { get; set; }
    public string? VisaNo { get; set; }
    public string? VisaType { get; set; }
    public DateOnly? VisaExpiresOn { get; set; }
    public string? DrivingLicenseNo { get; set; }
    public DateOnly? DrivingLicenseExpiresOn { get; set; }
    public string? Ntn { get; set; }
    public string? EobiNo { get; set; }
    public string? SocialSecurityNo { get; set; }
    public string? InsuranceProvider { get; set; }
    public string? InsurancePolicyNo { get; set; }
    public DateOnly? InsuranceExpiresOn { get; set; }
    public string? BankBranchCode { get; set; }
    public string? BankAccountTitle { get; set; }

    // ---------- Company file ----------
    public Guid? DivisionId { get; set; }
    public Guid? SubDepartmentId { get; set; }
    public Guid? GradeId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? EmployeeGroupId { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    /// <summary>Punch code on the biometric machine, when different from the employee code.</summary>
    public string? PunchCode { get; set; }
    public Guid? ExitTypeId { get; set; }
    public string? ExitReason { get; set; }
    /// <summary>Do not re-hire.</summary>
    public bool IsBlacklisted { get; set; }
    public string? BlacklistReason { get; set; }

    // ---------- Pay ----------
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public bool ProvidentFundMember { get; set; }

    // ---------- Where they may mark attendance ----------
    public bool AllowWebCheckIn { get; set; } = true;
    public bool AllowMobileCheckIn { get; set; } = true;
    /// <summary>Skip the office location check (field staff, remote workers).</summary>
    public bool CheckInAnywhere { get; set; }
    /// <summary>Comma-separated IP addresses or prefixes ("203.0.113.") check-ins must come from; empty = any.</summary>
    public string? AllowedIps { get; set; }
    /// <summary>A personal location (e.g. home for remote work), accepted besides the offices.</summary>
    public double? OwnLatitude { get; set; }
    public double? OwnLongitude { get; set; }
    public int? OwnRadiusMeters { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public bool IsCurrent => Status is EmploymentStatus.Probation or EmploymentStatus.Active;
}
