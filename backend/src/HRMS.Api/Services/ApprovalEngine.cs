using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>
/// Runs requests (leave, expense, …) through the company's approval chain for that request type.
/// Modules call <see cref="StartAsync"/> on submit and <see cref="DecideAsync"/> on approve/reject, then apply the
/// effect themselves when the result is <see cref="ApprovalStatus.Approved"/>. The caller saves changes.
/// Requests submitted before the engine existed have no <see cref="ApprovalRequest"/> and keep the old rule
/// (line manager or HR, one step).
/// </summary>
public class ApprovalEngine(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalPolicy legacy, NotificationService notify)
{
    /// <summary>
    /// Set by HR's "approve over the chain": the next approval decides every remaining step at once.
    /// Scoped to the request, so it never leaks into another decision.
    /// </summary>
    public bool Force { get; set; }

    public async Task<IReadOnlyList<FlowStep>> FlowAsync(ApprovalRequestType type)
    {
        var flow = await db.ApprovalFlows.Include(f => f.Steps).FirstOrDefaultAsync(f => f.RequestType == type);
        return flow is null
            ? ApprovalPlanner.DefaultFlow
            : flow.Steps.OrderBy(s => s.Order).Select(s => new FlowStep(s.Kind, s.EmployeeId)).ToList();
    }

    /// <summary>Creates the approval for a new request. Returns it already Approved when the chain is empty.</summary>
    public async Task<ApprovalRequest> StartAsync(ApprovalRequestType type, Guid subjectId, Guid employeeId, string summary)
    {
        // Sent again after a rejection, withdrawal or change: the old, closed chain makes way for a new one.
        var previous = await db.ApprovalRequests.Include(r => r.Steps).FirstOrDefaultAsync(r => r.RequestType == type && r.SubjectId == subjectId);
        if (previous is not null)
        {
            if (previous.Status == ApprovalStatus.Pending)
                throw new DomainException("This is already waiting for approval.");
            db.ApprovalRequests.Remove(previous);
            await db.SaveChangesAsync();
        }
        var chain = await db.Employees.Where(e => e.Id == employeeId)
            .Select(e => new
            {
                e.ManagerId,
                SecondId = e.Manager != null ? e.Manager.ManagerId : null,
                HeadId = db.Departments.Where(d => d.Id == e.DepartmentId).Select(d => d.HeadEmployeeId).FirstOrDefault(),
            })
            .FirstAsync();
        var planned = ApprovalPlanner.Plan(await FlowAsync(type), employeeId, chain.ManagerId, chain.SecondId, chain.HeadId);

        var request = new ApprovalRequest
        {
            RequestType = type,
            SubjectId = subjectId,
            EmployeeId = employeeId,
            Summary = summary.Length > 300 ? summary[..300] : summary,
            FiledByName = me.EmployeeId != employeeId && me.UserId is not null ? await LetterService.CurrentUserNameAsync(db, me) : null,
        };
        for (var i = 0; i < planned.Count; i++)
        {
            request.Steps.Add(new ApprovalStep
            {
                ApprovalRequestId = request.Id,
                Order = i + 1,
                Kind = planned[i].Kind,
                ApproverEmployeeId = planned[i].ApproverEmployeeId,
                Status = i == 0 ? ApprovalStepStatus.Current : ApprovalStepStatus.Waiting,
            });
        }
        if (planned.Count == 0)
        {
            request.Status = ApprovalStatus.Approved;
            request.CompletedAtUtc = clock.UtcNow;
        }
        else
        {
            request.CurrentStep = 1;
        }
        db.ApprovalRequests.Add(request);
        if (request.Status == ApprovalStatus.Pending)
            await NotifyApproversAsync(request);
        return request;
    }

