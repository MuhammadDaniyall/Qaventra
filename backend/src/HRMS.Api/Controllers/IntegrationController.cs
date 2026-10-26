using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
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

public record ApiTokenDto(Guid Id, string Name, string TokenPrefix, string? CreatedByName, DateTime CreatedAtUtc, DateOnly? ExpiresOn,
    DateTime? RevokedAtUtc, DateTime? LastUsedAtUtc);
public record CreateApiTokenRequest([Required, StringLength(100)] string Name, DateOnly? ExpiresOn);
/// <summary>The key is only ever returned here, once.</summary>
public record ApiTokenCreatedDto(ApiTokenDto Token, string Key);

public record IntegrationEmployeeDto(string EmployeeCode, string FirstName, string LastName, string? Email, string? Phone, string? Department,
    string? Designation, EmploymentStatus Status, DateOnly JoiningDate, DateOnly? LeavingDate, string? ManagerCode);
public record IntegrationAttendanceDto(string EmployeeCode, DateOnly Date, DateTime CheckInUtc, DateTime? CheckOutUtc, AttendanceStatus Status,
    int WorkedMinutes, int LateMinutes, AttendanceSource Source);
public record IntegrationLeaveDto(string EmployeeCode, string LeaveType, DateOnly FromDate, DateOnly ToDate, decimal Days, LeaveStatus Status);
public record IntegrationPayslipDto(string EmployeeCode, decimal GrossEarnings, decimal IncomeTax, decimal EobiEmployee, decimal OtherDeductions,
    decimal NetPay, decimal PaidDays);

/// <summary>Company admin's API keys for other systems.</summary>
[ApiController]
[Route("api/setup/api-tokens")]
[Authorize(Policy = Policies.Admin)]
public class ApiTokensController(HrmsDbContext db, ICurrentUser me, TenantClock clock) : ControllerBase
{
    [HttpGet]
    public Task<List<ApiTokenDto>> List() =>
        db.ApiTokens.OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new ApiTokenDto(t.Id, t.Name, t.TokenPrefix, t.CreatedByName, t.CreatedAtUtc, t.ExpiresOn, t.RevokedAtUtc, t.LastUsedAtUtc))
            .ToListAsync();

    [HttpPost]
    public async Task<ApiTokenCreatedDto> Create(CreateApiTokenRequest req)
    {
        if (req.ExpiresOn is { } exp && exp <= await clock.TodayAsync())
            throw new DomainException("The expiry date must be in the future.");
        var key = "qvk_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = new ApiToken
        {
            Name = req.Name.Trim(), TokenHash = IntegrationController.Hash(key), TokenPrefix = key[..10], ExpiresOn = req.ExpiresOn,
            CreatedByName = await db.Users.IgnoreQueryFilters().Where(u => u.Id == me.UserId).Select(u => u.FullName).FirstOrDefaultAsync(),
        };
        db.ApiTokens.Add(token);
        await db.SaveChangesAsync();
        return new(new(token.Id, token.Name, token.TokenPrefix, token.CreatedByName, token.CreatedAtUtc, token.ExpiresOn, null, null), key);
    }

    /// <summary>Stops the key at once. Kept in the list so it is clear what was turned off and when.</summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var t = await db.ApiTokens.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("API key");
        t.RevokedAtUtc ??= clock.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>
