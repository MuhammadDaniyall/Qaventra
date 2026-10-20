using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record ApplyLeaveRequest(Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, [StringLength(1000)] string? Reason);

public record DecideLeaveRequest([StringLength(1000)] string? Note);

public record LeaveRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, Guid LeaveTypeId, string LeaveType,
    DateOnly FromDate, DateOnly ToDate, decimal Days, string? Reason, LeaveStatus Status,
    DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote, int AttachmentCount);

public record LeaveBalanceDto(Guid LeaveTypeId, string LeaveType, string Code, decimal Entitled, decimal Used, decimal Pending, decimal Available);

public record LeaveDaysDto(decimal Days);

[ApiController]
[Route("api/leave")]
[Authorize(Policy = Policies.CompanyUser)]
public class LeaveController(HrmsDbContext db, LeaveService leave, ApprovalEngine approvals, ICurrentUser me) : ControllerBase
{
    [HttpGet("balances")]
    public async Task<List<LeaveBalanceDto>> MyBalances([FromQuery] int? year)
    {
        var employeeId = MyEmployeeId();
        var y = year ?? DateTime.UtcNow.Year;
        await leave.EnsureBalancesAsync(employeeId, y);

        var pending = await db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId && r.Status == LeaveStatus.Pending && r.FromDate.Year == y)
            .GroupBy(r => r.LeaveTypeId).Select(g => new { g.Key, Days = g.Sum(r => r.Days) })
            .ToDictionaryAsync(x => x.Key, x => x.Days);

        var balances = await db.LeaveBalances.Where(b => b.EmployeeId == employeeId && b.Year == y)
            .Select(b => new { b.LeaveTypeId, b.LeaveType!.Name, b.LeaveType.Code, b.Entitled, b.Used })
            .OrderBy(b => b.Name).ToListAsync();

        return balances.Select(b =>
        {
            var p = pending.GetValueOrDefault(b.LeaveTypeId);
            return new LeaveBalanceDto(b.LeaveTypeId, b.Name, b.Code, b.Entitled, b.Used, p, b.Entitled - b.Used - p);
        }).ToList();
    }

    [HttpGet("count-days")]
    public async Task<LeaveDaysDto> CountDays([FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        new(await leave.CountDaysAsync(MyEmployeeId(), from, to));

    [HttpGet("my")]
    public Task<List<LeaveRequestDto>> My() =>
        Project(db.LeaveRequests.Where(r => r.EmployeeId == me.EmployeeId)).ToListAsync();

    [HttpPost]
    public async Task<LeaveRequestDto> Apply(ApplyLeaveRequest req)
    {
        var request = await leave.ApplyAsync(MyEmployeeId(), req.LeaveTypeId, req.FromDate, req.ToDate, req.Reason);
        return await Project(db.LeaveRequests.Where(r => r.Id == request.Id)).FirstAsync();
    }

    /// <summary>
    /// For HR, every request. For a manager: pending ones whose current approval step is theirs, and decided ones they
    /// took part in or that belong to their direct reports. Requests from before approval chains use the direct-report rule.
    /// </summary>
    [HttpGet("approvals"), Authorize(Policy = Policies.Approver)]
    public Task<List<LeaveRequestDto>> Approvals([FromQuery] LeaveStatus status = LeaveStatus.Pending)
    {
        var q = db.LeaveRequests.Where(r => r.Status == status && r.EmployeeId != me.EmployeeId);
        if (!me.IsHr())
        {
            var waiting = approvals.WaitingForMe(ApprovalRequestType.Leave);
            var chained = approvals.WithChain(ApprovalRequestType.Leave);
            var decided = approvals.DecidedByMe(ApprovalRequestType.Leave);
            q = status == LeaveStatus.Pending
                ? q.Where(r => waiting.Contains(r.Id) || (!chained.Contains(r.Id) && r.Employee!.ManagerId == me.EmployeeId))
                : q.Where(r => decided.Contains(r.Id) || r.Employee!.ManagerId == me.EmployeeId);
        }
        return Project(q).ToListAsync();
    }

    [HttpPost("{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, DecideLeaveRequest req)
    {
        await leave.DecideAsync(id, approve: true, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, DecideLeaveRequest req)
    {
        await leave.DecideAsync(id, approve: false, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await leave.CancelAsync(id);
        return NoContent();
    }

    private Guid MyEmployeeId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");

    private IQueryable<LeaveRequestDto> Project(IQueryable<Domain.Entities.LeaveRequest> q) =>
        q.OrderByDescending(r => r.CreatedAtUtc).Select(r => new LeaveRequestDto(
            r.Id, r.EmployeeId, r.Employee!.FirstName + " " + r.Employee.LastName, r.Employee.EmployeeCode,
            r.LeaveTypeId, r.LeaveType!.Name, r.FromDate, r.ToDate, r.Days, r.Reason, r.Status,
            r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote,
            db.Attachments.Count(a => a.OwnerType == AttachmentOwner.Leave && a.OwnerId == r.Id)));
}