    /// <summary>
    /// Records the signed-in user's decision on the current step. Returns Pending when the request moves on to the
    /// next approver, Approved after the last step, Rejected on any rejection.
    /// </summary>
    public async Task<ApprovalStatus> DecideAsync(ApprovalRequestType type, Guid subjectId, Guid employeeId, bool approve, string? note)
    {
        var request = await LoadAsync(type, subjectId);
        if (request is null)
        {
            if (!await legacy.CanApproveForAsync(employeeId))
                throw new ForbiddenException("You cannot approve or reject this request.");
            return approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        }

        if (request.Status != ApprovalStatus.Pending)
            throw new DomainException($"This request is already {request.Status.ToString().ToLowerInvariant()}.");
        var step = request.Steps.First(s => s.Status == ApprovalStepStatus.Current);
        if (Force && approve)
        {
            if (!me.IsHr() || request.EmployeeId == me.EmployeeId)
                throw new ForbiddenException("Only HR can approve over the chain, and not their own requests.");
            var by = await LetterService.CurrentUserNameAsync(db, me);
            foreach (var s in request.Steps.Where(s => s.Status is ApprovalStepStatus.Current or ApprovalStepStatus.Waiting))
            {
                s.Status = ApprovalStepStatus.Approved;
                s.DecidedByUserId = me.UserId;
                s.DecidedByName = $"{by} (forced by HR)";
                s.DecidedAtUtc = clock.UtcNow;
                s.Note = note;
            }
            request.Forced = true;
            Close(request, ApprovalStatus.Approved);
            await notify.ToEmployeesAsync([request.EmployeeId], NotificationKind.RequestDecided,
                $"Your {Noun(type)} was approved", request.Summary + (string.IsNullOrWhiteSpace(note) ? "" : $"\n“{note}”"), MyPage(type));
            return request.Status;
        }
        var actingFor = await ActingForAsync(request, step);
        if (actingFor is null)
            throw new ForbiddenException("This request is waiting for someone else's approval.");

        step.Status = approve ? ApprovalStepStatus.Approved : ApprovalStepStatus.Rejected;
        step.DecidedByUserId = me.UserId;
        var myName = await LetterService.CurrentUserNameAsync(db, me);
        step.DecidedByName = actingFor == Guid.Empty || actingFor == me.EmployeeId
            ? myName
            : $"{myName} for {await db.Employees.Where(e => e.Id == actingFor).Select(e => e.FirstName + " " + e.LastName).FirstAsync()}";
        step.DecidedAtUtc = clock.UtcNow;
        step.Note = note;

        var next = request.Steps.Where(s => s.Order > step.Order).OrderBy(s => s.Order).FirstOrDefault();
        if (!approve)
        {
            Close(request, ApprovalStatus.Rejected);
        }
        else if (next is null)
        {
            Close(request, ApprovalStatus.Approved);
        }
        else
        {
            next.Status = ApprovalStepStatus.Current;
            request.CurrentStep = next.Order;
            await NotifyApproversAsync(request);
        }

        if (request.Status != ApprovalStatus.Pending)
        {
            var verb = request.Status == ApprovalStatus.Approved ? "approved" : "rejected";
            await notify.ToEmployeesAsync([request.EmployeeId], NotificationKind.RequestDecided,
                $"Your {Noun(type)} was {verb}", request.Summary + (string.IsNullOrWhiteSpace(note) ? "" : $"\n“{note}”"), MyPage(type));
        }
        return request.Status;
    }

    public async Task CancelAsync(ApprovalRequestType type, Guid subjectId)
    {
        var request = await LoadAsync(type, subjectId);
        if (request is { Status: ApprovalStatus.Pending })
            Close(request, ApprovalStatus.Cancelled);
    }

    /// <summary>Whether the signed-in user may decide the request now.</summary>
    public async Task<bool> CanDecideAsync(ApprovalRequestType type, Guid subjectId, Guid employeeId)
    {
        var request = await LoadAsync(type, subjectId);
        if (request is null)
            return await legacy.CanApproveForAsync(employeeId);
        var step = request.Steps.FirstOrDefault(s => s.Status == ApprovalStepStatus.Current);
        return request.Status == ApprovalStatus.Pending && step is not null && await ActingForAsync(request, step) is not null;
    }

