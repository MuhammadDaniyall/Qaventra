using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record EmployeeListItemDto(
    Guid Id, string EmployeeCode, string FullName, string? Email, string? Phone,
    string? Department, string? Designation, EmploymentStatus Status, DateOnly JoiningDate, bool HasLogin, RecordStampsDto Stamps);

/// <summary>What goes on an employee ID card.</summary>
public record IdCardDto(Guid Id, string EmployeeCode, string Name, string? Designation, string? Department, string? BloodGroup, DateOnly JoiningDate, string? Phone);

/// <summary>One box on the org chart: who the person is and who they report to.</summary>
public record OrgChartNodeDto(Guid Id, string Name, string? Designation, string? Department, Guid? ManagerId);

public record EmployeeDto(
    Guid Id, string EmployeeCode, string FirstName, string LastName, string? Email, string? Phone, string? Cnic,
    DateOnly? DateOfBirth, Gender? Gender, DateOnly JoiningDate, DateOnly? LeavingDate, EmploymentStatus Status,
    string? Address, string? BankName, string? BankAccount, string? PersonalEmail, MaritalStatus? MaritalStatus, string? BloodGroup,
    Guid? DepartmentId, string? Department, Guid? DesignationId, string? Designation,
    Guid? ManagerId, string? Manager, Guid? ShiftId, string? Shift,
    Guid? UserId, UserRole? LoginRole, bool? LoginActive, RecordStampsDto Stamps);

public record SaveEmployeeRequest(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required] DateOnly JoiningDate,
    [StringLength(30)] string? EmployeeCode,
    [EmailAddress] string? Email,
    [StringLength(30)] string? Phone,
    [RegularExpression(@"^\d{5}-\d{7}-\d$", ErrorMessage = "CNIC must look like 12345-1234567-1.")] string? Cnic,
    DateOnly? DateOfBirth,
    Gender? Gender,
    EmploymentStatus Status,
    DateOnly? LeavingDate,
    [StringLength(500)] string? Address,
    [StringLength(100)] string? BankName,
    [StringLength(50)] string? BankAccount,
    Guid? DepartmentId,
    Guid? DesignationId,
    Guid? ManagerId,
    Guid? ShiftId,
    [EmailAddress, StringLength(256)] string? PersonalEmail = null,
    MaritalStatus? MaritalStatus = null,
    [RegularExpression(@"^(A|B|AB|O)[+-]$", ErrorMessage = "Choose a blood group such as B+ or O-.")] string? BloodGroup = null);

public record CreateLoginRequest([Required, EmailAddress] string Email, [Required, StringLength(100, MinimumLength = 8)] string Password, UserRole Role);

public record UpdateLoginRequest(UserRole Role, bool IsActive);

public record SalaryComponentDto([Required, StringLength(100)] string Name, SalaryComponentKind Kind, [Range(0, 100_000_000)] decimal Amount, bool IsTaxable);

/// <summary>Also the save request, so Stamps is optional and ignored on save.</summary>
public record SalaryDto(Guid? Id, DateOnly EffectiveFrom, [Range(0, 100_000_000)] decimal BasicSalary, bool EobiApplicable, List<SalaryComponentDto> Components,
    RecordStampsDto? Stamps = null, PayBasis PayBasis = PayBasis.Monthly, [Range(0, 10_000_000)] decimal? Rate = null);

