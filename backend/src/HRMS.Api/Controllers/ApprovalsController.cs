using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record FlowStepDto(ApproverKind Kind, Guid? EmployeeId, string? EmployeeName);

public record ApprovalFlowDto(ApprovalRequestType RequestType, bool IsDefault, List<FlowStepDto> Steps);

public record SaveFlowStep(ApproverKind Kind, Guid? EmployeeId);

public record SaveApprovalFlowRequest([MaxLength(5)] List<SaveFlowStep> Steps);

public record InboxItemDto(
    ApprovalRequestType RequestType, Guid SubjectId, Guid EmployeeId, string EmployeeName, string EmployeeCode,
    string Summary, int Step, int TotalSteps, bool AssignedToMe, DateTime CreatedAtUtc, string? ActingFor, string? FiledBy = null);

public record DecideRequest(bool Approve, [StringLength(1000)] string? Note, bool Force = false);

public record DelegationDto(
    Guid Id, Guid FromEmployeeId, string From, Guid ToEmployeeId, string To, DateOnly FromDate, DateOnly ToDate, List<ApprovalRequestType> RequestTypes,
    string? Reason, bool IsCancelled, bool IsActive, string? CreatedByName, bool CanCancel);

public record SaveDelegationRequest(Guid? FromEmployeeId, Guid ToEmployeeId, DateOnly FromDate, DateOnly ToDate, List<ApprovalRequestType>? RequestTypes,
    [StringLength(500)] string? Reason);

public record ApprovalStepDto(
    int Order, ApproverKind Kind, string? ApproverName, ApprovalStepStatus Status, string? DecidedByName, DateTime? DecidedAtUtc, string? Note);

public record ApprovalTimelineDto(ApprovalStatus Status, List<ApprovalStepDto> Steps, bool Forced, string? FiledBy = null);

[ApiController]
[Route("api/approvals")]
[Authorize(Policy = Policies.CompanyUser)]
public class ApprovalsController(HrmsDbContext db, ICurrentUser me, ApprovalEngine engine, TenantClock clock, IServiceProvider services) : ControllerBase
{
    [HttpGet("flows"), Authorize(Policy = Policies.Hr)]
    public async Task<List<ApprovalFlowDto>> Flows()
    {
        var flows = await db.ApprovalFlows.Include(f => f.Steps).ToListAsync();
        var names = await EmployeeNamesAsync(flows.SelectMany(f => f.Steps).Select(s => s.EmployeeId));
        return Enum.GetValues<ApprovalRequestType>().Select(type =>
        {
            var flow = flows.FirstOrDefault(f => f.RequestType == type);
            var steps = flow is null
                ? ApprovalPlanner.DefaultFlow.Select(s => new FlowStepDto(s.Kind, s.EmployeeId, null)).ToList()
                : flow.Steps.OrderBy(s => s.Order).Select(s => new FlowStepDto(s.Kind, s.EmployeeId, s.EmployeeId is { } id ? names.GetValueOrDefault(id) : null)).ToList();
            return new ApprovalFlowDto(type, flow is null, steps);
        }).ToList();
    }

