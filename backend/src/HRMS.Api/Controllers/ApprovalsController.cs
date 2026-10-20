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
    string Summary, int Step, int TotalSteps, bool AssignedToMe, DateTime CreatedAtUtc);

public record ApprovalStepDto(
    int Order, ApproverKind Kind, string? ApproverName, ApprovalStepStatus Status, string? DecidedByName, DateTime? DecidedAtUtc, string? Note);

public record ApprovalTimelineDto(ApprovalStatus Status, List<ApprovalStepDto> Steps);

[ApiController]
[Route("api/approvals")]
[Authorize(Policy = Policies.CompanyUser)]
public class ApprovalsController(HrmsDbContext db, ICurrentUser me) : ControllerBase
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
        var q = db.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending && a.EmployeeId != mine);
        if (!isHr)
            q = q.Where(a => a.Steps.Any(s => s.Status == ApprovalStepStatus.Current && s.ApproverEmployeeId == mine));

        var rows = await q.OrderBy(a => a.CreatedAtUtc).Take(500).Select(a => new
        {
            a.RequestType, a.SubjectId, a.EmployeeId, a.Employee!.FirstName, a.Employee.LastName, a.Employee.EmployeeCode,
            a.Summary, a.CurrentStep, Total = a.Steps.Count, a.CreatedAtUtc,
            Current = a.Steps.Where(s => s.Status == ApprovalStepStatus.Current).Select(s => new { s.Kind, s.ApproverEmployeeId }).FirstOrDefault(),
        }).ToListAsync();

        return rows.Select(r => new InboxItemDto(
            r.RequestType, r.SubjectId, r.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, r.Summary, r.CurrentStep, r.Total,
            r.Current is { } c && (c.ApproverEmployeeId == mine && mine != null || (c.Kind == ApproverKind.Hr && isHr)),
            r.CreatedAtUtc)).ToList();
    }

    /// <summary>The chain for one request, for the requester, its approvers and HR. 204 for requests that predate chains.</summary>
    [HttpGet("{type}/{subjectId:guid}")]
    public async Task<ActionResult<ApprovalTimelineDto>> Timeline(ApprovalRequestType type, Guid subjectId)
    {
        var a = await db.ApprovalRequests.Include(x => x.Steps).FirstOrDefaultAsync(x => x.RequestType == type && x.SubjectId == subjectId);
        if (a is null)
            return NoContent();
        var involved = a.EmployeeId == me.EmployeeId || me.IsHr() || (me.EmployeeId is { } mine && a.Steps.Any(s => s.ApproverEmployeeId == mine));
        if (!involved)
            throw new NotFoundException("Approval");

        var names = await EmployeeNamesAsync(a.Steps.Select(s => s.ApproverEmployeeId));
        return new ApprovalTimelineDto(a.Status, a.Steps.OrderBy(s => s.Order).Select(s => new ApprovalStepDto(
            s.Order, s.Kind, s.ApproverEmployeeId is { } id ? names.GetValueOrDefault(id) : null,
            s.Status, s.DecidedByName, s.DecidedAtUtc, s.Note)).ToList());
    }

    private async Task<Dictionary<Guid, string>> EmployeeNamesAsync(IEnumerable<Guid?> ids)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await db.Employees.Where(e => list.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
    }
}
