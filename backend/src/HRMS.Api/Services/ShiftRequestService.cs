using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>
/// Shift change (another shift on some days) and shift swap (exchange schedules with a colleague, who must agree
/// first). Both then go through the shift-change approval chain and are written to the roster once approved.
/// </summary>
public class ShiftRequestService(
    HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, ScheduleService schedule, NotificationService notify)
{
    public const int MaxDays = 31;

    public async Task<ShiftRequest> RequestChangeAsync(Guid employeeId, DateOnly from, DateOnly to, Guid shiftId, string reason)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId) ?? throw new NotFoundException("Shift");
        await ValidateAsync(employeeId, from, to, reason);
        var calendar = await schedule.ForEmployeeAsync(employeeId, from, to);
        if (Days(from, to).Where(calendar.IsWorkingDay).All(d => calendar.ShiftOn(d)?.Id == shiftId))
            throw new DomainException($"You are already on {shift.Name} on the working days you chose.");

        var request = new ShiftRequest { EmployeeId = employeeId, Kind = ShiftRequestKind.Change, FromDate = from, ToDate = to, ShiftId = shiftId, Reason = reason.Trim() };
        db.ShiftRequests.Add(request);
        await StartChainAsync(request, $"Shift change to {shift.Name} · {Range(from, to)}");
        await db.SaveChangesAsync();
        return request;
    }

    public async Task<ShiftRequest> RequestSwapAsync(Guid employeeId, Guid colleagueId, DateOnly from, DateOnly to, string reason)
    {
        if (colleagueId == employeeId)
            throw new DomainException("Choose a colleague to swap with.");
        var colleague = await db.Employees.FirstOrDefaultAsync(e => e.Id == colleagueId) ?? throw new NotFoundException("Colleague");
        if (!colleague.IsCurrent)
            throw new DomainException($"{colleague.FullName} no longer works here.");
        await ValidateAsync(employeeId, from, to, reason);
        if (await OverlapsAsync(colleagueId, from, to))
            throw new DomainException($"{colleague.FirstName} already has a shift request on some of those days.");
        var calendars = await schedule.ForEmployeesAsync([employeeId, colleagueId], from, to);
        if (Days(from, to).All(d => Same(calendars[employeeId], calendars[colleagueId], d)))
            throw new DomainException($"You and {colleague.FirstName} have the same schedule on those days, so there is nothing to swap.");

        var request = new ShiftRequest
        {
            EmployeeId = employeeId, Kind = ShiftRequestKind.Swap, FromDate = from, ToDate = to, SwapWithEmployeeId = colleagueId,
            Reason = reason.Trim(), Status = ShiftRequestStatus.AwaitingColleague,
        };
        db.ShiftRequests.Add(request);
        var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        await notify.ToEmployeesAsync([colleagueId], NotificationKind.ApprovalNeeded,
            $"{name} wants to swap shifts with you", $"{Range(from, to)} · {request.Reason}", "/me/attendance");
        await db.SaveChangesAsync();
        return request;
    }

    /// <summary>The colleague agrees (the request then goes to the approvers) or says no.</summary>
    public async Task RespondAsync(Guid id, bool accept)
    {
        var request = await db.ShiftRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Shift request");
        if (request.SwapWithEmployeeId != me.EmployeeId)
            throw new ForbiddenException("Only the colleague asked can answer this swap.");
        if (request.Status != ShiftRequestStatus.AwaitingColleague)
            throw new DomainException("This swap is no longer waiting for your answer.");
        request.ColleagueRespondedAtUtc = clock.UtcNow;
        var colleague = await db.Employees.Where(e => e.Id == request.SwapWithEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        if (!accept)
        {
            request.Status = ShiftRequestStatus.Declined;
            await notify.ToEmployeesAsync([request.EmployeeId], NotificationKind.RequestDecided,
                $"{colleague} declined your shift swap", Range(request.FromDate, request.ToDate), "/me/attendance");
            await db.SaveChangesAsync();
            return;
        }
        await schedule.EnsureOpenAsync(request.FromDate, request.ToDate);
        request.Status = ShiftRequestStatus.Pending;
        await StartChainAsync(request, $"Shift swap with {colleague} · {Range(request.FromDate, request.ToDate)}");
        await db.SaveChangesAsync();
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var request = await db.ShiftRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Shift request");
        if (request.Status != ShiftRequestStatus.Pending)
            throw new DomainException("This request is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.ShiftChange, request.Id, request.EmployeeId, approve, note);
        if (outcome != ApprovalStatus.Pending)
            await CloseAsync(request, outcome == ApprovalStatus.Approved, note);
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid id)
    {
        var request = await db.ShiftRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Shift request");
        if (request.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this request.");
        if (request.Status is not (ShiftRequestStatus.Pending or ShiftRequestStatus.AwaitingColleague))
            throw new DomainException("Only a request that is still waiting can be cancelled.");
        if (request.Status == ShiftRequestStatus.Pending)
            await approvals.CancelAsync(ApprovalRequestType.ShiftChange, request.Id);
        request.Status = ShiftRequestStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    private async Task StartChainAsync(ShiftRequest request, string summary)
    {
        var approval = await approvals.StartAsync(ApprovalRequestType.ShiftChange, request.Id, request.EmployeeId, summary);
        if (approval.Status == ApprovalStatus.Approved)
            await CloseAsync(request, approved: true, "Approved automatically");
    }

    /// <summary>Approved: writes the new days to the roster. Off days stay off on a change; a swap exchanges everything.</summary>
    private async Task CloseAsync(ShiftRequest request, bool approved, string? note)
    {
        request.Status = approved ? ShiftRequestStatus.Approved : ShiftRequestStatus.Rejected;
        request.DecidedAtUtc = clock.UtcNow;
        request.DecisionNote = note;
        if (!approved)
            return;
        await schedule.EnsureOpenAsync(request.FromDate, request.ToDate);
        var (from, to) = (request.FromDate, request.ToDate);
        if (request.Kind == ShiftRequestKind.Change)
        {
            var calendar = await schedule.ForEmployeeAsync(request.EmployeeId, from, to);
            foreach (var d in Days(from, to).Where(calendar.IsWorkingDay))
                await SetAsync(request.EmployeeId, d, request.ShiftId, off: false, ShiftAssignmentSource.Request);
            return;
        }
        var other = request.SwapWithEmployeeId!.Value;
        var calendars = await schedule.ForEmployeesAsync([request.EmployeeId, other], from, to);
        var (mine, theirs) = (calendars[request.EmployeeId], calendars[other]);
        // Read both days before writing: the schedules share the tracked roster entries being changed.
        var days = Days(from, to).Where(d => !Same(mine, theirs, d))
            .Select(d => (Day: d, Mine: (mine.ShiftOn(d)?.Id, mine.IsOff(d)), Theirs: (theirs.ShiftOn(d)?.Id, theirs.IsOff(d))))
            .ToList();
        foreach (var (d, m, t) in days)
        {
            await SetAsync(request.EmployeeId, d, t.Item1, t.Item2, ShiftAssignmentSource.Swap);
            await SetAsync(other, d, m.Item1, m.Item2, ShiftAssignmentSource.Swap);
        }
    }

    private async Task SetAsync(Guid employeeId, DateOnly date, Guid? shiftId, bool off, ShiftAssignmentSource source)
    {
        var a = db.ShiftAssignments.Local.FirstOrDefault(x => x.EmployeeId == employeeId && x.Date == date)
                ?? await db.ShiftAssignments.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date);
        if (a is null)
        {
            a = new ShiftAssignment { EmployeeId = employeeId, Date = date };
            db.ShiftAssignments.Add(a);
        }
        a.ShiftId = off ? null : shiftId;
        a.IsOff = off || shiftId is null;
        a.Source = source;
    }

    private async Task ValidateAsync(Guid employeeId, DateOnly from, DateOnly to, string reason)
    {
        if (employeeId != me.EmployeeId)
            throw new ForbiddenException("You can only request your own shifts. HR changes others' shifts on the roster.");
        var today = await clock.TodayAsync();
        if (from < today)
            throw new DomainException("Shift requests are for today or later. For past days, send an attendance request.");
        if (to < from)
            throw new DomainException("The end date is before the start date.");
        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
            throw new DomainException($"Ask for at most {MaxDays} days at a time.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Give a reason for your approver.");
        await schedule.EnsureOpenAsync(from, to);
        if (await OverlapsAsync(employeeId, from, to))
            throw new DomainException("You already have a shift request waiting on some of those days.");
    }

    private Task<bool> OverlapsAsync(Guid employeeId, DateOnly from, DateOnly to) =>
        db.ShiftRequests.AnyAsync(r => (r.EmployeeId == employeeId || r.SwapWithEmployeeId == employeeId)
                                       && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.AwaitingColleague)
                                       && r.FromDate <= to && r.ToDate >= from);

    private static bool Same(EmployeeSchedule a, EmployeeSchedule b, DateOnly d) =>
        a.IsOff(d) == b.IsOff(d) && a.ShiftOn(d)?.Id == b.ShiftOn(d)?.Id;

    private static IEnumerable<DateOnly> Days(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
            yield return d;
    }

    private static string Range(DateOnly from, DateOnly to) => from == to ? $"{from:d MMM}" : $"{from:d MMM} – {to:d MMM}";
}
