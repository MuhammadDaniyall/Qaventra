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

public record BudgetPlanListDto(Guid Id, string Name, DateOnly FromDate, DateOnly ToDate, BudgetStatus Status, int Headcount, decimal Cost, int Positions,
    RecordStampsDto Stamps);

public record SaveBudgetPlanRequest([Required, StringLength(150)] string Name, DateOnly FromDate, DateOnly ToDate, [StringLength(2000)] string? Notes);

public record BudgetPositionRequest(Guid? DepartmentId, Guid? DesignationId, int Headcount, decimal MonthlyCost, DateOnly? FilledFrom, [StringLength(500)] string? Notes,
    [StringLength(50)] string? Code = null, Guid? GradeId = null, Guid? CostCenterId = null, [StringLength(50)] string? ReportsToCode = null,
    decimal? SalaryMin = null, decimal? SalaryMax = null, [StringLength(4000)] string? JobDescription = null);

public record SavePositionsRequest([Required] List<BudgetPositionRequest> Positions);

public record BudgetLineDto(
    Guid Id, Guid? DepartmentId, string Department, Guid? DesignationId, string Designation, int Headcount, decimal MonthlyCost, DateOnly? FilledFrom, string? Notes,
    int OnRoll, int Hiring, int Vacant, decimal BudgetCost, decimal ActualCost, decimal Variance,
    string? Code = null, Guid? GradeId = null, Guid? CostCenterId = null, string? ReportsToCode = null, decimal? SalaryMin = null, decimal? SalaryMax = null,
    string? JobDescription = null);

public record UnplannedDto(Guid? DepartmentId, string Department, Guid? DesignationId, string Designation, int OnRoll, decimal MonthlyCost);

public record BudgetPlanDto(
    Guid Id, string Name, DateOnly FromDate, DateOnly ToDate, BudgetStatus Status, string? Notes, DateTime? SubmittedAtUtc, string? DecidedByName, DateTime? DecidedAtUtc,
    string? DecisionNote, List<BudgetLineDto> Lines, List<UnplannedDto> Unplanned, int Headcount, int OnRoll, int Vacant, int Hiring, decimal BudgetCost, decimal ActualCost,
    bool CanEdit, bool CanDecide, bool CanClose, RecordStampsDto Stamps);

public record BudgetDecisionRequest(bool Approve, [StringLength(1000)] string? Note);

public record BudgetCheckDto(bool HasPlan, string? Plan, bool InBudget, int Approved, int OnRoll, int Hiring, int Open);

/// <summary>
/// Manpower budgeting: a plan of approved headcount and monthly cost per department and designation, prepared by
/// HR and approved by the company admin, compared with who is on the roll and who is being hired.
/// </summary>
[ApiController]
[Route("api/budget")]
[Authorize(Policy = Policies.CompanyUser)]
public class BudgetController(HrmsDbContext db, TenantClock clock, ICurrentUser me) : ControllerBase
{
    private static readonly BudgetStatus[] Editable = [BudgetStatus.Draft, BudgetStatus.Rejected];

    [HttpGet("plans"), Authorize(Policy = Policies.Hr)]
    public async Task<List<BudgetPlanListDto>> Plans() =>
        await db.BudgetPlans.OrderByDescending(p => p.FromDate)
            .Select(p => new BudgetPlanListDto(p.Id, p.Name, p.FromDate, p.ToDate, p.Status, p.Positions.Sum(x => x.Headcount),
                p.Positions.Sum(x => x.Headcount * x.MonthlyCost), p.Positions.Count, new RecordStampsDto(p.CreatedAtUtc, p.CreatedBy, p.ModifiedAtUtc, p.ModifiedBy))).ToListAsync();

