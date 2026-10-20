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

public record ResignRequest(DateOnly ProposedLastDay, [StringLength(2000)] string? Reason);

public record InitiateSeparationRequest(
    Guid EmployeeId,
    SeparationType Type,
    DateOnly NoticeDate,
    DateOnly LastWorkingDay,
    [Range(0, 365)] int NoticePeriodDays,
    [StringLength(2000)] string? Reason);

public record AcceptSeparationRequest(DateOnly LastWorkingDay, [Range(0, 365)] int NoticePeriodDays, [StringLength(1000)] string? Note);

public record DeclineSeparationRequest([StringLength(1000)] string? Note);

public record ExitInterviewRequest([StringLength(100)] string? ExitReason, [Range(1, 5)] int Rating, bool WouldRecommend, [StringLength(4000)] string? Feedback);

public record AddClearanceItemRequest([Required, StringLength(100)] string Department, [Required, StringLength(200)] string Title);

public record UpdateClearanceItemRequest(bool IsCleared, [StringLength(500)] string? Note);

public record SaveSettlementRequest(
    [Range(0, 365)] decimal EncashedLeaveDays,
    bool RecoverNoticeShortfall,
    [Range(0, 100_000_000)] decimal OtherAdditions,
    [Range(0, 100_000_000)] decimal OtherDeductions,
    [StringLength(1000)] string? Note);

public record SeparationListItemDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Department, string? Designation,
    SeparationType Type, SeparationStatus Status, DateOnly NoticeDate, DateOnly ProposedLastDay, DateOnly? LastWorkingDay,
    int ClearanceDone, int ClearanceTotal, DateTime CreatedAtUtc);

public record ClearanceItemDto(Guid Id, string Department, string Title, bool IsCleared, string? ClearedByName, DateTime? ClearedAtUtc, string? Note);

public record SeparationDto(
    SeparationListItemDto Summary, DateOnly JoiningDate, int NoticePeriodDays, string? Reason, string? DecisionNote, DateTime? DecidedAtUtc,
    string? ExitReason, int? ExitRating, bool? WouldRecommend, string? ExitFeedback, DateTime? ExitInterviewAtUtc,
    List<ClearanceItemDto> Clearance, SettlementDto? Settlement, DateTime? CompletedAtUtc);

public record SettlementDto(
    bool HasSalary, decimal MonthlyGross, decimal DailyRate, List<LeaveBalanceLeft> LeaveBalances, List<PayableClaim> Claims,
    decimal EncashedLeaveDays, decimal LeaveEncashment, int NoticeServedDays, int NoticeShortfallDays, bool RecoverNoticeShortfall,
    decimal NoticeRecovery, decimal ExpenseReimbursement, decimal OtherAdditions, decimal OtherDeductions, string? Note,
    decimal Net, DateTime? SavedAtUtc);

[ApiController]
[Route("api/separations")]
[Authorize(Policy = Policies.CompanyUser)]
public class SeparationsController(HrmsDbContext db, SeparationService separations, ICurrentUser me) : ControllerBase
{
    /// <summary>The signed-in employee's latest separation, or 204 if they never started one.</summary>
    [HttpGet("my")]
    public async Task<ActionResult<SeparationDto>> My()
    {
        var id = await db.Separations.Where(s => s.EmployeeId == me.EmployeeId)
            .OrderByDescending(s => s.CreatedAtUtc).Select(s => (Guid?)s.Id).FirstOrDefaultAsync();
        return id is null ? NoContent() : await Detail(id.Value, forHr: false);
    }

