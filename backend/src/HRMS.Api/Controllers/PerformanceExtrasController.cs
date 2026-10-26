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

public record KpiDto(Guid Id, string Name, string? Description, string? Category, string? Unit, string? DefaultTarget, decimal DefaultWeight, bool IsActive);
public record SaveKpiRequest(
    [Required, StringLength(200)] string Name, [StringLength(1000)] string? Description, [StringLength(100)] string? Category,
    [StringLength(30)] string? Unit, [StringLength(200)] string? DefaultTarget, [Range(0, 100)] decimal DefaultWeight, bool IsActive = true);

public record ReviewFeedbackDto(Guid Id, Guid FromEmployeeId, string From, int? Rating, string? Comment, DateTime? SubmittedAtUtc);
public record RequestFeedbackRequest([Required, MinLength(1), MaxLength(10)] List<Guid> EmployeeIds);
/// <summary>A feedback request waiting on the signed-in person.</summary>
public record MyFeedbackRequestDto(Guid Id, Guid ReviewId, string Employee, string? Designation, string Cycle, int? Rating, string? Comment, DateTime? SubmittedAtUtc);
public record GiveFeedbackRequest([Range(1, 5)] int Rating, [Required, StringLength(4000)] string Comment);

public record CheckInDto(Guid Id, string Author, bool ByEmployee, string Period, int? Progress, string Note, DateTime CreatedAtUtc);
public record AddCheckInRequest([Required, StringLength(20)] string Period, [Range(0, 100)] int? Progress, [Required, StringLength(4000)] string Note);

/// <summary>KPI library, feedback from other reviewers, and quarterly check-ins on a review.</summary>
[ApiController]
[Route("api/performance")]
[Authorize(Policy = Policies.CompanyUser)]
public class PerformanceExtrasController(HrmsDbContext db, PerformanceService perf, ICurrentUser me, TenantClock clock, NotificationService notify) : ControllerBase
{
    // ---------- KPI library ----------

    /// <summary>Everyone setting goals can pick from the library; HR also sees inactive ones.</summary>
    [HttpGet("kpis")]
    public Task<List<KpiDto>> Kpis() =>
        db.Kpis.Where(k => k.IsActive || me.IsHr()).OrderBy(k => k.Category).ThenBy(k => k.Name)
            .Select(k => new KpiDto(k.Id, k.Name, k.Description, k.Category, k.Unit, k.DefaultTarget, k.DefaultWeight, k.IsActive)).ToListAsync();

    [HttpPost("kpis"), Authorize(Policy = Policies.Hr)]
    public async Task<KpiDto> AddKpi(SaveKpiRequest req)
    {
        var k = new Kpi();
        Apply(k, req);
        db.Kpis.Add(k);
        await db.SaveChangesAsync();
        return ToDto(k);
    }

