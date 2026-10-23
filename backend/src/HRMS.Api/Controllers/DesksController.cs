using System.ComponentModel.DataAnnotations;
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

public record DeskSettingsDto(int MaxDaysAhead, bool RequireCheckIn, int ReleaseAfterMinutes, int DayStartsAt);

public record FloorDto(Guid Id, Guid OfficeLocationId, string Office, string Name, int Order, int Desks, int Rooms);

public record SaveFloorRequest(Guid OfficeLocationId, [Required, StringLength(100)] string Name, int Order);

public record SpaceDto(Guid Id, Guid FloorId, string Name, SpaceKind Kind, int Seats, string? Features, Guid? AssignedEmployeeId, string? AssignedTo, bool IsActive);

public record SaveSpaceRequest(Guid FloorId, [Required, StringLength(100)] string Name, SpaceKind Kind, int Seats, [StringLength(300)] string? Features, Guid? AssignedEmployeeId,
    bool IsActive = true);

public record BulkDesksRequest(Guid FloorId, [Required, StringLength(20)] string Prefix, int From, int To, [StringLength(300)] string? Features);

public record SlotDto(Guid BookingId, Guid EmployeeId, string EmployeeName, TimeOnly? From, TimeOnly? To, string? Title, BookingStatus Status, bool Mine);

public record SpaceDayDto(Guid Id, string Name, SpaceKind Kind, int Seats, string? Features, Guid? AssignedEmployeeId, string? AssignedTo, List<SlotDto> Bookings, bool Free);

public record FloorDayDto(DateOnly Date, Guid FloorId, string Floor, string Office, List<SpaceDayDto> Spaces, int DesksFree, int DesksTotal);

public record BookRequest(Guid SpaceId, DateOnly Date, TimeOnly? From, TimeOnly? To, [StringLength(200)] string? Title, int Attendees = 1, Guid? ForEmployeeId = null);

public record MyBookingDto(Guid Id, Guid SpaceId, string Space, SpaceKind Kind, string Floor, string Office, DateOnly Date, TimeOnly? From, TimeOnly? To, string? Title,
    int Attendees, BookingStatus Status, bool CanCheckIn);

public record InOfficeDto(Guid EmployeeId, string Name, string? Department, string Space, string Floor);

public record UtilisationRowDto(string Name, string Floor, SpaceKind Kind, int Bookings, int CheckedIn, int Released, double UsedPercent);

public record UtilisationDto(int WorkingDays, int Bookings, int CheckedIn, int NoShows, double DeskUsePercent, List<UtilisationRowDto> Spaces);

[ApiController]
[Route("api/desks")]
[Authorize(Policy = Policies.CompanyUser)]
public class DesksController(HrmsDbContext db, DeskService desks, TenantClock clock, ICurrentUser me) : ControllerBase
{
    // ---------- Settings and layout (HR) ----------

    [HttpGet("settings")]
    public async Task<DeskSettingsDto> Settings()
    {
        var s = await desks.SettingsAsync();
        return new DeskSettingsDto(s.MaxDaysAhead, s.RequireCheckIn, s.ReleaseAfterMinutes, s.DayStartsAt);
    }