[ApiController]
[Route("api/employees")]
[Authorize(Policy = Policies.CompanyUser)]
public class EmployeesController(HrmsDbContext db, EmployeeService employees, ICurrentUser me, TenantAccessChecker access) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.Approver)]
    public async Task<PagedResult<EmployeeListItemDto>> List(
        [FromQuery] string? search, [FromQuery] Guid? departmentId, [FromQuery] EmploymentStatus? status,
        [FromQuery] bool former = false, [FromQuery] bool withoutLogin = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] Guid? groupId = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = db.Employees.AsNoTracking();

        // Line managers see only their direct reports.
        if (me.Role == UserRole.Manager)
            q = q.Where(e => e.ManagerId == me.EmployeeId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(e => e.FirstName.Contains(s) || e.LastName.Contains(s) || e.EmployeeCode.Contains(s)
                             || (e.Email != null && e.Email.Contains(s)) || (e.Cnic != null && e.Cnic.Contains(s)));
        }
        if (departmentId is not null) q = q.Where(e => e.DepartmentId == departmentId);
        if (status is not null) q = q.Where(e => e.Status == status);
        if (former) q = q.Where(e => e.Status == EmploymentStatus.Resigned || e.Status == EmploymentStatus.Terminated);
        if (withoutLogin) q = q.Where(e => !db.Users.Any(u => u.EmployeeId == e.Id));
        if (groupId is { } g)
            q = q.Where(e => e.DivisionId == g || e.SubDepartmentId == g || e.GradeId == g || e.CostCenterId == g || e.RegionId == g || e.EmployeeGroupId == g);

        var total = await q.CountAsync();
        var items = await q.OrderBy(e => e.EmployeeCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new EmployeeListItemDto(
                e.Id, e.EmployeeCode, e.FirstName + " " + e.LastName, e.Email, e.Phone,
                e.Department != null ? e.Department.Name : null,
                e.Designation != null ? e.Designation.Title : null,
                e.Status, e.JoiningDate,
                db.Users.Any(u => u.EmployeeId == e.Id),
                new RecordStampsDto(e.CreatedAtUtc, e.CreatedBy, e.ModifiedAtUtc, e.ModifiedBy)))
            .ToListAsync();
        return new PagedResult<EmployeeListItemDto>(items, total, page, pageSize);
    }

    /// <summary>All current employees as id/name pairs, for dropdowns (manager pickers etc.).</summary>
    [HttpGet("lookup"), Authorize(Policy = Policies.Approver)]
    public Task<List<LookupDto>> Lookup([FromQuery] bool includeFormer = false) =>
        db.Employees.Where(e => includeFormer || e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .OrderBy(e => e.FirstName)
            .Select(e => new LookupDto(e.Id, e.EmployeeCode + " · " + e.FirstName + " " + e.LastName
                + (e.Status == EmploymentStatus.Resigned || e.Status == EmploymentStatus.Terminated ? " (left)" : "")))
            .ToListAsync();

    /// <summary>Current employees' ID card details, optionally for one department.</summary>
    [HttpGet("id-cards"), Authorize(Policy = Policies.Hr)]
    public Task<List<IdCardDto>> IdCards([FromQuery] Guid? departmentId) =>
        db.Employees.AsNoTracking()
            .Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .Where(e => departmentId == null || e.DepartmentId == departmentId)
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new IdCardDto(e.Id, e.EmployeeCode, e.FirstName + " " + e.LastName,
                e.Designation != null ? e.Designation.Title : null, e.Department != null ? e.Department.Name : null,
                e.BloodGroup, e.JoiningDate, e.Phone))
            .ToListAsync();

    /// <summary>The company's ID card design (JSON the page understands); empty until HR saves one.</summary>
    [HttpGet("id-card-template"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> IdCardTemplate()
    {
        var json = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.IdCardTemplateJson).FirstOrDefaultAsync();
        return Content(string.IsNullOrWhiteSpace(json) ? "{}" : json, "application/json");
    }

    [HttpPut("id-card-template"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveIdCardTemplate([FromBody] System.Text.Json.JsonElement template)
    {
        if (template.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new DomainException("The design must be an object.");
        var json = template.GetRawText();
        if (json.Length > 4000)
            throw new DomainException("The design is too large.");
        var tenant = await db.Tenants.FirstAsync(t => t.Id == db.CurrentTenantId);
        tenant.IdCardTemplateJson = json;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Current employees with their manager, for the org chart. Everyone in the company may see it.</summary>
    [HttpGet("org-chart")]
    public Task<List<OrgChartNodeDto>> OrgChart() =>
        db.Employees.AsNoTracking()
            .Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new OrgChartNodeDto(
                e.Id, e.FirstName + " " + e.LastName,
                e.Designation != null ? e.Designation.Title : null,
                e.Department != null ? e.Department.Name : null,
                e.ManagerId))
            .ToListAsync();

    [HttpGet("me")]
    public async Task<EmployeeDto> MyProfile()
    {
        var id = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        return await Load(id);
    }

    [HttpGet("{id:guid}"), Authorize(Policy = Policies.Approver)]
    public async Task<EmployeeDto> Get(Guid id)
    {
        await EnsureCanView(id);
        var dto = await Load(id);
        // Managers see their team's work details, not their personal or bank details.
        if (!me.IsHr() && id != me.EmployeeId)
            dto = dto with { Cnic = null, BankName = null, BankAccount = null, Address = null, PersonalEmail = null, DateOfBirth = null };
        return dto;
    }

    [HttpPost, Authorize(Policy = Policies.Hr)]
    public async Task<EmployeeDto> Create(SaveEmployeeRequest req)
    {
        await ValidateReferences(req, null);
        var employee = await employees.CreateAsync(new NewEmployee(
            req.FirstName, req.LastName, req.JoiningDate, req.EmployeeCode, req.Email, req.Phone, req.Cnic,
            req.DateOfBirth, req.Gender, req.Status, req.Address, req.BankName, req.BankAccount,
            req.DepartmentId, req.DesignationId, req.ManagerId, req.ShiftId,
            req.PersonalEmail, req.MaritalStatus, req.BloodGroup));
        return await Load(employee.Id);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<EmployeeDto> Update(Guid id, SaveEmployeeRequest req)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Employee");
        await ValidateReferences(req, id);
        if (!e.IsCurrent && req.Status is EmploymentStatus.Probation or EmploymentStatus.Active)
            throw new DomainException("Use Re-hire to bring this person back.");

        if (!string.IsNullOrWhiteSpace(req.EmployeeCode) && req.EmployeeCode.Trim() != e.EmployeeCode)
        {
            if (await db.Employees.AnyAsync(x => x.EmployeeCode == req.EmployeeCode.Trim()))
                throw new DomainException($"Employee code {req.EmployeeCode} is already in use.");
            e.EmployeeCode = req.EmployeeCode.Trim();
        }
        var newEmail = req.Email?.Trim().ToLowerInvariant();
        var login = await db.Users.FirstOrDefaultAsync(u => u.EmployeeId == id);
        var emailChanged = newEmail is not null && newEmail != e.Email && newEmail != login?.Email;
        if (login is not null && emailChanged && await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == newEmail))
            throw new DomainException("This email is already registered to another login.");
        e.FirstName = req.FirstName.Trim();
        e.LastName = req.LastName.Trim();
        e.Email = newEmail;
        e.Phone = req.Phone;
        e.Cnic = req.Cnic;
        e.DateOfBirth = req.DateOfBirth;
        e.Gender = req.Gender;
        e.JoiningDate = req.JoiningDate;
        e.LeavingDate = req.LeavingDate;
        var confirmed = e.Status == EmploymentStatus.Probation && req.Status == EmploymentStatus.Active;
        e.Status = req.Status;
        e.Address = req.Address;
        e.BankName = req.BankName;
        e.BankAccount = req.BankAccount;
        e.PersonalEmail = req.PersonalEmail?.Trim().ToLowerInvariant();
        e.MaritalStatus = req.MaritalStatus;
        e.BloodGroup = req.BloodGroup;
        var (oldDepartment, oldDesignation, oldManager) = (e.DepartmentId, e.DesignationId, e.ManagerId);
        e.DepartmentId = req.DepartmentId;
        e.DesignationId = req.DesignationId;
        e.ManagerId = req.ManagerId;
        e.ShiftId = req.ShiftId ?? e.ShiftId;
        if (e.DepartmentId != oldDepartment)
            await employees.ClearStaleSubDepartmentAsync(e);
        await employees.TrackProfileEditAsync(e, oldDepartment, oldDesignation, oldManager);

        if (login is not null)
        {
            // The login follows the profile's name and work email.
            login.FullName = e.FullName;
            if (emailChanged)
                login.Email = newEmail!;
            // A person who has left can no longer sign in.
            if (!e.IsCurrent)
                login.IsActive = false;
            access.InvalidateUser(login.Id);
        }
        await db.SaveChangesAsync();
        if (confirmed)
            await HttpContext.RequestServices.GetRequiredService<LetterService>().AutoIssueAsync(id, LetterAutoUse.Confirmation);
        return await Load(id);
    }

    [HttpPost("{id:guid}/login"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> CreateLogin(Guid id, CreateLoginRequest req)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Employee");
        GuardRoleAssignment(req.Role);
        await employees.CreateLoginAsync(employee, req.Email, req.Password, req.Role);
        return NoContent();
    }

    [HttpPut("{id:guid}/login"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateLogin(Guid id, UpdateLoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmployeeId == id) ?? throw new NotFoundException("Login");
        if (user.Id == me.UserId)
            throw new DomainException("You cannot change your own role or disable your own login.");
        GuardRoleAssignment(req.Role);
        if (user.Role == UserRole.CompanyAdmin && !me.IsCompanyAdmin())
            throw new ForbiddenException("Only a company admin can change another admin's login.");
        if (req.IsActive && !user.IsActive && !await db.Employees.AnyAsync(e => e.Id == id && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new DomainException("This employee has left. Re-hire them before turning their login on.");
        // A new role ends the sessions signed in with the old one.
        if (user.Role != req.Role)
            user.SecurityStamp = Guid.NewGuid();
        user.Role = req.Role;
        user.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        access.InvalidateUser(user.Id);
        return NoContent();
    }

    [HttpGet("{id:guid}/salary"), Authorize(Policy = Policies.Hr)]
    public async Task<ActionResult<SalaryDto>> GetSalary(Guid id)
    {
        var s = await db.EmployeeSalaries.Include(x => x.Components)
            .Where(x => x.EmployeeId == id).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync();
        if (s is null)
            return NoContent();
        return new SalaryDto(s.Id, s.EffectiveFrom, s.BasicSalary, s.EobiApplicable,
            s.Components.Select(c => new SalaryComponentDto(c.Name, c.Kind, c.Amount, c.IsTaxable)).ToList(), RecordStampsDto.Of(s), s.PayBasis, s.Rate);
    }

    /// <summary>Saves the salary effective from the given date. Same date replaces it; a new date keeps history.</summary>
    [HttpPut("{id:guid}/salary"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveSalary(Guid id, SalaryDto req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == id))
            throw new NotFoundException("Employee");
        var components = req.Components ?? [];
        if (components.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name)))
            throw new DomainException("Every salary component needs a name.");
        if (!Enum.IsDefined(req.PayBasis))
            throw new DomainException("Choose monthly, daily or hourly pay.");
        if (req.PayBasis != PayBasis.Monthly && req.Rate is not > 0)
            throw new DomainException($"Enter the pay per {(req.PayBasis == PayBasis.Daily ? "day" : "hour")}.");

        var salary = await db.EmployeeSalaries.Include(x => x.Components)
            .FirstOrDefaultAsync(x => x.EmployeeId == id && x.EffectiveFrom == req.EffectiveFrom);
        if (salary is null)
        {
            salary = new EmployeeSalary { EmployeeId = id, EffectiveFrom = req.EffectiveFrom };
            db.EmployeeSalaries.Add(salary);
        }
        else
        {
            salary.Components.Clear();
        }

        salary.PayBasis = req.PayBasis;
        salary.Rate = req.PayBasis == PayBasis.Monthly ? null : req.Rate;
        // A daily or hourly rate is earned from attendance; the basic on file is what a full month at 26 days or 208 hours would be.
        salary.BasicSalary = req.PayBasis switch
        {
            PayBasis.Daily => Math.Round(req.Rate!.Value * 26, 0),
            PayBasis.Hourly => Math.Round(req.Rate!.Value * 208, 0),
            _ => req.BasicSalary,
        };
        salary.EobiApplicable = req.EobiApplicable;
        salary.Components.AddRange(components.Select(c => new SalaryComponent
        {
            Name = c.Name.Trim(), Kind = c.Kind, Amount = c.Amount, IsTaxable = c.IsTaxable,
        }));
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Logins for many employees at once, each with a temporary password (emailed when email is set up).</summary>
    [HttpPost("logins"), Authorize(Policy = Policies.Hr)]
    public Task<IssueLoginsResult> IssueLogins(IssueLoginsRequest req)
    {
        GuardRoleAssignment(req.Role);
        return employees.IssueLoginsAsync(req.EmployeeIds ?? [], req.Role);
    }

    [HttpPost("{id:guid}/login/reset-password"), Authorize(Policy = Policies.Hr)]
    public Task<IssuedLogin> ResetPassword(Guid id) => employees.ResetPasswordAsync(id);

    // ---------- Emergency contacts ----------

    [HttpGet("me/emergency-contacts")]
    public Task<List<EmergencyContactDto>> MyEmergencyContacts() => Contacts(MyId());

    [HttpPut("me/emergency-contacts")]
    public Task<List<EmergencyContactDto>> SaveMyEmergencyContacts(SaveEmergencyContactsRequest req) => SaveContacts(MyId(), req);

    [HttpGet("{id:guid}/emergency-contacts"), Authorize(Policy = Policies.Approver)]
    public async Task<List<EmergencyContactDto>> EmergencyContacts(Guid id)
    {
        await EnsureCanView(id);
        return await Contacts(id);
    }

    [HttpPut("{id:guid}/emergency-contacts"), Authorize(Policy = Policies.Hr)]
    public async Task<List<EmergencyContactDto>> SaveEmergencyContacts(Guid id, SaveEmergencyContactsRequest req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == id))
            throw new NotFoundException("Employee");
        return await SaveContacts(id, req);
    }

    private Task<List<EmergencyContactDto>> Contacts(Guid employeeId) =>
        db.EmergencyContacts.Where(c => c.EmployeeId == employeeId).OrderBy(c => c.Order)
            .Select(c => new EmergencyContactDto(c.Name, c.Relation, c.Phone, c.AltPhone)).ToListAsync();

    /// <summary>Replaces the whole list; the first contact is the one to call first.</summary>
    private async Task<List<EmergencyContactDto>> SaveContacts(Guid employeeId, SaveEmergencyContactsRequest req)
    {
        var list = req.Contacts ?? [];
        if (list.Count > 5)
            throw new DomainException("Keep up to 5 emergency contacts.");
        if (list.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Relation) || string.IsNullOrWhiteSpace(c.Phone)))
            throw new DomainException("Every contact needs a name, relation and phone number.");
        db.EmergencyContacts.RemoveRange(await db.EmergencyContacts.Where(c => c.EmployeeId == employeeId).ToListAsync());
        var order = 0;
        foreach (var c in list)
            db.EmergencyContacts.Add(new EmergencyContact
            {
                EmployeeId = employeeId, Order = ++order, Name = c.Name.Trim(), Relation = c.Relation.Trim(), Phone = c.Phone.Trim(),
                AltPhone = string.IsNullOrWhiteSpace(c.AltPhone) ? null : c.AltPhone.Trim(),
            });
        await db.SaveChangesAsync();
        return await Contacts(employeeId);
    }

    // ---------- Job history ----------

    [HttpGet("me/job-history")]
    public Task<List<JobChangeDto>> MyJobHistory() => JobHistory(MyId(), includeRecorder: false);

    [HttpGet("{id:guid}/job-history"), Authorize(Policy = Policies.Approver)]
    public async Task<List<JobChangeDto>> JobHistory(Guid id)
    {
        await EnsureCanView(id);
        return await JobHistory(id, includeRecorder: true);
    }

    [HttpPost("{id:guid}/job-changes"), Authorize(Policy = Policies.Hr)]
    public async Task<EmployeeDto> RecordJobChange(Guid id, RecordJobChangeRequest req)
    {
        await employees.RecordJobChangeAsync(id, new JobChangeInput(req.Kind, req.EffectiveDate, req.DepartmentId, req.DesignationId, req.ManagerId, req.Note,
            req.GradeId, req.DivisionId, req.CostCenterId));
        return await Load(id);
    }

    [HttpPost("{id:guid}/rehire"), Authorize(Policy = Policies.Hr)]
    public async Task<EmployeeDto> Rehire(Guid id, RehireRequest req)
    {
        await employees.RehireAsync(id, new RehireInput(req.JoiningDate, req.Status, req.DepartmentId, req.DesignationId, req.ManagerId, req.ShiftId, req.Note));
        return await Load(id);
    }

    private Task<List<JobChangeDto>> JobHistory(Guid employeeId, bool includeRecorder) =>
        db.JobChanges.Where(c => c.EmployeeId == employeeId).OrderByDescending(c => c.EffectiveDate).ThenByDescending(c => c.CreatedAtUtc)
            .Select(c => new JobChangeDto(c.Id, c.Kind, c.EffectiveDate, c.FromDepartment, c.ToDepartment, c.FromDesignation, c.ToDesignation,
                c.FromManager, c.ToManager, c.Note, includeRecorder ? c.RecordedByName : null, c.CreatedAtUtc,
                // The employee's own view hides who recorded the change, so it gets no stamps either.
                includeRecorder ? new RecordStampsDto(c.CreatedAtUtc, c.CreatedBy, c.ModifiedAtUtc, c.ModifiedBy) : null,
                c.FromGrade, c.ToGrade, c.FromDivision, c.ToDivision, c.FromCostCenter, c.ToCostCenter))
            .ToListAsync();

    private Guid MyId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");

    private void GuardRoleAssignment(UserRole role)
    {
        if (role == UserRole.SuperAdmin)
            throw new ForbiddenException("SuperAdmin cannot be assigned from a company.");
        if (role == UserRole.CompanyAdmin && !me.IsCompanyAdmin())
            throw new ForbiddenException("Only a company admin can create another admin.");
    }

    private async Task EnsureCanView(Guid id)
    {
        if (me.Role == UserRole.Manager && id != me.EmployeeId
            && !await db.Employees.AnyAsync(e => e.Id == id && e.ManagerId == me.EmployeeId))
            throw new ForbiddenException("You can only view your own team.");
    }

    private async Task ValidateReferences(SaveEmployeeRequest req, Guid? selfId)
    {
        if (req.DepartmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d)) throw new NotFoundException("Department");
        if (req.DesignationId is { } g && !await db.Designations.AnyAsync(x => x.Id == g)) throw new NotFoundException("Designation");
        if (req.ShiftId is { } s && !await db.Shifts.AnyAsync(x => x.Id == s)) throw new NotFoundException("Shift");
        if (req.ManagerId is { } m)
        {
            if (m == selfId) throw new DomainException("An employee cannot be their own manager.");
            if (!await db.Employees.AnyAsync(x => x.Id == m)) throw new NotFoundException("Manager");
            if (selfId is { } self) await employees.EnsureNoReportingLoopAsync(self, m);
        }
        if (req.LeavingDate is { } left && left < req.JoiningDate)
            throw new DomainException("Leaving date cannot be before the joining date.");
        if (req.Status is EmploymentStatus.Resigned or EmploymentStatus.Terminated && req.LeavingDate is null)
            throw new DomainException("Set a leaving date for a resigned or terminated employee.");
    }

    private async Task<EmployeeDto> Load(Guid id)
    {
        return await db.Employees.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new EmployeeDto(
                e.Id, e.EmployeeCode, e.FirstName, e.LastName, e.Email, e.Phone, e.Cnic, e.DateOfBirth, e.Gender,
                e.JoiningDate, e.LeavingDate, e.Status, e.Address, e.BankName, e.BankAccount, e.PersonalEmail, e.MaritalStatus, e.BloodGroup,
                e.DepartmentId, e.Department != null ? e.Department.Name : null,
                e.DesignationId, e.Designation != null ? e.Designation.Title : null,
                e.ManagerId, e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null,
                e.ShiftId, e.Shift != null ? e.Shift.Name : null,
                db.Users.Where(u => u.EmployeeId == e.Id).Select(u => (Guid?)u.Id).FirstOrDefault(),
                db.Users.Where(u => u.EmployeeId == e.Id).Select(u => (UserRole?)u.Role).FirstOrDefault(),
                db.Users.Where(u => u.EmployeeId == e.Id).Select(u => (bool?)u.IsActive).FirstOrDefault(),
                new RecordStampsDto(e.CreatedAtUtc, e.CreatedBy, e.ModifiedAtUtc, e.ModifiedBy)))
            .FirstOrDefaultAsync() ?? throw new NotFoundException("Employee");
    }
}

