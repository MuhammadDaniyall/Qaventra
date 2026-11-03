using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>One employee's working calendar over a range: roster entries first, then their own shift and weekly off days.</summary>
public sealed class EmployeeSchedule(
    Shift? defaultShift, IReadOnlyDictionary<DateOnly, ShiftAssignment> roster, IReadOnlyDictionary<Guid, Shift> shifts, IReadOnlySet<DateOnly> holidays)
{
    public Shift? DefaultShift => defaultShift;

    public bool IsRostered(DateOnly date) => roster.ContainsKey(date);

    /// <summary>The shift worked that day; null when off or when the employee has no shift.</summary>
    public Shift? ShiftOn(DateOnly date) =>
        roster.TryGetValue(date, out var a) ? (a.ShiftId is { } id ? shifts.GetValueOrDefault(id) : null) : IsOff(date) ? null : defaultShift;

    public bool IsOff(DateOnly date) =>
        roster.TryGetValue(date, out var a) ? a.IsOff : defaultShift?.WeeklyOffDays.Contains(date.DayOfWeek) == true;

    public bool IsHoliday(DateOnly date) => holidays.Contains(date);

    public bool IsWorkingDay(DateOnly date) => !IsOff(date) && !IsHoliday(date);

    public int CountWorkingDays(DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
            if (IsWorkingDay(d))
                count++;
        return count;
    }
}

/// <summary>Who works which shift on which day, and which months are closed for changes.</summary>
public class ScheduleService(HrmsDbContext db)
{
    public async Task<EmployeeSchedule> ForEmployeeAsync(Guid employeeId, DateOnly from, DateOnly to) =>
        (await ForEmployeesAsync([employeeId], from, to))[employeeId];

