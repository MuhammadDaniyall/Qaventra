using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record OrgLookupDto(Guid Id, LookupKind Kind, string Name, string? Code, Guid? ParentId, Guid? HeadEmployeeId, bool IsActive, int Employees);
public record SaveOrgLookupRequest(
    LookupKind Kind, [Required, StringLength(150)] string Name, [StringLength(30)] string? Code, Guid? ParentId, Guid? HeadEmployeeId, bool IsActive = true);

/// <summary>Personal documents and company grouping on an employee's file (the parts not on the main profile form).</summary>
public record EmployeeFileDto(
    [StringLength(100)] string? FatherName, [StringLength(100)] string? PlaceOfBirth, [StringLength(100)] string? Nationality, [StringLength(100)] string? Religion,
    DateOnly? CnicIssuedOn, DateOnly? CnicExpiresOn,
    [StringLength(40)] string? PassportNo, DateOnly? PassportIssuedOn, DateOnly? PassportExpiresOn,
    [StringLength(40)] string? VisaNo, [StringLength(100)] string? VisaType, DateOnly? VisaExpiresOn,
    [StringLength(40)] string? DrivingLicenseNo, DateOnly? DrivingLicenseExpiresOn,
    [StringLength(40)] string? Ntn, [StringLength(40)] string? EobiNo, [StringLength(40)] string? SocialSecurityNo,
    [StringLength(100)] string? InsuranceProvider, [StringLength(40)] string? InsurancePolicyNo, DateOnly? InsuranceExpiresOn,
    [StringLength(40)] string? BankBranchCode, [StringLength(100)] string? BankAccountTitle,
    Guid? DivisionId, Guid? SubDepartmentId, Guid? GradeId, Guid? CostCenterId, Guid? RegionId, Guid? EmployeeGroupId,
    DateOnly? ConfirmationDate, DateOnly? ContractEndDate, [StringLength(40)] string? PunchCode,
    Guid? ExitTypeId, [StringLength(500)] string? ExitReason, bool IsBlacklisted, [StringLength(500)] string? BlacklistReason,
    bool AllowWebCheckIn = true, bool AllowMobileCheckIn = true, bool CheckInAnywhere = false, [StringLength(500)] string? AllowedIps = null,
    [Range(-90, 90)] double? OwnLatitude = null, [Range(-180, 180)] double? OwnLongitude = null, [Range(20, 100_000)] int? OwnRadiusMeters = null,
    PaymentMethod PaymentMethod = PaymentMethod.BankTransfer, bool ProvidentFundMember = false,
    bool VpsMember = false, [StringLength(150)] string? VpsProvider = null, [StringLength(50)] string? VpsAccountNo = null,
    [Range(0, 50)] decimal VpsEmployeePercent = 0, [Range(0, 50)] decimal VpsEmployerPercent = 0, PayFrequency PayFrequency = PayFrequency.Monthly);

public record DependantDto(
    [Required, StringLength(150)] string Name, [Required, StringLength(50)] string Relation,
    [RegularExpression(@"^\d{5}-\d{7}-\d$", ErrorMessage = "CNIC must look like 12345-1234567-1.")] string? Cnic,
    DateOnly? DateOfBirth, Gender? Gender, bool IsNominee, [Range(0, 100)] decimal? NomineeShare, bool CoveredByInsurance);

public record QualificationDto(
    [Required, StringLength(150)] string Degree, [Required, StringLength(200)] string Institute, [StringLength(150)] string? Field,
    [StringLength(30)] string? Grade, [Range(1950, 2100)] int? FromYear, [Range(1950, 2100)] int? ToYear, bool IsHighest);

public record WorkExperienceDto(
    [Required, StringLength(200)] string Organization, [Required, StringLength(150)] string Designation, DateOnly? FromDate, DateOnly? ToDate,
    [Range(0, 100_000_000)] decimal? LastSalary, [StringLength(500)] string? ReasonForLeaving);

public record EmployeeRecordsDto(List<DependantDto> Dependants, List<QualificationDto> Qualifications, List<WorkExperienceDto> Experience);

/// <summary>Company lookups (divisions, grades, cost centers…) and the extra parts of an employee's file.</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = Policies.CompanyUser)]
public class EmployeeFileController(HrmsDbContext db, ICurrentUser me) : ControllerBase
{
    // ---------- Lookups ----------

