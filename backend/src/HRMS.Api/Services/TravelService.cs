using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record TravelLegInput(string From, string To, DateOnly Date, TravelMode Mode, string? Preference);

public record TravelInput(
    Guid? ReasonId, string Purpose, TripType TripType, bool IsInternational, DateOnly? ReturnOn, List<TravelLegInput> Legs, string? UrgentReason,
    bool NeedsHotel, string? HotelCity, DateOnly? CheckIn, DateOnly? CheckOut, bool NeedsPickDrop, string? PickDropNotes, decimal? EstimatedCost,
    decimal? AdvanceRequested);

public record LegBooking(Guid LegId, string? BookingRef, decimal? BookingCost);

public record BookingInput(List<LegBooking> Legs, string? HotelName, string? HotelRef, decimal? HotelCost, string? Notes);

public record TravelExpenseInput(DateOnly Date, ExpenseCategory Category, string Description, decimal Amount);

public record TravelSettlementInput(List<TravelExpenseInput> Expenses, int PerDiemDays, string? Note);

/// <summary>
/// Business travel: the request (route, hotel, pick and drop, advance) goes through the travel approval chain, the
/// travel desk books it and pays the advance, and after the trip the traveller settles expenses and daily allowance.
/// The approved settlement reaches payroll as a reimbursement, or as a recovery when the advance was more.
/// </summary>
public class TravelService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, NotificationService notify)
{
    public async Task<TravelSettings> SettingsAsync() => await db.TravelSettings.FirstOrDefaultAsync() ?? new TravelSettings();

    /// <summary>HR and the travel desk book trips, pay advances and see every trip.</summary>
    public async Task<bool> IsDeskAsync()
    {
        if (me.IsHr())
            return true;
        var s = await SettingsAsync();
        return HelpdeskService.Has(s.DeskIds, me.EmployeeId);
    }

    /// <summary>The trip, if the signed-in user is the traveller, an approver, the desk or HR.</summary>
    public async Task<TravelRequest> LoadAsync(Guid id)
    {
        var t = await db.TravelRequests.Include(x => x.Legs).Include(x => x.Expenses).Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("Trip");
        if (t.EmployeeId == me.EmployeeId || await IsDeskAsync() || await approvals.IsApproverAsync(ApprovalRequestType.Travel, t.Id, t.EmployeeId))
            return t;
        throw new NotFoundException("Trip");
    }

    public async Task<TravelRequest> RequestAsync(TravelInput input)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var settings = await SettingsAsync();
        var today = await clock.TodayAsync();

