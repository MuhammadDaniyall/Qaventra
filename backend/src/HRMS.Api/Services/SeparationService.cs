using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record SettlementInputs(decimal EncashedLeaveDays, bool RecoverNoticeShortfall, decimal OtherAdditions, decimal OtherDeductions, string? Note);

public record SettlementSnapshot(
    decimal MonthlyGross, bool HasSalary, IReadOnlyList<LeaveBalanceLeft> LeaveBalances,
    IReadOnlyList<PayableClaim> Claims, SettlementResult Result);

public record LeaveBalanceLeft(string LeaveType, decimal Remaining);

public record PayableClaim(Guid Id, string Title, decimal Total);

public class SeparationService(HrmsDbContext db, ICurrentUser me, TenantClock clock, LeaveService leave)
{
    public static readonly IReadOnlyList<(string Department, string Title)> DefaultChecklist =
    [
        ("Line manager", "Work and responsibilities handed over"),
        ("IT", "Laptop, phone and accessories returned"),
        ("IT", "Email and system access revoked"),
        ("Admin", "ID card, keys and access card returned"),
        ("Finance", "Loans, advances and dues settled"),
        ("HR", "Exit interview completed"),
    ];

    public async Task<Separation> ResignAsync(Guid employeeId, DateOnly proposedLastDay, string? reason)
    {
        var today = await clock.TodayAsync();
        if (proposedLastDay < today)
            throw new DomainException("Your proposed last working day cannot be in the past.");
        await EnsureNoOpenSeparationAsync(employeeId);

        var separation = new Separation
        {
            EmployeeId = employeeId,
            Type = SeparationType.Resignation,
            NoticeDate = today,
            ProposedLastDay = proposedLastDay,
            Reason = reason,
            InitiatedByUserId = me.UserId,
        };
        db.Separations.Add(separation);
        await db.SaveChangesAsync();
        return separation;
    }

