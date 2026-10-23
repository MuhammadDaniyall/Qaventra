using System.Security.Cryptography;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
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

/// <summary>A transfer, promotion or other job change. Department, designation and manager are the new values.</summary>
public record JobChangeInput(JobChangeKind Kind, DateOnly EffectiveDate, Guid? DepartmentId, Guid? DesignationId, Guid? ManagerId, string? Note);

public record RehireInput(
    DateOnly JoiningDate, EmploymentStatus Status, Guid? DepartmentId, Guid? DesignationId, Guid? ManagerId, Guid? ShiftId, string? Note);

/// <summary>A login handed out by HR. Password is only returned when it could not be emailed.</summary>
public record IssuedLogin(Guid EmployeeId, string Name, string Email, string? Password);

public record SkippedLogin(Guid EmployeeId, string Name, string Reason);

public record IssueLoginsResult(bool Emailed, List<IssuedLogin> Created, List<SkippedLogin> Skipped);

public class EmployeeService(
    HrmsDbContext db, IPasswordHasher<User> hasher, LeaveService leave, ICurrentUser me, TenantClock clock,
    NotificationService notify, SmtpOptions smtp, IServiceProvider services)
{
    public async Task<Employee> CreateAsync(NewEmployee input)
    {
        await EnsurePlanRoomAsync();

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
        // Resolved here, not injected: onboarding needs services that themselves create employees' records.
        await services.GetRequiredService<OnboardingService>().AutoStartAsync(employee.Id);
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
        await services.GetRequiredService<OnboardingService>().WelcomeAsync([employee.Id]);
        return user;
    }

    // ---------- Job history ----------

    /// <summary>Records a transfer, promotion, new title or new manager and applies it to the employee.</summary>
    public async Task<JobChange> RecordJobChangeAsync(Guid employeeId, JobChangeInput input)
    {
        if (input.Kind is JobChangeKind.Rehire or JobChangeKind.ProfileEdit || !Enum.IsDefined(input.Kind))
            throw new DomainException("Choose transfer, promotion, demotion, new title or new manager.");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId) ?? throw new NotFoundException("Employee");
        if (!employee.IsCurrent)
            throw new DomainException("This employee has left. Re-hire them first.");
        var today = await clock.TodayAsync();
        if (input.EffectiveDate > today)
            throw new DomainException("Record the change on or after the day it takes effect.");
        if (input.EffectiveDate < employee.JoiningDate)
            throw new DomainException("The change cannot be before the joining date.");
        await ValidateJobAsync(employeeId, input.DepartmentId, input.DesignationId, input.ManagerId);
        if (input.DepartmentId == employee.DepartmentId && input.DesignationId == employee.DesignationId && input.ManagerId == employee.ManagerId)
            throw new DomainException("Nothing changes. Pick the new department, designation or manager.");

        var change = await StartChangeAsync(employee, input.Kind, input.EffectiveDate, input.Note);
        employee.DepartmentId = input.DepartmentId;
        employee.DesignationId = input.DesignationId;
        employee.ManagerId = input.ManagerId;
        await FinishChangeAsync(change, employee);
        await db.SaveChangesAsync();
        await services.GetRequiredService<LetterService>().AutoIssueAsync(employeeId, LetterAutoUse.JobChange,
            new Dictionary<string, string?> { ["EffectiveDate"] = LetterService.LongDate(input.EffectiveDate) });
        return change;
    }

    /// <summary>
    /// Call before saving a profile edit: when HR changed the department, designation or manager on the form,
    /// the old values are kept in the job history.
    /// </summary>
    public async Task TrackProfileEditAsync(Employee employee, Guid? oldDepartmentId, Guid? oldDesignationId, Guid? oldManagerId)
    {
        if (employee.DepartmentId == oldDepartmentId && employee.DesignationId == oldDesignationId && employee.ManagerId == oldManagerId)
            return;
        var (from, to) = (await JobNamesAsync(oldDepartmentId, oldDesignationId, oldManagerId),
            await JobNamesAsync(employee.DepartmentId, employee.DesignationId, employee.ManagerId));
        db.JobChanges.Add(new JobChange
        {
            EmployeeId = employee.Id,
            Kind = JobChangeKind.ProfileEdit,
            EffectiveDate = await clock.TodayAsync(),
            FromDepartment = from.Department, FromDesignation = from.Designation, FromManager = from.Manager,
            ToDepartment = to.Department, ToDesignation = to.Designation, ToManager = to.Manager,
            Note = "Changed on the profile",
            RecordedByName = await RecorderNameAsync(),
        });
    }

    /// <summary>Brings back someone who left, keeping their record and history. Their login stays off until HR turns it on.</summary>
    public async Task<Employee> RehireAsync(Guid employeeId, RehireInput input)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId) ?? throw new NotFoundException("Employee");
        if (employee.IsCurrent)
            throw new DomainException($"{employee.FullName} still works here.");
        if (employee.IsBlacklisted)
            throw new DomainException($"{employee.FullName} is on the do-not-hire list{(employee.BlacklistReason is { } why ? $" ({why})" : "")}. Remove them from it first.");
        if (input.Status is not (EmploymentStatus.Probation or EmploymentStatus.Active))
            throw new DomainException("A re-hired employee starts on probation or as active.");
        if (employee.LeavingDate is { } left && input.JoiningDate <= left)
            throw new DomainException($"The new joining date must be after they left on {left:d MMM yyyy}.");
        if (input.JoiningDate > (await clock.TodayAsync()).AddDays(90))
            throw new DomainException("The joining date can be at most 90 days ahead.");
        await EnsurePlanRoomAsync();
        await ValidateJobAsync(employeeId, input.DepartmentId, input.DesignationId, input.ManagerId);
        if (input.ShiftId is { } shift && !await db.Shifts.AnyAsync(s => s.Id == shift))
            throw new NotFoundException("Shift");

        var previous = $"Previously worked {employee.JoiningDate:d MMM yyyy} – {employee.LeavingDate?.ToString("d MMM yyyy") ?? "unknown"}.";
        var change = await StartChangeAsync(employee, JobChangeKind.Rehire, input.JoiningDate,
            string.IsNullOrWhiteSpace(input.Note) ? previous : $"{previous} {input.Note.Trim()}");
        employee.JoiningDate = input.JoiningDate;
        employee.LeavingDate = null;
        employee.Status = input.Status;
        employee.DepartmentId = input.DepartmentId;
        employee.DesignationId = input.DesignationId;
        employee.ManagerId = input.ManagerId;
        employee.ShiftId = input.ShiftId ?? employee.ShiftId;
        await FinishChangeAsync(change, employee);
        await db.SaveChangesAsync();
        await leave.EnsureBalancesAsync(employee.Id, input.JoiningDate.Year);
        return employee;
    }

    private record JobNames(string? Department, string? Designation, string? Manager);

    private async Task<JobChange> StartChangeAsync(Employee employee, JobChangeKind kind, DateOnly effective, string? note)
    {
        var from = await JobNamesAsync(employee.DepartmentId, employee.DesignationId, employee.ManagerId);
        var change = new JobChange
        {
            EmployeeId = employee.Id,
            Kind = kind,
            EffectiveDate = effective,
            FromDepartment = from.Department,
            FromDesignation = from.Designation,
            FromManager = from.Manager,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RecordedByName = await RecorderNameAsync(),
        };
        db.JobChanges.Add(change);
        return change;
    }

    private async Task FinishChangeAsync(JobChange change, Employee employee)
    {
        var to = await JobNamesAsync(employee.DepartmentId, employee.DesignationId, employee.ManagerId);
        (change.ToDepartment, change.ToDesignation, change.ToManager) = (to.Department, to.Designation, to.Manager);
    }

    private async Task<JobNames> JobNamesAsync(Guid? departmentId, Guid? designationId, Guid? managerId) => new(
        departmentId is { } d ? await db.Departments.Where(x => x.Id == d).Select(x => x.Name).FirstOrDefaultAsync() : null,
        designationId is { } g ? await db.Designations.Where(x => x.Id == g).Select(x => x.Title).FirstOrDefaultAsync() : null,
        managerId is { } m ? await db.Employees.Where(x => x.Id == m).Select(x => x.FirstName + " " + x.LastName).FirstOrDefaultAsync() : null);

    private async Task<string?> RecorderNameAsync() =>
        me.UserId is { } uid ? await db.Users.IgnoreQueryFilters().Where(u => u.Id == uid).Select(u => u.FullName).FirstOrDefaultAsync() : null;

    private async Task ValidateJobAsync(Guid employeeId, Guid? departmentId, Guid? designationId, Guid? managerId)
    {
        if (departmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d)) throw new NotFoundException("Department");
        if (designationId is { } g && !await db.Designations.AnyAsync(x => x.Id == g)) throw new NotFoundException("Designation");
        if (managerId is { } m)
        {
            if (m == employeeId) throw new DomainException("An employee cannot be their own manager.");
            if (!await db.Employees.AnyAsync(x => x.Id == m && (x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation)))
                throw new DomainException("Choose a manager who currently works here.");
        }
    }

    private async Task EnsurePlanRoomAsync()
    {
        var limit = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.MaxEmployees).FirstOrDefaultAsync();
        if (limit is { } max && await db.Employees.CountAsync(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation) >= max)
            throw new DomainException($"Your plan allows {max} active employees. Contact your provider to raise the limit.");
    }

    // ---------- Logins ----------

    /// <summary>
    /// Gives each chosen employee a login with their work email and a temporary password they must change on first
    /// sign-in. With email set up the details are emailed; otherwise they are returned once for HR to hand out.
    /// </summary>
    public async Task<IssueLoginsResult> IssueLoginsAsync(IReadOnlyCollection<Guid> employeeIds, UserRole role)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0)
            throw new DomainException("Choose at least one employee.");
        if (ids.Count > 500)
            throw new DomainException("Send at most 500 logins at a time.");
        var employees = await db.Employees.Where(e => ids.Contains(e.Id)).OrderBy(e => e.FirstName).ToListAsync();
        var withLogin = (await db.Users.Where(u => u.EmployeeId != null && ids.Contains(u.EmployeeId.Value))
            .Select(u => u.EmployeeId!.Value).ToListAsync()).ToHashSet();
        var emails = employees.Where(e => e.Email != null).Select(e => e.Email!).ToList();
        var taken = (await db.Users.IgnoreQueryFilters().Where(u => emails.Contains(u.Email)).Select(u => u.Email).ToListAsync()).ToHashSet();

        var created = new List<IssuedLogin>();
        var skipped = new List<SkippedLogin>();
        foreach (var e in employees)
        {
            var reason = !e.IsCurrent ? "Has left the company"
                : withLogin.Contains(e.Id) ? "Already has a login"
                : string.IsNullOrWhiteSpace(e.Email) ? "No work email on the profile"
                : taken.Contains(e.Email) ? "Email is already used by another login"
                : null;
            if (reason is not null)
            {
                skipped.Add(new SkippedLogin(e.Id, e.FullName, reason));
                continue;
            }
            var password = GeneratePassword();
            var user = new User { Email = e.Email!, FullName = e.FullName, Role = role, EmployeeId = e.Id, MustChangePassword = true };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            taken.Add(user.Email);
            SendSignInEmail(user, password, "Your Qaventra login is ready");
            created.Add(new IssuedLogin(e.Id, e.FullName, user.Email, smtp.IsConfigured ? null : password));
        }
        await db.SaveChangesAsync();
        await services.GetRequiredService<OnboardingService>().WelcomeAsync(created.Select(c => c.EmployeeId).ToList());
        return new IssueLoginsResult(smtp.IsConfigured, created, skipped);
    }

    /// <summary>Sets a new temporary password for an employee's login; they must change it on their next sign-in.</summary>
    public async Task<IssuedLogin> ResetPasswordAsync(Guid employeeId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmployeeId == employeeId) ?? throw new NotFoundException("Login");
        if (user.Id == me.UserId)
            throw new DomainException("Change your own password from My profile.");
        if (user.Role == UserRole.CompanyAdmin && me.Role != UserRole.CompanyAdmin)
            throw new ForbiddenException("Only a company admin can reset another admin's password.");
        var password = GeneratePassword();
        user.PasswordHash = hasher.HashPassword(user, password);
        user.MustChangePassword = true;
        SendSignInEmail(user, password, "Your Qaventra password was reset");
        await db.SaveChangesAsync();
        return new IssuedLogin(employeeId, user.FullName, user.Email, smtp.IsConfigured ? null : password);
    }

    private void SendSignInEmail(User user, string password, string title)
    {
        if (!smtp.IsConfigured)
            return;
        notify.EmailOnly(user.Email, title,
            $"Hi {user.FullName},\n\nSign in with:\nEmail: {user.Email}\nTemporary password: {password}\n\nYou will be asked to choose your own password when you sign in.",
            "/login");
    }

    /// <summary>Three groups of four letters and digits, e.g. "Kp7m-Rt42-Wx9q", with no look-alike characters.</summary>
    public static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        while (true)
        {
            var c = Enumerable.Range(0, 12).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray();
            if (c.Any(char.IsDigit) && c.Any(char.IsUpper) && c.Any(char.IsLower))
                return $"{new string(c, 0, 4)}-{new string(c, 4, 4)}-{new string(c, 8, 4)}";
        }
    }

    private async Task<string> NextCodeAsync()
    {
        var codes = await db.Employees.Where(e => e.EmployeeCode.StartsWith("EMP-")).Select(e => e.EmployeeCode).ToListAsync();
        var max = codes.Select(c => int.TryParse(c[4..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"EMP-{max + 1:D4}";
    }
}