        if (string.IsNullOrWhiteSpace(input.Purpose))
            throw new DomainException("Say what the trip is for.");
        if (!Enum.IsDefined(input.TripType))
            throw new DomainException("Choose one way, return or multi-city.");
        if (input.ReasonId is { } rid && !await db.TravelReasons.AnyAsync(r => r.Id == rid && r.IsActive))
            throw new NotFoundException("Reason");
        var legs = input.Legs ?? [];
        var needed = input.TripType switch { TripType.OneWay => 1, TripType.Return => 2, _ => 2 };
        if (input.TripType == TripType.MultiCity ? legs.Count < 2 || legs.Count > 10 : legs.Count != needed)
            throw new DomainException(input.TripType switch
            {
                TripType.OneWay => "A one-way trip has one journey.",
                TripType.Return => "A return trip has the outward and the return journey.",
                _ => "A multi-city trip has 2 to 10 journeys.",
            });
        for (var i = 0; i < legs.Count; i++)
        {
            var l = legs[i];
            if (string.IsNullOrWhiteSpace(l.From) || string.IsNullOrWhiteSpace(l.To))
                throw new DomainException($"Journey {i + 1}: enter where from and where to.");
            if (string.Equals(l.From.Trim(), l.To.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new DomainException($"Journey {i + 1}: from and to are the same.");
            if (!Enum.IsDefined(l.Mode))
                throw new DomainException($"Journey {i + 1}: choose how you travel.");
            if (i > 0 && l.Date < legs[i - 1].Date)
                throw new DomainException($"Journey {i + 1} is before journey {i}.");
        }
        var depart = legs[0].Date;
        if (depart < today)
            throw new DomainException("The trip starts in the past.");
        var back = input.TripType == TripType.OneWay ? input.ReturnOn ?? depart : legs[^1].Date;
        if (back < depart)
            throw new DomainException("The trip ends before it starts.");
        if ((back.DayNumber - depart.DayNumber) > 180)
            throw new DomainException("A trip can last at most 180 days.");
        var late = depart.DayNumber - today.DayNumber < settings.MinNoticeDays;
        if (late && string.IsNullOrWhiteSpace(input.UrgentReason))
            throw new DomainException($"Trips need {settings.MinNoticeDays} days' notice. Say why this one is urgent.");
        if (input.NeedsHotel)
        {
            if (string.IsNullOrWhiteSpace(input.HotelCity))
                throw new DomainException("Say which city you need a hotel in.");
            if (input.CheckIn is not { } ci || input.CheckOut is not { } co || co <= ci)
                throw new DomainException("Enter the hotel check-in and a later check-out date.");
            if (ci < depart || co > back.AddDays(1))
                throw new DomainException("The hotel dates must fall within the trip.");
        }
        if (input.AdvanceRequested is < 0 || input.EstimatedCost is < 0)
            throw new DomainException("Amounts cannot be negative.");
        if (await db.TravelRequests.AnyAsync(t => t.EmployeeId == employeeId && t.Status != TravelStatus.Rejected && t.Status != TravelStatus.Cancelled
                                                  && t.DepartOn <= back && t.ReturnOn >= depart))
            throw new DomainException("You already have a trip on these dates.");

        var trip = new TravelRequest
        {
            EmployeeId = employeeId,
            ReasonId = input.ReasonId,
            Purpose = input.Purpose.Trim(),
            TripType = input.TripType,
            IsInternational = input.IsInternational,
            DepartOn = depart,
            ReturnOn = back,
            UrgentReason = late ? Clean(input.UrgentReason, 500) : null,
            NeedsHotel = input.NeedsHotel,
            HotelCity = input.NeedsHotel ? Clean(input.HotelCity, 100) : null,
            CheckIn = input.NeedsHotel ? input.CheckIn : null,
            CheckOut = input.NeedsHotel ? input.CheckOut : null,
            NeedsPickDrop = input.NeedsPickDrop,
            PickDropNotes = input.NeedsPickDrop ? Clean(input.PickDropNotes, 500) : null,
            EstimatedCost = input.EstimatedCost is > 0 ? input.EstimatedCost : null,
            AdvanceRequested = input.AdvanceRequested is > 0 ? input.AdvanceRequested : null,
        };
        for (var i = 0; i < legs.Count; i++)
            trip.Legs.Add(new TravelLeg
            {
                TravelRequestId = trip.Id, Order = i + 1, From = legs[i].From.Trim(), To = legs[i].To.Trim(), Date = legs[i].Date, Mode = legs[i].Mode,
                Preference = Clean(legs[i].Preference, 150),
            });
        db.TravelRequests.Add(trip);

        var approval = await approvals.StartAsync(ApprovalRequestType.Travel, trip.Id, employeeId, Summary(trip));
        if (approval.Status == ApprovalStatus.Approved)
            await ApproveAsync(trip, "Approved automatically");
        await db.SaveChangesAsync();
        return trip;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var trip = await db.TravelRequests.Include(t => t.Legs).FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Trip");
        if (trip.Status != TravelStatus.Pending)
            throw new DomainException("This trip is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Travel, trip.Id, trip.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
            await ApproveAsync(trip, note);
        else if (outcome == ApprovalStatus.Rejected)
        {
            trip.Status = TravelStatus.Rejected;
            trip.DecidedAtUtc = clock.UtcNow;
            trip.DecisionNote = Clean(note, 1000);
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid id, string? reason)
    {
        var trip = await LoadAsync(id);
        if (trip.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("Only the traveller or HR can cancel a trip.");
        if (trip.Status is not (TravelStatus.Pending or TravelStatus.Approved or TravelStatus.Booked))
            throw new DomainException("This trip can no longer be cancelled.");
        if (trip.DepartOn <= await clock.TodayAsync() && trip.Status == TravelStatus.Booked)
            throw new DomainException("The trip has started. Settle it instead.");
        if (trip.Status == TravelStatus.Pending)
            await approvals.CancelAsync(ApprovalRequestType.Travel, trip.Id);
        var wasBooked = trip.Status == TravelStatus.Booked;
        trip.Status = TravelStatus.Cancelled;
        trip.DecisionNote = Clean(reason, 1000) ?? trip.DecisionNote;
        if (wasBooked || trip.AdvancePaid is > 0)
            await NotifyDeskAsync($"Trip cancelled: {trip.Employee!.FullName}, {Route(trip)}",
                (wasBooked ? "Cancel the tickets and hotel. " : "") + (trip.AdvancePaid is > 0 ? $"An advance of PKR {trip.AdvancePaid:N0} was paid; settle it to recover it." : ""),
                trip.Id);
        await db.SaveChangesAsync();
    }

    /// <summary>Desk: records the tickets and hotel and tells the traveller. Can be updated until the trip is settled.</summary>
    public async Task BookAsync(Guid id, BookingInput input)
    {
        await RequireDeskAsync();
        var trip = await LoadAsync(id);
        if (trip.Status is not (TravelStatus.Approved or TravelStatus.Booked))
            throw new DomainException(trip.Status == TravelStatus.Pending ? "The trip is still waiting for approval." : "This trip cannot be booked.");
        if (trip.Settlement == SettlementStatus.Settled)
            throw new DomainException("This trip is settled.");
        foreach (var b in input.Legs ?? [])
        {
            var leg = trip.Legs.FirstOrDefault(l => l.Id == b.LegId) ?? throw new NotFoundException("Journey");
            if (b.BookingCost is < 0)
                throw new DomainException("Costs cannot be negative.");
            leg.BookingRef = Clean(b.BookingRef, 100);
            leg.BookingCost = b.BookingCost;
        }
        if (input.HotelCost is < 0)
            throw new DomainException("Costs cannot be negative.");
        trip.HotelName = Clean(input.HotelName, 150);
        trip.HotelRef = Clean(input.HotelRef, 100);
        trip.HotelCost = input.HotelCost;
        trip.BookingNotes = Clean(input.Notes, 2000);
        var first = trip.Status != TravelStatus.Booked;
        trip.Status = TravelStatus.Booked;
        trip.BookedAtUtc = clock.UtcNow;
        trip.BookedByName = await LetterService.CurrentUserNameAsync(db, me);
        await notify.ToEmployeesAsync([trip.EmployeeId], NotificationKind.Travel,
            first ? $"Your trip to {Destination(trip)} is booked" : $"Your booking for {Destination(trip)} was updated",
            string.Join(" · ", trip.Legs.OrderBy(l => l.Order).Select(l => $"{l.From}→{l.To} {l.Date:d MMM}" + (l.BookingRef is null ? "" : $" ({l.BookingRef})")))
            + (trip.HotelName is null ? "" : $" · Hotel: {trip.HotelName}"), $"/travel/{trip.Id}");
        await db.SaveChangesAsync();
    }

    public async Task PayAdvanceAsync(Guid id, decimal amount, DateOnly paidOn, string? via)
    {
        await RequireDeskAsync();
        var trip = await LoadAsync(id);
        if (trip.Status is not (TravelStatus.Approved or TravelStatus.Booked))
            throw new DomainException("Advances are paid on approved trips.");
        if (trip.Settlement is SettlementStatus.Submitted or SettlementStatus.Settled)
            throw new DomainException("The trip is being settled.");
        if (amount <= 0)
            throw new DomainException("Enter the amount paid.");
        if (paidOn > await clock.TodayAsync())
            throw new DomainException("Record the advance on the day it is paid.");
        trip.AdvancePaid = amount;
        trip.AdvancePaidOn = paidOn;
        trip.AdvancePaidVia = Clean(via, 100);
        await notify.ToEmployeesAsync([trip.EmployeeId], NotificationKind.Travel, $"Travel advance of PKR {amount:N0} paid",
            $"For your trip to {Destination(trip)}. Settle it after you are back.", $"/travel/{trip.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>Traveller: expenses and days of allowance after the trip. Replaces an earlier draft or returned settlement.</summary>
    public async Task SubmitSettlementAsync(Guid id, TravelSettlementInput input)
    {
        var trip = await LoadAsync(id);
        if (trip.EmployeeId != me.EmployeeId)
            throw new ForbiddenException("Only the traveller settles the trip.");
        if (trip.Status is not (TravelStatus.Approved or TravelStatus.Booked))
            throw new DomainException("Only approved trips are settled.");
        if (trip.Settlement is SettlementStatus.Submitted or SettlementStatus.Settled)
            throw new DomainException(trip.Settlement == SettlementStatus.Settled ? "This trip is settled." : "The settlement is waiting for HR.");
        if (trip.DepartOn > await clock.TodayAsync())
            throw new DomainException("Settle the trip once it has started.");
        var settings = await SettingsAsync();
        var maxDays = trip.ReturnOn.DayNumber - trip.DepartOn.DayNumber + 1;
        if (input.PerDiemDays < 0 || input.PerDiemDays > maxDays)
            throw new DomainException($"Daily allowance is for 0 to {maxDays} days.");
        var lines = input.Expenses ?? [];
        if (lines.Count > 50)
            throw new DomainException("At most 50 expense lines.");
        foreach (var l in lines)
        {
            if (l.Amount <= 0 || string.IsNullOrWhiteSpace(l.Description) || !Enum.IsDefined(l.Category))
                throw new DomainException("Each expense needs a category, a description and an amount.");
            if (l.Date < trip.DepartOn.AddDays(-7) || l.Date > trip.ReturnOn.AddDays(7))
                throw new DomainException($"{l.Description.Trim()}: the date is not around the trip.");
        }
        db.TravelExpenses.RemoveRange(trip.Expenses);
        trip.Expenses.Clear();
        foreach (var l in lines)
        {
            // Added through the set: a new child with its key already set would otherwise be taken for an existing row.
            var e = new TravelExpense { TravelRequestId = trip.Id, Date = l.Date, Category = l.Category, Description = l.Description.Trim(), Amount = l.Amount };
            db.TravelExpenses.Add(e);
            trip.Expenses.Add(e);
        }
        trip.PerDiemDays = input.PerDiemDays;
        trip.PerDiemRate = trip.IsInternational ? settings.PerDiemInternational : settings.PerDiemDomestic;
        trip.SettlementNote = Clean(input.Note, 2000);
        trip.Settlement = SettlementStatus.Submitted;
        trip.SettlementSubmittedAtUtc = clock.UtcNow;
        var net = Net(trip);
        await notify.ToHrAsync(NotificationKind.Travel, $"{trip.Employee!.FullName} settled the trip to {Destination(trip)}",
            net >= 0 ? $"PKR {net:N0} to reimburse" : $"PKR {-net:N0} to recover from salary", $"/travel/{trip.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// HR: approves the settlement and sends the difference to payroll (this month), or returns it to the traveller.
    /// </summary>
    public async Task DecideSettlementAsync(Guid id, bool approve, string? note)
    {
        if (!me.IsHr())
            throw new ForbiddenException("Only HR approves settlements.");
        var trip = await LoadAsync(id);
        if (trip.Settlement != SettlementStatus.Submitted)
            throw new DomainException("No settlement is waiting.");
        if (trip.EmployeeId == me.EmployeeId)
            throw new ForbiddenException("Someone else in HR must approve your own settlement.");
        trip.SettlementDecisionNote = Clean(note, 1000);
        if (!approve)
        {
            if (string.IsNullOrWhiteSpace(note))
                throw new DomainException("Say what needs fixing.");
            trip.Settlement = SettlementStatus.Returned;
            await notify.ToEmployeesAsync([trip.EmployeeId], NotificationKind.Travel, $"Your travel settlement was sent back", note, $"/travel/{trip.Id}");
            await db.SaveChangesAsync();
            return;
        }

        var net = Net(trip);
        var today = await clock.TodayAsync();
        if (net != 0)
        {
            var pay = new PayRequest
            {
                EmployeeId = trip.EmployeeId,
                Kind = net > 0 ? PayRequestKind.Allowance : PayRequestKind.Deduction,
                Title = net > 0 ? $"Travel reimbursement: {Destination(trip)}" : $"Travel advance recovery: {Destination(trip)}",
                Amount = Math.Abs(net),
                IsTaxable = false,
                Year = today.Year,
                Month = today.Month,
                Reason = $"Trip {trip.DepartOn:d MMM}–{trip.ReturnOn:d MMM yyyy}",
                Status = RequestStatus.Approved,
                DecidedAtUtc = clock.UtcNow,
                DecisionNote = "From the travel settlement",
            };
            db.PayRequests.Add(pay);
            trip.PayRequestId = pay.Id;
        }
        trip.Settlement = SettlementStatus.Settled;
        trip.SettledAtUtc = clock.UtcNow;
        await notify.ToEmployeesAsync([trip.EmployeeId], NotificationKind.Travel, $"Your trip to {Destination(trip)} is settled",
            net > 0 ? $"PKR {net:N0} will be paid with your {today:MMMM} salary."
            : net < 0 ? $"PKR {-net:N0} of unused advance will be recovered from your {today:MMMM} salary."
            : "Nothing is owed either way.", $"/travel/{trip.Id}");
        await db.SaveChangesAsync();
    }

    // ---------- Helpers ----------

    public static decimal Claimed(TravelRequest t) => t.Expenses.Sum(e => e.Amount) + (t.PerDiemDays ?? 0) * (t.PerDiemRate ?? 0);

    /// <summary>What the company owes the traveller (negative: what the traveller owes back).</summary>
    public static decimal Net(TravelRequest t) => Claimed(t) - (t.AdvancePaid ?? 0);

    public static decimal BookedCost(TravelRequest t) => t.Legs.Sum(l => l.BookingCost ?? 0) + (t.HotelCost ?? 0);

    public static string Destination(TravelRequest t)
    {
        var legs = t.Legs.OrderBy(l => l.Order).ToList();
        return legs.Count == 0 ? "" : t.TripType == TripType.MultiCity ? string.Join(", ", legs.Select(l => l.To).Distinct().Take(3)) : legs[0].To;
    }

    public static string Route(TravelRequest t)
    {
        var legs = t.Legs.OrderBy(l => l.Order).ToList();
        return legs.Count == 0 ? "" : string.Join(" → ", new[] { legs[0].From }.Concat(legs.Select(l => l.To)));
    }

    private static string Summary(TravelRequest t) =>
        $"{Route(t)} · {t.DepartOn:d MMM}" + (t.ReturnOn != t.DepartOn ? $"–{t.ReturnOn:d MMM}" : "") + $" · {t.Purpose}"
        + (t.UrgentReason is null ? "" : " · URGENT") + (t.AdvanceRequested is > 0 ? $" · advance PKR {t.AdvanceRequested:N0}" : "");

    private async Task ApproveAsync(TravelRequest trip, string? note)
    {
        trip.Status = TravelStatus.Approved;
        trip.DecidedAtUtc = clock.UtcNow;
        trip.DecisionNote = Clean(note, 1000);
        var name = await db.Employees.Where(e => e.Id == trip.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        var needs = new List<string> { "tickets" };
        if (trip.NeedsHotel) needs.Add("hotel");
        if (trip.NeedsPickDrop) needs.Add("pick and drop");
        if (trip.AdvanceRequested is > 0) needs.Add($"advance PKR {trip.AdvanceRequested:N0}");
        await NotifyDeskAsync($"Book travel for {name}: {Route(trip)}", $"{trip.DepartOn:d MMM yyyy} · {string.Join(", ", needs)}", trip.Id);
    }

    private async Task NotifyDeskAsync(string title, string? body, Guid tripId)
    {
        var desk = HelpdeskService.Ids((await SettingsAsync()).DeskIds);
        if (desk.Count > 0)
            await notify.ToEmployeesAsync(desk, NotificationKind.Travel, title, body, $"/travel/{tripId}");
        else
            await notify.ToHrAsync(NotificationKind.Travel, title, body, $"/travel/{tripId}");
    }

    private async Task RequireDeskAsync()
    {
        if (!await IsDeskAsync())
            throw new ForbiddenException("Only HR and the travel desk can do this.");
    }

    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();
}