    /// <summary>Whether the signed-in user is (or was) an approver on the request, so they may see its details.</summary>
    public async Task<bool> IsApproverAsync(ApprovalRequestType type, Guid subjectId, Guid employeeId)
    {
        if (me.IsHr())
            return true;
        var request = await LoadAsync(type, subjectId);
        if (request is null)
            return await legacy.CanApproveForAsync(employeeId);
        if (me.EmployeeId is not { } mine)
            return false;
        if (request.Steps.Any(s => s.ApproverEmployeeId == mine))
            return true;
        var delegators = await DelegatorsAsync(type);
        return request.Steps.Any(s => s.ApproverEmployeeId is { } a && delegators.Contains(a));
    }

    /// <summary>Ids of pending requests of a type whose current step waits for the signed-in user (or someone they stand in for).</summary>
    public async Task<IQueryable<Guid>> WaitingForMeAsync(ApprovalRequestType type)
    {
        var mine = me.EmployeeId;
        var forIds = await DelegatorsAsync(type);
        if (mine is { } m)
            forIds.Add(m);
        return db.ApprovalRequests
            .Where(a => a.RequestType == type && a.Status == ApprovalStatus.Pending && a.EmployeeId != mine
                        && a.Steps.Any(s => s.Status == ApprovalStepStatus.Current && s.ApproverEmployeeId != null && forIds.Contains(s.ApproverEmployeeId.Value)))
            .Select(a => a.SubjectId);
    }

    /// <summary>People who have handed the signed-in user their approvals (for this kind of request, or any) today.</summary>
    public async Task<List<Guid>> DelegatorsAsync(ApprovalRequestType? type)
    {
        if (me.EmployeeId is not { } mine)
            return [];
        var today = await clock.TodayAsync();
        var list = await db.ApprovalDelegations
            .Where(d => d.ToEmployeeId == mine && !d.IsCancelled && d.FromDate <= today && d.ToDate >= today)
            .Select(d => new { d.FromEmployeeId, d.RequestTypes }).ToListAsync();
        return list.Where(d => type is null || Covers(d.RequestTypes, type.Value)).Select(d => d.FromEmployeeId).Distinct().ToList();
    }

    public static bool Covers(string types, ApprovalRequestType type) =>
        string.IsNullOrEmpty(types) || types.Split(',').Contains(((int)type).ToString());

    /// <summary>Who stands in for the approver today, if anyone.</summary>
    private async Task<Guid?> DelegateForAsync(Guid approver, ApprovalRequestType type)
    {
        var today = await clock.TodayAsync();
        var list = await db.ApprovalDelegations
            .Where(d => d.FromEmployeeId == approver && !d.IsCancelled && d.FromDate <= today && d.ToDate >= today)
            .Select(d => new { d.ToEmployeeId, d.RequestTypes }).ToListAsync();
        return list.FirstOrDefault(d => Covers(d.RequestTypes, type))?.ToEmployeeId;
    }

    /// <summary>Ids of all requests of a type that have a chain (the rest predate the engine).</summary>
    public IQueryable<Guid> WithChain(ApprovalRequestType type) =>
        db.ApprovalRequests.Where(a => a.RequestType == type).Select(a => a.SubjectId);

    /// <summary>Ids of requests of a type the signed-in user has decided a step on.</summary>
    public IQueryable<Guid> DecidedByMe(ApprovalRequestType type)
    {
        var userId = me.UserId;
        return db.ApprovalRequests.Where(a => a.RequestType == type && a.Steps.Any(s => s.DecidedByUserId == userId)).Select(a => a.SubjectId);
    }

