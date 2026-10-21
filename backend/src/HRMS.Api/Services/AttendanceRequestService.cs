using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>Missed punches, work from home, official duty and time corrections, applied to attendance once approved.</summary>
public class AttendanceRequestService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, AttendanceService attendance)
{
    /// <summary>How far back an employee may ask to fix attendance.</summary>
    public const int MaxDaysBack = 45;

    public static string Label(AttendanceRequestKind kind) => kind switch
    {
        AttendanceRequestKind.MissedPunch => "Missed punch",
        AttendanceRequestKind.WorkFromHome => "Work from home",
        AttendanceRequestKind.OfficialDuty => "Official duty",
        _ => "Time correction",
    };

    public async Task<AttendanceRequest> SubmitAsync(Guid employeeId, DateOnly date, AttendanceRequestKind kind, TimeOnly checkIn, TimeOnly? checkOut, string reason)
    {
        var today = await clock.TodayAsync();
        if (date > today)
            throw new DomainException(kind == AttendanceRequestKind.WorkFromHome
                ? "Ask for work from home on the day itself or afterwards; for future days apply for leave or ask your manager."
                : "You cannot request attendance for a future date.");
        if (date < today.AddDays(-MaxDaysBack))
            throw new DomainException($"Requests can go back at most {MaxDaysBack} days. Ask HR to correct older attendance.");
        if (checkOut is { } co && co <= checkIn)
            throw new DomainException("Check-out must be after check-in.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Give a reason so your approver knows what happened.");

        var joined = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.JoiningDate).FirstAsync();
        if (date < joined)
            throw new DomainException("That date is before your joining date.");
        if (await db.AttendanceRequests.AnyAsync(r => r.EmployeeId == employeeId && r.Date == date && r.Status == RequestStatus.Pending))
            throw new DomainException("You already have a pending request for this day.");
        if (await db.LeaveRequests.AnyAsync(r => r.EmployeeId == employeeId && r.Status == LeaveStatus.Approved && r.FromDate <= date && r.ToDate >= date))
            throw new DomainException("You are on approved leave that day. Cancel the leave first if you worked.");

        var request = new AttendanceRequest
        {
            EmployeeId = employeeId,
            Date = date,
            Kind = kind,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Reason = reason.Trim(),
        };
        db.AttendanceRequests.Add(request);
        var summary = $"{Label(kind)} · {date:d MMM} · {checkIn:HH\\:mm}" + (checkOut is { } o ? $"–{o:HH\\:mm}" : "");
        var approval = await approvals.StartAsync(ApprovalRequestType.Attendance, request.Id, employeeId, summary);
        await db.SaveChangesAsync();
        if (approval.Status == ApprovalStatus.Approved)
            await ApplyAsync(request, approved: true, "Approved automatically");
        return request;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var request = await db.AttendanceRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Attendance request");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException($"This request is already {request.Status.ToString().ToLowerInvariant()}.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Attendance, request.Id, request.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Pending)
            await db.SaveChangesAsync();
        else
            await ApplyAsync(request, outcome == ApprovalStatus.Approved, note);
    }

    public async Task CancelAsync(Guid id)
    {
        var request = await db.AttendanceRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Attendance request");
        if (request.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this request.");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException("Only a pending request can be cancelled.");
        request.Status = RequestStatus.Cancelled;
        await approvals.CancelAsync(ApprovalRequestType.Attendance, request.Id);
        await db.SaveChangesAsync();
    }

    /// <summary>Approved: writes the day's attendance with the requested times (late/half day from the shift).</summary>
    private async Task ApplyAsync(AttendanceRequest request, bool approved, string? note)
    {
        request.Status = approved ? RequestStatus.Approved : RequestStatus.Rejected;
        request.DecidedAtUtc = clock.UtcNow;
        request.DecisionNote = note;
        if (approved)
        {
            var text = $"{Label(request.Kind)}: {request.Reason}";
            await attendance.SaveManualAsync(request.EmployeeId, request.Date, request.CheckIn, request.CheckOut,
                AttendanceSource.Request, text.Length > 300 ? text[..300] : text);
        }
        else
            await db.SaveChangesAsync();
    }
}
