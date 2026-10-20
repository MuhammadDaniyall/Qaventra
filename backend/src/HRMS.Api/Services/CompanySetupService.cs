using System.Security.Cryptography;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record NewCompany(
    string Name,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string? AdminPassword,
    string? Phone,
    string? City,
    string? Address,
    int? MaxEmployees);

public record CreatedCompany(Tenant Company, User Admin, string? GeneratedPassword);

/// <summary>Company creation is done only by the platform owner (SuperAdmin).</summary>
public class CompanySetupService(HrmsDbContext db, IPasswordHasher<User> hasher, EmployeeService employees)
{
    /// <summary>
    /// Creates a company with default HR settings and its Company Super Admin login (also an employee, so self-service works).
    /// When no password is given one is generated and returned once.
    /// </summary>
    public async Task<CreatedCompany> CreateAsync(NewCompany input)
    {
        var email = input.AdminEmail.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email))
            throw new DomainException("This email is already registered.");
        if (await db.Tenants.AnyAsync(t => t.Name == input.Name.Trim()))
            throw new DomainException("A company with this name already exists.");

        var tenant = new Tenant
        {
            Name = input.Name.Trim(),
            Phone = input.Phone,
            City = input.City,
            Address = input.Address,
            MaxEmployees = input.MaxEmployees,
        };
        db.Tenants.Add(tenant);
        db.UseTenant(tenant.Id);

        var department = new Department { Name = "Administration", Code = "ADM" };
        var designation = new Designation { Title = "Chief Executive Officer" };
        var shift = new Shift
        {
            Name = "General (9 to 6)",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0),
            GraceMinutes = 15,
            WeeklyOffDays = [DayOfWeek.Saturday, DayOfWeek.Sunday],
            IsDefault = true,
        };
        db.AddRange(department, designation, shift);

        // Common Pakistani entitlements (Factories Act / Shops & Establishments); companies can change them.
        db.LeaveTypes.AddRange(
            new LeaveType { Name = "Annual Leave", Code = "AL", AnnualQuota = 14 },
            new LeaveType { Name = "Casual Leave", Code = "CL", AnnualQuota = 10 },
            new LeaveType { Name = "Sick Leave", Code = "SL", AnnualQuota = 8 },
            new LeaveType { Name = "Unpaid Leave", Code = "UL", AnnualQuota = 0, IsPaid = false });
        db.PayrollSettings.Add(new PayrollSettings());
        await db.SaveChangesAsync();

        var adminEmployee = await employees.CreateAsync(new NewEmployee(
            FirstName: input.AdminFirstName,
            LastName: input.AdminLastName,
            Email: email,
            JoiningDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Status: EmploymentStatus.Active,
            DepartmentId: department.Id,
            DesignationId: designation.Id,
            ShiftId: shift.Id));

        var generated = string.IsNullOrWhiteSpace(input.AdminPassword) ? GeneratePassword() : null;
        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            FullName = $"{input.AdminFirstName} {input.AdminLastName}".Trim(),
            Role = UserRole.CompanyAdmin,
            EmployeeId = adminEmployee.Id,
        };
        user.PasswordHash = hasher.HashPassword(user, generated ?? input.AdminPassword!);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new CreatedCompany(tenant, user, generated);
    }

    /// <summary>Sets a new password for the company's (first) Company Super Admin; generates one when none is given.</summary>
    public async Task<(User Admin, string? GeneratedPassword)> ResetAdminPasswordAsync(Guid tenantId, string? password)
    {
        var admin = await db.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && u.Role == UserRole.CompanyAdmin)
            .OrderBy(u => u.CreatedAtUtc).FirstOrDefaultAsync()
            ?? throw new DomainException("This company has no Company Super Admin.");
        var generated = string.IsNullOrWhiteSpace(password) ? GeneratePassword() : null;
        admin.PasswordHash = hasher.HashPassword(admin, generated ?? password!);
        admin.IsActive = true;
        await db.SaveChangesAsync();
        return (admin, generated);
    }

    public static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var body = new string(Enumerable.Range(0, 12).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
        return body + "@" + RandomNumberGenerator.GetInt32(10, 100);
    }
}
