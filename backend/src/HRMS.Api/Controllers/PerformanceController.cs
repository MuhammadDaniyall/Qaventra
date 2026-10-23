using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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

public record PerformanceSettingsDto(List<string> RatingLabels, List<int> BellCurve, List<RaiseSlab> RaiseSlabs);

public record PerformanceTemplateDto(Guid Id, string Name, string? Description, List<ReviewSection> Sections, bool SelfReview, int Cycles);

public record SavePerformanceTemplateRequest([Required, StringLength(100)] string Name, [StringLength(500)] string? Description, [Required] List<ReviewSection> Sections, bool SelfReview = true);

public record SaveCycleRequest(
    [Required, StringLength(150)] string Name, DateOnly PeriodFrom, DateOnly PeriodTo, Guid TemplateId, List<Guid>? DepartmentIds,
    DateOnly? GoalsDue, DateOnly? SelfReviewDue, DateOnly? ManagerReviewDue);

public record CycleDto(
    Guid Id, string Name, DateOnly PeriodFrom, DateOnly PeriodTo, Guid TemplateId, string Template, List<Guid> DepartmentIds, List<string> Departments,
    DateOnly? GoalsDue, DateOnly? SelfReviewDue, DateOnly? ManagerReviewDue, ReviewCycleStatus Status, int Reviews, Dictionary<string, int> ByStage);

public record ReviewItemDto(
    Guid Id, int SectionIndex, string Title, string? Description, string? Target, decimal Weight, int? SelfScore, string? SelfComment,
    int? ManagerScore, string? ManagerComment);

public record ReviewDto(
    Guid Id, Guid CycleId, string Cycle, DateOnly PeriodFrom, DateOnly PeriodTo, Guid EmployeeId, string EmployeeName, string EmployeeCode,
    string? Designation, string? Department, Guid? ReviewerEmployeeId, string? Reviewer, ReviewStage Stage, List<ReviewSection> Sections,
    bool SelfReviewEnabled, List<ReviewItemDto> Items, decimal? SelfScore, string? SelfComment, decimal? ManagerScore, string? ManagerComment,
    string? Strengths, string? Improvements, string? DevelopmentPlan, int? Potential, int? FinalRating, string? FinalLabel, string? CalibrationNote,
    bool? EmployeeAgrees, string? EmployeeSignOffComment, DateOnly? Due, bool IsEmployee, bool IsReviewer, bool IsHr);

public record ReviewListItemDto(
    Guid Id, Guid CycleId, string Cycle, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Department, string? Reviewer,
    ReviewStage Stage, decimal? SelfScore, decimal? ManagerScore, int? FinalRating, int? Potential, bool? EmployeeAgrees, decimal? SuggestedRaise);

public record CalibrationDto(List<ReviewListItemDto> Reviews, List<int> BellTarget, List<int> BellActual, List<string> Labels);

public record GoalsRequest([Required, MaxLength(20)] List<GoalInput> Goals);

public record DecideGoalsRequest(bool Approve, [StringLength(1000)] string? Note);

public record SelfReviewRequest([Required] List<ItemScoreInput> Scores, [StringLength(4000)] string? Comment, bool Submit);

public record ManagerReviewRequest(
    [Required] List<ItemScoreInput> Scores, [StringLength(4000)] string? Comment, [StringLength(4000)] string? Strengths, [StringLength(4000)] string? Improvements,
    [StringLength(4000)] string? DevelopmentPlan, int? Potential, bool Submit);

public record CalibrateRequest(int FinalRating, [StringLength(4000)] string? Note);

public record ReleaseRequest(List<Guid>? ReviewIds);

public record SignOffRequest(bool Agree, [StringLength(4000)] string? Comment);

[ApiController]
[Route("api/performance")]
[Authorize(Policy = Policies.CompanyUser)]
public class PerformanceController(HrmsDbContext db, PerformanceService perf, TenantClock clock, ICurrentUser me) : ControllerBase
{
    private static JsonSerializerOptions Json => PerformanceService.Json;

    // ---------- Settings ----------

