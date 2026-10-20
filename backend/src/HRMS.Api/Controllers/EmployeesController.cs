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
    string? Department, string? Designation, EmploymentStatus Status, DateOnly JoiningDate, bool HasLogin);

public record EmployeeDto(
    Guid Id, string EmployeeCode, string FirstName, string LastName, string? Email, string? Phone, string? Cnic,
    DateOnly? DateOfBirth, Gender? Gender, DateOnly JoiningDate, DateOnly? LeavingDate, EmploymentStatus Status,
    string? Address, string? BankName, string? BankAccount,
    Guid? DepartmentId, string? Department, Guid? DesignationId, string? Designation,
    Guid? ManagerId, string? Manager, Guid? ShiftId, string? Shift,
    Guid? UserId, UserRole? LoginRole, bool? LoginActive);

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
    Guid? ShiftId);

public record CreateLoginRequest([Required, EmailAddress] string Email, [Required, StringLength(100, MinimumLength = 8)] string Password, UserRole Role);

public record UpdateLoginRequest(UserRole Role, bool IsActive);

public record SalaryComponentDto([Required, StringLength(100)] string Name, SalaryComponentKind Kind, [Range(0, 100_000_000)] decimal Amount, bool IsTaxable);

public record SalaryDto(Guid? Id, DateOnly EffectiveFrom, [Range(0, 100_000_000)] decimal BasicSalary, bool EobiApplicable, List<SalaryComponentDto> Components);

[ApiController]
[Route("api/employees")]
[Authorize(Policy = Policies.CompanyUser)]
public class EmployeesController(HrmsDbContext db, EmployeeService employees, ICurrentUser me, TenantAccessChecker access) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.Approver)]
    public async Task<PagedResult<EmployeeListItemDto>> List(
        [FromQuery] string? search, [FromQuery] Guid? departmentId, [FromQuery] EmploymentStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
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

        var total = await q.CountAsync();
        var items = await q.OrderBy(e => e.EmployeeCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new EmployeeListItemDto(
                e.Id, e.EmployeeCode, e.FirstName + " " + e.LastName, e.Email, e.Phone,
                e.Department != null ? e.Department.Name : null,
                e.Designation != null ? e.Designation.Title : null,
                e.Status, e.JoiningDate,
                db.Users.Any(u => u.EmployeeId == e.Id)))
            .ToListAsync();
        return new PagedResult<EmployeeListItemDto>(items, total, page, pageSize);
    }

    /// <summary>All current employees as id/name pairs, for dropdowns (manager pickers etc.).</summary>
    [HttpGet("lookup"), Authorize(Policy = Policies.Approver)]
    public Task<List<LookupDto>> Lookup() =>
        db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .OrderBy(e => e.FirstName)
            .Select(e => new LookupDto(e.Id, e.EmployeeCode + " · " + e.FirstName + " " + e.LastName))
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
        return await Load(id);
    }

    [HttpPost, Authorize(Policy = Policies.Hr)]
    public async Task<EmployeeDto> Create(SaveEmployeeRequest req)
    {
        await ValidateReferences(req, null);
        var employee = await employees.CreateAsync(new NewEmployee(
            req.FirstName, req.LastName, req.JoiningDate, req.EmployeeCode, req.Email, req.Phone, req.Cnic,
            req.DateOfBirth, req.Gender, req.Status, req.Address, req.BankName, req.BankAccount,
            req.DepartmentId, req.DesignationId, req.ManagerId, req.ShiftId));
        return await Load(employee.Id);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<EmployeeDto> Update(Guid id, SaveEmployeeRequest req)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Employee");
        await ValidateReferences(req, id);

        if (!string.IsNullOrWhiteSpace(req.EmployeeCode) && req.EmployeeCode.Trim() != e.EmployeeCode)
        {
            if (await db.Employees.AnyAsync(x => x.EmployeeCode == req.EmployeeCode.Trim()))
                throw new DomainException($"Employee code {req.EmployeeCode} is already in use.");
            e.EmployeeCode = req.EmployeeCode.Trim();
        }
        e.FirstName = req.FirstName.Trim();
        e.LastName = req.LastName.Trim();
        e.Email = req.Email?.Trim().ToLowerInvariant();
        e.Phone = req.Phone;
        e.Cnic = req.Cnic;
        e.DateOfBirth = req.DateOfBirth;
        e.Gender = req.Gender;
        e.JoiningDate = req.JoiningDate;
        e.LeavingDate = req.LeavingDate;
        e.Status = req.Status;
        e.Address = req.Address;
        e.BankName = req.BankName;
        e.BankAccount = req.BankAccount;
        e.DepartmentId = req.DepartmentId;
        e.DesignationId = req.DesignationId;
        e.ManagerId = req.ManagerId;
        e.ShiftId = req.ShiftId ?? e.ShiftId;

        // A person who has left can no longer sign in.
        if (!e.IsCurrent)
        {
            var login = await db.Users.FirstOrDefaultAsync(u => u.EmployeeId == id);
            if (login is not null)
            {
                login.IsActive = false;
                access.InvalidateUser(login.Id);
            }
        }
        await db.SaveChangesAsync();
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
            s.Components.Select(c => new SalaryComponentDto(c.Name, c.Kind, c.Amount, c.IsTaxable)).ToList());
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

        salary.BasicSalary = req.BasicSalary;
        salary.EobiApplicable = req.EobiApplicable;
        salary.Components.AddRange(components.Select(c => new SalaryComponent
        {
            Name = c.Name.Trim(), Kind = c.Kind, Amount = c.Amount, IsTaxable = c.IsTaxable,
        }));
        await db.SaveChangesAsync();
        return NoContent();
    }

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
        }
        if (req.LeavingDate is { } left && left < req.JoiningDate)
            throw new DomainException("Leaving date cannot be before the joining date.");
    }

    private async Task<EmployeeDto> Load(Guid id)
    {
        return await db.Employees.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new EmployeeDto(
                e.Id, e.EmployeeCode, e.FirstName, e.LastName, e.Email, e.Phone, e.Cnic, e.DateOfBirth, e.Gender,
                e.JoiningDate, e.LeavingDate, e.Status, e.Address, e.BankName, e.BankAccount,
                e.DepartmentId, e.Department != null ? e.Department.Name : null,
                e.DesignationId, e.Designation != null ? e.Designation.Title : null,
                e.ManagerId, e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null,
                e.ShiftId, e.Shift != null ? e.Shift.Name : null,
                db.Users.Where(u => u.EmployeeId == e.Id).Select(u => (Guid?)u.Id).FirstOrDefault(),
                db.Users.Where(u => u.EmployeeId == e.Id).Select(u => (UserRole?)u.Role).FirstOrDefault(),
                db.Users.Where(u => u.EmployeeId == e.Id).Select(u => (bool?)u.IsActive).FirstOrDefault()))
            .FirstOrDefaultAsync() ?? throw new NotFoundException("Employee");
    }
}

public record LookupDto(Guid Id, string Name);