    [HttpPut("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveSettings(DeskSettingsDto req)
    {
        if (req.MaxDaysAhead is < 0 or > 90 || req.ReleaseAfterMinutes is < 10 or > 480 || req.DayStartsAt is < 0 or > 23)
            throw new DomainException("Check the days, minutes and hour.");
        var s = await db.DeskSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new DeskSettings();
            db.DeskSettings.Add(s);
        }
        s.MaxDaysAhead = req.MaxDaysAhead;
        s.RequireCheckIn = req.RequireCheckIn;
        s.ReleaseAfterMinutes = req.ReleaseAfterMinutes;
        s.DayStartsAt = req.DayStartsAt;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("floors")]
    public async Task<List<FloorDto>> Floors() =>
        await db.Floors.OrderBy(f => f.Office!.Name).ThenBy(f => f.Order).ThenBy(f => f.Name)
            .Select(f => new FloorDto(f.Id, f.OfficeLocationId, f.Office!.Name, f.Name, f.Order,
                db.Spaces.Count(s => s.FloorId == f.Id && s.IsActive && s.Kind == SpaceKind.Desk),
                db.Spaces.Count(s => s.FloorId == f.Id && s.IsActive && s.Kind != SpaceKind.Desk)))
            .ToListAsync();

    [HttpPost("floors"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddFloor(SaveFloorRequest req)
    {
        var f = new Floor();
        await ApplyAsync(f, req);
        db.Floors.Add(f);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("floors/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateFloor(Guid id, SaveFloorRequest req)
    {
        var f = await db.Floors.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Floor");
        await ApplyAsync(f, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("floors/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteFloor(Guid id)
    {
        var f = await db.Floors.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Floor");
        if (await db.Spaces.AnyAsync(s => s.FloorId == id))
            throw new DomainException("Remove the desks and rooms on this floor first.");
        db.Floors.Remove(f);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("floors/{id:guid}/spaces")]
    public async Task<List<SpaceDto>> Spaces(Guid id) =>
        await db.Spaces.Where(s => s.FloorId == id).OrderBy(s => s.Kind).ThenBy(s => s.Name)
            .Select(s => new SpaceDto(s.Id, s.FloorId, s.Name, s.Kind, s.Seats, s.Features, s.AssignedEmployeeId,
                db.Employees.Where(e => e.Id == s.AssignedEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(), s.IsActive))
            .ToListAsync();

    [HttpPost("spaces"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddSpace(SaveSpaceRequest req)
    {
        var s = new Space();
        await ApplyAsync(s, req);
        db.Spaces.Add(s);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Adds numbered desks at once, e.g. A-01 to A-20. Names already used are skipped.</summary>
    [HttpPost("spaces/bulk"), Authorize(Policy = Policies.Hr)]
    public async Task<object> AddDesks(BulkDesksRequest req)
    {
        if (!await db.Floors.AnyAsync(f => f.Id == req.FloorId))
            throw new NotFoundException("Floor");
        if (req.From < 0 || req.To < req.From || req.To - req.From >= 200)
            throw new DomainException("Add 1 to 200 desks at a time.");
        var width = Math.Max(2, req.To.ToString().Length);
        var names = Enumerable.Range(req.From, req.To - req.From + 1).Select(n => $"{req.Prefix.Trim()}{n.ToString().PadLeft(width, '0')}").ToList();
        var existing = await db.Spaces.Where(s => s.FloorId == req.FloorId && names.Contains(s.Name)).Select(s => s.Name).ToListAsync();
        var added = 0;
        foreach (var name in names.Except(existing))
        {
            db.Spaces.Add(new Space { FloorId = req.FloorId, Name = name, Kind = SpaceKind.Desk, Seats = 1, Features = string.IsNullOrWhiteSpace(req.Features) ? null : req.Features.Trim() });
            added++;
        }
        await db.SaveChangesAsync();
        return new { added, skipped = existing.Count };
    }

    [HttpPut("spaces/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateSpace(Guid id, SaveSpaceRequest req)
    {
        var s = await db.Spaces.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Space");
        await ApplyAsync(s, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("spaces/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteSpace(Guid id)
    {
        var s = await db.Spaces.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Space");
        if (await db.SpaceBookings.AnyAsync(b => b.SpaceId == id))
            throw new DomainException("This space has bookings. Turn it off instead.");
        db.Spaces.Remove(s);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Booking ----------

    /// <summary>A floor on a day: every desk and room with its bookings.</summary>
    [HttpGet("floors/{id:guid}/day")]
    public async Task<FloorDayDto> FloorDay(Guid id, [FromQuery] DateOnly? date)
    {
        await desks.ReleaseNoShowsAsync();
        var day = date ?? await clock.TodayAsync();
        var floor = await db.Floors.Include(f => f.Office).FirstOrDefaultAsync(f => f.Id == id) ?? throw new NotFoundException("Floor");
        var spaces = await db.Spaces.Where(s => s.FloorId == id && s.IsActive).OrderBy(s => s.Kind).ThenBy(s => s.Name).ToListAsync();
        var ids = spaces.Select(s => s.Id).ToList();
        var bookings = await db.SpaceBookings.Where(b => ids.Contains(b.SpaceId) && b.Date == day && DeskService.Live.Contains(b.Status))
            .OrderBy(b => b.From).Select(b => new { b.Id, b.SpaceId, b.EmployeeId, Name = b.Employee!.FirstName + " " + b.Employee.LastName, b.From, b.To, b.Title, b.Status })
            .ToListAsync();
        var owners = spaces.Where(s => s.AssignedEmployeeId != null).Select(s => s.AssignedEmployeeId!.Value).ToList();
        var ownerNames = await db.Employees.Where(e => owners.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        var list = spaces.Select(s =>
        {
            var b = bookings.Where(x => x.SpaceId == s.Id).Select(x => new SlotDto(x.Id, x.EmployeeId, x.Name, x.From, x.To, s.Kind == SpaceKind.Desk ? null : x.Title, x.Status, x.EmployeeId == me.EmployeeId)).ToList();
            var free = s.AssignedEmployeeId is null && (s.Kind != SpaceKind.Desk || b.Count == 0);
            return new SpaceDayDto(s.Id, s.Name, s.Kind, s.Seats, s.Features, s.AssignedEmployeeId, s.AssignedEmployeeId is { } o ? ownerNames.GetValueOrDefault(o) : null, b, free);
        }).ToList();
        var deskList = list.Where(s => s.Kind == SpaceKind.Desk).ToList();
        return new FloorDayDto(day, floor.Id, floor.Name, floor.Office!.Name, list, deskList.Count(s => s.Free), deskList.Count);
    }

    [HttpPost("bookings")]
    public async Task<IActionResult> Book(BookRequest req)
    {
        await desks.BookAsync(new DeskBookingInput(req.SpaceId, req.Date, req.From, req.To, req.Title, req.Attendees, req.ForEmployeeId));
        return NoContent();
    }

    [HttpPost("bookings/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await desks.CancelAsync(id);
        return NoContent();
    }

    [HttpPost("bookings/{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id)
    {
        await desks.CheckInAsync(id);
        return NoContent();
    }

    [HttpGet("bookings/my")]
    public async Task<List<MyBookingDto>> My()
    {
        await desks.ReleaseNoShowsAsync();
        var today = await clock.TodayAsync();
        var settings = await desks.SettingsAsync();
        var mine = me.EmployeeId ?? Guid.Empty;
        var rows = await db.SpaceBookings.Where(b => b.EmployeeId == mine && b.Date >= today.AddDays(-14))
            .OrderBy(b => b.Date).ThenBy(b => b.From)
            .Select(b => new { b.Id, b.SpaceId, Space = b.Space!.Name, b.Space.Kind, Floor = b.Space.Floor!.Name, Office = b.Space.Floor.Office!.Name, b.Date, b.From, b.To, b.Title, b.Attendees, b.Status })
            .ToListAsync();
        return rows.Select(b => new MyBookingDto(b.Id, b.SpaceId, b.Space, b.Kind, b.Floor, b.Office, b.Date, b.From, b.To, b.Title, b.Attendees, b.Status,
            settings.RequireCheckIn && b.Status == BookingStatus.Booked && b.Date == today)).ToList();
    }

    /// <summary>Colleagues with a desk booked in the office on a day.</summary>
    [HttpGet("in-office")]
    public async Task<List<InOfficeDto>> InOffice([FromQuery] DateOnly? date)
    {
        var day = date ?? await clock.TodayAsync();
        var booked = await db.SpaceBookings.Where(b => b.Date == day && DeskService.Live.Contains(b.Status) && b.Space!.Kind == SpaceKind.Desk)
            .Select(b => new InOfficeDto(b.EmployeeId, b.Employee!.FirstName + " " + b.Employee.LastName, b.Employee.Department != null ? b.Employee.Department.Name : null,
                b.Space!.Name, b.Space.Floor!.Name)).ToListAsync();
        var permanent = await db.Spaces.Where(s => s.IsActive && s.AssignedEmployeeId != null && s.Kind == SpaceKind.Desk)
            .Select(s => new { s.AssignedEmployeeId, s.Name, Floor = s.Floor!.Name }).ToListAsync();
        var ids = permanent.Select(p => p.AssignedEmployeeId!.Value).Where(id => booked.All(b => b.EmployeeId != id)).ToList();
        var onLeave = await db.LeaveRequests.Where(l => l.Status == LeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day && ids.Contains(l.EmployeeId)).Select(l => l.EmployeeId).ToListAsync();
        var people = await db.Employees.Where(e => ids.Contains(e.Id) && !onLeave.Contains(e.Id))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : null }).ToListAsync();
        booked.AddRange(people.Select(p =>
        {
            var desk = permanent.First(x => x.AssignedEmployeeId == p.Id);
            return new InOfficeDto(p.Id, p.Name, p.Department, desk.Name, desk.Floor);
        }));
        return booked.OrderBy(b => b.Name).ToList();
    }

    [HttpGet("utilisation"), Authorize(Policy = Policies.Hr)]
    public async Task<UtilisationDto> Utilisation([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var today = await clock.TodayAsync();
        var end = to ?? today;
        var start = from ?? end.AddDays(-29);
        var days = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(i => start.AddDays(i)).Count(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        var spaces = await db.Spaces.Where(s => s.IsActive).Select(s => new { s.Id, s.Name, Floor = s.Floor!.Name, s.Kind }).ToListAsync();
        var bookings = await db.SpaceBookings.Where(b => b.Date >= start && b.Date <= end && b.Status != BookingStatus.Cancelled)
            .Select(b => new { b.SpaceId, b.Status, b.Date }).ToListAsync();
        var rows = spaces.Select(s =>
        {
            var mine = bookings.Where(b => b.SpaceId == s.Id).ToList();
            var usedDays = mine.Where(b => b.Status != BookingStatus.Released).Select(b => b.Date).Distinct().Count();
            return new UtilisationRowDto(s.Name, s.Floor, s.Kind, mine.Count, mine.Count(b => b.Status == BookingStatus.CheckedIn), mine.Count(b => b.Status == BookingStatus.Released),
                days == 0 ? 0 : Math.Round(100.0 * usedDays / days, 1));
        }).OrderByDescending(r => r.UsedPercent).ToList();
        var deskRows = rows.Where(r => r.Kind == SpaceKind.Desk).ToList();
        return new UtilisationDto(days, bookings.Count, bookings.Count(b => b.Status == BookingStatus.CheckedIn), bookings.Count(b => b.Status == BookingStatus.Released),
            deskRows.Count == 0 ? 0 : Math.Round(deskRows.Average(r => r.UsedPercent), 1), rows);
    }

    // ---------- Helpers ----------

    private async Task ApplyAsync(Floor f, SaveFloorRequest r)
    {
        if (!await db.OfficeLocations.AnyAsync(o => o.Id == r.OfficeLocationId))
            throw new NotFoundException("Office");
        var name = r.Name.Trim();
        if (await db.Floors.AnyAsync(x => x.OfficeLocationId == r.OfficeLocationId && x.Name == name && x.Id != f.Id))
            throw new DomainException("This office already has a floor with that name.");
        f.OfficeLocationId = r.OfficeLocationId;
        f.Name = name;
        f.Order = r.Order;
    }

    private async Task ApplyAsync(Space s, SaveSpaceRequest r)
    {
        if (!await db.Floors.AnyAsync(f => f.Id == r.FloorId))
            throw new NotFoundException("Floor");
        if (!Enum.IsDefined(r.Kind))
            throw new DomainException("Choose desk, meeting room or cabin.");
        var name = r.Name.Trim();
        if (await db.Spaces.AnyAsync(x => x.FloorId == r.FloorId && x.Name == name && x.Id != s.Id))
            throw new DomainException("This floor already has a space with that name.");
        if (r.Kind != SpaceKind.Desk && r.Seats is < 1 or > 500)
            throw new DomainException("Seats are 1 to 500.");
        if (r.AssignedEmployeeId is { } a)
        {
            if (!await db.Employees.AnyAsync(e => e.Id == a))
                throw new NotFoundException("Employee");
            if (await db.Spaces.AnyAsync(x => x.AssignedEmployeeId == a && x.Id != s.Id && x.Kind == r.Kind))
                throw new DomainException("This person already has a permanent one.");
        }
        s.FloorId = r.FloorId;
        s.Name = name;
        s.Kind = r.Kind;
        s.Seats = r.Kind == SpaceKind.Desk ? 1 : r.Seats;
        s.Features = string.IsNullOrWhiteSpace(r.Features) ? null : r.Features.Trim();
        s.AssignedEmployeeId = r.AssignedEmployeeId;
        s.IsActive = r.IsActive;
    }
}
