using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record DeskBookingInput(Guid SpaceId, DateOnly Date, TimeOnly? From, TimeOnly? To, string? Title, int Attendees, Guid? ForEmployeeId);

/// <summary>
/// Desk and room booking: desks for a whole day, rooms and cabins by the hour. One desk per person per day;
/// rooms cannot overlap. With check-in required, bookings nobody checks into are freed after a while.
/// </summary>
public class DeskService(HrmsDbContext db, ICurrentUser me, TenantClock clock)
{
    public static readonly BookingStatus[] Live = [BookingStatus.Booked, BookingStatus.CheckedIn];

    public async Task<DeskSettings> SettingsAsync() => await db.DeskSettings.FirstOrDefaultAsync() ?? new DeskSettings();

    public async Task<SpaceBooking> BookAsync(DeskBookingInput input)
    {
        var employeeId = input.ForEmployeeId is { } other && me.IsHr() ? other : me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var space = await db.Spaces.Include(s => s.Floor).FirstOrDefaultAsync(s => s.Id == input.SpaceId && s.IsActive) ?? throw new NotFoundException("Space");
        var settings = await SettingsAsync();
        var today = await clock.TodayAsync();
        if (input.Date < today)
            throw new DomainException("That day has passed.");
        if (input.Date > today.AddDays(settings.MaxDaysAhead))
            throw new DomainException($"You can book up to {settings.MaxDaysAhead} days ahead.");
        if (space.AssignedEmployeeId is { } owner && owner != employeeId)
            throw new DomainException($"{space.Name} is someone's permanent desk.");

        TimeOnly? from = input.From, to = input.To;
        if (space.Kind == SpaceKind.Desk)
        {
            from = null;
            to = null;
        }
        else
        {
            if (from is not { } f || to is not { } t || t <= f)
                throw new DomainException("Choose a start and a later end time.");
            if ((t - f).TotalHours > 12)
                throw new DomainException("A room can be booked for at most 12 hours.");
            if (input.Date == today && f < TimeOnly.FromDateTime(await clock.LocalNowAsync()).AddMinutes(-15))
                throw new DomainException("That time has passed.");
            if (input.Attendees < 1 || input.Attendees > space.Seats)
                throw new DomainException($"{space.Name} seats {space.Seats}.");
        }
        await ReleaseNoShowsAsync();

        var taken = await db.SpaceBookings.Where(b => b.SpaceId == space.Id && b.Date == input.Date && Live.Contains(b.Status)).ToListAsync();
        if (taken.Any(b => Overlaps(b.From, b.To, from, to)))
            throw new DomainException(space.Kind == SpaceKind.Desk ? $"{space.Name} is already booked that day." : $"{space.Name} is booked at that time.");
        if (space.Kind == SpaceKind.Desk && await db.SpaceBookings.AnyAsync(b => b.EmployeeId == employeeId && b.Date == input.Date && Live.Contains(b.Status) && b.Space!.Kind == SpaceKind.Desk))
            throw new DomainException(employeeId == me.EmployeeId ? "You already have a desk that day." : "They already have a desk that day.");

        var booking = new SpaceBooking
        {
            SpaceId = space.Id,
            EmployeeId = employeeId,
            Date = input.Date,
            From = from,
            To = to,
            Title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim(),
            Attendees = space.Kind == SpaceKind.Desk ? 1 : input.Attendees,
        };
        db.SpaceBookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    public async Task CancelAsync(Guid id)
    {
        var b = await db.SpaceBookings.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Booking");
        if (b.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this booking.");
        if (!Live.Contains(b.Status))
            throw new DomainException("This booking is no longer active.");
        if (b.Date < await clock.TodayAsync())
            throw new DomainException("This booking is in the past.");
        b.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    /// <summary>The person says they are here: from 30 minutes before the start until it ends.</summary>
    public async Task CheckInAsync(Guid id)
    {
        await ReleaseNoShowsAsync();
        var b = await db.SpaceBookings.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == me.EmployeeId) ?? throw new NotFoundException("Booking");
        if (b.Status == BookingStatus.CheckedIn)
            return;
        if (b.Status != BookingStatus.Booked)
            throw new DomainException(b.Status == BookingStatus.Released ? "This booking was released because nobody checked in." : "This booking is no longer active.");
        var now = await clock.LocalNowAsync();
        var settings = await SettingsAsync();
        var start = b.Date.ToDateTime(b.From ?? new TimeOnly(settings.DayStartsAt, 0));
        var end = b.Date.ToDateTime(b.To ?? new TimeOnly(23, 59));
        if (now < start.AddMinutes(-30) || now > end)
            throw new DomainException("Check in on the day, from half an hour before the start.");
        b.Status = BookingStatus.CheckedIn;
        b.CheckedInAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Frees today's bookings nobody checked into in time (only when the company requires check-in).</summary>
    public async Task ReleaseNoShowsAsync()
    {
        var settings = await SettingsAsync();
        if (!settings.RequireCheckIn)
            return;
        var now = await clock.LocalNowAsync();
        var today = DateOnly.FromDateTime(now);
        var open = await db.SpaceBookings.Where(b => b.Date <= today && b.Status == BookingStatus.Booked).ToListAsync();
        foreach (var b in open)
        {
            var start = b.Date.ToDateTime(b.From ?? new TimeOnly(settings.DayStartsAt, 0));
            if (now > start.AddMinutes(settings.ReleaseAfterMinutes))
                b.Status = BookingStatus.Released;
        }
        if (open.Any(b => b.Status == BookingStatus.Released))
            await db.SaveChangesAsync();
    }

    /// <summary>Whole-day bookings (null times) overlap everything on that day.</summary>
    public static bool Overlaps(TimeOnly? aFrom, TimeOnly? aTo, TimeOnly? bFrom, TimeOnly? bTo) =>
        aFrom is null || bFrom is null || (aFrom < bTo && bFrom < aTo);
}