    [HttpPut("kpis/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<KpiDto> UpdateKpi(Guid id, SaveKpiRequest req)
    {
        var k = await db.Kpis.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("KPI");
        Apply(k, req);
        await db.SaveChangesAsync();
        return ToDto(k);
    }

    [HttpDelete("kpis/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteKpi(Guid id)
    {
        var k = await db.Kpis.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("KPI");
        db.Kpis.Remove(k);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Feedback from other reviewers ----------

    /// <summary>Feedback on a review; only its reviewer and HR see it (the employee does not).</summary>
    [HttpGet("reviews/{id:guid}/feedback")]
    public async Task<List<ReviewFeedbackDto>> Feedback(Guid id)
    {
        var (r, _) = await perf.LoadAsync(id);
        if (!perf.IsReviewer(r) || perf.IsEmployee(r))
            return [];
        return await (from f in db.ReviewFeedback
                      join e in db.Employees on f.FromEmployeeId equals e.Id
                      where f.ReviewId == id
                      orderby f.CreatedAtUtc
                      select new ReviewFeedbackDto(f.Id, f.FromEmployeeId, e.FirstName + " " + e.LastName, f.Rating, f.Comment, f.SubmittedAtUtc)).ToListAsync();
    }

    [HttpPost("reviews/{id:guid}/feedback")]
    public async Task<IActionResult> RequestFeedback(Guid id, RequestFeedbackRequest req)
    {
        var (r, _) = await perf.LoadAsync(id);
        if (!perf.IsReviewer(r) || perf.IsEmployee(r))
            throw new ForbiddenException("Only the reviewer or HR can ask for feedback.");
        var requester = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var ids = req.EmployeeIds.Distinct().Where(e => e != r.EmployeeId).ToList();
        if (await db.Employees.CountAsync(e => ids.Contains(e.Id)) != ids.Count)
            throw new NotFoundException("Employee");
        var already = await db.ReviewFeedback.Where(f => f.ReviewId == id).Select(f => f.FromEmployeeId).ToListAsync();
        var added = ids.Except(already).ToList();
        foreach (var e in added)
            db.ReviewFeedback.Add(new ReviewFeedback { ReviewId = id, FromEmployeeId = e, RequestedByEmployeeId = requester });
        var name = await db.Employees.Where(e => e.Id == r.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        await notify.ToEmployeesAsync(added, NotificationKind.Performance, $"Your feedback on {name} is requested", r.Cycle!.Name, "/performance?tab=feedback");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("feedback/mine")]
    public async Task<List<MyFeedbackRequestDto>> MyFeedback()
    {
        var mine = me.EmployeeId;
        if (mine is null) return [];
        return await (from f in db.ReviewFeedback
                      join r in db.PerformanceReviews on f.ReviewId equals r.Id
                      where f.FromEmployeeId == mine && r.Cycle!.Status != ReviewCycleStatus.Closed
                      orderby f.SubmittedAtUtc != null, f.CreatedAtUtc descending
                      select new MyFeedbackRequestDto(f.Id, r.Id, r.Employee!.FirstName + " " + r.Employee.LastName,
                          r.Employee.Designation != null ? r.Employee.Designation.Title : null, r.Cycle!.Name, f.Rating, f.Comment, f.SubmittedAtUtc))
            .ToListAsync();
    }

    [HttpPut("feedback/{id:guid}")]
    public async Task<IActionResult> GiveFeedback(Guid id, GiveFeedbackRequest req)
    {
        var f = await db.ReviewFeedback.FirstOrDefaultAsync(x => x.Id == id && x.FromEmployeeId == me.EmployeeId) ?? throw new NotFoundException("Feedback request");
        var stage = await db.PerformanceReviews.Where(r => r.Id == f.ReviewId).Select(r => r.Stage).FirstAsync();
        if (stage is ReviewStage.SignOff or ReviewStage.Done)
            throw new DomainException("This review is already released.");
        f.Rating = req.Rating;
        f.Comment = req.Comment.Trim();
        f.SubmittedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Quarterly check-ins ----------

    [HttpGet("reviews/{id:guid}/check-ins")]
    public async Task<List<CheckInDto>> CheckIns(Guid id)
    {
        var (r, _) = await perf.LoadAsync(id);
        return await (from c in db.ReviewCheckIns
                      join e in db.Employees on c.AuthorEmployeeId equals e.Id
                      where c.ReviewId == id
                      orderby c.CreatedAtUtc descending
                      select new CheckInDto(c.Id, e.FirstName + " " + e.LastName, c.AuthorEmployeeId == r.EmployeeId, c.Period, c.Progress, c.Note, c.CreatedAtUtc))
            .ToListAsync();
    }

    [HttpPost("reviews/{id:guid}/check-ins")]
    public async Task<IActionResult> AddCheckIn(Guid id, AddCheckInRequest req)
    {
        var (r, _) = await perf.LoadAsync(id);
        var author = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        if (r.Stage is ReviewStage.Done)
            throw new DomainException("This review is finished.");
        db.ReviewCheckIns.Add(new ReviewCheckIn { ReviewId = id, AuthorEmployeeId = author, Period = req.Period.Trim(), Progress = req.Progress, Note = req.Note.Trim() });
        // Tell the other side: the reviewer when the employee writes, the employee when the reviewer does.
        var other = perf.IsEmployee(r) ? r.ReviewerEmployeeId : r.EmployeeId;
        if (other is { } o)
            await notify.ToEmployeesAsync([o], NotificationKind.Performance, $"New check-in for {req.Period.Trim()}", r.Cycle!.Name, $"/performance/reviews/{id}");
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Kpi k, SaveKpiRequest req)
    {
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        k.Name = req.Name.Trim();
        k.Description = T(req.Description);
        k.Category = T(req.Category);
        k.Unit = T(req.Unit);
        k.DefaultTarget = T(req.DefaultTarget);
        k.DefaultWeight = req.DefaultWeight;
        k.IsActive = req.IsActive;
    }

    private static KpiDto ToDto(Kpi k) => new(k.Id, k.Name, k.Description, k.Category, k.Unit, k.DefaultTarget, k.DefaultWeight, k.IsActive);
}
