using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record SubmitCompOffRequest(DateOnly WorkedOn, decimal Days, [Required, StringLength(1000)] string Reason, Guid? EmployeeId = null);

public record CompOffDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, DateOnly WorkedOn, decimal Days, string Reason,
    RequestStatus Status, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote, int? WorkedMinutes);

[ApiController]
[Route("api/leave/comp-off")]
[Authorize(Policy = Policies.CompanyUser)]
public class CompOffController(HrmsDbContext db, CompOffService compOff, ICurrentUser me, OnBehalfService onBehalf) : ControllerBase
{
    [HttpGet("my")]
    public Task<List<CompOffDto>> My() => Project(db.CompOffRequests.Where(c => c.EmployeeId == me.EmployeeId)).ToListAsync();

    [HttpPost]
    public async Task<CompOffDto> Submit(SubmitCompOffRequest req)
    {
        var employeeId = await onBehalf.ResolveAsync(req.EmployeeId);
        var c = await compOff.SubmitAsync(employeeId, req.WorkedOn, req.Days, req.Reason);
        return await Project(db.CompOffRequests.Where(x => x.Id == c.Id)).FirstAsync();
    }

    [HttpPost("{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, DecideAttendanceRequest req)
    {
        await compOff.DecideAsync(id, approve: true, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, DecideAttendanceRequest req)
    {
        await compOff.DecideAsync(id, approve: false, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await compOff.CancelAsync(id);
        return NoContent();
    }

    private IQueryable<CompOffDto> Project(IQueryable<CompOffRequest> q) =>
        q.OrderByDescending(c => c.CreatedAtUtc).Select(c => new CompOffDto(
            c.Id, c.EmployeeId, c.Employee!.FirstName + " " + c.Employee.LastName, c.Employee.EmployeeCode, c.WorkedOn, c.Days, c.Reason,
            c.Status, c.CreatedAtUtc, c.DecidedAtUtc, c.DecisionNote,
            db.AttendanceRecords.Where(a => a.EmployeeId == c.EmployeeId && a.Date == c.WorkedOn).Select(a => (int?)a.WorkedMinutes).FirstOrDefault()));
}