public record LookupDto(Guid Id, string Name);

public record IssueLoginsRequest([Required, MaxLength(500)] List<Guid> EmployeeIds, UserRole Role = UserRole.Employee);

public record EmergencyContactDto(
    [Required, StringLength(150)] string Name, [Required, StringLength(50)] string Relation,
    [Required, StringLength(30)] string Phone, [StringLength(30)] string? AltPhone);

public record SaveEmergencyContactsRequest(List<EmergencyContactDto> Contacts);

public record JobChangeDto(
    Guid Id, JobChangeKind Kind, DateOnly EffectiveDate, string? FromDepartment, string? ToDepartment, string? FromDesignation,
    string? ToDesignation, string? FromManager, string? ToManager, string? Note, string? RecordedByName, DateTime CreatedAtUtc,
    RecordStampsDto? Stamps, string? FromGrade = null, string? ToGrade = null, string? FromDivision = null, string? ToDivision = null,
    string? FromCostCenter = null, string? ToCostCenter = null);

public record RecordJobChangeRequest(
    JobChangeKind Kind, DateOnly EffectiveDate, Guid? DepartmentId, Guid? DesignationId, Guid? ManagerId, [StringLength(1000)] string? Note,
    Guid? GradeId = null, Guid? DivisionId = null, Guid? CostCenterId = null);

public record RehireRequest(
    DateOnly JoiningDate, EmploymentStatus Status, Guid? DepartmentId, Guid? DesignationId, Guid? ManagerId, Guid? ShiftId,
    [StringLength(1000)] string? Note);
