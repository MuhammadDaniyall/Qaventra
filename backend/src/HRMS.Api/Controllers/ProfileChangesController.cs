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

/// <summary>Field key (e.g. "phone", "bankAccount") to the new value; null or empty clears an optional field.</summary>
public record SubmitProfileChangeRequest([Required] Dictionary<string, string?> Changes, [StringLength(1000)] string? Reason);

public record DecideProfileChangeRequest([StringLength(1000)] string? Note);

public record ProfileChangeDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, IReadOnlyList<FieldChange> Changes, string? Reason,
    RequestStatus Status, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote);

[ApiController]
[Route("api/employees/profile-changes")]
[Authorize(Policy = Policies.CompanyUser)]
public class ProfileChangesController(HrmsDbContext db, ProfileChangeService changes, ApprovalEngine approvals, ICurrentUser me) : ControllerBase
{
    [HttpGet("my")]
    public Task<List<ProfileChangeDto>> My() => ProjectAsync(db.ProfileChangeRequests.Where(r => r.EmployeeId == me.EmployeeId));

    [HttpPost]
    public async Task<ProfileChangeDto> Submit(SubmitProfileChangeRequest req)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var r = await changes.SubmitAsync(employeeId, req.Changes, req.Reason);
        return (await ProjectAsync(db.ProfileChangeRequests.Where(x => x.Id == r.Id))).Single();
    }

    /// <summary>All for HR; for others, pending ones waiting on them and decided ones they took part in.</summary>
    [HttpGet, Authorize(Policy = Policies.Approver)]
    public Task<List<ProfileChangeDto>> List([FromQuery] RequestStatus status = RequestStatus.Pending)
    {
        var q = db.ProfileChangeRequests.Where(r => r.Status == status && r.EmployeeId != me.EmployeeId);
        if (!me.IsHr())
        {
            var mine = status == RequestStatus.Pending
                ? approvals.WaitingForMe(ApprovalRequestType.ProfileChange)
                : approvals.DecidedByMe(ApprovalRequestType.ProfileChange);
            q = q.Where(r => mine.Contains(r.Id));
        }
        return ProjectAsync(q);
    }

    [HttpPost("{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, DecideProfileChangeRequest req)
    {
        await changes.DecideAsync(id, approve: true, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, DecideProfileChangeRequest req)
    {
        await changes.DecideAsync(id, approve: false, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await changes.CancelAsync(id);
        return NoContent();
    }

    private async Task<List<ProfileChangeDto>> ProjectAsync(IQueryable<ProfileChangeRequest> q)
    {
        var rows = await q.OrderByDescending(r => r.CreatedAtUtc).Take(200).Select(r => new
        {
            r.Id, r.EmployeeId, r.Employee!.FirstName, r.Employee.LastName, r.Employee.EmployeeCode, r.ChangesJson, r.Reason,
            r.Status, r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote,
        }).ToListAsync();
        return rows.Select(r => new ProfileChangeDto(
            r.Id, r.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, ProfileChangeService.Read(r.ChangesJson), r.Reason,
            r.Status, r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote)).ToList();
    }
}