/// Read-only data for other systems, with a company API key in the X-Api-Key header. Nothing here can change data.
/// </summary>
[ApiController]
[Route("api/integration")]
[AllowAnonymous]
public class IntegrationController(HrmsDbContext db, TenantClock clock, TenantAccessChecker access) : ControllerBase
{
    public const string KeyHeader = "X-Api-Key";
    private const int MaxDays = 93;

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim())));

    [HttpGet("employees")]
    public async Task<ActionResult<List<IntegrationEmployeeDto>>> Employees([FromQuery] bool includeLeft = false)
    {
        if (!await AuthorizeAsync()) return Unauthorized();
        return await db.Employees.AsNoTracking()
            .Where(e => includeLeft || e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new IntegrationEmployeeDto(e.EmployeeCode, e.FirstName, e.LastName, e.Email, e.Phone,
                e.Department != null ? e.Department.Name : null, e.Designation != null ? e.Designation.Title : null,
                e.Status, e.JoiningDate, e.LeavingDate, e.Manager != null ? e.Manager.EmployeeCode : null))
            .ToListAsync();
    }

    [HttpGet("attendance")]
    public async Task<ActionResult<List<IntegrationAttendanceDto>>> Attendance([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (!await AuthorizeAsync()) return Unauthorized();
        CheckRange(from, to);
        return await db.AttendanceRecords.AsNoTracking().Where(a => a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date).ThenBy(a => a.Employee!.EmployeeCode)
            .Select(a => new IntegrationAttendanceDto(a.Employee!.EmployeeCode, a.Date, a.CheckInUtc, a.CheckOutUtc, a.Status, a.WorkedMinutes, a.LateMinutes, a.Source))
            .ToListAsync();
    }

    [HttpGet("leave")]
    public async Task<ActionResult<List<IntegrationLeaveDto>>> Leave([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (!await AuthorizeAsync()) return Unauthorized();
        CheckRange(from, to);
        return await db.LeaveRequests.AsNoTracking().Where(r => r.FromDate <= to && r.ToDate >= from)
            .OrderBy(r => r.FromDate)
            .Select(r => new IntegrationLeaveDto(r.Employee!.EmployeeCode, r.LeaveType!.Name, r.FromDate, r.ToDate, r.Days, r.Status))
            .ToListAsync();
    }

    /// <summary>Finalized payroll only, so other systems never pick up a draft that may still change.</summary>
    [HttpGet("payroll/{year:int}/{month:int}")]
    public async Task<ActionResult<List<IntegrationPayslipDto>>> Payroll(int year, int month)
    {
        if (!await AuthorizeAsync()) return Unauthorized();
        // Every finalized run in the month: monthly, and the weekly or bi-weekly periods ending in it.
        var runs = await db.PayrollRuns.AsNoTracking().Where(r => r.Year == year && r.Month == month && r.Status == PayrollRunStatus.Finalized).Select(r => r.Id).ToListAsync();
        if (runs.Count == 0) return NotFound();
        return await db.Payslips.AsNoTracking().Where(p => runs.Contains(p.PayrollRunId)).OrderBy(p => p.Employee!.EmployeeCode).ThenBy(p => p.PayrollRun!.PeriodStart)
            .Select(p => new IntegrationPayslipDto(p.Employee!.EmployeeCode, p.GrossEarnings, p.IncomeTax, p.EobiEmployee, p.OtherDeductions, p.NetPay, p.PaidDays))
            .ToListAsync();
    }

    /// <summary>Finds the key's company and runs the rest of the request inside it.</summary>
    private async Task<bool> AuthorizeAsync()
    {
        var key = Request.Headers[KeyHeader].ToString();
        if (string.IsNullOrWhiteSpace(key)) return false;
        var hash = Hash(key);
        var token = await db.ApiTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAtUtc == null);
        // A suspended or expired company's keys stop working, like its logins.
        if (token is null || await access.TenantAsync(token.TenantId) != TenantAccess.Allowed) return false;
        db.UseTenant(token.TenantId);
        if (token.ExpiresOn is { } exp && exp < await clock.TodayAsync()) return false;
        if (token.LastUsedAtUtc is null || clock.UtcNow - token.LastUsedAtUtc > TimeSpan.FromMinutes(5))
        {
            token.LastUsedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync();
        }
        return true;
    }

    private static void CheckRange(DateOnly from, DateOnly to)
    {
        if (to < from) throw new DomainException("'to' must be on or after 'from'.");
        if (to.DayNumber - from.DayNumber > MaxDays) throw new DomainException($"Ask for at most {MaxDays} days at a time.");
    }
}
