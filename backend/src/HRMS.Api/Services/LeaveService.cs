using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public class LeaveService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, ScheduleService schedule)
{
    /// <summary>
    /// Creates missing balances for every active paid leave type: the year's quota (prorated for joiners; zero for
    /// compensatory leave) plus unused days carried forward from last year up to the type's limit.
    /// </summary>
    public async Task EnsureBalancesAsync(Guid employeeId, int year)
    {
        var employee = await db.Employees.FirstAsync(e => e.Id == employeeId);
        var types = await db.LeaveTypes.Where(t => t.IsActive && t.IsPaid).ToListAsync();
        var existing = await db.LeaveBalances.Where(b => b.EmployeeId == employeeId && b.Year == year)
            .Select(b => b.LeaveTypeId).ToListAsync();
        var missing = types.Where(t => !existing.Contains(t.Id)).ToList();
        if (missing.Count == 0)
            return;

        var lastYear = await db.LeaveBalances.Where(b => b.EmployeeId == employeeId && b.Year == year - 1).ToListAsync();
        foreach (var type in missing)
        {
            var previous = lastYear.FirstOrDefault(b => b.LeaveTypeId == type.Id);
            db.LeaveBalances.Add(new LeaveBalance
            {
                EmployeeId = employeeId,
                LeaveTypeId = type.Id,
                Year = year,
                Entitled = type.IsCompensatory ? 0 : LeavePolicy.YearEntitlement(type.AnnualQuota, employee.JoiningDate, year),
                CarriedForward = previous is null ? 0 : LeavePolicy.CarryForward(previous.Remaining, type.CarryForwardMax),
            });
        }
        await db.SaveChangesAsync();
    }

    public record BalanceView(LeaveBalance Balance, LeaveType Type, decimal Earned, decimal Pending, decimal Available);

    /// <summary>Balances with what is earned so far (monthly accrual), pending requests and what can still be applied for.</summary>
    public async Task<List<BalanceView>> BalancesAsync(Guid employeeId, int year)
    {
        await EnsureBalancesAsync(employeeId, year);
        var joining = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.JoiningDate).FirstAsync();
        var today = await clock.TodayAsync();
        var pending = await db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId && r.Status == LeaveStatus.Pending && r.FromDate.Year == year)
            .GroupBy(r => r.LeaveTypeId).Select(g => new { g.Key, Days = g.Sum(r => r.Days) })
            .ToDictionaryAsync(x => x.Key, x => x.Days);
        var balances = await db.LeaveBalances.Include(b => b.LeaveType)
            .Where(b => b.EmployeeId == employeeId && b.Year == year).ToListAsync();
        return balances.OrderBy(b => b.LeaveType!.Name).Select(b =>
        {
            var type = b.LeaveType!;
            var earned = type.IsCompensatory ? 0 : Math.Min(b.Entitled, LeavePolicy.Earned(type.Accrual, type.AnnualQuota, joining, year, today));
            var p = pending.GetValueOrDefault(b.LeaveTypeId);
            return new BalanceView(b, type, earned, p, earned + b.CarriedForward + b.Adjustment + b.Credited - b.Used - p);
        }).ToList();
    }

    /// <summary>Whether a request needs a document attached: its reason says so, or it is longer than the type allows without one.</summary>
    public async Task<bool> NeedsDocumentAsync(LeaveRequest r, LeaveType type)
    {
        if (type.DocumentAfterDays is { } n && r.Days > n)
            return true;
        return r.ReasonId is { } rid && await db.LeaveReasons.AnyAsync(x => x.Id == rid && x.RequiresDocument);
    }

    /// <summary>Working days in the range by the employee's roster: off days and holidays do not count.</summary>
    public async Task<decimal> CountDaysAsync(Guid employeeId, DateOnly from, DateOnly to) =>
        to < from ? 0 : (await schedule.ForEmployeeAsync(employeeId, from, to)).CountWorkingDays(from, to);

    public record LeaveApplication(
        Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, string? Reason,
        LeaveDuration Duration = LeaveDuration.FullDay, HalfDayPart? HalfDayPart = null, TimeOnly? StartTime = null, TimeOnly? EndTime = null, Guid? ReasonId = null);

    public async Task<LeaveRequest> ApplyAsync(Guid employeeId, LeaveApplication a)
    {
        var (from, to) = (a.FromDate, a.ToDate);
        if (to < from)
            throw new DomainException("End date must be on or after the start date.");
        if (from.Year != to.Year)
            throw new DomainException("A leave request cannot span two calendar years; split it into two requests.");

        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == a.LeaveTypeId && t.IsActive)
            ?? throw new NotFoundException("Leave type");
        var employee = await db.Employees.Include(e => e.Shift).FirstAsync(e => e.Id == employeeId);

        if (type.ApplicableGender is { } g && employee.Gender != g)
            throw new DomainException($"{type.Name} is only for {g.ToString().ToLowerInvariant()} employees.");
        if (!type.AllowedOnProbation && employee.Status == EmploymentStatus.Probation)
            throw new DomainException($"{type.Name} is available after probation.");
        var today = await clock.TodayAsync();
        if (type.MinNoticeDays > 0 && from < today.AddDays(type.MinNoticeDays) && !me.IsHr())
            throw new DomainException($"{type.Name} must be requested at least {type.MinNoticeDays} day(s) in advance.");

        LeaveReason? reason = null;
        if (a.ReasonId is { } rid)
            reason = await db.LeaveReasons.FirstOrDefaultAsync(r => r.Id == rid && r.LeaveTypeId == type.Id && r.IsActive) ?? throw new NotFoundException("Reason");
        else if (await db.LeaveReasons.AnyAsync(r => r.LeaveTypeId == type.Id && r.IsActive))
            throw new DomainException($"Choose a reason for the {type.Name}.");

        decimal days;
        if (a.Duration == LeaveDuration.FullDay)
        {
            days = await CountDaysAsync(employeeId, from, to);
            if (days <= 0)
                throw new DomainException("The selected dates are all weekly off days or holidays.");
            if (type.Sandwich)
            {
                // Offs and holidays between the first and last working day are counted too.
                var plan = await schedule.ForEmployeeAsync(employeeId, from, to);
                var first = from;
                while (!plan.IsWorkingDay(first)) first = first.AddDays(1);
                var last = to;
                while (!plan.IsWorkingDay(last)) last = last.AddDays(-1);
                days = last.DayNumber - first.DayNumber + 1;
            }
        }
        else
        {
            if (from != to)
                throw new DomainException("Half day and short leave are for a single day.");
            if (await CountDaysAsync(employeeId, from, to) <= 0)
                throw new DomainException("That day is a weekly off day or a holiday.");
            if (a.Duration == LeaveDuration.HalfDay)
            {
                if (!type.AllowHalfDay)
                    throw new DomainException($"{type.Name} cannot be taken as a half day.");
                if (a.HalfDayPart is null)
                    throw new DomainException("Choose the first or second half of the day.");
                days = 0.5m;
            }
            else
            {
                if (!type.AllowShortLeave)
                    throw new DomainException($"{type.Name} cannot be taken as short leave.");
                if (a.StartTime is not { } st || a.EndTime is not { } et || et <= st)
                    throw new DomainException("Short leave needs a start and an end time.");
                if ((et - st).TotalHours > 4)
                    throw new DomainException("Short leave is at most 4 hours; apply for a half day instead.");
                days = LeavePolicy.ShortLeaveDays(st, et, employee.Shift?.StartTime ?? new TimeOnly(9, 0), employee.Shift?.EndTime ?? new TimeOnly(17, 0));
            }
        }
        if (type.MaxConsecutiveDays is { } max && days > max)
            throw new DomainException($"{type.Name} can be at most {max} working day(s) at a time.");
        if (reason?.MaxDays is { } rmax && days > rmax)
            throw new DomainException($"{type.Name} for {reason.Name.ToLowerInvariant()} can be at most {rmax:0.#} day(s).");

        // Two half days (first and second half) may share a date; anything else overlapping is refused.
        var clashes = await db.LeaveRequests.Where(r => r.EmployeeId == employeeId
            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
            && r.FromDate <= to && r.ToDate >= from).ToListAsync();
        var fine = clashes.All(r => a.Duration == LeaveDuration.HalfDay && r.Duration == LeaveDuration.HalfDay && r.HalfDayPart != a.HalfDayPart);
        if (!fine)
            throw new DomainException("You already have a leave request covering some of these dates.");

        if (type.IsPaid)
        {
            var balance = (await BalancesAsync(employeeId, from.Year)).FirstOrDefault(b => b.Type.Id == type.Id);
            var available = balance?.Available ?? 0;
            if (days > available)
                throw new DomainException(type.Accrual == LeaveAccrual.Monthly && balance is not null && balance.Earned < balance.Balance.Entitled
                    ? $"Not enough {type.Name} earned yet: {available:0.##} day(s) available now ({type.Name} is earned monthly), {days:0.##} requested."
                    : $"Not enough {type.Name} balance: {available:0.##} day(s) available, {days:0.##} requested.");
        }

        var request = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveTypeId = type.Id,
            FromDate = from,
            ToDate = to,
            Days = days,
            Duration = a.Duration,
            HalfDayPart = a.Duration == LeaveDuration.HalfDay ? a.HalfDayPart : null,
            StartTime = a.Duration == LeaveDuration.ShortLeave ? a.StartTime : null,
            EndTime = a.Duration == LeaveDuration.ShortLeave ? a.EndTime : null,
            Reason = a.Reason,
            ReasonId = reason?.Id,
        };
        db.LeaveRequests.Add(request);

        var length = a.Duration switch
        {
            LeaveDuration.HalfDay => a.HalfDayPart == HalfDayPart.FirstHalf ? "half day (first half)" : "half day (second half)",
            LeaveDuration.ShortLeave => $"short leave {a.StartTime:HH\\:mm}–{a.EndTime:HH\\:mm}",
            _ => $"{days:0.#} day{(days == 1 ? "" : "s")}",
        };
        var summary = $"{type.Name}{(reason is null ? "" : $" ({reason.Name})")} · {length} · {from:d MMM}" + (to != from ? $" – {to:d MMM}" : "")
                      + (await NeedsDocumentAsync(request, type) ? " · document needed" : "");
        var approval = await approvals.StartAsync(ApprovalRequestType.Leave, request.Id, employeeId, summary);
        await db.SaveChangesAsync();
        if (approval.Status == ApprovalStatus.Approved)
            await ApplyDecisionAsync(request, approve: true, note: "Approved automatically");
        return request;
    }

    /// <summary>
    /// Records the signed-in approver's decision on the current step of the request's chain. The leave is approved
    /// (and the balance used) only after the last step.
    /// </summary>
    public async Task DecideAsync(Guid requestId, bool approve, string? note)
    {
        var request = await db.LeaveRequests.Include(r => r.LeaveType).FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException("Leave request");
        if (request.Status != LeaveStatus.Pending)
            throw new DomainException($"This request is already {request.Status.ToString().ToLowerInvariant()}.");
        if (approve && await NeedsDocumentAsync(request, request.LeaveType!)
            && !await db.Attachments.AnyAsync(x => x.OwnerType == AttachmentOwner.Leave && x.OwnerId == request.Id))
            throw new DomainException("This leave needs a supporting document. Ask for it to be attached before approving.");

        var outcome = await approvals.DecideAsync(ApprovalRequestType.Leave, request.Id, request.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Pending)
            await db.SaveChangesAsync();
        else
            await ApplyDecisionAsync(request, outcome == ApprovalStatus.Approved, note);
    }

    private async Task ApplyDecisionAsync(LeaveRequest request, bool approve, string? note)
    {
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
            await approvals.CancelAsync(ApprovalRequestType.Leave, request.Id);
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
