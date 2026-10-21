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

public record OfficeLocationDto(Guid Id, string Name, double Latitude, double Longitude, int RadiusMeters);
public record SaveOfficeLocationRequest([Required, StringLength(150)] string Name, [Range(-90, 90)] double Latitude, [Range(-180, 180)] double Longitude, [Range(20, 5000)] int RadiusMeters);

public record ShiftDto(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, int GraceMinutes, List<DayOfWeek> WeeklyOffDays, bool IsDefault);
public record SaveShiftRequest([Required, StringLength(100)] string Name, TimeOnly StartTime, TimeOnly EndTime, [Range(0, 180)] int GraceMinutes, List<DayOfWeek> WeeklyOffDays, bool IsDefault);

public record HolidayDto(Guid Id, DateOnly Date, string Name);
public record SaveHolidayRequest(DateOnly Date, [Required, StringLength(150)] string Name);

public record LeaveTypeDto(
    Guid Id, string Name, string Code, decimal AnnualQuota, bool IsPaid, bool IsActive,
    LeaveAccrual Accrual, decimal CarryForwardMax, int? MaxConsecutiveDays, int MinNoticeDays, Gender? ApplicableGender,
    bool AllowedOnProbation, bool AllowHalfDay, bool AllowShortLeave, bool IsCompensatory);
public record SaveLeaveTypeRequest(
    [Required, StringLength(100)] string Name, [Required, StringLength(10)] string Code, [Range(0, 365)] decimal AnnualQuota, bool IsPaid, bool IsActive,
    LeaveAccrual Accrual = LeaveAccrual.Upfront, [Range(0, 365)] decimal CarryForwardMax = 0, [Range(1, 365)] int? MaxConsecutiveDays = null,
    [Range(0, 90)] int MinNoticeDays = 0, Gender? ApplicableGender = null, bool AllowedOnProbation = true, bool AllowHalfDay = true,
    bool AllowShortLeave = false, bool IsCompensatory = false);

/// <summary>Company configuration. Everyone can read (pickers and calendars need it); only HR can change it.</summary>
[ApiController]
[Route("api/setup")]
[Authorize(Policy = Policies.CompanyUser)]
public class SetupController(HrmsDbContext db) : ControllerBase
{
    [HttpGet("office-locations")]
    public Task<List<OfficeLocationDto>> Offices() =>
        db.OfficeLocations.OrderBy(o => o.Name).Select(o => new OfficeLocationDto(o.Id, o.Name, o.Latitude, o.Longitude, o.RadiusMeters)).ToListAsync();

    [HttpPost("office-locations"), Authorize(Policy = Policies.Hr)]
    public async Task<OfficeLocationDto> CreateOffice(SaveOfficeLocationRequest req)
    {
        var o = new OfficeLocation();
        Apply(o, req);
        db.OfficeLocations.Add(o);
        await db.SaveChangesAsync();
        return new OfficeLocationDto(o.Id, o.Name, o.Latitude, o.Longitude, o.RadiusMeters);
    }