    [HttpGet("lookups")]
    public async Task<List<OrgLookupDto>> Lookups([FromQuery] LookupKind? kind)
    {
        var q = db.OrgLookups.AsNoTracking();
        if (kind is { } k) q = q.Where(x => x.Kind == k);
        var rows = await q.OrderBy(x => x.Kind).ThenBy(x => x.Name).ToListAsync();
        var counts = await CountsAsync();
        return rows.Select(x => new OrgLookupDto(x.Id, x.Kind, x.Name, x.Code, x.ParentId, x.HeadEmployeeId, x.IsActive, counts.GetValueOrDefault(x.Id))).ToList();
    }

    private async Task<Dictionary<Guid, int>> CountsAsync()
    {
        var people = await db.Employees.AsNoTracking()
            .Select(e => new { e.DivisionId, e.SubDepartmentId, e.GradeId, e.CostCenterId, e.RegionId, e.EmployeeGroupId, e.ExitTypeId })
            .ToListAsync();
        return people.SelectMany(p => new[] { p.DivisionId, p.SubDepartmentId, p.GradeId, p.CostCenterId, p.RegionId, p.EmployeeGroupId, p.ExitTypeId })
            .Where(x => x is not null).GroupBy(x => x!.Value).ToDictionary(g => g.Key, g => g.Count());
    }

    [HttpPost("lookups"), Authorize(Policy = Policies.Hr)]
    public async Task<OrgLookupDto> AddLookup(SaveOrgLookupRequest req)
    {
        var x = new OrgLookup { Kind = req.Kind };
        await ApplyAsync(x, req);
        db.OrgLookups.Add(x);
        await db.SaveChangesAsync();
        return new OrgLookupDto(x.Id, x.Kind, x.Name, x.Code, x.ParentId, x.HeadEmployeeId, x.IsActive, 0);
    }

