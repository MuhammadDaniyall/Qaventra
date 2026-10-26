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

public record BiometricDeviceDto(
    Guid Id, string Name, string? SerialNumber, Guid? OfficeLocationId, string? Office, string TokenPrefix, bool IsActive,
    DateTime? LastSeenAtUtc, int PunchesReceived);

/// <summary>The token is only ever returned here, when a device is added or its token is replaced.</summary>
public record BiometricDeviceWithTokenDto(BiometricDeviceDto Device, string Token);

public record SaveBiometricDeviceRequest(
    [Required, StringLength(100)] string Name,
    [StringLength(100)] string? SerialNumber,
    Guid? OfficeLocationId,
    bool IsActive = true);

/// <summary>One punch: the person's punch code (as enrolled on the machine) and the company-local time.</summary>
public record MachinePunch([Required, StringLength(50)] string PunchCode, DateTime Time);

public record MachinePunchBatch([Required, MinLength(1), MaxLength(5000)] List<MachinePunch> Punches);

public record SkippedPunch(string PunchCode, DateTime Time, string Reason);

public record MachinePunchResult(int Accepted, List<SkippedPunch> Skipped);

/// <summary>
/// Attendance machines. HR manages devices; a device (or the bridge software next to it) posts punches to
/// /api/biometric/punches with its token in the X-Device-Token header.
/// </summary>
[ApiController]
[Route("api/biometric")]
public class BiometricController(HrmsDbContext db, AttendanceService attendance, TenantClock clock, TenantAccessChecker access) : ControllerBase
{
    public const string TokenHeader = "X-Device-Token";

    [HttpGet("devices"), Authorize(Policy = Policies.Hr)]
    public Task<List<BiometricDeviceDto>> List() =>
        Project(db.BiometricDevices.OrderBy(d => d.Name)).ToListAsync();

    [HttpPost("devices"), Authorize(Policy = Policies.Hr)]
    public async Task<BiometricDeviceWithTokenDto> Create(SaveBiometricDeviceRequest req)
    {
        var device = new BiometricDevice();
        await Apply(device, req);
        var token = NewToken(device);
        db.BiometricDevices.Add(device);
        await db.SaveChangesAsync();
        return new(await Project(db.BiometricDevices.Where(d => d.Id == device.Id)).FirstAsync(), token);
    }

    [HttpPut("devices/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<BiometricDeviceDto> Update(Guid id, SaveBiometricDeviceRequest req)
    {
        var device = await db.BiometricDevices.FirstOrDefaultAsync(d => d.Id == id) ?? throw new NotFoundException("Attendance machine");
        await Apply(device, req);
        await db.SaveChangesAsync();
        return await Project(db.BiometricDevices.Where(d => d.Id == id)).FirstAsync();
    }

    /// <summary>Replaces the token; the old one stops working at once.</summary>
    [HttpPost("devices/{id:guid}/token"), Authorize(Policy = Policies.Hr)]
    public async Task<BiometricDeviceWithTokenDto> NewToken(Guid id)
    {
        var device = await db.BiometricDevices.FirstOrDefaultAsync(d => d.Id == id) ?? throw new NotFoundException("Attendance machine");
        var token = NewToken(device);
        await db.SaveChangesAsync();
        return new(await Project(db.BiometricDevices.Where(d => d.Id == id)).FirstAsync(), token);
    }

    [HttpDelete("devices/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var device = await db.BiometricDevices.FirstOrDefaultAsync(d => d.Id == id) ?? throw new NotFoundException("Attendance machine");
        db.BiometricDevices.Remove(device);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Punches from a machine. Each person's punches are grouped by day: the first is the check-in, the last the
    /// check-out. Unknown punch codes, closed months and days HR entered by hand are skipped and listed back.
    /// </summary>
    [HttpPost("punches"), AllowAnonymous]
    public async Task<ActionResult<MachinePunchResult>> Punches(MachinePunchBatch batch)
    {
        var token = Request.Headers[TokenHeader].ToString();
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized();
        var hash = Hash(token);
        var device = await db.BiometricDevices.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.TokenHash == hash && d.IsActive);
        // A suspended or expired company's machines stop, like its logins.
        if (device is null || await access.TenantAsync(device.TenantId) != TenantAccess.Allowed)
            return Unauthorized();
        // A device belongs to one company; everything after this runs inside it.
        db.UseTenant(device.TenantId);

        var codes = batch.Punches.Select(p => p.PunchCode.Trim()).Distinct().ToList();
        var people = await db.Employees.Where(e => e.PunchCode != null && codes.Contains(e.PunchCode))
            .Select(e => new { e.Id, e.PunchCode }).ToListAsync();
        var byCode = people.GroupBy(p => p.PunchCode!).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var skipped = new List<SkippedPunch>();
        var accepted = 0;
        foreach (var group in batch.Punches.GroupBy(p => (Code: p.PunchCode.Trim(), Date: DateOnly.FromDateTime(p.Time))))
        {
            if (!byCode.TryGetValue(group.Key.Code, out var employeeId))
            {
                skipped.AddRange(group.Select(p => new SkippedPunch(p.PunchCode, p.Time, "no employee has this punch code")));
                continue;
            }
            var reason = await attendance.RecordMachinePunchesAsync(employeeId, group.Key.Date, group.Select(p => TimeOnly.FromDateTime(p.Time)).ToList(), device.Name);
            if (reason is null)
                accepted += group.Count();
            else
                skipped.AddRange(group.Select(p => new SkippedPunch(p.PunchCode, p.Time, reason)));
        }

        device.LastSeenAtUtc = clock.UtcNow;
        device.PunchesReceived += accepted;
        await db.SaveChangesAsync();
        return new MachinePunchResult(accepted, skipped);
    }

    private async Task Apply(BiometricDevice device, SaveBiometricDeviceRequest req)
    {
        if (req.OfficeLocationId is { } office && !await db.OfficeLocations.AnyAsync(o => o.Id == office))
            throw new NotFoundException("Office location");
        device.Name = req.Name.Trim();
        device.SerialNumber = string.IsNullOrWhiteSpace(req.SerialNumber) ? null : req.SerialNumber.Trim();
        device.OfficeLocationId = req.OfficeLocationId;
        device.IsActive = req.IsActive;
    }

    private static string NewToken(BiometricDevice device)
    {
        var token = "qvd_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        device.TokenHash = Hash(token);
        device.TokenPrefix = token[..10];
        return token;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    private IQueryable<BiometricDeviceDto> Project(IQueryable<BiometricDevice> q) =>
        q.Select(d => new BiometricDeviceDto(
            d.Id, d.Name, d.SerialNumber, d.OfficeLocationId,
            db.OfficeLocations.Where(o => o.Id == d.OfficeLocationId).Select(o => o.Name).FirstOrDefault(),
            d.TokenPrefix, d.IsActive, d.LastSeenAtUtc, d.PunchesReceived));
}