    [HttpPut("office-locations/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateOffice(Guid id, SaveOfficeLocationRequest req)
    {
        Apply(await db.OfficeLocations.FirstOrDefaultAsync(o => o.Id == id) ?? throw new NotFoundException("Office location"), req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("office-locations/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteOffice(Guid id)
    {
        db.OfficeLocations.Remove(await db.OfficeLocations.FirstOrDefaultAsync(o => o.Id == id) ?? throw new NotFoundException("Office location"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(OfficeLocation o, SaveOfficeLocationRequest req)
    {
        o.Name = req.Name.Trim();
        o.Latitude = req.Latitude;
        o.Longitude = req.Longitude;
        o.RadiusMeters = req.RadiusMeters;
    }

    [HttpGet("shifts")]
    public Task<List<ShiftDto>> Shifts() =>
        db.Shifts.OrderBy(s => s.Name).Select(s => new ShiftDto(s.Id, s.Name, s.StartTime, s.EndTime, s.GraceMinutes, s.WeeklyOffDays, s.IsDefault)).ToListAsync();

    [HttpPost("shifts"), Authorize(Policy = Policies.Hr)]
    public async Task<ShiftDto> CreateShift(SaveShiftRequest req)
    {
        var s = new Shift();
        await ApplyShift(s, req);
        db.Shifts.Add(s);
        await db.SaveChangesAsync();
        return new ShiftDto(s.Id, s.Name, s.StartTime, s.EndTime, s.GraceMinutes, s.WeeklyOffDays, s.IsDefault);
    }

    [HttpPut("shifts/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateShift(Guid id, SaveShiftRequest req)
    {
        await ApplyShift(await db.Shifts.FirstOrDefaultAsync(s => s.Id == id) ?? throw new NotFoundException("Shift"), req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("shifts/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteShift(Guid id)
    {
        var s = await db.Shifts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Shift");
        if (s.IsDefault) throw new DomainException("Make another shift the default before deleting this one.");
        if (await db.Employees.AnyAsync(e => e.ShiftId == id)) throw new DomainException("Move employees off this shift before deleting it.");
        db.Shifts.Remove(s);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyShift(Shift s, SaveShiftRequest req)
    {
        if (req.EndTime <= req.StartTime)
            throw new DomainException("Shift end time must be after the start time. Overnight shifts are not supported yet.");
        s.Name = req.Name.Trim();
        s.StartTime = req.StartTime;
        s.EndTime = req.EndTime;
        s.GraceMinutes = req.GraceMinutes;
        s.WeeklyOffDays = req.WeeklyOffDays.Distinct().ToList();
        if (req.IsDefault && !s.IsDefault)
            await db.Shifts.Where(x => x.IsDefault).ForEachAsync(x => x.IsDefault = false);
        s.IsDefault = req.IsDefault || s.IsDefault;
    }

    [HttpGet("holidays")]
    public Task<List<HolidayDto>> Holidays([FromQuery] int? year) =>
        db.Holidays.Where(h => year == null || h.Date.Year == year).OrderBy(h => h.Date).Select(h => new HolidayDto(h.Id, h.Date, h.Name)).ToListAsync();

    [HttpPost("holidays"), Authorize(Policy = Policies.Hr)]
    public async Task<HolidayDto> CreateHoliday(SaveHolidayRequest req)
    {
        var h = new Holiday { Date = req.Date, Name = req.Name.Trim() };
        db.Holidays.Add(h);
        await db.SaveChangesAsync();
        return new HolidayDto(h.Id, h.Date, h.Name);
    }

    [HttpPut("holidays/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateHoliday(Guid id, SaveHolidayRequest req)
    {
        var h = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Holiday");
        h.Date = req.Date;
        h.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("holidays/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteHoliday(Guid id)
    {
        db.Holidays.Remove(await db.Holidays.FirstOrDefaultAsync(h => h.Id == id) ?? throw new NotFoundException("Holiday"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("leave-types")]
    public Task<List<LeaveTypeDto>> LeaveTypes() =>
        db.LeaveTypes.OrderBy(t => t.Name).Select(t => new LeaveTypeDto(t.Id, t.Name, t.Code, t.AnnualQuota, t.IsPaid, t.IsActive,
            t.Accrual, t.CarryForwardMax, t.MaxConsecutiveDays, t.MinNoticeDays, t.ApplicableGender, t.AllowedOnProbation, t.AllowHalfDay,
            t.AllowShortLeave, t.IsCompensatory)).ToListAsync();

    [HttpPost("leave-types"), Authorize(Policy = Policies.Hr)]
    public async Task<LeaveTypeDto> CreateLeaveType(SaveLeaveTypeRequest req)
    {
        var t = new LeaveType();
        Apply(t, req);
        db.LeaveTypes.Add(t);
        await db.SaveChangesAsync();
        return new LeaveTypeDto(t.Id, t.Name, t.Code, t.AnnualQuota, t.IsPaid, t.IsActive, t.Accrual, t.CarryForwardMax, t.MaxConsecutiveDays,
            t.MinNoticeDays, t.ApplicableGender, t.AllowedOnProbation, t.AllowHalfDay, t.AllowShortLeave, t.IsCompensatory);
    }

    /// <summary>Quota changes apply to balances created from now on; existing balances keep their entitlement.</summary>
    [HttpPut("leave-types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateLeaveType(Guid id, SaveLeaveTypeRequest req)
    {
        var t = await db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Leave type");
        Apply(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(LeaveType t, SaveLeaveTypeRequest req)
    {
        if (req.IsCompensatory && !req.IsPaid)
            throw new DomainException("Compensatory leave must be paid leave.");
        t.Name = req.Name.Trim();
        t.Code = req.Code.Trim().ToUpperInvariant();
        t.AnnualQuota = req.IsCompensatory ? 0 : req.AnnualQuota;
        t.IsPaid = req.IsPaid;
        t.IsActive = req.IsActive;
        t.Accrual = req.Accrual;
        t.CarryForwardMax = req.CarryForwardMax;
        t.MaxConsecutiveDays = req.MaxConsecutiveDays;
        t.MinNoticeDays = req.MinNoticeDays;
        t.ApplicableGender = req.ApplicableGender;
        t.AllowedOnProbation = req.AllowedOnProbation;
        t.AllowHalfDay = req.AllowHalfDay;
        t.AllowShortLeave = req.AllowShortLeave;
        t.IsCompensatory = req.IsCompensatory;
    }

    /// <summary>Only a leave type that was never used can be deleted; otherwise mark it inactive.</summary>
    [HttpDelete("leave-types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteLeaveType(Guid id)
    {
        var t = await db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Leave type");
        if (await db.LeaveRequests.AnyAsync(r => r.LeaveTypeId == id) || await db.LeaveBalances.AnyAsync(b => b.LeaveTypeId == id && b.Used > 0))
            throw new DomainException("This leave type has been used. Mark it inactive instead of deleting it.");
        db.LeaveBalances.RemoveRange(db.LeaveBalances.Where(b => b.LeaveTypeId == id));
        db.LeaveTypes.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }
}