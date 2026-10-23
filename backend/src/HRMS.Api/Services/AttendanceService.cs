using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public class AttendanceService(HrmsDbContext db, TenantClock clock, ScheduleService schedule)
{
    public async Task<AttendanceRecord> CheckInAsync(Guid employeeId, double? latitude, double? longitude, AttendanceSource source, string? ip = null)
    {
        var today = await clock.TodayAsync();
        if (await db.AttendanceRecords.AnyAsync(a => a.EmployeeId == employeeId && a.Date == today))
            throw new DomainException("You have already checked in today.");
        await schedule.EnsureOpenAsync(today);

        var employee = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == employeeId);
        CheckChannel(employee, source, ip);
        var officeId = await MatchOfficeAsync(employee, latitude, longitude);
        var shift = await ShiftForAsync(employeeId, today);
        var localNow = await clock.LocalNowAsync();
        if (shift?.EarliestCheckInMinutes is { } early && TimeOnly.FromDateTime(localNow) < shift.StartTime.AddMinutes(-early)
            && shift.StartTime.AddMinutes(-early) < shift.StartTime)
            throw new DomainException($"Check-in opens at {shift.StartTime.AddMinutes(-early):HH:mm}.");

        var record = new AttendanceRecord
        {
            EmployeeId = employeeId,
            Date = today,
            CheckInUtc = clock.UtcNow,
            CheckInLatitude = latitude,
            CheckInLongitude = longitude,
            OfficeLocationId = officeId,
            Source = source,
        };
        ApplyArrival(record, shift, TimeOnly.FromDateTime(localNow));
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();
        return record;
    }

    public async Task<AttendanceRecord> CheckOutAsync(Guid employeeId, double? latitude, double? longitude, AttendanceSource source = AttendanceSource.Web, string? ip = null)
    {
        var today = await clock.TodayAsync();
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today)
            ?? throw new DomainException("You have not checked in today.");
        if (record.CheckOutUtc is not null)
            throw new DomainException("You have already checked out today.");

        var employee = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == employeeId);
        CheckChannel(employee, source, ip);
        await MatchOfficeAsync(employee, latitude, longitude);
        record.CheckOutUtc = clock.UtcNow;
        record.CheckOutLatitude = latitude;
        record.CheckOutLongitude = longitude;
        await schedule.EnsureOpenAsync(today);
        await ApplyWorkedTimeAsync(record, await ShiftForAsync(employeeId, today));
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
        await schedule.EnsureOpenAsync(date);

        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == date);
        if (record is null)
        {
            record = new AttendanceRecord { EmployeeId = employeeId, Date = date };
            db.AttendanceRecords.Add(record);
        }

        var shift = await ShiftForAsync(employeeId, date);
        record.Source = source;
        record.Note = note;
        record.CheckInUtc = await clock.ToUtcAsync(date.ToDateTime(checkIn));
        record.CheckOutUtc = checkOut is { } o ? await clock.ToUtcAsync(date.ToDateTime(o)) : null;
        ApplyArrival(record, shift, checkIn);
        record.WorkedMinutes = 0;
        record.EarlyLeaveMinutes = 0;
        if (record.CheckOutUtc is not null)
            await ApplyWorkedTimeAsync(record, shift);

        await db.SaveChangesAsync();
        return record;
    }

    /// <summary>Web or phone, and the allowed networks, as set on the employee's file.</summary>
    private static void CheckChannel(Employee employee, AttendanceSource source, string? ip)
    {
        if (source == AttendanceSource.Mobile && !employee.AllowMobileCheckIn)
            throw new DomainException("You cannot mark attendance from the phone. Use the web or the attendance machine.");
        if (source == AttendanceSource.Web && !employee.AllowWebCheckIn)
            throw new DomainException("You cannot mark attendance from the web. Use the phone app or the attendance machine.");
        if (!string.IsNullOrWhiteSpace(employee.AllowedIps) && source is AttendanceSource.Web or AttendanceSource.Mobile)
        {
            var allowed = employee.AllowedIps.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (ip is null || !allowed.Any(a => ip == a || (a.EndsWith('.') || a.EndsWith(':')) && ip.StartsWith(a, StringComparison.Ordinal)))
                throw new DomainException("Attendance can only be marked from the office network.");
        }
    }

    /// <summary>When the company has office locations, the punch must be inside one of them (or the person's own place).</summary>
    private async Task<Guid?> MatchOfficeAsync(Employee employee, double? latitude, double? longitude)
    {
        if (employee.CheckInAnywhere)
            return null;
        var offices = await db.OfficeLocations.ToListAsync();
        var own = employee.OwnLatitude is { } olat && employee.OwnLongitude is { } olng ? (olat, olng, employee.OwnRadiusMeters ?? 200) : ((double, double, int)?)null;
        if (offices.Count == 0 && own is null)
            return null;
        if (latitude is null || longitude is null)
            throw new DomainException("Location is required. Allow location access and try again.");
        if (own is { } o && GeoFence.DistanceMeters(latitude.Value, longitude.Value, o.Item1, o.Item2) <= o.Item3)
            return null;
        if (offices.Count == 0)
            throw new DomainException("You are away from your approved location.");

        var nearest = offices
            .Select(o => (Office: o, Distance: GeoFence.DistanceMeters(latitude.Value, longitude.Value, o.Latitude, o.Longitude)))
            .OrderBy(x => x.Distance)
            .First();
        if (nearest.Distance > nearest.Office.RadiusMeters)
            throw new DomainException($"You are {nearest.Distance:0} m from {nearest.Office.Name}; you must be within {nearest.Office.RadiusMeters} m to mark attendance.");
        return nearest.Office.Id;
    }

    /// <summary>The rostered shift that day; null on a day off, so nobody is late on a day off.</summary>
    private async Task<Shift?> ShiftForAsync(Guid employeeId, DateOnly date) =>
        (await schedule.ForEmployeeAsync(employeeId, date, date)).ShiftOn(date);

    /// <summary>Late or present from the check-in time; late minutes are counted after the grace.</summary>
    private static void ApplyArrival(AttendanceRecord record, Shift? shift, TimeOnly localIn)
    {
        record.LateMinutes = 0;
        record.Status = AttendanceStatus.Present;
        if (shift is null || localIn <= shift.StartTime.AddMinutes(shift.GraceMinutes) || localIn < shift.StartTime)
            return;
        record.Status = AttendanceStatus.Late;
        record.LateMinutes = (int)(localIn - shift.StartTime).TotalMinutes - shift.GraceMinutes;
    }

    /// <summary>
    /// Worked time, early leave and day flags once the person checks out: under the half-day mark = half day, under the
    /// short-day mark = short day (both replace late), otherwise the arrival flag stays.
    /// </summary>
    private async Task ApplyWorkedTimeAsync(AttendanceRecord record, Shift? shift)
    {
        record.WorkedMinutes = (int)(record.CheckOutUtc!.Value - record.CheckInUtc).TotalMinutes;
        record.EarlyLeaveMinutes = 0;
        if (shift is null)
            return;
        var expected = shift.ExpectedMinutes;
        var halfMark = shift.HalfDayMinutes ?? expected / 2;
        if (expected > 0 && record.WorkedMinutes < halfMark)
            record.Status = AttendanceStatus.HalfDay;
        else if (shift.ShortDayMinutes is { } shortMark && record.WorkedMinutes < shortMark)
            record.Status = AttendanceStatus.ShortDay;
        var localOut = TimeOnly.FromDateTime(await clock.ToLocalAsync(record.CheckOutUtc.Value));
        var earlyBy = (int)(shift.EndTime - localOut).TotalMinutes;
        if (localOut < shift.EndTime && localOut > shift.StartTime && earlyBy > shift.EarlyLeaveGraceMinutes)
            record.EarlyLeaveMinutes = earlyBy - shift.EarlyLeaveGraceMinutes;
    }
}