    public async Task<Dictionary<Guid, EmployeeSchedule>> ForEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to)
    {
        var ids = employeeIds.Distinct().ToList();
        var shifts = await db.Shifts.ToDictionaryAsync(s => s.Id);
        var people = await db.Employees.Where(e => ids.Contains(e.Id)).Select(e => new { e.Id, e.ShiftId, e.RegionId, e.Religion }).ToDictionaryAsync(e => e.Id);
        var roster = (await db.ShiftAssignments.Where(a => ids.Contains(a.EmployeeId) && a.Date >= from && a.Date <= to).ToListAsync())
            .ToLookup(a => a.EmployeeId);
        var holidays = await db.Holidays.Where(h => h.Date >= from && h.Date <= to)
            .Select(h => new { h.Date, h.RegionIds, h.Religion, h.ExemptEmployeeIds }).ToListAsync();
        var everyone = holidays.Where(h => h.RegionIds == null && h.Religion == null && h.ExemptEmployeeIds == null).Select(h => h.Date).ToHashSet();
        var targeted = holidays.Where(h => h.RegionIds != null || h.Religion != null || h.ExemptEmployeeIds != null).ToList();
        return ids.ToDictionary(id => id, id =>
        {
            var p = people.GetValueOrDefault(id);
            var mine = everyone;
            if (targeted.Count > 0)
            {
                mine = [.. everyone];
                foreach (var h in targeted)
                    if (Applies(h.RegionIds, h.Religion, h.ExemptEmployeeIds, id, p?.RegionId, p?.Religion))
                        mine.Add(h.Date);
            }
            return new EmployeeSchedule(
                p?.ShiftId is { } s ? shifts.GetValueOrDefault(s) : null,
                roster[id].ToDictionary(a => a.Date), shifts, mine);
        });
    }

    /// <summary>Whether a region-, religion- or exemption-limited holiday is a day off for this person.</summary>
    public static bool Applies(string? regionIds, string? religion, string? exemptIds, Guid employeeId, Guid? regionId, string? employeeReligion)
    {
        if (exemptIds is not null && HelpdeskService.Has(exemptIds, employeeId))
            return false;
        if (!string.IsNullOrEmpty(regionIds) && (regionId is not { } r || !HelpdeskService.Has(regionIds, r)))
            return false;
        if (!string.IsNullOrEmpty(religion) && !string.Equals(religion.Trim(), employeeReligion?.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    // ---------- Closed months ----------

    public Task<bool> IsClosedAsync(DateOnly date) =>
        db.AttendancePeriods.AnyAsync(p => p.Year == date.Year && p.Month == date.Month);

    /// <summary>Throws when any day in the range falls in a closed month.</summary>
    public async Task EnsureOpenAsync(DateOnly from, DateOnly to)
    {
        var a = from.Year * 12 + from.Month;
        var b = to.Year * 12 + to.Month;
        var closed = await db.AttendancePeriods.Where(p => p.Year * 12 + p.Month >= a && p.Year * 12 + p.Month <= b)
            .OrderBy(p => p.Year).ThenBy(p => p.Month).Select(p => new { p.Year, p.Month }).FirstOrDefaultAsync();
        if (closed is not null)
            throw new DomainException($"Attendance for {new DateOnly(closed.Year, closed.Month, 1):MMMM yyyy} is closed. Ask HR to reopen it first.");
    }

    public Task EnsureOpenAsync(DateOnly date) => EnsureOpenAsync(date, date);

    // ---------- Roster ----------

    /// <summary>
    /// Sets each employee's shift (or a day off) on every day of the range that matches the weekdays, or clears those
    /// days back to their own shift. Returns how many days were written.
    /// </summary>
    public async Task<int> CopyAsync(IReadOnlyCollection<Guid>? employeeIds, DateOnly sourceFrom, DateOnly sourceTo, DateOnly targetFrom, int times)
    {
        if (sourceTo < sourceFrom)
            throw new DomainException("The end date is before the start date.");
        var length = sourceTo.DayNumber - sourceFrom.DayNumber + 1;
        if (length > 31)
            throw new DomainException("Copy at most a month at a time.");
        if (times is < 1 or > 12)
            throw new DomainException("Repeat it 1 to 12 times.");
        if (targetFrom <= sourceTo && targetFrom.AddDays(length * times - 1) >= sourceFrom)
            throw new DomainException("The copy would overlap the days it copies from.");
        var targetTo = targetFrom.AddDays(length * times - 1);
        if (targetTo.DayNumber - targetFrom.DayNumber > 370)
            throw new DomainException("Plan at most a year ahead.");
        await EnsureOpenAsync(targetFrom, targetTo);

        var q = db.ShiftAssignments.Where(a => a.Date >= sourceFrom && a.Date <= sourceTo && a.Source == ShiftAssignmentSource.Roster);
        if (employeeIds is { Count: > 0 })
            q = q.Where(a => employeeIds.Contains(a.EmployeeId));
        var source = await q.ToListAsync();
        if (source.Count == 0)
            throw new DomainException("There is nothing planned in those days to copy.");
        var ids = source.Select(a => a.EmployeeId).Distinct().ToList();
        var existing = (await db.ShiftAssignments.Where(a => ids.Contains(a.EmployeeId) && a.Date >= targetFrom && a.Date <= targetTo).ToListAsync())
            .ToDictionary(a => (a.EmployeeId, a.Date));
        var count = 0;
        for (var n = 0; n < times; n++)
            foreach (var s in source)
            {
                var day = targetFrom.AddDays(n * length + (s.Date.DayNumber - sourceFrom.DayNumber));
                if (!existing.TryGetValue((s.EmployeeId, day), out var a))
                {
                    a = new ShiftAssignment { EmployeeId = s.EmployeeId, Date = day };
                    db.ShiftAssignments.Add(a);
                    existing[(s.EmployeeId, day)] = a;
                }
                a.ShiftId = s.ShiftId;
                a.IsOff = s.IsOff;
                a.Source = ShiftAssignmentSource.Roster;
                count++;
            }
        await db.SaveChangesAsync();
        return count;
    }

    public async Task<int> AssignAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, IReadOnlyCollection<DayOfWeek>? weekdays,
        Guid? shiftId, bool off, bool clear, ShiftAssignmentSource source = ShiftAssignmentSource.Roster)
    {
        if (to < from)
            throw new DomainException("The end date is before the start date.");
        if (to.DayNumber - from.DayNumber > 92)
            throw new DomainException("Plan at most three months at a time.");
        if (!clear && !off && shiftId is null)
            throw new DomainException("Choose a shift, a day off, or reset to the usual shift.");
        if (shiftId is { } sid && !await db.Shifts.AnyAsync(s => s.Id == sid))
            throw new NotFoundException("Shift");
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0)
            throw new DomainException("Choose at least one employee.");
        if (await db.Employees.CountAsync(e => ids.Contains(e.Id)) != ids.Count)
            throw new NotFoundException("Employee");
        await EnsureOpenAsync(from, to);

        var existing = (await db.ShiftAssignments.Where(a => ids.Contains(a.EmployeeId) && a.Date >= from && a.Date <= to).ToListAsync())
            .ToDictionary(a => (a.EmployeeId, a.Date));
        var count = 0;
        foreach (var id in ids)
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (weekdays is { Count: > 0 } && !weekdays.Contains(d.DayOfWeek))
                    continue;
                existing.TryGetValue((id, d), out var a);
                if (clear)
                {
                    if (a is not null)
                        db.ShiftAssignments.Remove(a);
                }
                else
                {
                    if (a is null)
                    {
                        a = new ShiftAssignment { EmployeeId = id, Date = d };
                        db.ShiftAssignments.Add(a);
                    }
                    a.ShiftId = off ? null : shiftId;
                    a.IsOff = off;
                    a.Source = source;
                }
                count++;
            }
        await db.SaveChangesAsync();
        return count;
    }
}
