using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public class LeaveService(HrmsDbContext db, ICurrentUser me, TenantClock clock)
{
    /// <summary>Creates missing balances for every active paid leave type, prorated in the joining year.</summary>
    public async Task EnsureBalancesAsync(Guid employeeId, int year)
    {
        var employee = await db.Employees.FirstAsync(e => e.Id == employeeId);
        var types = await db.LeaveTypes.Where(t => t.IsActive && t.IsPaid).ToListAsync();
        var existing = await db.LeaveBalances.Where(b => b.EmployeeId == employeeId && b.Year == year)
            .Select(b => b.LeaveTypeId).ToListAsync();

        foreach (var type in types.Where(t => !existing.Contains(t.Id)))
        {
            var entitled = type.AnnualQuota;
            if (employee.JoiningDate.Year == year)
            {
                var monthsLeft = 12 - employee.JoiningDate.Month + 1;
                entitled = Math.Round(type.AnnualQuota * monthsLeft / 12m * 2, MidpointRounding.AwayFromZero) / 2;
            }
            db.LeaveBalances.Add(new LeaveBalance { EmployeeId = employeeId, LeaveTypeId = type.Id, Year = year, Entitled = entitled });
        }
        await db.SaveChangesAsync();
    }

    public async Task<decimal> CountDaysAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        var (weeklyOff, holidays) = await CalendarForAsync(employeeId, from, to);
        return WorkCalendar.CountWorkingDays(from, to, weeklyOff, holidays);
    }

    public async Task<(IReadOnlyCollection<DayOfWeek> WeeklyOff, IReadOnlySet<DateOnly> Holidays)> CalendarForAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        var weeklyOff = await db.Employees.Where(e => e.Id == employeeId)
            .Select(e => e.Shift != null ? e.Shift.WeeklyOffDays : null).FirstOrDefaultAsync() ?? [];
        var holidays = await db.Holidays.Where(h => h.Date >= from && h.Date <= to).Select(h => h.Date).ToListAsync();
        return (weeklyOff, holidays.ToHashSet());
    }

    public async Task<LeaveRequest> ApplyAsync(Guid employeeId, Guid leaveTypeId, DateOnly from, DateOnly to, string? reason)
    {
        if (to < from)
            throw new DomainException("End date must be on or after the start date.");
        if (from.Year != to.Year)
            throw new DomainException("A leave request cannot span two calendar years; split it into two requests.");

        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == leaveTypeId && t.IsActive)
            ?? throw new NotFoundException("Leave type");

        var days = await CountDaysAsync(employeeId, from, to);
        if (days <= 0)
            throw new DomainException("The selected dates are all weekly off days or holidays.");

        var overlaps = await db.LeaveRequests.AnyAsync(r => r.EmployeeId == employeeId
            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
            && r.FromDate <= to && r.ToDate >= from);
        if (overlaps)
            throw new DomainException("You already have a leave request covering some of these dates.");

        if (type.IsPaid)
        {
            await EnsureBalancesAsync(employeeId, from.Year);
            var balance = await db.LeaveBalances.FirstAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == type.Id && b.Year == from.Year);
            var pending = await db.LeaveRequests
                .Where(r => r.EmployeeId == employeeId && r.LeaveTypeId == type.Id && r.Status == LeaveStatus.Pending && r.FromDate.Year == from.Year)
                .SumAsync(r => (decimal?)r.Days) ?? 0;
            var available = balance.Entitled - balance.Used - pending;
            if (days > available)
                throw new DomainException($"Not enough {type.Name} balance: {available:0.#} day(s) available, {days:0.#} requested.");
        }

        var request = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveTypeId = type.Id,
            FromDate = from,
            ToDate = to,
            Days = days,
            Reason = reason,
        };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();
        return request;
    }

    /// <summary>HR and admins may decide any request; a manager only their direct reports'. Nobody decides their own.</summary>
    public async Task<bool> CanDecideAsync(LeaveRequest request)
    {
        if (request.EmployeeId == me.EmployeeId)
            return false;
        if (me.IsHr())
            return true;
        if (me.Role == UserRole.Manager && me.EmployeeId is { } managerId)
            return await db.Employees.AnyAsync(e => e.Id == request.EmployeeId && e.ManagerId == managerId);
        return false;
    }

    public async Task DecideAsync(Guid requestId, bool approve, string? note)
    {
        var request = await db.LeaveRequests.Include(r => r.LeaveType).FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException("Leave request");
        if (request.Status != LeaveStatus.Pending)
            throw new DomainException($"This request is already {request.Status.ToString().ToLowerInvariant()}.");
        if (!await CanDecideAsync(request))
            throw new ForbiddenException("You cannot approve or reject this request.");

        request.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        request.DecidedByUserId = me.UserId;
        request.DecidedAtUtc = clock.UtcNow;
        request.DecisionNote = note;

        if (approve && request.LeaveType!.IsPaid)
        {
            await EnsureBalancesAsync(request.EmployeeId, request.FromDate.Year);
            var balance = await db.LeaveBalances.FirstAsync(b => b.EmployeeId == request.EmployeeId && b.LeaveTypeId == request.LeaveTypeId && b.Year == request.FromDate.Year);
            balance.Used += request.Days;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>The employee may cancel a pending request; HR may also cancel an approved one, which returns the days.</summary>
    public async Task CancelAsync(Guid requestId)
    {
        var request = await db.LeaveRequests.Include(r => r.LeaveType).FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException("Leave request");
        var isHr = me.IsHr();
        var isOwner = request.EmployeeId == me.EmployeeId;

        if (request.Status == LeaveStatus.Pending && (isOwner || isHr))
        {
            request.Status = LeaveStatus.Cancelled;
        }
        else if (request.Status == LeaveStatus.Approved && isHr)
        {
            request.Status = LeaveStatus.Cancelled;
            if (request.LeaveType!.IsPaid)
            {
                var balance = await db.LeaveBalances.FirstAsync(b => b.EmployeeId == request.EmployeeId && b.LeaveTypeId == request.LeaveTypeId && b.Year == request.FromDate.Year);
                balance.Used -= request.Days;
            }
        }
        else
        {
            throw new ForbiddenException("This request cannot be cancelled.");
        }
        await db.SaveChangesAsync();
    }
}
