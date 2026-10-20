using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record NewEmployee(
    string FirstName,
    string LastName,
    DateOnly JoiningDate,
    string? EmployeeCode = null,
    string? Email = null,
    string? Phone = null,
    string? Cnic = null,
    DateOnly? DateOfBirth = null,
    Gender? Gender = null,
    EmploymentStatus Status = EmploymentStatus.Probation,
    string? Address = null,
    string? BankName = null,
    string? BankAccount = null,
    Guid? DepartmentId = null,
    Guid? DesignationId = null,
    Guid? ManagerId = null,
    Guid? ShiftId = null);

public class EmployeeService(HrmsDbContext db, IPasswordHasher<User> hasher, LeaveService leave)
{
    public async Task<Employee> CreateAsync(NewEmployee input)
    {
        var limit = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.MaxEmployees).FirstOrDefaultAsync();
        if (limit is { } max && await db.Employees.CountAsync(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation) >= max)
            throw new DomainException($"Your plan allows {max} active employees. Contact your provider to raise the limit.");

        var code = string.IsNullOrWhiteSpace(input.EmployeeCode) ? await NextCodeAsync() : input.EmployeeCode.Trim();
        if (await db.Employees.AnyAsync(e => e.EmployeeCode == code))
            throw new DomainException($"Employee code {code} is already in use.");

        var shiftId = input.ShiftId ?? await db.Shifts.Where(s => s.IsDefault).Select(s => (Guid?)s.Id).FirstOrDefaultAsync();

        var employee = new Employee
        {
            EmployeeCode = code,
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            Email = input.Email?.Trim().ToLowerInvariant(),
            Phone = input.Phone,
            Cnic = input.Cnic,
            DateOfBirth = input.DateOfBirth,
            Gender = input.Gender,
            JoiningDate = input.JoiningDate,
            Status = input.Status,
            Address = input.Address,
            BankName = input.BankName,
            BankAccount = input.BankAccount,
            DepartmentId = input.DepartmentId,
            DesignationId = input.DesignationId,
            ManagerId = input.ManagerId,
            ShiftId = shiftId,
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        await leave.EnsureBalancesAsync(employee.Id, input.JoiningDate.Year);
        return employee;
    }

    /// <summary>Gives an employee a login. The email must be unique across all companies.</summary>
    public async Task<User> CreateLoginAsync(Employee employee, string email, string password, UserRole role)
    {
        email = email.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email))
            throw new DomainException("This email is already registered.");
        if (await db.Users.AnyAsync(u => u.EmployeeId == employee.Id))
            throw new DomainException("This employee already has a login.");

        var user = new User { Email = email, FullName = employee.FullName, Role = role, EmployeeId = employee.Id };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<string> NextCodeAsync()
    {
        var codes = await db.Employees.Where(e => e.EmployeeCode.StartsWith("EMP-")).Select(e => e.EmployeeCode).ToListAsync();
        var max = codes.Select(c => int.TryParse(c[4..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"EMP-{max + 1:D4}";
    }
}