    [HttpPost("resign")]
    public async Task<SeparationDto> Resign(ResignRequest req)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var s = await separations.ResignAsync(employeeId, req.ProposedLastDay, req.Reason);
        return await Detail(s.Id, forHr: false);
    }

    [HttpGet, Authorize(Policy = Policies.Hr)]
    public async Task<List<SeparationListItemDto>> List([FromQuery] SeparationStatus? status)
    {
        var q = db.Separations.AsQueryable();
        if (status is { } st)
            q = q.Where(s => s.Status == st);
        return await ProjectList(q.OrderByDescending(s => s.CreatedAtUtc)).Take(500).ToListAsync();
    }

    [HttpPost, Authorize(Policy = Policies.Hr)]
    public async Task<SeparationDto> Initiate(InitiateSeparationRequest req)
    {
        var s = await separations.InitiateAsync(req.EmployeeId, req.Type, req.NoticeDate, req.LastWorkingDay, req.NoticePeriodDays, req.Reason);
        return await Detail(s.Id, forHr: true);
    }

    [HttpGet("{id:guid}")]
    public async Task<SeparationDto> Get(Guid id)
    {
        var owner = await db.Separations.Where(s => s.Id == id).Select(s => (Guid?)s.EmployeeId).FirstOrDefaultAsync()
            ?? throw new NotFoundException("Separation");
        if (!me.IsHr() && owner != me.EmployeeId)
            throw new NotFoundException("Separation");
        return await Detail(id, forHr: me.IsHr() && owner != me.EmployeeId);
    }

    [HttpPost("{id:guid}/accept"), Authorize(Policy = Policies.Hr)]
    public async Task<SeparationDto> Accept(Guid id, AcceptSeparationRequest req)
    {
        await separations.ApproveAsync(id, req.LastWorkingDay, req.NoticePeriodDays, req.Note);
        return await Detail(id, forHr: true);
    }

    [HttpPost("{id:guid}/decline"), Authorize(Policy = Policies.Hr)]
    public async Task<SeparationDto> Decline(Guid id, DeclineSeparationRequest req)
    {
        await separations.RejectAsync(id, req.Note);
        return await Detail(id, forHr: false);
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        await separations.WithdrawAsync(id);
        return NoContent();
    }

    [HttpPut("{id:guid}/exit-interview")]
    public async Task<SeparationDto> ExitInterview(Guid id, ExitInterviewRequest req)
    {
        await separations.SaveExitInterviewAsync(id, req.ExitReason, req.Rating, req.WouldRecommend, req.Feedback);
        return await Detail(id, forHr: false);
    }

    [HttpPost("{id:guid}/clearance"), Authorize(Policy = Policies.Hr)]
    public async Task<ClearanceItemDto> AddClearanceItem(Guid id, AddClearanceItemRequest req)
    {
        var c = await separations.AddClearanceItemAsync(id, req.Department, req.Title);
        return new ClearanceItemDto(c.Id, c.Department, c.Title, c.IsCleared, c.ClearedByName, c.ClearedAtUtc, c.Note);
    }

    [HttpPut("{id:guid}/clearance/{itemId:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateClearanceItem(Guid id, Guid itemId, UpdateClearanceItemRequest req)
    {
        await separations.UpdateClearanceItemAsync(id, itemId, req.IsCleared, req.Note);
        return NoContent();
    }

    [HttpDelete("{id:guid}/clearance/{itemId:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteClearanceItem(Guid id, Guid itemId)
    {
        await separations.DeleteClearanceItemAsync(id, itemId);
        return NoContent();
    }

    [HttpPut("{id:guid}/settlement"), Authorize(Policy = Policies.Hr)]
    public async Task<SeparationDto> SaveSettlement(Guid id, SaveSettlementRequest req)
    {
        await separations.SaveSettlementAsync(id, new SettlementInputs(req.EncashedLeaveDays, req.RecoverNoticeShortfall, req.OtherAdditions, req.OtherDeductions, req.Note));
        return await Detail(id, forHr: true);
    }

    [HttpPost("{id:guid}/complete"), Authorize(Policy = Policies.Hr)]
    public async Task<SeparationDto> Complete(Guid id)
    {
        await separations.CompleteAsync(id);
        return await Detail(id, forHr: true);
    }

    /// <summary>HR sees the settlement while it is being worked out; the employee only once it is final.</summary>
    private async Task<SeparationDto> Detail(Guid id, bool forHr)
    {
        var s = await separations.LoadAsync(id);
        var summary = await ProjectList(db.Separations.Where(x => x.Id == id)).FirstAsync();
        var joining = await db.Employees.Where(e => e.Id == s.EmployeeId).Select(e => e.JoiningDate).FirstAsync();

        SettlementDto? settlement = null;
        if (s.Status == SeparationStatus.Completed || (forHr && s.Status == SeparationStatus.InProgress))
        {
            var snap = await separations.SettlementAsync(s);
            var r = snap.Result;
            settlement = new SettlementDto(
                snap.HasSalary, snap.MonthlyGross, r.DailyRate, snap.LeaveBalances.ToList(), snap.Claims.ToList(),
                s.EncashedLeaveDays, r.LeaveEncashment, r.NoticeServedDays, r.NoticeShortfallDays, s.RecoverNoticeShortfall,
                r.NoticeRecovery, r.ExpenseReimbursement, r.OtherAdditions, r.OtherDeductions, s.SettlementNote,
                r.Net, s.SettlementSavedAtUtc);
        }

        return new SeparationDto(
            summary, joining, s.NoticePeriodDays, s.Reason, s.DecisionNote, s.DecidedAtUtc,
            s.ExitReason, s.ExitRating, s.WouldRecommend, s.ExitFeedback, s.ExitInterviewAtUtc,
            s.Clearance.OrderBy(c => c.SortOrder)
                .Select(c => new ClearanceItemDto(c.Id, c.Department, c.Title, c.IsCleared, c.ClearedByName, c.ClearedAtUtc, c.Note)).ToList(),
            settlement, s.CompletedAtUtc);
    }

    private static IQueryable<SeparationListItemDto> ProjectList(IQueryable<Separation> q) =>
        q.Select(s => new SeparationListItemDto(
            s.Id, s.EmployeeId, s.Employee!.FirstName + " " + s.Employee.LastName, s.Employee.EmployeeCode,
            s.Employee.Department != null ? s.Employee.Department.Name : null,
            s.Employee.Designation != null ? s.Employee.Designation.Title : null,
            s.Type, s.Status, s.NoticeDate, s.ProposedLastDay, s.LastWorkingDay,
            s.Clearance.Count(c => c.IsCleared), s.Clearance.Count, s.CreatedAtUtc));
}