    /// <summary>HR starts a separation directly (termination, retirement, contract end, or a resignation received offline).</summary>
    public async Task<Separation> InitiateAsync(Guid employeeId, SeparationType type, DateOnly noticeDate, DateOnly lastWorkingDay, int noticePeriodDays, string? reason)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId))
            throw new NotFoundException("Employee");
        if (employeeId == me.EmployeeId)
            throw new DomainException("You cannot start your own separation here; use My exit instead.");
        if (lastWorkingDay < noticeDate)
            throw new DomainException("The last working day cannot be before the notice date.");
        await EnsureNoOpenSeparationAsync(employeeId);

        var separation = new Separation
        {
            EmployeeId = employeeId,
            Type = type,
            Status = SeparationStatus.InProgress,
            NoticeDate = noticeDate,
            ProposedLastDay = lastWorkingDay,
            LastWorkingDay = lastWorkingDay,
            NoticePeriodDays = noticePeriodDays,
            Reason = reason,
            InitiatedByUserId = me.UserId,
            DecidedByUserId = me.UserId,
            DecidedAtUtc = clock.UtcNow,
            RecoverNoticeShortfall = false,
        };
        AddDefaultChecklist(separation);
        db.Separations.Add(separation);
        await db.SaveChangesAsync();
        return separation;
    }

    public async Task ApproveAsync(Guid id, DateOnly lastWorkingDay, int noticePeriodDays, string? note)
    {
        var s = await LoadAsync(id);
        RequireHrNotSelf(s);
        if (s.Status != SeparationStatus.Requested)
            throw new DomainException("Only a requested resignation can be accepted.");
        if (lastWorkingDay < s.NoticeDate)
            throw new DomainException("The last working day cannot be before the notice date.");

        s.Status = SeparationStatus.InProgress;
        s.LastWorkingDay = lastWorkingDay;
        s.NoticePeriodDays = noticePeriodDays;
        s.DecidedByUserId = me.UserId;
        s.DecidedAtUtc = clock.UtcNow;
        s.DecisionNote = note;
        s.RecoverNoticeShortfall = true;
        AddDefaultChecklist(s);
        await db.SaveChangesAsync();
    }

    public async Task RejectAsync(Guid id, string? note)
    {
        var s = await LoadAsync(id);
        RequireHrNotSelf(s);
        if (s.Status != SeparationStatus.Requested)
            throw new DomainException("Only a requested resignation can be declined.");
        s.Status = SeparationStatus.Rejected;
        s.DecidedByUserId = me.UserId;
        s.DecidedAtUtc = clock.UtcNow;
        s.DecisionNote = note;
        await db.SaveChangesAsync();
    }

    /// <summary>The employee may withdraw a request HR has not accepted yet; HR may cancel any open separation.</summary>
    public async Task WithdrawAsync(Guid id)
    {
        var s = await LoadAsync(id);
        var owner = s.EmployeeId == me.EmployeeId;
        if (!(owner && s.Status == SeparationStatus.Requested) && !(me.IsHr() && !owner && s.IsOpen))
            throw new ForbiddenException("This separation cannot be withdrawn.");
        s.Status = SeparationStatus.Withdrawn;
        await db.SaveChangesAsync();
    }

    public async Task SaveExitInterviewAsync(Guid id, string? exitReason, int rating, bool wouldRecommend, string? feedback)
    {
        var s = await LoadAsync(id);
        if (s.EmployeeId != me.EmployeeId)
            throw new ForbiddenException("Only the leaving employee fills in the exit interview.");
        if (s.Status != SeparationStatus.InProgress)
            throw new DomainException("The exit interview opens once HR accepts your resignation.");

        s.ExitReason = exitReason;
        s.ExitRating = rating;
        s.WouldRecommend = wouldRecommend;
        s.ExitFeedback = feedback;
        s.ExitInterviewAtUtc = clock.UtcNow;

        // Tick the matching checklist item so HR sees it done.
        var item = s.Clearance.FirstOrDefault(c => c.Department == "HR" && c.Title.StartsWith("Exit interview"));
        if (item is { IsCleared: false })
        {
            item.IsCleared = true;
            item.ClearedAtUtc = clock.UtcNow;
            item.ClearedByName = "Submitted by employee";
        }
        await db.SaveChangesAsync();
    }

    public async Task<ClearanceItem> AddClearanceItemAsync(Guid id, string department, string title)
    {
        var s = await LoadAsync(id);
        RequireHrNotSelf(s);
        RequireInProgress(s);
        var item = new ClearanceItem
        {
            SeparationId = s.Id,
            Department = department.Trim(),
            Title = title.Trim(),
            SortOrder = s.Clearance.Select(c => c.SortOrder).DefaultIfEmpty(0).Max() + 1,
        };
        db.ClearanceItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateClearanceItemAsync(Guid id, Guid itemId, bool isCleared, string? note)
    {
        var s = await LoadAsync(id);
        RequireHrNotSelf(s);
        RequireInProgress(s);
        var item = s.Clearance.FirstOrDefault(c => c.Id == itemId) ?? throw new NotFoundException("Checklist item");
        if (item.IsCleared != isCleared)
        {
            item.IsCleared = isCleared;
            item.ClearedAtUtc = isCleared ? clock.UtcNow : null;
            item.ClearedByName = isCleared ? await LetterService.CurrentUserNameAsync(db, me) : null;
        }
        item.Note = note;
        await db.SaveChangesAsync();
    }

    public async Task DeleteClearanceItemAsync(Guid id, Guid itemId)
    {
        var s = await LoadAsync(id);
        RequireHrNotSelf(s);
        RequireInProgress(s);
        var item = s.Clearance.FirstOrDefault(c => c.Id == itemId) ?? throw new NotFoundException("Checklist item");
        db.ClearanceItems.Remove(item);
        await db.SaveChangesAsync();
    }

    public async Task SaveSettlementAsync(Guid id, SettlementInputs input)
    {
        var s = await LoadAsync(id);
        RequireHrNotSelf(s);
        RequireInProgress(s);
        s.EncashedLeaveDays = input.EncashedLeaveDays;
        s.RecoverNoticeShortfall = input.RecoverNoticeShortfall;
        s.OtherAdditions = input.OtherAdditions;
        s.OtherDeductions = input.OtherDeductions;
        s.SettlementNote = input.Note;
        s.SettlementSavedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Settlement as it stands: frozen amounts once completed, otherwise worked out from today's data.</summary>
    public async Task<SettlementSnapshot> SettlementAsync(Separation s)
    {
        var lastDay = s.LastWorkingDay ?? s.ProposedLastDay;
        var salary = await LetterService.CurrentSalaryAsync(db, s.EmployeeId, lastDay);
        var gross = salary?.Gross ?? 0;

        if (s.Status != SeparationStatus.Completed)
            await leave.EnsureBalancesAsync(s.EmployeeId, lastDay.Year);
        var balances = await db.LeaveBalances
            .Where(b => b.EmployeeId == s.EmployeeId && b.Year == lastDay.Year && b.LeaveType!.IsPaid)
            .Select(b => new LeaveBalanceLeft(b.LeaveType!.Name, b.Entitled - b.Used))
            .ToListAsync();

        if (s.Status == SeparationStatus.Completed)
        {
            var frozen = SettlementCalculator.Calculate(Inputs(s, gross, lastDay, s.ExpenseReimbursement));
            return new SettlementSnapshot(gross, salary is not null, balances, [],
                frozen with { LeaveEncashment = s.LeaveEncashmentAmount, NoticeRecovery = s.NoticeRecoveryAmount, Net = s.NetSettlement });
        }

        var claims = await PayableClaims(s.EmployeeId).Select(c => new PayableClaim(c.Id, c.Title, c.Total)).ToListAsync();
        var result = SettlementCalculator.Calculate(Inputs(s, gross, lastDay, claims.Sum(c => c.Total)));
        return new SettlementSnapshot(gross, salary is not null, balances, claims, result);
    }

    private static SettlementInput Inputs(Separation s, decimal gross, DateOnly lastDay, decimal expenses) => new(
        gross, s.EncashedLeaveDays, s.NoticePeriodDays, s.NoticeDate, lastDay, s.RecoverNoticeShortfall,
        expenses, s.OtherAdditions, s.OtherDeductions);

    /// <summary>Approved claims not yet paid, including ones sitting on a draft payroll run.</summary>
    private IQueryable<ExpenseClaim> PayableClaims(Guid employeeId) =>
        db.ExpenseClaims.Where(c => c.EmployeeId == employeeId && c.Status == ExpenseStatus.Approved
            && (c.PayrollRunId == null || db.PayrollRuns.Any(r => r.Id == c.PayrollRunId && r.Status == PayrollRunStatus.Draft)));

    /// <summary>
    /// Closes the separation on or after the last working day: freezes the settlement, pays outstanding claims,
    /// marks the employee as left and switches off their login.
    /// </summary>
    public async Task CompleteAsync(Guid id)
    {
        var s = await LoadAsync(id);
        RequireHrNotSelf(s);
        RequireInProgress(s);
        var lastDay = s.LastWorkingDay!.Value;
        var today = await clock.TodayAsync();
        if (today < lastDay)
            throw new DomainException($"You can complete this separation on or after the last working day ({LetterService.LongDate(lastDay)}).");
        if (s.Clearance.Any(c => !c.IsCleared))
            throw new DomainException("Clear every checklist item before completing.");
        if (s.SettlementSavedAtUtc is null)
            throw new DomainException("Review and save the final settlement before completing.");

        var snapshot = await SettlementAsync(s);
        s.LeaveEncashmentAmount = snapshot.Result.LeaveEncashment;
        s.NoticeRecoveryAmount = snapshot.Result.NoticeRecovery;
        s.ExpenseReimbursement = snapshot.Result.ExpenseReimbursement;
        s.NetSettlement = snapshot.Result.Net;
        s.Status = SeparationStatus.Completed;
        s.CompletedAtUtc = clock.UtcNow;

        var claimIds = snapshot.Claims.Select(c => c.Id).ToList();
        foreach (var claim in await db.ExpenseClaims.Where(c => claimIds.Contains(c.Id)).ToListAsync())
        {
            claim.Status = ExpenseStatus.Paid;
            claim.PayrollRunId = null;
            claim.PaidAtUtc = s.CompletedAtUtc;
            claim.PaidVia = "Final settlement";
        }

        var employee = await db.Employees.FirstAsync(e => e.Id == s.EmployeeId);
        employee.Status = s.Type == SeparationType.Termination ? EmploymentStatus.Terminated : EmploymentStatus.Resigned;
        employee.LeavingDate = lastDay;
        foreach (var user in await db.Users.Where(u => u.EmployeeId == s.EmployeeId).ToListAsync())
            user.IsActive = false;

        await db.SaveChangesAsync();
    }

    public async Task<Separation> LoadAsync(Guid id) =>
        await db.Separations.Include(x => x.Clearance).FirstOrDefaultAsync(x => x.Id == id)
        ?? throw new NotFoundException("Separation");

    private async Task EnsureNoOpenSeparationAsync(Guid employeeId)
    {
        if (await db.Separations.AnyAsync(x => x.EmployeeId == employeeId
                && (x.Status == SeparationStatus.Requested || x.Status == SeparationStatus.InProgress)))
            throw new DomainException("This employee already has an open separation.");
        if (await db.Employees.AnyAsync(e => e.Id == employeeId && !(e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new DomainException("This employee has already left.");
    }

    private void RequireHrNotSelf(Separation s)
    {
        if (!me.IsHr())
            throw new ForbiddenException("Only HR can manage separations.");
        if (s.EmployeeId == me.EmployeeId)
            throw new ForbiddenException("You cannot process your own separation.");
    }

    private static void RequireInProgress(Separation s)
    {
        if (s.Status != SeparationStatus.InProgress)
            throw new DomainException("This separation is not in progress.");
    }

    // Added through the DbSet: items attached to an already-tracked separation would otherwise be treated as existing rows.
    private void AddDefaultChecklist(Separation s)
    {
        var order = 0;
        foreach (var (department, title) in DefaultChecklist)
        {
            var item = new ClearanceItem { SeparationId = s.Id, Department = department, Title = title, SortOrder = ++order };
            s.Clearance.Add(item);
            db.ClearanceItems.Add(item);
        }
    }
}