    [HttpGet("plans/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<BudgetPlanDto> Plan(Guid id)
    {
        var p = await db.BudgetPlans.Include(x => x.Positions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Budget");
        return await ToDtoAsync(p);
    }

    [HttpPost("plans"), Authorize(Policy = Policies.Hr)]
    public async Task<BudgetPlanListDto> Create(SaveBudgetPlanRequest req, [FromQuery] bool fromStaff = false)
    {
        Validate(req);
        var p = new BudgetPlan { Name = req.Name.Trim(), FromDate = req.FromDate, ToDate = req.ToDate, Notes = Clean(req.Notes) };
        db.BudgetPlans.Add(p);
        if (fromStaff)
            foreach (var g in await StaffAsync())
                p.Positions.Add(new BudgetPosition { PlanId = p.Id, DepartmentId = g.DepartmentId, DesignationId = g.DesignationId, Headcount = g.Count, MonthlyCost = Math.Round(g.Average, 0) });
        foreach (var x in p.Positions)
            db.BudgetPositions.Add(x);
        await db.SaveChangesAsync();
        return new BudgetPlanListDto(p.Id, p.Name, p.FromDate, p.ToDate, p.Status, p.Positions.Sum(x => x.Headcount), p.Positions.Sum(x => x.Headcount * x.MonthlyCost), p.Positions.Count,
            RecordStampsDto.Of(p));
    }

    [HttpPut("plans/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Update(Guid id, SaveBudgetPlanRequest req)
    {
        var p = await EditableAsync(id);
        Validate(req);
        if (p.Positions.Any(x => x.FilledFrom is { } f && (f < req.FromDate || f > req.ToDate)))
            throw new DomainException("Hiring dates must fall within the plan.");
        p.Name = req.Name.Trim();
        p.FromDate = req.FromDate;
        p.ToDate = req.ToDate;
        p.Notes = Clean(req.Notes);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Replaces the plan's positions.</summary>
    [HttpPut("plans/{id:guid}/positions"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SavePositions(Guid id, SavePositionsRequest req)
    {
        var p = await EditableAsync(id);
        var lines = req.Positions;
        if (lines.Count > 500)
            throw new DomainException("At most 500 lines.");
        if (lines.Any(l => l.Headcount is < 0 or > 100000 || l.MonthlyCost < 0))
            throw new DomainException("Check the headcount and cost.");
        if (lines.GroupBy(l => (l.DepartmentId, l.DesignationId)).Any(g => g.Count() > 1))
            throw new DomainException("Each department and designation appears once.");
        if (lines.Any(l => l.FilledFrom is { } f && (f < p.FromDate || f > p.ToDate)))
            throw new DomainException("Hiring dates must fall within the plan.");
        var depts = lines.Where(l => l.DepartmentId != null).Select(l => l.DepartmentId!.Value).Distinct().ToList();
        var desigs = lines.Where(l => l.DesignationId != null).Select(l => l.DesignationId!.Value).Distinct().ToList();
        if (await db.Departments.CountAsync(d => depts.Contains(d.Id)) != depts.Count || await db.Designations.CountAsync(d => desigs.Contains(d.Id)) != desigs.Count)
            throw new NotFoundException("Department or designation");
        if (lines.Any(l => l.SalaryMin is < 0 || l.SalaryMax is < 0 || (l.SalaryMin is { } lo && l.SalaryMax is { } hi && hi < lo)))
            throw new DomainException("Check the salary band: the top is below the bottom.");
        var codes = lines.Where(l => !string.IsNullOrWhiteSpace(l.Code)).Select(l => l.Code!.Trim()).ToList();
        if (codes.Count != codes.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new DomainException("Each position code is used once.");
        if (lines.Any(l => !string.IsNullOrWhiteSpace(l.ReportsToCode) && !codes.Contains(l.ReportsToCode.Trim(), StringComparer.OrdinalIgnoreCase)))
            throw new DomainException("\"Reports to\" must be the code of another position in this plan.");
        if (lines.Any(l => !string.IsNullOrWhiteSpace(l.ReportsToCode) && string.Equals(l.ReportsToCode.Trim(), l.Code?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("A position cannot report to itself.");
        var lookupIds = lines.SelectMany(l => new[] { l.GradeId, l.CostCenterId }).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var lookups = await db.OrgLookups.Where(x => lookupIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Kind);
        if (lines.Any(l => (l.GradeId is { } g && lookups.GetValueOrDefault(g) != LookupKind.Grade) || (l.CostCenterId is { } c && lookups.GetValueOrDefault(c) != LookupKind.CostCenter)))
            throw new DomainException("Choose the grade and cost center from the lists.");
        db.BudgetPositions.RemoveRange(p.Positions);
        var added = new List<(BudgetPositionRequest Line, BudgetPosition Position)>();
        foreach (var l in lines.Where(l => l.Headcount > 0))
        {
            var position = new BudgetPosition
            {
                PlanId = p.Id, DepartmentId = l.DepartmentId, DesignationId = l.DesignationId, Headcount = l.Headcount, MonthlyCost = l.MonthlyCost, FilledFrom = l.FilledFrom,
                Notes = Clean(l.Notes), Code = string.IsNullOrWhiteSpace(l.Code) ? null : l.Code.Trim(), GradeId = l.GradeId, CostCenterId = l.CostCenterId,
                SalaryMin = l.SalaryMin, SalaryMax = l.SalaryMax, JobDescription = string.IsNullOrWhiteSpace(l.JobDescription) ? null : l.JobDescription.Trim(),
            };
            db.BudgetPositions.Add(position);
            added.Add((l, position));
        }
        foreach (var (l, position) in added.Where(x => !string.IsNullOrWhiteSpace(x.Line.ReportsToCode)))
            position.ReportsToId = added.FirstOrDefault(x => string.Equals(x.Position.Code, l.ReportsToCode!.Trim(), StringComparison.OrdinalIgnoreCase)).Position?.Id;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("plans/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var p = await EditableAsync(id);
        db.BudgetPlans.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("plans/{id:guid}/submit"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Submit(Guid id, [FromServices] NotificationService notify)
    {
        var p = await EditableAsync(id);
        if (p.Positions.Count == 0)
            throw new DomainException("Add positions before submitting.");
        p.Status = BudgetStatus.Submitted;
        p.SubmittedAtUtc = clock.UtcNow;
        var admins = db.Users.Where(u => u.Role == UserRole.CompanyAdmin && u.EmployeeId != null).Select(u => u.EmployeeId!.Value);
        await notify.ToEmployeesAsync(await admins.ToListAsync(), NotificationKind.Recruitment, $"Manpower budget to approve: {p.Name}",
            $"{p.Positions.Sum(x => x.Headcount)} positions", "/budget");
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Company admin: approve or send back. An approved plan cannot overlap another approved one.</summary>
    [HttpPost("plans/{id:guid}/decide"), Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> Decide(Guid id, BudgetDecisionRequest req)
    {
        var p = await db.BudgetPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Budget");
        if (p.Status != BudgetStatus.Submitted)
            throw new DomainException("This budget is not waiting for approval.");
        if (!req.Approve && string.IsNullOrWhiteSpace(req.Note))
            throw new DomainException("Say what should change.");
        if (req.Approve && await db.BudgetPlans.AnyAsync(x => x.Id != id && x.Status == BudgetStatus.Approved && x.FromDate <= p.ToDate && x.ToDate >= p.FromDate))
            throw new DomainException("Another approved budget covers these dates. Close it first.");
        p.Status = req.Approve ? BudgetStatus.Approved : BudgetStatus.Rejected;
        p.DecidedByName = await LetterService.CurrentUserNameAsync(db, me);
        p.DecidedAtUtc = clock.UtcNow;
        p.DecisionNote = Clean(req.Note);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("plans/{id:guid}/close"), Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> Close(Guid id)
    {
        var p = await db.BudgetPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Budget");
        if (p.Status != BudgetStatus.Approved)
            throw new DomainException("Only approved budgets are closed.");
        p.Status = BudgetStatus.Closed;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// For a hiring request: whether the current approved budget has room for the department and designation.
    /// A line without a designation covers the rest of the department (designations without their own line).
    /// </summary>
    [HttpGet("check")]
    public async Task<BudgetCheckDto> Check([FromQuery] Guid? departmentId, [FromQuery] Guid? designationId)
    {
        var today = await clock.TodayAsync();
        var plan = await db.BudgetPlans.Include(p => p.Positions)
            .Where(p => p.Status == BudgetStatus.Approved && p.FromDate <= today && p.ToDate >= today).FirstOrDefaultAsync();
        if (plan is null)
            return new BudgetCheckDto(false, null, true, 0, 0, 0, 0);
        var line = plan.Positions.FirstOrDefault(x => x.DepartmentId == departmentId && x.DesignationId == designationId)
                   ?? plan.Positions.FirstOrDefault(x => x.DepartmentId == departmentId && x.DesignationId == null);
        if (line is null)
            return new BudgetCheckDto(true, plan.Name, false, 0, 0, 0, 0);
        var (onRoll, hiring) = await CountsAsync(line, plan.Positions);
        var open = Math.Max(0, line.Headcount - onRoll - hiring);
        return new BudgetCheckDto(true, plan.Name, open > 0, line.Headcount, onRoll, hiring, open);
    }

    // ---------- Helpers ----------

    private async Task<BudgetPlanDto> ToDtoAsync(BudgetPlan p)
    {
        var depts = await db.Departments.ToDictionaryAsync(d => d.Id, d => d.Name);
        var desigs = await db.Designations.ToDictionaryAsync(d => d.Id, d => d.Title);
        var staff = await StaffAsync();
        var hiring = await HiringAsync();
        var today = await clock.TodayAsync();
        var lines = new List<BudgetLineDto>();
        foreach (var x in p.Positions.OrderBy(x => x.DepartmentId is { } d ? depts.GetValueOrDefault(d) : "").ThenBy(x => x.DesignationId is { } g ? desigs.GetValueOrDefault(g) : ""))
        {
            var matching = staff.Where(s => Covers(x, p.Positions, s.DepartmentId, s.DesignationId)).ToList();
            var onRoll = matching.Sum(s => s.Count);
            var hires = hiring.Where(h => Covers(x, p.Positions, h.DepartmentId, h.DesignationId)).Sum(h => h.Count);
            var budget = x.Headcount * x.MonthlyCost * Months(x.FilledFrom ?? p.FromDate, p.ToDate);
            // Today's payroll for these people, carried over the whole plan: what the roll costs if nothing changes.
            var actual = matching.Sum(s => s.Total) * Months(p.FromDate, p.ToDate);
            lines.Add(new BudgetLineDto(x.Id, x.DepartmentId, x.DepartmentId is { } d ? depts.GetValueOrDefault(d) ?? "" : "No department",
                x.DesignationId, x.DesignationId is { } g ? desigs.GetValueOrDefault(g) ?? "" : "All designations", x.Headcount, x.MonthlyCost, x.FilledFrom, x.Notes,
                onRoll, hires, Math.Max(0, x.Headcount - onRoll), Math.Round(budget, 0), Math.Round(actual, 0), Math.Round(budget - actual, 0),
                x.Code, x.GradeId, x.CostCenterId, x.ReportsToId is { } rt ? p.Positions.FirstOrDefault(y => y.Id == rt)?.Code : null, x.SalaryMin, x.SalaryMax, x.JobDescription));
        }
        var unplanned = staff.Where(s => !p.Positions.Any(x => x.DepartmentId == s.DepartmentId && (x.DesignationId == null || x.DesignationId == s.DesignationId)))
            .Select(s => new UnplannedDto(s.DepartmentId, s.DepartmentId is { } d ? depts.GetValueOrDefault(d) ?? "" : "No department",
                s.DesignationId, s.DesignationId is { } g ? desigs.GetValueOrDefault(g) ?? "" : "No designation", s.Count, Math.Round(s.Average, 0)))
            .ToList();
        var editable = Editable.Contains(p.Status);
        return new BudgetPlanDto(p.Id, p.Name, p.FromDate, p.ToDate, p.Status, p.Notes, p.SubmittedAtUtc, p.DecidedByName, p.DecidedAtUtc, p.DecisionNote, lines, unplanned,
            lines.Sum(l => l.Headcount), lines.Sum(l => l.OnRoll), lines.Sum(l => l.Vacant), lines.Sum(l => l.Hiring), lines.Sum(l => l.BudgetCost), lines.Sum(l => l.ActualCost),
            editable, p.Status == BudgetStatus.Submitted && me.IsCompanyAdmin(), p.Status == BudgetStatus.Approved && me.IsCompanyAdmin(),
            RecordStampsDto.Of(p));
    }

    private record StaffGroup(Guid? DepartmentId, Guid? DesignationId, int Count, decimal Total, decimal Average);

    /// <summary>Current employees grouped by department and designation, with their monthly gross.</summary>
    private async Task<List<StaffGroup>> StaffAsync()
    {
        var today = await clock.TodayAsync();
        var people = await db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .Select(e => new { e.Id, e.DepartmentId, e.DesignationId }).ToListAsync();
        var ids = people.Select(p => p.Id).ToList();
        var salaries = (await db.EmployeeSalaries.Include(s => s.Components).Where(s => ids.Contains(s.EmployeeId) && s.EffectiveFrom <= today).ToListAsync())
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(g => g.Key, g =>
            {
                var s = g.OrderByDescending(x => x.EffectiveFrom).First();
                return s.BasicSalary + s.Components.Where(c => c.Kind == SalaryComponentKind.Earning).Sum(c => c.Amount);
            });
        return people.GroupBy(p => (p.DepartmentId, p.DesignationId)).Select(g =>
        {
            var total = g.Sum(p => salaries.GetValueOrDefault(p.Id));
            return new StaffGroup(g.Key.DepartmentId, g.Key.DesignationId, g.Count(), total, g.Count() == 0 ? 0 : total / g.Count());
        }).ToList();
    }

    /// <summary>Open job posts' remaining positions by department and designation.</summary>
    private async Task<List<(Guid? DepartmentId, Guid? DesignationId, int Count)>> HiringAsync()
    {
        var posts = await db.JobPosts.Where(p => p.Status == JobPostStatus.Open || p.Status == JobPostStatus.Closed)
            .Select(p => new { p.DepartmentId, p.DesignationId, Left = p.Positions - db.JobApplications.Count(a => a.JobPostId == p.Id && a.Stage == ApplicationStage.Hired) })
            .ToListAsync();
        return posts.GroupBy(p => (p.DepartmentId, p.DesignationId)).Select(g => (g.Key.DepartmentId, g.Key.DesignationId, g.Sum(p => Math.Max(0, p.Left)))).ToList();
    }

    private async Task<(int OnRoll, int Hiring)> CountsAsync(BudgetPosition line, IEnumerable<BudgetPosition> all)
    {
        var staff = await StaffAsync();
        var hiring = await HiringAsync();
        return (staff.Where(s => Covers(line, all, s.DepartmentId, s.DesignationId)).Sum(s => s.Count),
            hiring.Where(h => Covers(line, all, h.DepartmentId, h.DesignationId)).Sum(h => h.Count));
    }

    /// <summary>
    /// Whether a line counts people of this department and designation. A line without a designation takes the
    /// department's designations that have no line of their own, so nobody is counted twice.
    /// </summary>
    private static bool Covers(BudgetPosition line, IEnumerable<BudgetPosition> all, Guid? departmentId, Guid? designationId) =>
        line.DepartmentId == departmentId && (line.DesignationId == designationId
            || line.DesignationId == null && !all.Any(o => o.DepartmentId == departmentId && o.DesignationId != null && o.DesignationId == designationId));

    /// <summary>Whole and part months from <paramref name="from"/> to <paramref name="to"/> (inclusive), by days.</summary>
    private static decimal Months(DateOnly from, DateOnly to) => to < from ? 0 : Math.Round((to.DayNumber - from.DayNumber + 1) / 30.4375m, 2);

    private async Task<BudgetPlan> EditableAsync(Guid id)
    {
        var p = await db.BudgetPlans.Include(x => x.Positions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Budget");
        if (!Editable.Contains(p.Status))
            throw new DomainException(p.Status == BudgetStatus.Submitted ? "This budget is waiting for approval." : "Approved budgets cannot be changed. Make a new one.");
        return p;
    }

    private static void Validate(SaveBudgetPlanRequest r)
    {
        if (r.ToDate <= r.FromDate || r.ToDate.DayNumber - r.FromDate.DayNumber > 800)
            throw new DomainException("A budget runs from a start to a later end, at most about two years.");
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
