using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>Credit compensatory leave for work on a weekly off day or holiday, through the approval chain.</summary>
public class CompOffService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, LeaveService leave)
{
    public const int MaxDaysBack = 60;

    public async Task<CompOffRequest> SubmitAsync(Guid employeeId, DateOnly workedOn, decimal days, string reason)
    {
        var today = await clock.TodayAsync();
        if (workedOn > today)
            throw new DomainException("You can claim comp-off only for a day you have already worked.");
        if (workedOn < today.AddDays(-MaxDaysBack))
            throw new DomainException($"Comp-off can be claimed within {MaxDaysBack} days of the day worked.");
        if (days is not (0.5m or 1m))
            throw new DomainException("Claim a half day (0.5) or a full day (1).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Say what you worked on.");
        if (await leave.CountDaysAsync(employeeId, workedOn, workedOn) > 0)
            throw new DomainException("That is a normal working day. Comp-off is for work on a weekly off day or a holiday.");
        if (!await db.AttendanceRecords.AnyAsync(a => a.EmployeeId == employeeId && a.Date == workedOn))
            throw new DomainException("There is no attendance for that day. Check in when you work, or send an attendance request first.");
        if (await db.CompOffRequests.AnyAsync(c => c.EmployeeId == employeeId && c.WorkedOn == workedOn
                && (c.Status == RequestStatus.Pending || c.Status == RequestStatus.Approved)))
            throw new DomainException("You have already claimed comp-off for that day.");

        var request = new CompOffRequest { EmployeeId = employeeId, WorkedOn = workedOn, Days = days, Reason = reason.Trim() };
        db.CompOffRequests.Add(request);
        var approval = await approvals.StartAsync(ApprovalRequestType.CompOff, request.Id, employeeId,
            $"Comp-off · {days:0.#} day · worked {workedOn:d MMM}");
        await db.SaveChangesAsync();
        if (approval.Status == ApprovalStatus.Approved)
            await ApplyAsync(request, true, "Approved automatically");
        return request;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var request = await db.CompOffRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Comp-off request");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException($"This request is already {request.Status.ToString().ToLowerInvariant()}.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.CompOff, request.Id, request.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Pending)
            await db.SaveChangesAsync();
        else
            await ApplyAsync(request, outcome == ApprovalStatus.Approved, note);
    }

    public async Task CancelAsync(Guid id)
    {
        var request = await db.CompOffRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Comp-off request");
        if (request.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this request.");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException("Only a pending request can be cancelled.");
        request.Status = RequestStatus.Cancelled;
        await approvals.CancelAsync(ApprovalRequestType.CompOff, request.Id);
        await db.SaveChangesAsync();
    }

    /// <summary>Approved: adds the days to the employee's compensatory leave for the year worked (creating that leave type if needed).</summary>
    private async Task ApplyAsync(CompOffRequest request, bool approved, string? note)
    {
        request.Status = approved ? RequestStatus.Approved : RequestStatus.Rejected;
        request.DecidedAtUtc = clock.UtcNow;
        request.DecisionNote = note;
        if (approved)
        {
            var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.IsCompensatory && t.IsActive);
            if (type is null)
            {
                type = new LeaveType { Name = "Compensatory Leave", Code = "CPL", AnnualQuota = 0, IsCompensatory = true };
                db.LeaveTypes.Add(type);
            }
            await db.SaveChangesAsync();
            await leave.EnsureBalancesAsync(request.EmployeeId, request.WorkedOn.Year);
            var balance = await db.LeaveBalances.FirstAsync(b => b.EmployeeId == request.EmployeeId && b.LeaveTypeId == type.Id && b.Year == request.WorkedOn.Year);
            balance.Credited += request.Days;
        }
        await db.SaveChangesAsync();
    }
}
