using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public class AttendanceService(HrmsDbContext db, TenantClock clock)
{
    public async Task<AttendanceRecord> CheckInAsync(Guid employeeId, double? latitude, double? longitude, AttendanceSource source)
    {
        var today = await clock.TodayAsync();
        if (await db.AttendanceRecords.AnyAsync(a => a.EmployeeId == employeeId && a.Date == today))
            throw new DomainException("You have already checked in today.");

        var officeId = await MatchOfficeAsync(latitude, longitude);
        var shift = await ShiftForAsync(employeeId);
        var localNow = await clock.LocalNowAsync();

        var record = new AttendanceRecord
        {
            EmployeeId = employeeId,
            Date = today,
            CheckInUtc = clock.UtcNow,
            CheckInLatitude = latitude,
            CheckInLongitude = longitude,
            OfficeLocationId = officeId,
            Source = source,
            Status = IsLate(shift, TimeOnly.FromDateTime(localNow)) ? AttendanceStatus.Late : AttendanceStatus.Present,
        };
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();
        return record;
    }

    public async Task<AttendanceRecord> CheckOutAsync(Guid employeeId, double? latitude, double? longitude)
    {
        var today = await clock.TodayAsync();
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today)
            ?? throw new DomainException("You have not checked in today.");
        if (record.CheckOutUtc is not null)
            throw new DomainException("You have already checked out today.");

        await MatchOfficeAsync(latitude, longitude);
        record.CheckOutUtc = clock.UtcNow;
        record.CheckOutLatitude = latitude;
        record.CheckOutLongitude = longitude;
        ApplyWorkedTime(record, await ShiftForAsync(employeeId));
        await db.SaveChangesAsync();
        return record;
    }

    /// <summary>HR correction or entry for a missed punch. Times are local to the company.</summary>
    public async Task<AttendanceRecord> SaveManualAsync(Guid employeeId, DateOnly date, TimeOnly checkIn, TimeOnly? checkOut,
        AttendanceSource source = AttendanceSource.Manual, string? note = null)
    {
        if (checkOut is { } co && co <= checkIn)
            throw new DomainException("Check-out must be after check-in.");
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId))
            throw new NotFoundException("Employee");

        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == date);
        if (record is null)
        {
            record = new AttendanceRecord { EmployeeId = employeeId, Date = date };
            db.AttendanceRecords.Add(record);
        }

        var shift = await ShiftForAsync(employeeId);
        record.Source = source;
        record.Note = note;
        record.CheckInUtc = await clock.ToUtcAsync(date.ToDateTime(checkIn));
        record.CheckOutUtc = checkOut is { } o ? await clock.ToUtcAsync(date.ToDateTime(o)) : null;
        record.Status = IsLate(shift, checkIn) ? AttendanceStatus.Late : AttendanceStatus.Present;
        record.WorkedMinutes = 0;
        if (record.CheckOutUtc is not null)
            ApplyWorkedTime(record, shift);

        await db.SaveChangesAsync();
        return record;
    }

    /// <summary>When the company has office locations, the punch must be inside one of them.</summary>
    private async Task<Guid?> MatchOfficeAsync(double? latitude, double? longitude)
    {
        var offices = await db.OfficeLocations.ToListAsync();
        if (offices.Count == 0)
            return null;
        if (latitude is null || longitude is null)
            throw new DomainException("Location is required. Allow location access and try again.");

        var nearest = offices
            .Select(o => (Office: o, Distance: GeoFence.DistanceMeters(latitude.Value, longitude.Value, o.Latitude, o.Longitude)))
            .OrderBy(x => x.Distance)
            .First();
        if (nearest.Distance > nearest.Office.RadiusMeters)
            throw new DomainException($"You are {nearest.Distance:0} m from {nearest.Office.Name}; you must be within {nearest.Office.RadiusMeters} m to mark attendance.");
        return nearest.Office.Id;
    }

    private async Task<Shift?> ShiftForAsync(Guid employeeId) =>
        await db.Employees.Where(e => e.Id == employeeId).Select(e => e.Shift).FirstOrDefaultAsync();

    private static bool IsLate(Shift? shift, TimeOnly localTime) =>
        shift is not null && localTime > shift.StartTime.AddMinutes(shift.GraceMinutes);

    private static void ApplyWorkedTime(AttendanceRecord record, Shift? shift)
    {
        record.WorkedMinutes = (int)(record.CheckOutUtc!.Value - record.CheckInUtc).TotalMinutes;
        if (shift is null)
            return;
        var shiftMinutes = (int)(shift.EndTime - shift.StartTime).TotalMinutes;
        if (shiftMinutes > 0 && record.WorkedMinutes < shiftMinutes / 2)
            record.Status = AttendanceStatus.HalfDay;
    }
}
