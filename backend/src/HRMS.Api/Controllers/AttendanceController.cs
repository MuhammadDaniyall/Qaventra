using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record PunchRequest([Range(-90, 90)] double? Latitude, [Range(-180, 180)] double? Longitude, AttendanceSource Source = AttendanceSource.Web);

public record ManualAttendanceRequest(Guid EmployeeId, DateOnly Date, TimeOnly CheckIn, TimeOnly? CheckOut);

/// <summary>Times are company-local.</summary>
public record AttendanceDto(Guid Id, Guid EmployeeId, DateOnly Date, DateTime CheckIn, DateTime? CheckOut, AttendanceStatus Status, AttendanceSource Source, int WorkedMinutes, string? Office, string? Note,
    int LateMinutes = 0, int EarlyLeaveMinutes = 0);

public record TodayDto(DateOnly Date, AttendanceDto? Record, string? ShiftName, TimeOnly? ShiftStart, TimeOnly? ShiftEnd, bool LocationRequired);

public record DailyRowDto(Guid EmployeeId, string EmployeeCode, string FullName, string? Department, AttendanceDto? Record, string? LeaveType);

[ApiController]
[Route("api/attendance")]
[Authorize(Policy = Policies.CompanyUser)]
public class AttendanceController(HrmsDbContext db, AttendanceService attendance, ScheduleService schedule, TenantClock clock, ICurrentUser me) : ControllerBase
{
    [HttpGet("today")]
    public async Task<TodayDto> Today()
    {
        var employeeId = MyEmployeeId();
        var today = await clock.TodayAsync();
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today);
        var shift = (await schedule.ForEmployeeAsync(employeeId, today, today)).ShiftOn(today);
        return new TodayDto(today, record is null ? null : await ToDto(record), shift?.Name, shift?.StartTime, shift?.EndTime,
            await db.OfficeLocations.AnyAsync());
    }

    [HttpPost("check-in")]
    public async Task<AttendanceDto> CheckIn(PunchRequest req) =>
        await ToDto(await attendance.CheckInAsync(MyEmployeeId(), req.Latitude, req.Longitude, req.Source, HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString()));

    [HttpPost("check-out")]
    public async Task<AttendanceDto> CheckOut(PunchRequest req) =>
        await ToDto(await attendance.CheckOutAsync(MyEmployeeId(), req.Latitude, req.Longitude, req.Source, HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString()));

    [HttpGet("my")]
    public async Task<List<AttendanceDto>> My([FromQuery] int year, [FromQuery] int month)
    {
        var employeeId = MyEmployeeId();
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var records = await db.AttendanceRecords.Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date).ToListAsync();
        var result = new List<AttendanceDto>();
        foreach (var r in records) result.Add(await ToDto(r));
        return result;
    }

    /// <summary>Everyone's status for one day. Managers see only their team.</summary>
    [HttpGet("daily"), Authorize(Policy = Policies.Approver)]
    public async Task<List<DailyRowDto>> Daily([FromQuery] DateOnly? date)
    {
        var day = date ?? await clock.TodayAsync();
        var employees = db.Employees.Where(e => (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation) && e.JoiningDate <= day);
        if (me.Role == UserRole.Manager)
            employees = employees.Where(e => e.ManagerId == me.EmployeeId);

        var list = await employees.OrderBy(e => e.EmployeeCode)
            .Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : null })
            .ToListAsync();
        var ids = list.Select(e => e.Id).ToList();
        var records = await db.AttendanceRecords.Where(a => a.Date == day && ids.Contains(a.EmployeeId)).ToDictionaryAsync(a => a.EmployeeId);
        var leaves = await db.LeaveRequests.Where(r => r.Status == LeaveStatus.Approved && r.FromDate <= day && r.ToDate >= day && ids.Contains(r.EmployeeId))
            .Select(r => new { r.EmployeeId, r.LeaveType!.Name }).ToListAsync();

        var rows = new List<DailyRowDto>();
        foreach (var e in list)
        {
            var dto = records.TryGetValue(e.Id, out var r) ? await ToDto(r) : null;
            rows.Add(new DailyRowDto(e.Id, e.EmployeeCode, e.Name, e.Department, dto, leaves.FirstOrDefault(l => l.EmployeeId == e.Id)?.Name));
        }
        return rows;
    }

    [HttpPost("manual"), Authorize(Policy = Policies.Hr)]
    public async Task<AttendanceDto> Manual(ManualAttendanceRequest req) =>
        await ToDto(await attendance.SaveManualAsync(req.EmployeeId, req.Date, req.CheckIn, req.CheckOut));

    private Guid MyEmployeeId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");

    private async Task<AttendanceDto> ToDto(AttendanceRecord r)
    {
        var office = r.OfficeLocationId is { } id ? await db.OfficeLocations.Where(o => o.Id == id).Select(o => o.Name).FirstOrDefaultAsync() : null;
        return new AttendanceDto(r.Id, r.EmployeeId, r.Date, await clock.ToLocalAsync(r.CheckInUtc),
            r.CheckOutUtc is { } o ? await clock.ToLocalAsync(o) : null, r.Status, r.Source, r.WorkedMinutes, office, r.Note, r.LateMinutes, r.EarlyLeaveMinutes);
    }
}
