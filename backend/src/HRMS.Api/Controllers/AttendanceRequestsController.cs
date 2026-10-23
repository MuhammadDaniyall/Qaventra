using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record SubmitAttendanceRequest(DateOnly Date, AttendanceRequestKind Kind, TimeOnly CheckIn, TimeOnly? CheckOut, [Required, StringLength(1000)] string Reason);

public record DecideAttendanceRequest([StringLength(1000)] string? Note);

public record AttendanceRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, DateOnly Date, AttendanceRequestKind Kind,
    TimeOnly CheckIn, TimeOnly? CheckOut, string Reason, RequestStatus Status, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote,
    TimeOnly? RecordedCheckIn, TimeOnly? RecordedCheckOut);

[ApiController]
[Route("api/attendance/requests")]
[Authorize(Policy = Policies.CompanyUser)]
public class AttendanceRequestsController(HrmsDbContext db, AttendanceRequestService requests, ApprovalEngine approvals, TenantClock clock, ICurrentUser me) : ControllerBase
{
    [HttpGet("my")]
    public Task<List<AttendanceRequestDto>> My() => ProjectAsync(db.AttendanceRequests.Where(r => r.EmployeeId == me.EmployeeId));

    [HttpPost]
    public async Task<AttendanceRequestDto> Submit(SubmitAttendanceRequest req)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var r = await requests.SubmitAsync(employeeId, req.Date, req.Kind, req.CheckIn, req.CheckOut, req.Reason);
        return (await ProjectAsync(db.AttendanceRequests.Where(x => x.Id == r.Id))).Single();
    }

    /// <summary>All for HR; for others, pending ones waiting on them and decided ones they took part in or that are their reports'.</summary>
    [HttpGet("approvals"), Authorize(Policy = Policies.Approver)]
    public async Task<List<AttendanceRequestDto>> Approvals([FromQuery] RequestStatus status = RequestStatus.Pending)
    {
        var q = db.AttendanceRequests.Where(r => r.Status == status && r.EmployeeId != me.EmployeeId);
        if (!me.IsHr())
        {
            var waiting = await approvals.WaitingForMeAsync(ApprovalRequestType.Attendance);
            var decided = approvals.DecidedByMe(ApprovalRequestType.Attendance);
            q = status == RequestStatus.Pending
                ? q.Where(r => waiting.Contains(r.Id))
                : q.Where(r => decided.Contains(r.Id) || r.Employee!.ManagerId == me.EmployeeId);
        }
        return await ProjectAsync(q);
    }

    [HttpPost("{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, DecideAttendanceRequest req)
    {
        await requests.DecideAsync(id, approve: true, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, DecideAttendanceRequest req)
    {
        await requests.DecideAsync(id, approve: false, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await requests.CancelAsync(id);
        return NoContent();
    }

    /// <summary>Includes what is recorded for that day now, so approvers see what will change.</summary>
    private async Task<List<AttendanceRequestDto>> ProjectAsync(IQueryable<AttendanceRequest> q)
    {
        var rows = await q.OrderByDescending(r => r.CreatedAtUtc).Take(500).Select(r => new
        {
            r.Id, r.EmployeeId, Name = r.Employee!.FirstName + " " + r.Employee.LastName, r.Employee.EmployeeCode, r.Date, r.Kind,
            r.CheckIn, r.CheckOut, r.Reason, r.Status, r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote,
            Recorded = db.AttendanceRecords.Where(a => a.EmployeeId == r.EmployeeId && a.Date == r.Date)
                .Select(a => new { a.CheckInUtc, a.CheckOutUtc }).FirstOrDefault(),
        }).ToListAsync();
        var zone = await clock.ZoneAsync();
        TimeOnly Local(DateTime utc) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
        return rows.Select(r => new AttendanceRequestDto(
            r.Id, r.EmployeeId, r.Name, r.EmployeeCode, r.Date, r.Kind, r.CheckIn, r.CheckOut, r.Reason, r.Status, r.CreatedAtUtc,
            r.DecidedAtUtc, r.DecisionNote,
            r.Recorded is null ? null : Local(r.Recorded.CheckInUtc),
            r.Recorded?.CheckOutUtc is { } o ? Local(o) : null)).ToList();
    }
}
