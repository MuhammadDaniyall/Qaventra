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
        var chain = await db.Employees.Where(e => e.Id == employeeId)
            .Select(e => new { e.ManagerId, SecondId = e.Manager != null ? e.Manager.ManagerId : null })
            .FirstAsync();
        var planned = ApprovalPlanner.Plan(await FlowAsync(type), employeeId, chain.ManagerId, chain.SecondId);

        var request = new ApprovalRequest
        {
            RequestType = type,
            SubjectId = subjectId,
            EmployeeId = employeeId,
            Summary = summary.Length > 300 ? summary[..300] : summary,
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
        if (!CanAct(request, step))
            throw new ForbiddenException("This request is waiting for someone else's approval.");

        step.Status = approve ? ApprovalStepStatus.Approved : ApprovalStepStatus.Rejected;
        step.DecidedByUserId = me.UserId;
        step.DecidedByName = await LetterService.CurrentUserNameAsync(db, me);
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
        return request.Status == ApprovalStatus.Pending && step is not null && CanAct(request, step);
    }

    /// <summary>Whether the signed-in user is (or was) an approver on the request, so they may see its details.</summary>
    public async Task<bool> IsApproverAsync(ApprovalRequestType type, Guid subjectId, Guid employeeId)
    {
        if (me.IsHr())
            return true;
        var request = await LoadAsync(type, subjectId);
        if (request is null)
            return await legacy.CanApproveForAsync(employeeId);
        return me.EmployeeId is { } mine && request.Steps.Any(s => s.ApproverEmployeeId == mine);
    }

    /// <summary>Ids of pending requests of a type whose current step waits for the signed-in user.</summary>
    public IQueryable<Guid> WaitingForMe(ApprovalRequestType type)
    {
        var mine = me.EmployeeId;
        return db.ApprovalRequests
            .Where(a => a.RequestType == type && a.Status == ApprovalStatus.Pending
                        && a.Steps.Any(s => s.Status == ApprovalStepStatus.Current && s.ApproverEmployeeId == mine))
            .Select(a => a.SubjectId);
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
            await notify.ToEmployeesAsync([approver], NotificationKind.ApprovalNeeded, title, request.Summary, "/approvals");
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
        _ => "/dashboard",
    };

    private bool CanAct(ApprovalRequest request, ApprovalStep step) =>
        request.EmployeeId != me.EmployeeId
        && (me.IsHr() || (me.EmployeeId is { } mine && step.ApproverEmployeeId == mine));

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