    /// <summary>Tells whoever must decide the current step: the named person, or everyone in HR for an HR step.</summary>
    private async Task NotifyApproversAsync(ApprovalRequest request)
    {
        var step = request.Steps.First(s => s.Status == ApprovalStepStatus.Current);
        var name = await db.Employees.Where(e => e.Id == request.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        var title = $"{name} needs your approval ({Noun(request.RequestType)})";
        if (step.ApproverEmployeeId is { } approver)
        {
            var to = new List<Guid> { approver };
            if (await DelegateForAsync(approver, request.RequestType) is { } stand)
                to.Add(stand);
            await notify.ToEmployeesAsync(to, NotificationKind.ApprovalNeeded, title, request.Summary, "/approvals");
        }
        else
            await notify.ToHrAsync(NotificationKind.ApprovalNeeded, title, request.Summary, "/approvals");
    }

    private static string Noun(ApprovalRequestType type) => type switch
    {
        ApprovalRequestType.Leave => "leave request",
        ApprovalRequestType.Expense => "expense claim",
        ApprovalRequestType.Attendance => "attendance request",
        ApprovalRequestType.CompOff => "comp-off request",
        ApprovalRequestType.PayRequest => "pay request",
        ApprovalRequestType.Loan => "loan request",
        ApprovalRequestType.ProfileChange => "profile change",
        ApprovalRequestType.ShiftChange => "shift request",
        ApprovalRequestType.Asset => "asset request",
        ApprovalRequestType.JobRequest => "hiring request",
        ApprovalRequestType.Travel => "travel request",
        ApprovalRequestType.Training => "training request",
        ApprovalRequestType.Exemption => "exemption request",
        ApprovalRequestType.OffDay => "days off request",
        ApprovalRequestType.Timesheet => "timesheet",
        ApprovalRequestType.FundWithdrawal => "fund withdrawal",
        ApprovalRequestType.ExpenseAdvance => "expense advance",
        ApprovalRequestType.FinalSettlement => "final settlement",
        _ => "request",
    };

    private static string MyPage(ApprovalRequestType type) => type switch
    {
        ApprovalRequestType.Leave => "/me/leave",
        ApprovalRequestType.Expense => "/me/expenses",
        ApprovalRequestType.Attendance => "/me/attendance",
        ApprovalRequestType.CompOff => "/me/leave",
        ApprovalRequestType.PayRequest => "/me/pay",
        ApprovalRequestType.Loan => "/me/pay",
        ApprovalRequestType.ProfileChange => "/me/profile",
        ApprovalRequestType.ShiftChange => "/me/attendance",
        ApprovalRequestType.Asset => "/me/assets",
        ApprovalRequestType.JobRequest => "/recruitment?tab=requests",
        ApprovalRequestType.Travel => "/travel",
        ApprovalRequestType.Training => "/training?tab=requests",
        ApprovalRequestType.Exemption => "/me/attendance",
        ApprovalRequestType.OffDay => "/me/attendance",
        ApprovalRequestType.Timesheet => "/timesheets",
        ApprovalRequestType.FundWithdrawal => "/me/pay",
        ApprovalRequestType.ExpenseAdvance => "/me/expenses",
        ApprovalRequestType.FinalSettlement => "/separations",
        _ => "/dashboard",
    };

    /// <summary>
    /// Whom the signed-in user acts for on this step: themselves (the approver, or HR stepping in; <see cref="Guid.Empty"/>
    /// when HR has no employee record) or the approver who delegated to them. Null when they cannot act.
    /// </summary>
    private async Task<Guid?> ActingForAsync(ApprovalRequest request, ApprovalStep step)
    {
        if (request.EmployeeId == me.EmployeeId)
            return null;
        if (me.EmployeeId is { } mine && step.ApproverEmployeeId == mine)
            return mine;
        if (step.ApproverEmployeeId is { } approver && (await DelegatorsAsync(request.RequestType)).Contains(approver))
            return approver;
        return me.IsHr() ? me.EmployeeId ?? Guid.Empty : null;
    }

    private void Close(ApprovalRequest request, ApprovalStatus status)
    {
        request.Status = status;
        request.CompletedAtUtc = clock.UtcNow;
        foreach (var s in request.Steps.Where(s => s.Status is ApprovalStepStatus.Waiting or ApprovalStepStatus.Current))
            s.Status = ApprovalStepStatus.Skipped;
    }

    private Task<ApprovalRequest?> LoadAsync(ApprovalRequestType type, Guid subjectId) =>
        db.ApprovalRequests.Include(a => a.Steps).FirstOrDefaultAsync(a => a.RequestType == type && a.SubjectId == subjectId);
}