    [HttpGet("settings")]
    public async Task<PerformanceSettingsDto> Settings()
    {
        var s = await db.PerformanceSettings.FirstOrDefaultAsync();
        List<T> Read<T>(string? json, IEnumerable<T> fallback)
        {
            var list = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<T>>(json, Json);
            return list is { Count: > 0 } ? list : fallback.ToList();
        }
        return new PerformanceSettingsDto(Read(s?.RatingLabelsJson, PerformanceMath.DefaultLabels), Read(s?.BellCurveJson, PerformanceMath.DefaultBellCurve),
            Read(s?.RaiseSlabsJson, PerformanceMath.DefaultRaises));
    }

    [HttpPut("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<PerformanceSettingsDto> SaveSettings(PerformanceSettingsDto req)
    {
        if (req.RatingLabels is not { Count: 5 } || req.RatingLabels.Any(string.IsNullOrWhiteSpace) || req.RatingLabels.Any(l => l.Length > 40))
            throw new DomainException("Name all five ratings (up to 40 characters each).");
        if (req.BellCurve is not { Count: 5 } || req.BellCurve.Any(x => x < 0) || req.BellCurve.Sum() != 100)
            throw new DomainException("The bell curve shares must add up to 100.");
        if ((req.RaiseSlabs ?? []).Count > 10 || (req.RaiseSlabs ?? []).Any(r => r.MinScore is < 1 or > 5 || r.RaisePercent is < 0 or > 100))
            throw new DomainException("Raise steps need a score from 1 to 5 and a raise from 0 to 100%.");
        var s = await db.PerformanceSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new PerformanceSettings();
            db.PerformanceSettings.Add(s);
        }
        s.RatingLabelsJson = JsonSerializer.Serialize(req.RatingLabels.Select(l => l.Trim()), Json);
        s.BellCurveJson = JsonSerializer.Serialize(req.BellCurve, Json);
        s.RaiseSlabsJson = JsonSerializer.Serialize((req.RaiseSlabs ?? []).OrderByDescending(r => r.MinScore), Json);
        await db.SaveChangesAsync();
        return await Settings();
    }

    // ---------- Templates ----------

    [HttpGet("templates"), Authorize(Policy = Policies.Hr)]
    public async Task<List<PerformanceTemplateDto>> Templates()
    {
        var used = await db.ReviewCycles.GroupBy(c => c.TemplateId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return (await db.PerformanceTemplates.OrderBy(t => t.Name).ToListAsync())
            .Select(t => new PerformanceTemplateDto(t.Id, t.Name, t.Description, PerformanceService.Sections(t.SectionsJson), t.SelfReview, used.GetValueOrDefault(t.Id))).ToList();
    }

    [HttpPost("templates"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> CreateTemplate(SavePerformanceTemplateRequest req)
    {
        var t = new PerformanceTemplate();
        await ApplyTemplateAsync(t, req);
        db.PerformanceTemplates.Add(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateTemplate(Guid id, SavePerformanceTemplateRequest req)
    {
        var t = await db.PerformanceTemplates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        await ApplyTemplateAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var t = await db.PerformanceTemplates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        if (await db.ReviewCycles.AnyAsync(c => c.TemplateId == id))
            throw new DomainException("Review cycles use this template.");
        db.PerformanceTemplates.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Cycles ----------

    [HttpGet("cycles"), Authorize(Policy = Policies.Hr)]
    public async Task<List<CycleDto>> Cycles()
    {
        var cycles = await db.ReviewCycles.OrderByDescending(c => c.PeriodTo).ToListAsync();
        var templates = await db.PerformanceTemplates.ToDictionaryAsync(t => t.Id, t => t.Name);
        var depts = await db.Departments.ToDictionaryAsync(d => d.Id, d => d.Name);
        var stages = await db.PerformanceReviews.GroupBy(r => new { r.CycleId, r.Stage }).Select(g => new { g.Key.CycleId, g.Key.Stage, Count = g.Count() }).ToListAsync();
        return cycles.Select(c =>
        {
            var ids = AnnouncementService.Departments(c.DepartmentIds).ToList();
            var mine = stages.Where(s => s.CycleId == c.Id).ToList();
            return new CycleDto(c.Id, c.Name, c.PeriodFrom, c.PeriodTo, c.TemplateId, templates.GetValueOrDefault(c.TemplateId) ?? "", ids,
                ids.Select(i => depts.GetValueOrDefault(i) ?? "").ToList(), c.GoalsDue, c.SelfReviewDue, c.ManagerReviewDue, c.Status, mine.Sum(s => s.Count),
                mine.ToDictionary(s => s.Stage.ToString(), s => s.Count));
        }).ToList();
    }

    [HttpPost("cycles"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> CreateCycle(SaveCycleRequest req)
    {
        var c = new ReviewCycle();
        await ApplyCycleAsync(c, req);
        db.ReviewCycles.Add(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("cycles/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateCycle(Guid id, SaveCycleRequest req)
    {
        var c = await db.ReviewCycles.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Review cycle");
        if (c.Status == ReviewCycleStatus.Closed)
            throw new DomainException("This cycle is closed.");
        if (c.Status == ReviewCycleStatus.Active && (c.TemplateId != req.TemplateId || string.Join(',', req.DepartmentIds ?? []) != c.DepartmentIds))
            throw new DomainException("After launch only the name, period and deadlines can change.");
        await ApplyCycleAsync(c, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("cycles/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteCycle(Guid id)
    {
        var c = await db.ReviewCycles.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Review cycle");
        if (c.Status != ReviewCycleStatus.Draft)
            throw new DomainException("Only a cycle that has not been launched can be deleted.");
        db.ReviewCycles.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("cycles/{id:guid}/launch"), Authorize(Policy = Policies.Hr)]
    public async Task<object> Launch(Guid id) => new { reviews = await perf.LaunchAsync(id) };

    [HttpPost("cycles/{id:guid}/close"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Close(Guid id)
    {
        var c = await db.ReviewCycles.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Review cycle");
        if (c.Status != ReviewCycleStatus.Active)
            throw new DomainException("Only a running cycle can be closed.");
        c.Status = ReviewCycleStatus.Closed;
        c.ClosedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("cycles/{id:guid}/release"), Authorize(Policy = Policies.Hr)]
    public async Task<object> Release(Guid id, ReleaseRequest req) => new { released = await perf.ReleaseAsync(id, req.ReviewIds) };

    /// <summary>Everyone in the cycle with scores, 9-box position and suggested raise, plus the bell curve against its target.</summary>
    [HttpGet("cycles/{id:guid}/calibration"), Authorize(Policy = Policies.Hr)]
    public async Task<CalibrationDto> Calibration(Guid id)
    {
        var settings = await Settings();
        var reviews = await ListAsync(db.PerformanceReviews.Where(r => r.CycleId == id), settings);
        var rated = reviews.Where(r => r.FinalRating is not null).ToList();
        var actual = Enumerable.Range(1, 5).Select(n => rated.Count == 0 ? 0 : (int)Math.Round(100.0 * rated.Count(r => r.FinalRating == n) / rated.Count)).ToList();
        return new CalibrationDto(reviews, settings.BellCurve, actual, settings.RatingLabels);
    }

    // ---------- Reviews ----------

    /// <summary>The signed-in employee's own reviews.</summary>
    [HttpGet("reviews/my")]
    public async Task<List<ReviewListItemDto>> MyReviews()
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        var list = await ListAsync(db.PerformanceReviews.Where(r => r.EmployeeId == mine && r.Cycle!.Status != ReviewCycleStatus.Draft), await Settings());
        // Scores stay hidden from the employee until HR releases the review.
        return list.Select(r => r.Stage < ReviewStage.SignOff ? r with { ManagerScore = null, FinalRating = null, Potential = null, SuggestedRaise = null } : r with { SuggestedRaise = null }).ToList();
    }

    /// <summary>Reviews the signed-in user writes as a manager, in running cycles.</summary>
    [HttpGet("reviews/team")]
    public async Task<List<ReviewListItemDto>> TeamReviews()
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        return (await ListAsync(db.PerformanceReviews.Where(r => r.ReviewerEmployeeId == mine && r.Cycle!.Status == ReviewCycleStatus.Active), await Settings()))
            .Select(r => r with { SuggestedRaise = null }).ToList();
    }

    [HttpGet("reviews/{id:guid}")]
    public async Task<ReviewDto> Get(Guid id)
    {
        var (r, sections) = await perf.LoadAsync(id);
        var isEmployee = perf.IsEmployee(r);
        var isReviewer = perf.IsReviewer(r) && !isEmployee;
        var showManager = !isEmployee || r.Stage >= ReviewStage.SignOff;
        var showSelf = isEmployee || r.Stage > ReviewStage.SelfReview || !r.Cycle!.SelfReview;
        var settings = await Settings();
        var e = await db.Employees.Where(x => x.Id == r.EmployeeId).Select(x => new
        {
            Name = x.FirstName + " " + x.LastName, x.EmployeeCode, Designation = x.Designation != null ? x.Designation.Title : null,
            Department = x.Department != null ? x.Department.Name : null,
        }).FirstAsync();
        var reviewer = r.ReviewerEmployeeId is { } rev ? await db.Employees.Where(x => x.Id == rev).Select(x => x.FirstName + " " + x.LastName).FirstOrDefaultAsync() : null;
        var c = r.Cycle!;
        var due = r.Stage switch
        {
            ReviewStage.Goals or ReviewStage.GoalsSubmitted => c.GoalsDue,
            ReviewStage.SelfReview => c.SelfReviewDue,
            ReviewStage.ManagerReview => c.ManagerReviewDue,
            _ => null,
        };
        return new ReviewDto(r.Id, c.Id, c.Name, c.PeriodFrom, c.PeriodTo, r.EmployeeId, e.Name, e.EmployeeCode, e.Designation, e.Department, r.ReviewerEmployeeId,
            reviewer, r.Stage, sections, c.SelfReview,
            r.Items.OrderBy(i => i.SectionIndex).ThenBy(i => i.Order).Select(i => new ReviewItemDto(i.Id, i.SectionIndex, i.Title, i.Description, i.Target, i.Weight,
                showSelf ? i.SelfScore : null, showSelf ? i.SelfComment : null, showManager ? i.ManagerScore : null, showManager ? i.ManagerComment : null)).ToList(),
            showSelf ? r.SelfScore : null, showSelf ? r.SelfComment : null, showManager ? r.ManagerScore : null, showManager ? r.ManagerComment : null,
            showManager ? r.Strengths : null, showManager ? r.Improvements : null, showManager ? r.DevelopmentPlan : null,
            isEmployee ? null : r.Potential, showManager ? r.FinalRating : null,
            showManager && r.FinalRating is { } f ? settings.RatingLabels[f - 1] : null, me.IsHr() ? r.CalibrationNote : null,
            r.EmployeeAgrees, r.EmployeeSignOffComment, due, isEmployee, isReviewer, me.IsHr());
    }

    [HttpPut("reviews/{id:guid}/goals")]
    public async Task<ReviewDto> SaveGoals(Guid id, GoalsRequest req)
    {
        await perf.SaveGoalsAsync(id, req.Goals);
        return await Get(id);
    }

    [HttpPost("reviews/{id:guid}/goals/submit")]
    public async Task<ReviewDto> SubmitGoals(Guid id)
    {
        await perf.SubmitGoalsAsync(id);
        return await Get(id);
    }

    [HttpPost("reviews/{id:guid}/goals/decide")]
    public async Task<ReviewDto> DecideGoals(Guid id, DecideGoalsRequest req)
    {
        await perf.DecideGoalsAsync(id, req.Approve, req.Note);
        return await Get(id);
    }

    [HttpPut("reviews/{id:guid}/self")]
    public async Task<ReviewDto> Self(Guid id, SelfReviewRequest req)
    {
        await perf.SaveSelfAsync(id, req.Scores, req.Comment, req.Submit);
        return await Get(id);
    }

    [HttpPut("reviews/{id:guid}/manager")]
    public async Task<ReviewDto> Manager(Guid id, ManagerReviewRequest req)
    {
        await perf.SaveManagerAsync(id, new ManagerReviewInput(req.Scores, req.Comment, req.Strengths, req.Improvements, req.DevelopmentPlan, req.Potential), req.Submit);
        return await Get(id);
    }

    [HttpPost("reviews/{id:guid}/calibrate"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Calibrate(Guid id, CalibrateRequest req)
    {
        await perf.CalibrateAsync(id, req.FinalRating, req.Note);
        return NoContent();
    }

    [HttpPost("reviews/{id:guid}/signoff")]
    public async Task<ReviewDto> SignOff(Guid id, SignOffRequest req)
    {
        await perf.SignOffAsync(id, req.Agree, req.Comment);
        return await Get(id);
    }

    [HttpPost("reviews/{id:guid}/revert"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Revert(Guid id)
    {
        await perf.RevertAsync(id);
        return NoContent();
    }

    // ---------- Helpers ----------

    private async Task<List<ReviewListItemDto>> ListAsync(IQueryable<PerformanceReview> q, PerformanceSettingsDto settings)
    {
        var rows = await q.OrderBy(r => r.Employee!.FirstName).Select(r => new
        {
            r.Id, r.CycleId, Cycle = r.Cycle!.Name, r.EmployeeId, Name = r.Employee!.FirstName + " " + r.Employee.LastName, r.Employee.EmployeeCode,
            Department = r.Employee.Department != null ? r.Employee.Department.Name : null,
            Reviewer = db.Employees.Where(e => e.Id == r.ReviewerEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            r.Stage, r.SelfScore, r.ManagerScore, r.FinalRating, r.Potential, r.EmployeeAgrees,
        }).ToListAsync();
        return rows.Select(r => new ReviewListItemDto(r.Id, r.CycleId, r.Cycle, r.EmployeeId, r.Name, r.EmployeeCode, r.Department, r.Reviewer, r.Stage,
            r.SelfScore, r.ManagerScore, r.FinalRating, r.Potential, r.EmployeeAgrees,
            // The raise follows the rating people see (after calibration), not the unrounded score.
            r.FinalRating is { } f ? PerformanceMath.Raise(f, settings.RaiseSlabs) : r.ManagerScore is { } s ? PerformanceMath.Raise(s, settings.RaiseSlabs) : null)).ToList();
    }

    private async Task ApplyTemplateAsync(PerformanceTemplate t, SavePerformanceTemplateRequest req)
    {
        var name = req.Name.Trim();
        if (await db.PerformanceTemplates.AnyAsync(x => x.Id != t.Id && x.Name == name))
            throw new DomainException($"There is already a {name} template.");
        var sections = (req.Sections ?? []).Select(s => s with
        {
            Name = s.Name?.Trim() ?? "",
            Items = s.Kind == ReviewSectionKind.Competencies ? (s.Items ?? []).Where(i => !string.IsNullOrWhiteSpace(i.Title)).Select(i => i with { Title = i.Title.Trim() }).ToList() : [],
        }).ToList();
        PerformanceService.ValidateSections(sections);
        t.Name = name;
        t.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        t.SectionsJson = JsonSerializer.Serialize(sections, Json);
        t.SelfReview = req.SelfReview;
    }

    private async Task ApplyCycleAsync(ReviewCycle c, SaveCycleRequest req)
    {
        if (req.PeriodTo < req.PeriodFrom)
            throw new DomainException("The period ends before it starts.");
        if (!await db.PerformanceTemplates.AnyAsync(t => t.Id == req.TemplateId))
            throw new NotFoundException("Template");
        var depts = (req.DepartmentIds ?? []).Distinct().ToList();
        if (await db.Departments.CountAsync(d => depts.Contains(d.Id)) != depts.Count)
            throw new NotFoundException("Department");
        c.Name = req.Name.Trim();
        c.PeriodFrom = req.PeriodFrom;
        c.PeriodTo = req.PeriodTo;
        c.TemplateId = req.TemplateId;
        c.DepartmentIds = string.Join(',', depts);
        c.GoalsDue = req.GoalsDue;
        c.SelfReviewDue = req.SelfReviewDue;
        c.ManagerReviewDue = req.ManagerReviewDue;
    }
}
