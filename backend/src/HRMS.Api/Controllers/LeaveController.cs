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

public record ApplyLeaveRequest(
    Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, [StringLength(1000)] string? Reason,
    LeaveDuration Duration = LeaveDuration.FullDay, HalfDayPart? HalfDayPart = null, TimeOnly? StartTime = null, TimeOnly? EndTime = null);

public record DecideLeaveRequest([StringLength(1000)] string? Note);

public record LeaveRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, Guid LeaveTypeId, string LeaveType,
    DateOnly FromDate, DateOnly ToDate, decimal Days, string? Reason, LeaveStatus Status,
    DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote, int AttachmentCount,
    LeaveDuration Duration, HalfDayPart? HalfDayPart, TimeOnly? StartTime, TimeOnly? EndTime);

/// <summary>
/// Entitled = the year's quota; Earned = how much of it is available so far (monthly accrual);
/// Available = earned + carried forward + adjustment + comp-off credits - used - pending.
/// </summary>
public record LeaveBalanceDto(
    Guid LeaveTypeId, string LeaveType, string Code, decimal Entitled, decimal Earned, decimal CarriedForward, decimal Adjustment,
    string? AdjustmentNote, decimal Credited, decimal Used, decimal Pending, decimal Available, LeaveAccrual Accrual, bool IsCompensatory);

public record AdjustBalanceRequest([Range(-365, 365)] decimal Adjustment, [Required, StringLength(300)] string Note);

public record LeaveDaysDto(decimal Days);

[ApiController]
[Route("api/leave")]
[Authorize(Policy = Policies.CompanyUser)]
public class LeaveController(HrmsDbContext db, LeaveService leave, ApprovalEngine approvals, ICurrentUser me) : ControllerBase
{
    [HttpGet("balances")]
    public async Task<List<LeaveBalanceDto>> MyBalances([FromQuery] int? year) =>
        ToDtos(await leave.BalancesAsync(MyEmployeeId(), year ?? DateTime.UtcNow.Year));

    /// <summary>HR: any employee's balances for a year.</summary>
    [HttpGet("balances/{employeeId:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<List<LeaveBalanceDto>> EmployeeBalances(Guid employeeId, [FromQuery] int? year)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId))
            throw new Infrastructure.NotFoundException("Employee");
        return ToDtos(await leave.BalancesAsync(employeeId, year ?? DateTime.UtcNow.Year));
    }

    /// <summary>HR: add or remove days from a balance (replaces the previous adjustment), with a reason for the record.</summary>
    [HttpPut("balances/{employeeId:guid}/{leaveTypeId:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AdjustBalance(Guid employeeId, Guid leaveTypeId, [FromQuery] int year, AdjustBalanceRequest req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId))
            throw new Infrastructure.NotFoundException("Employee");
        await leave.EnsureBalancesAsync(employeeId, year);
        var balance = await db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year)
            ?? throw new Infrastructure.NotFoundException("Leave balance");
        balance.Adjustment = req.Adjustment;
        balance.AdjustmentNote = req.Note.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static List<LeaveBalanceDto> ToDtos(List<LeaveService.BalanceView> views) => views.Select(v => new LeaveBalanceDto(
        v.Type.Id, v.Type.Name, v.Type.Code, v.Balance.Entitled, v.Earned, v.Balance.CarriedForward, v.Balance.Adjustment,
        v.Balance.AdjustmentNote, v.Balance.Credited, v.Balance.Used, v.Pending, v.Available, v.Type.Accrual, v.Type.IsCompensatory)).ToList();

    [HttpGet("count-days")]
    public async Task<LeaveDaysDto> CountDays([FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        new(await leave.CountDaysAsync(MyEmployeeId(), from, to));

    [HttpGet("my")]
    public Task<List<LeaveRequestDto>> My() =>
        Project(db.LeaveRequests.Where(r => r.EmployeeId == me.EmployeeId)).ToListAsync();

    [HttpPost]
    public async Task<LeaveRequestDto> Apply(ApplyLeaveRequest req)
    {
        var request = await leave.ApplyAsync(MyEmployeeId(), new LeaveService.LeaveApplication(
            req.LeaveTypeId, req.FromDate, req.ToDate, req.Reason, req.Duration, req.HalfDayPart, req.StartTime, req.EndTime));
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
            db.Attachments.Count(a => a.OwnerType == AttachmentOwner.Leave && a.OwnerId == r.Id),
            r.Duration, r.HalfDayPart, r.StartTime, r.EndTime));
}