    /// <summary>Replaces a request type's chain. An empty list means requests are approved automatically.</summary>
    [HttpPut("flows/{type}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveFlow(ApprovalRequestType type, SaveApprovalFlowRequest req)
    {
        foreach (var step in req.Steps)
        {
            if (step.Kind == ApproverKind.SpecificEmployee)
            {
                if (step.EmployeeId is not { } id || !await db.Employees.AnyAsync(e => e.Id == id))
                    throw new DomainException("Choose the employee for each 'specific person' step.");
            }
            else if (step.EmployeeId is not null)
            {
                throw new DomainException("Only a 'specific person' step names an employee.");
            }
        }

        var flow = await db.ApprovalFlows.Include(f => f.Steps).FirstOrDefaultAsync(f => f.RequestType == type);
        if (flow is null)
        {
            flow = new ApprovalFlow { RequestType = type };
            db.ApprovalFlows.Add(flow);
        }
        else
        {
            db.ApprovalFlowSteps.RemoveRange(flow.Steps);
            flow.Steps.Clear();
        }
        var order = 0;
        foreach (var step in req.Steps)
        {
            var s = new ApprovalFlowStep { ApprovalFlowId = flow.Id, Order = ++order, Kind = step.Kind, EmployeeId = step.EmployeeId };
            flow.Steps.Add(s);
            db.ApprovalFlowSteps.Add(s);
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Everything waiting for the signed-in user. HR also sees requests waiting for others, to step in.</summary>
    [HttpGet("inbox"), Authorize(Policy = Policies.Approver)]
    public async Task<List<InboxItemDto>> Inbox()
    {
        var mine = me.EmployeeId;
        var isHr = me.IsHr();
        // Approvers who handed their approvals to me today, with the kinds of request they handed over.
        var today = await clock.TodayAsync();
        var delegations = mine is { } m0
            ? await db.ApprovalDelegations.Where(d => d.ToEmployeeId == m0 && !d.IsCancelled && d.FromDate <= today && d.ToDate >= today)
                .Select(d => new { d.FromEmployeeId, d.RequestTypes }).ToListAsync()
            : [];
        var forIds = delegations.Select(d => d.FromEmployeeId).Distinct().ToList();
        var q = db.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending && a.EmployeeId != mine);
        if (!isHr)
            q = q.Where(a => a.Steps.Any(s => s.Status == ApprovalStepStatus.Current && s.ApproverEmployeeId != null
                                              && (s.ApproverEmployeeId == mine || forIds.Contains(s.ApproverEmployeeId.Value))));

        var rows = await q.OrderBy(a => a.CreatedAtUtc).Take(500).Select(a => new
        {
            a.RequestType, a.SubjectId, a.EmployeeId, a.Employee!.FirstName, a.Employee.LastName, a.Employee.EmployeeCode,
            a.Summary, a.CurrentStep, Total = a.Steps.Count, a.CreatedAtUtc, a.FiledByName,
            Current = a.Steps.Where(s => s.Status == ApprovalStepStatus.Current).Select(s => new { s.Kind, s.ApproverEmployeeId }).FirstOrDefault(),
        }).ToListAsync();

        var forNames = await EmployeeNamesAsync(forIds.Select(x => (Guid?)x));
        bool Delegated(ApprovalRequestType type, Guid? approver) =>
            approver is { } a && delegations.Any(d => d.FromEmployeeId == a && ApprovalEngine.Covers(d.RequestTypes, type));
        return rows
            .Where(r => isHr || r.Current is { } c0 && (c0.ApproverEmployeeId == mine || Delegated(r.RequestType, c0.ApproverEmployeeId)))
            .Select(r =>
            {
                var delegated = r.Current is { } c1 && c1.ApproverEmployeeId != mine && Delegated(r.RequestType, c1.ApproverEmployeeId);
                return new InboxItemDto(
                    r.RequestType, r.SubjectId, r.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, r.Summary, r.CurrentStep, r.Total,
                    r.Current is { } c && (c.ApproverEmployeeId == mine && mine != null || (c.Kind == ApproverKind.Hr && isHr) || delegated),
                    r.CreatedAtUtc, delegated ? forNames.GetValueOrDefault(r.Current!.ApproverEmployeeId!.Value) : null, r.FiledByName);
            }).ToList();
    }

    /// <summary>The chain for one request, for the requester, its approvers and HR. 204 for requests that predate chains.</summary>
    [HttpGet("{type}/{subjectId:guid}")]
    public async Task<ActionResult<ApprovalTimelineDto>> Timeline(ApprovalRequestType type, Guid subjectId)
    {
        var a = await db.ApprovalRequests.Include(x => x.Steps).FirstOrDefaultAsync(x => x.RequestType == type && x.SubjectId == subjectId);
        if (a is null)
            return NoContent();
        var involved = a.EmployeeId == me.EmployeeId || await engine.IsApproverAsync(type, subjectId, a.EmployeeId);
        if (!involved)
            throw new NotFoundException("Approval");

        var names = await EmployeeNamesAsync(a.Steps.Select(s => s.ApproverEmployeeId));
        return new ApprovalTimelineDto(a.Status, a.Steps.OrderBy(s => s.Order).Select(s => new ApprovalStepDto(
            s.Order, s.Kind, s.ApproverEmployeeId is { } id ? names.GetValueOrDefault(id) : null,
            s.Status, s.DecidedByName, s.DecidedAtUtc, s.Note)).ToList(), a.Forced, a.FiledByName);
    }

    /// <summary>
    /// Approve or reject any kind of request from one place. With <c>force</c>, HR approves every remaining step at once
    /// ("forcefully approved"); the module then applies the request as for a normal final approval.
    /// </summary>
    [HttpPost("{type}/{subjectId:guid}/decide")]
    public async Task<IActionResult> Decide(ApprovalRequestType type, Guid subjectId, DecideRequest req)
    {
        if (req.Force)
        {
            if (!me.IsHr())
                throw new ForbiddenException("Only HR can approve over the chain.");
            if (!req.Approve)
                throw new DomainException("Force is for approving; reject as usual.");
            if (string.IsNullOrWhiteSpace(req.Note))
                throw new DomainException("Say why you are approving over the chain.");
            engine.Force = true;
        }
        Func<Guid, bool, string?, Task> decide = type switch
        {
            ApprovalRequestType.Leave => services.GetRequiredService<LeaveService>().DecideAsync,
            ApprovalRequestType.Expense => services.GetRequiredService<ExpenseService>().DecideAsync,
            ApprovalRequestType.Attendance => services.GetRequiredService<AttendanceRequestService>().DecideAsync,
            ApprovalRequestType.CompOff => services.GetRequiredService<CompOffService>().DecideAsync,
            ApprovalRequestType.PayRequest => services.GetRequiredService<PayRequestService>().DecideAsync,
            ApprovalRequestType.Loan => services.GetRequiredService<PayRequestService>().DecideLoanAsync,
            ApprovalRequestType.ProfileChange => services.GetRequiredService<ProfileChangeService>().DecideAsync,
            ApprovalRequestType.ShiftChange => services.GetRequiredService<ShiftRequestService>().DecideAsync,
            ApprovalRequestType.Asset => services.GetRequiredService<AssetService>().DecideAsync,
            ApprovalRequestType.JobRequest => services.GetRequiredService<RecruitmentService>().DecideRequestAsync,
            ApprovalRequestType.Travel => services.GetRequiredService<TravelService>().DecideAsync,
            ApprovalRequestType.Training => services.GetRequiredService<TrainingService>().DecideAsync,
            ApprovalRequestType.Exemption => services.GetRequiredService<ExemptionService>().DecideAsync,
            ApprovalRequestType.OffDay => services.GetRequiredService<OffDayService>().DecideAsync,
            ApprovalRequestType.Timesheet => services.GetRequiredService<TimesheetService>().DecideAsync,
            ApprovalRequestType.FundWithdrawal => services.GetRequiredService<FundService>().DecideAsync,
            ApprovalRequestType.ExpenseAdvance => services.GetRequiredService<ExpenseAdvanceService>().DecideAsync,
            ApprovalRequestType.FinalSettlement => services.GetRequiredService<SettlementApprovalService>().DecideAsync,
            _ => throw new NotFoundException("Request type"),
        };
        await decide(subjectId, req.Approve, req.Note);
        return NoContent();
    }

    // ---------- Delegation ----------

    /// <summary>Delegations from or to the signed-in user; HR sees all.</summary>
    [HttpGet("delegations")]
    public async Task<List<DelegationDto>> Delegations()
    {
        var mine = me.EmployeeId;
        var q = db.ApprovalDelegations.AsQueryable();
        if (!me.IsHr())
            q = q.Where(d => d.FromEmployeeId == mine || d.ToEmployeeId == mine);
        var list = await q.OrderByDescending(d => d.FromDate).Take(300).ToListAsync();
        var names = await EmployeeNamesAsync(list.SelectMany(d => new Guid?[] { d.FromEmployeeId, d.ToEmployeeId }));
        var today = await clock.TodayAsync();
        return list.Select(d => new DelegationDto(d.Id, d.FromEmployeeId, names.GetValueOrDefault(d.FromEmployeeId) ?? "", d.ToEmployeeId,
            names.GetValueOrDefault(d.ToEmployeeId) ?? "", d.FromDate, d.ToDate,
            string.IsNullOrEmpty(d.RequestTypes) ? [] : d.RequestTypes.Split(',').Select(x => (ApprovalRequestType)int.Parse(x)).ToList(),
            d.Reason, d.IsCancelled, !d.IsCancelled && d.FromDate <= today && d.ToDate >= today, d.CreatedByName,
            !d.IsCancelled && d.ToDate >= today && (me.IsHr() || d.FromEmployeeId == mine))).ToList();
    }

    /// <summary>
    /// Hand approvals to someone for a period. Approvers delegate their own; HR can set one up for anyone
    /// (e.g. when a manager is away unexpectedly).
    /// </summary>
    [HttpPost("delegations")]
    public async Task<IActionResult> Delegate(SaveDelegationRequest req, [FromServices] NotificationService notify)
    {
        var from = req.FromEmployeeId is { } f && me.IsHr() ? f : me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        if (req.ToEmployeeId == from)
            throw new DomainException("Choose someone else to approve for you.");
        var today = await clock.TodayAsync();
        if (req.ToDate < req.FromDate || req.ToDate < today)
            throw new DomainException("The end date must be today or later, and after the start.");
        if (req.ToDate.DayNumber - req.FromDate.DayNumber > 365)
            throw new DomainException("A delegation lasts at most a year.");
        if (!await db.Employees.AnyAsync(e => e.Id == req.ToEmployeeId && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new NotFoundException("Employee");
        if (!await db.Users.AnyAsync(u => u.EmployeeId == req.ToEmployeeId && u.IsActive))
            throw new DomainException("That person has no login, so they cannot approve.");
        var types = (req.RequestTypes ?? []).Distinct().ToList();
        if (types.Any(t => !Enum.IsDefined(t)))
            throw new DomainException("Unknown request type.");
        var typeText = string.Join(',', types.Select(t => (int)t).OrderBy(x => x));
        var overlapping = await db.ApprovalDelegations.Where(d => d.FromEmployeeId == from && !d.IsCancelled && d.FromDate <= req.ToDate && d.ToDate >= req.FromDate)
            .Select(d => d.RequestTypes).ToListAsync();
        if (overlapping.Any(o => string.IsNullOrEmpty(o) || types.Count == 0 || types.Any(t => ApprovalEngine.Covers(o, t))))
            throw new DomainException("These approvals are already delegated for some of these days.");
        if (await db.ApprovalDelegations.AnyAsync(d => d.FromEmployeeId == req.ToEmployeeId && d.ToEmployeeId == from && !d.IsCancelled && d.FromDate <= req.ToDate && d.ToDate >= req.FromDate))
            throw new DomainException("That person has delegated their approvals to this approver on these days.");
        db.ApprovalDelegations.Add(new ApprovalDelegation
        {
            FromEmployeeId = from, ToEmployeeId = req.ToEmployeeId, FromDate = req.FromDate, ToDate = req.ToDate, RequestTypes = typeText,
            Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(), CreatedByName = await LetterService.CurrentUserNameAsync(db, me),
        });
        var fromName = await db.Employees.Where(e => e.Id == from).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        await notify.ToEmployeesAsync([req.ToEmployeeId, from], NotificationKind.ApprovalNeeded, $"You approve for {fromName} from {req.FromDate:d MMM} to {req.ToDate:d MMM}",
            types.Count == 0 ? "All kinds of requests." : string.Join(", ", types), "/approvals");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("delegations/{id:guid}/cancel")]
    public async Task<IActionResult> CancelDelegation(Guid id)
    {
        var d = await db.ApprovalDelegations.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Delegation");
        if (d.FromEmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("Only the approver or HR can end this delegation.");
        if (d.IsCancelled)
            return NoContent();
        var today = await clock.TodayAsync();
        // Not started (or started today): drop it. Running: end it yesterday, keeping the days it was used.
        if (d.FromDate >= today)
            d.IsCancelled = true;
        else
            d.ToDate = today.AddDays(-1);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Dictionary<Guid, string>> EmployeeNamesAsync(IEnumerable<Guid?> ids)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await db.Employees.Where(e => list.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
    }
}
