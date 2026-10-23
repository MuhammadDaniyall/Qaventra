using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>
/// One entry in a company's own lists used to group people: divisions, grades, cost centers, regions, employee groups,
/// sub-departments (under a department) and exit types.
/// </summary>
public class OrgLookup : TenantEntity
{
    public LookupKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    /// <summary>Department a sub-department belongs to.</summary>
    public Guid? ParentId { get; set; }
    /// <summary>Head of a division or sub-department.</summary>
    public Guid? HeadEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Family member on the employee's file (for insurance, nominee for final dues).</summary>
public class Dependant : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public string Relation { get; set; } = "";
    public string? Cnic { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public bool IsNominee { get; set; }
    /// <summary>Share of dues if several nominees (percent).</summary>
    public decimal? NomineeShare { get; set; }
    public bool CoveredByInsurance { get; set; }
    public int Order { get; set; }
}

/// <summary>A degree, diploma or certificate.</summary>
public class Qualification : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public string Degree { get; set; } = "";
    public string Institute { get; set; } = "";
    public string? Field { get; set; }
    /// <summary>Grade, division or CGPA as written on the certificate.</summary>
    public string? Grade { get; set; }
    public int? FromYear { get; set; }
    public int? ToYear { get; set; }
    public bool IsHighest { get; set; }
    public int Order { get; set; }
}

/// <summary>A previous job.</summary>
public class WorkExperience : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public string Organization { get; set; } = "";
    public string Designation { get; set; } = "";
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public decimal? LastSalary { get; set; }
    public string? ReasonForLeaving { get; set; }
    public int Order { get; set; }
}