    [HttpPut("lookups/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateLookup(Guid id, SaveOrgLookupRequest req)
    {
        var x = await db.OrgLookups.FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Item");
        await ApplyAsync(x, req with { Kind = x.Kind });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("lookups/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteLookup(Guid id)
    {
        var x = await db.OrgLookups.FirstOrDefaultAsync(l => l.Id == id) ?? throw new NotFoundException("Item");
        if ((await CountsAsync()).GetValueOrDefault(id) > 0)
            throw new DomainException("People are still linked to this. Move them or turn it off instead.");
        db.OrgLookups.Remove(x);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(OrgLookup x, SaveOrgLookupRequest req)
    {
        if (!Enum.IsDefined(req.Kind)) throw new DomainException("Choose a list.");
        var name = req.Name.Trim();
        if (name.Length == 0) throw new DomainException("Enter a name.");
        Guid? parent = req.Kind == LookupKind.SubDepartment ? req.ParentId : null;
        if (req.Kind == LookupKind.SubDepartment && (parent is not { } d || !await db.Departments.AnyAsync(p => p.Id == d)))
            throw new DomainException("Choose the department this sub-department belongs to.");
        if (await db.OrgLookups.AnyAsync(l => l.Id != x.Id && l.Kind == req.Kind && l.ParentId == parent && l.Name == name))
            throw new DomainException($"{name} is already on the list.");
        if (req.HeadEmployeeId is { } h && !await db.Employees.AnyAsync(e => e.Id == h))
            throw new NotFoundException("Employee");
        x.Name = name;
        x.Code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim();
        x.ParentId = parent;
        x.HeadEmployeeId = req.HeadEmployeeId;
        x.IsActive = req.IsActive;
    }

    // ---------- Employee file ----------

    [HttpGet("employees/me/file")]
    public Task<EmployeeFileDto> MyFile() => File(MyId());

    [HttpGet("employees/{id:guid}/file"), Authorize(Policy = Policies.Approver)]
    public async Task<EmployeeFileDto> EmployeeFile(Guid id)
    {
        await EnsureCanViewAsync(id);
        return await File(id);
    }

    private async Task<EmployeeFileDto> File(Guid id)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Employee");
        return new EmployeeFileDto(e.FatherName, e.PlaceOfBirth, e.Nationality, e.Religion, e.CnicIssuedOn, e.CnicExpiresOn,
            e.PassportNo, e.PassportIssuedOn, e.PassportExpiresOn, e.VisaNo, e.VisaType, e.VisaExpiresOn, e.DrivingLicenseNo, e.DrivingLicenseExpiresOn,
            e.Ntn, e.EobiNo, e.SocialSecurityNo, e.InsuranceProvider, e.InsurancePolicyNo, e.InsuranceExpiresOn, e.BankBranchCode, e.BankAccountTitle,
            e.DivisionId, e.SubDepartmentId, e.GradeId, e.CostCenterId, e.RegionId, e.EmployeeGroupId, e.ConfirmationDate, e.ContractEndDate,
            e.PunchCode, e.ExitTypeId, e.ExitReason, e.IsBlacklisted, e.BlacklistReason,
            e.AllowWebCheckIn, e.AllowMobileCheckIn, e.CheckInAnywhere, e.AllowedIps, e.OwnLatitude, e.OwnLongitude, e.OwnRadiusMeters,
            e.PaymentMethod, e.ProvidentFundMember, e.VpsMember, e.VpsProvider, e.VpsAccountNo, e.VpsEmployeePercent, e.VpsEmployerPercent, e.PayFrequency);
    }

    [HttpPut("employees/{id:guid}/file"), Authorize(Policy = Policies.Hr)]
    public async Task<EmployeeFileDto> SaveFile(Guid id, EmployeeFileDto f)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Employee");
        await CheckLookupAsync(f.DivisionId, LookupKind.Division);
        await CheckLookupAsync(f.SubDepartmentId, LookupKind.SubDepartment);
        await CheckLookupAsync(f.GradeId, LookupKind.Grade);
        await CheckLookupAsync(f.CostCenterId, LookupKind.CostCenter);
        await CheckLookupAsync(f.RegionId, LookupKind.Region);
        await CheckLookupAsync(f.EmployeeGroupId, LookupKind.EmployeeGroup);
        await CheckLookupAsync(f.ExitTypeId, LookupKind.ExitType);
        if (f.SubDepartmentId is { } sd && e.DepartmentId is { } dep && !await db.OrgLookups.AnyAsync(l => l.Id == sd && l.ParentId == dep))
            throw new DomainException("That sub-department belongs to another department.");
        Order(f.CnicIssuedOn, f.CnicExpiresOn, "CNIC");
        Order(f.PassportIssuedOn, f.PassportExpiresOn, "passport");
        if (f.ContractEndDate is { } ce && ce < e.JoiningDate) throw new DomainException("The contract cannot end before the joining date.");
        if (f.ConfirmationDate is { } cd && cd < e.JoiningDate) throw new DomainException("Confirmation cannot be before the joining date.");
        if (f.PunchCode is { Length: > 0 } pc && await db.Employees.AnyAsync(x => x.Id != id && x.PunchCode == pc.Trim()))
            throw new DomainException($"Punch code {pc.Trim()} is already used by someone else.");

        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        e.FatherName = T(f.FatherName); e.PlaceOfBirth = T(f.PlaceOfBirth); e.Nationality = T(f.Nationality); e.Religion = T(f.Religion);
        e.CnicIssuedOn = f.CnicIssuedOn; e.CnicExpiresOn = f.CnicExpiresOn;
        e.PassportNo = T(f.PassportNo); e.PassportIssuedOn = f.PassportIssuedOn; e.PassportExpiresOn = f.PassportExpiresOn;
        e.VisaNo = T(f.VisaNo); e.VisaType = T(f.VisaType); e.VisaExpiresOn = f.VisaExpiresOn;
        e.DrivingLicenseNo = T(f.DrivingLicenseNo); e.DrivingLicenseExpiresOn = f.DrivingLicenseExpiresOn;
        e.Ntn = T(f.Ntn); e.EobiNo = T(f.EobiNo); e.SocialSecurityNo = T(f.SocialSecurityNo);
        e.InsuranceProvider = T(f.InsuranceProvider); e.InsurancePolicyNo = T(f.InsurancePolicyNo); e.InsuranceExpiresOn = f.InsuranceExpiresOn;
        e.BankBranchCode = T(f.BankBranchCode); e.BankAccountTitle = T(f.BankAccountTitle);
        e.DivisionId = f.DivisionId; e.SubDepartmentId = f.SubDepartmentId; e.GradeId = f.GradeId; e.CostCenterId = f.CostCenterId;
        e.RegionId = f.RegionId; e.EmployeeGroupId = f.EmployeeGroupId;
        e.ConfirmationDate = f.ConfirmationDate; e.ContractEndDate = f.ContractEndDate; e.PunchCode = T(f.PunchCode);
        e.ExitTypeId = f.ExitTypeId; e.ExitReason = T(f.ExitReason);
        e.IsBlacklisted = f.IsBlacklisted; e.BlacklistReason = f.IsBlacklisted ? T(f.BlacklistReason) : null;
        if ((f.OwnLatitude is null) != (f.OwnLongitude is null))
            throw new DomainException("Enter both latitude and longitude for the personal location.");
        e.AllowWebCheckIn = f.AllowWebCheckIn; e.AllowMobileCheckIn = f.AllowMobileCheckIn; e.CheckInAnywhere = f.CheckInAnywhere;
        e.AllowedIps = T(f.AllowedIps); e.OwnLatitude = f.OwnLatitude; e.OwnLongitude = f.OwnLongitude;
        e.OwnRadiusMeters = f.OwnLatitude is null ? null : f.OwnRadiusMeters ?? 200;
        e.PaymentMethod = Enum.IsDefined(f.PaymentMethod) ? f.PaymentMethod : PaymentMethod.BankTransfer;
        e.PayFrequency = Enum.IsDefined(f.PayFrequency) ? f.PayFrequency : PayFrequency.Monthly;
        e.ProvidentFundMember = f.ProvidentFundMember;
        if (f.VpsMember && f.VpsEmployeePercent + f.VpsEmployerPercent <= 0)
            throw new DomainException("Give the pension contribution as a percent of basic salary.");
        e.VpsMember = f.VpsMember; e.VpsProvider = T(f.VpsProvider); e.VpsAccountNo = T(f.VpsAccountNo);
        e.VpsEmployeePercent = f.VpsEmployeePercent; e.VpsEmployerPercent = f.VpsEmployerPercent;
        await db.SaveChangesAsync();
        return await File(id);
    }

    private static void Order(DateOnly? from, DateOnly? to, string what)
    {
        if (from is { } a && to is { } b && b < a) throw new DomainException($"The {what} expiry must be after its issue date.");
    }

    private async Task CheckLookupAsync(Guid? id, LookupKind kind)
    {
        if (id is { } x && !await db.OrgLookups.AnyAsync(l => l.Id == x && l.Kind == kind))
            throw new DomainException($"Choose a {Label(kind)} from the list.");
    }

    public static string Label(LookupKind k) => k switch
    {
        LookupKind.CostCenter => "cost center",
        LookupKind.EmployeeGroup => "employee group",
        LookupKind.SubDepartment => "sub-department",
        LookupKind.ExitType => "exit type",
        _ => k.ToString().ToLowerInvariant(),
    };

    // ---------- Family, education and past jobs ----------

    [HttpGet("employees/me/records")]
    public Task<EmployeeRecordsDto> MyRecords() => Records(MyId());

    [HttpPut("employees/me/records")]
    public Task<EmployeeRecordsDto> SaveMyRecords(EmployeeRecordsDto req) => SaveRecords(MyId(), req);

    [HttpGet("employees/{id:guid}/records"), Authorize(Policy = Policies.Approver)]
    public async Task<EmployeeRecordsDto> EmployeeRecords(Guid id)
    {
        await EnsureCanViewAsync(id);
        return await Records(id);
    }

    [HttpPut("employees/{id:guid}/records"), Authorize(Policy = Policies.Hr)]
    public async Task<EmployeeRecordsDto> SaveEmployeeRecords(Guid id, EmployeeRecordsDto req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == id)) throw new NotFoundException("Employee");
        return await SaveRecords(id, req);
    }

    private async Task<EmployeeRecordsDto> Records(Guid id) => new(
        await db.Dependants.Where(x => x.EmployeeId == id).OrderBy(x => x.Order)
            .Select(x => new DependantDto(x.Name, x.Relation, x.Cnic, x.DateOfBirth, x.Gender, x.IsNominee, x.NomineeShare, x.CoveredByInsurance)).ToListAsync(),
        await db.Qualifications.Where(x => x.EmployeeId == id).OrderBy(x => x.Order)
            .Select(x => new QualificationDto(x.Degree, x.Institute, x.Field, x.Grade, x.FromYear, x.ToYear, x.IsHighest)).ToListAsync(),
        await db.WorkExperiences.Where(x => x.EmployeeId == id).OrderBy(x => x.Order)
            .Select(x => new WorkExperienceDto(x.Organization, x.Designation, x.FromDate, x.ToDate, x.LastSalary, x.ReasonForLeaving)).ToListAsync());

    /// <summary>Replaces all three lists.</summary>
    private async Task<EmployeeRecordsDto> SaveRecords(Guid id, EmployeeRecordsDto req)
    {
        var deps = req.Dependants ?? [];
        var quals = req.Qualifications ?? [];
        var jobs = req.Experience ?? [];
        if (deps.Count > 20 || quals.Count > 20 || jobs.Count > 30) throw new DomainException("That is too many entries.");
        if (deps.Any(d => string.IsNullOrWhiteSpace(d.Name) || string.IsNullOrWhiteSpace(d.Relation)))
            throw new DomainException("Every family member needs a name and relation.");
        var shares = deps.Where(d => d.IsNominee).Sum(d => d.NomineeShare ?? 0);
        if (shares > 100) throw new DomainException("Nominee shares add up to more than 100%.");
        if (quals.Any(q => string.IsNullOrWhiteSpace(q.Degree) || string.IsNullOrWhiteSpace(q.Institute)))
            throw new DomainException("Every qualification needs a degree and institute.");
        if (quals.Any(q => q.FromYear is { } a && q.ToYear is { } b && b < a))
            throw new DomainException("A qualification ends before it starts.");
        if (jobs.Any(j => string.IsNullOrWhiteSpace(j.Organization) || string.IsNullOrWhiteSpace(j.Designation)))
            throw new DomainException("Every past job needs an organization and designation.");
        if (jobs.Any(j => j.FromDate is { } a && j.ToDate is { } b && b < a))
            throw new DomainException("A past job ends before it starts.");

        db.Dependants.RemoveRange(await db.Dependants.Where(x => x.EmployeeId == id).ToListAsync());
        db.Qualifications.RemoveRange(await db.Qualifications.Where(x => x.EmployeeId == id).ToListAsync());
        db.WorkExperiences.RemoveRange(await db.WorkExperiences.Where(x => x.EmployeeId == id).ToListAsync());
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var i = 0;
        foreach (var d in deps)
            db.Dependants.Add(new Dependant
            {
                EmployeeId = id, Name = d.Name.Trim(), Relation = d.Relation.Trim(), Cnic = T(d.Cnic), DateOfBirth = d.DateOfBirth, Gender = d.Gender,
                IsNominee = d.IsNominee, NomineeShare = d.IsNominee ? d.NomineeShare : null, CoveredByInsurance = d.CoveredByInsurance, Order = i++,
            });
        i = 0;
        foreach (var q in quals)
            db.Qualifications.Add(new Qualification
            {
                EmployeeId = id, Degree = q.Degree.Trim(), Institute = q.Institute.Trim(), Field = T(q.Field), Grade = T(q.Grade),
                FromYear = q.FromYear, ToYear = q.ToYear, IsHighest = q.IsHighest, Order = i++,
            });
        i = 0;
        foreach (var j in jobs)
            db.WorkExperiences.Add(new WorkExperience
            {
                EmployeeId = id, Organization = j.Organization.Trim(), Designation = j.Designation.Trim(), FromDate = j.FromDate, ToDate = j.ToDate,
                LastSalary = j.LastSalary, ReasonForLeaving = T(j.ReasonForLeaving), Order = i++,
            });
        await db.SaveChangesAsync();
        return await Records(id);
    }

    private Guid MyId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");

    private async Task EnsureCanViewAsync(Guid id)
    {
        if (me.Role == UserRole.Manager && id != me.EmployeeId
            && !await db.Employees.AnyAsync(e => e.Id == id && e.ManagerId == me.EmployeeId))
            throw new ForbiddenException("You can only view your own team.");
    }
}
