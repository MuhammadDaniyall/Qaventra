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

public record ProjectMemberDto(Guid EmployeeId, string? Name, decimal? HourlyRate);
public record ProjectTaskDto(Guid Id, string Name, Guid? AssigneeEmployeeId, string? Assignee, DateOnly? DueDate, decimal? EstimatedHours, bool IsDone, decimal LoggedHours);
public record ProjectDto(Guid Id, string Name, string? Code, string? Client, string? Description, Guid? ManagerEmployeeId, string? Manager, DateOnly? StartDate,
    DateOnly? EndDate, decimal? BudgetHours, bool Billable, bool OpenToAll, ProjectStatus Status, List<ProjectMemberDto> Members, List<ProjectTaskDto> Tasks,
    decimal LoggedHours, decimal Cost, bool CanManage);

public record SaveProjectRequest([Required, StringLength(150)] string Name, [StringLength(30)] string? Code, [StringLength(150)] string? Client,
    [StringLength(2000)] string? Description, Guid? ManagerEmployeeId, DateOnly? StartDate, DateOnly? EndDate, [Range(0, 1_000_000)] decimal? BudgetHours,
    bool Billable, bool OpenToAll, ProjectStatus Status, List<ProjectMemberDto>? Members);

public record SaveProjectTaskRequest([Required, StringLength(200)] string Name, Guid? AssigneeEmployeeId, DateOnly? DueDate, [Range(0, 100_000)] decimal? EstimatedHours, bool IsDone);

public record TimesheetLineDto(DateOnly Date, Guid ProjectId, Guid? TaskId, [Range(1, 1440)] int Minutes, [StringLength(500)] string? Note);

public record TimesheetDto(Guid? Id, Guid EmployeeId, string EmployeeName, DateOnly WeekStart, TimesheetStatus Status, DateTime? SubmittedAtUtc, string? DecisionNote,
    List<TimesheetLineDto> Lines, int TotalMinutes, Dictionary<string, int> AttendanceMinutes);

public record SaveTimesheetRequest(DateOnly WeekStart, List<TimesheetLineDto> Lines);

public record TimesheetDecisionRequest([StringLength(1000)] string? Note);

/// <summary>Weekly timesheets: save, submit to the approval chain, approve or send back.</summary>
public class TimesheetService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals)
{
    public static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public async Task<Timesheet> SaveAsync(Guid employeeId, DateOnly weekStart, List<TimesheetLineDto> lines)
    {
        weekStart = Monday(weekStart);
        if (weekStart > Monday(await clock.TodayAsync()).AddDays(7))
            throw new DomainException("You can fill this week and next week ahead, not further.");
        var sheet = await db.Timesheets.Include(t => t.Lines).FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStart == weekStart);
        if (sheet is { Status: TimesheetStatus.Submitted or TimesheetStatus.Approved })
            throw new DomainException(sheet.Status == TimesheetStatus.Approved ? "This week is approved and locked." : "This week is waiting for approval. Withdraw it to change it.");
        lines ??= [];
        if (lines.Count > 200) throw new DomainException("That is too many lines for a week.");
        if (lines.Any(l => l.Date < weekStart || l.Date > weekStart.AddDays(6)))
            throw new DomainException("Every line must be inside the week.");
        if (lines.Any(l => l.Minutes is < 1 or > 1440))
            throw new DomainException("Enter the time for every line.");
        foreach (var day in lines.GroupBy(l => l.Date))
            if (day.Sum(l => l.Minutes) > 1440)
                throw new DomainException($"{day.Key:ddd d MMM} adds up to more than 24 hours.");
        var projectIds = lines.Select(l => l.ProjectId).Distinct().ToList();
        var projects = await db.Projects.Include(p => p.Members).Include(p => p.Tasks).Where(p => projectIds.Contains(p.Id)).ToListAsync();
        if (projects.Count != projectIds.Count) throw new NotFoundException("Project");
        foreach (var l in lines)
        {
            var p = projects.First(x => x.Id == l.ProjectId);
            if (p.Status != ProjectStatus.Active && sheet?.Lines.Any(x => x.ProjectId == p.Id) != true)
                throw new DomainException($"{p.Name} is not active.");
            if (!p.OpenToAll && p.ManagerEmployeeId != employeeId && p.Members.All(m => m.EmployeeId != employeeId))
                throw new DomainException($"You are not on the {p.Name} team.");
            if (l.TaskId is { } t && p.Tasks.All(x => x.Id != t))
                throw new DomainException($"That task is not part of {p.Name}.");
            if ((p.StartDate is { } s && l.Date < s) || (p.EndDate is { } e && l.Date > e))
                throw new DomainException($"{l.Date:d MMM} is outside {p.Name}'s dates.");
        }

        if (sheet is null)
        {
            sheet = new Timesheet { EmployeeId = employeeId, WeekStart = weekStart };
            db.Timesheets.Add(sheet);
        }
        else
            db.TimesheetLines.RemoveRange(sheet.Lines);
        sheet.Status = TimesheetStatus.Draft;
        foreach (var l in lines.OrderBy(l => l.Date))
            db.TimesheetLines.Add(new TimesheetLine
            {
                TimesheetId = sheet.Id, Date = l.Date, ProjectId = l.ProjectId, TaskId = l.TaskId, Minutes = l.Minutes,
                Note = string.IsNullOrWhiteSpace(l.Note) ? null : l.Note.Trim(),
            });
        await db.SaveChangesAsync();
        return sheet;
    }

    public async Task SubmitAsync(Guid employeeId, DateOnly weekStart)
    {
        weekStart = Monday(weekStart);
        var sheet = await db.Timesheets.Include(t => t.Lines).FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStart == weekStart)
                    ?? throw new DomainException("Save some hours first.");
        if (sheet.Status is TimesheetStatus.Submitted or TimesheetStatus.Approved)
            throw new DomainException("This week is already submitted.");
        if (sheet.Lines.Count == 0)
            throw new DomainException("Add some hours first.");
        sheet.Status = TimesheetStatus.Submitted;
        sheet.SubmittedAtUtc = clock.UtcNow;
        sheet.DecisionNote = null;
        var hours = sheet.Lines.Sum(l => l.Minutes) / 60m;
        var approval = await approvals.StartAsync(ApprovalRequestType.Timesheet, sheet.Id, employeeId, $"Timesheet, week of {weekStart:d MMM yyyy}: {hours:0.##} h");
        if (approval.Status == ApprovalStatus.Approved)
        {
            sheet.Status = TimesheetStatus.Approved;
            sheet.DecidedAtUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    public async Task WithdrawAsync(Guid employeeId, DateOnly weekStart)
    {
        var sheet = await db.Timesheets.FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStart == Monday(weekStart)) ?? throw new NotFoundException("Timesheet");
        if (sheet.Status != TimesheetStatus.Submitted)
            throw new DomainException("Only a week waiting for approval can be withdrawn.");
        await approvals.CancelAsync(ApprovalRequestType.Timesheet, sheet.Id);
        sheet.Status = TimesheetStatus.Draft;
        await db.SaveChangesAsync();
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var sheet = await db.Timesheets.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Timesheet");
        if (sheet.Status != TimesheetStatus.Submitted)
            throw new DomainException("This timesheet is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Timesheet, sheet.Id, sheet.EmployeeId, approve, note);
        if (outcome != ApprovalStatus.Pending)
        {
            sheet.Status = outcome == ApprovalStatus.Approved ? TimesheetStatus.Approved : TimesheetStatus.Rejected;
            sheet.DecidedAtUtc = clock.UtcNow;
            sheet.DecisionNote = note;
        }
        await db.SaveChangesAsync();
    }
}

[ApiController]
[Route("api")]
[Authorize(Policy = Policies.CompanyUser)]
public class ProjectsController(HrmsDbContext db, TimesheetService timesheets, ApprovalEngine approvals, TenantClock clock, ICurrentUser me) : ControllerBase
{
    // ---------- Projects ----------

    /// <summary>HR sees every project; others see the ones they manage, belong to, or that are open to all.</summary>
    [HttpGet("projects")]
    public async Task<List<ProjectDto>> Projects([FromQuery] bool includeClosed = false)
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        var q = db.Projects.Include(p => p.Members).Include(p => p.Tasks).AsNoTracking();
        if (!me.IsHr())
            q = q.Where(p => p.OpenToAll || p.ManagerEmployeeId == mine || p.Members.Any(m => m.EmployeeId == mine));
        if (!includeClosed)
            q = q.Where(p => p.Status != ProjectStatus.Closed);
        var list = await q.OrderBy(p => p.Name).ToListAsync();
        return await ToDtosAsync(list);
    }

    [HttpGet("projects/{id:guid}")]
    public async Task<ProjectDto> Project(Guid id) =>
        (await ToDtosAsync([await db.Projects.Include(p => p.Members).Include(p => p.Tasks).AsNoTracking().FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Project")]))[0];

    private async Task<List<ProjectDto>> ToDtosAsync(List<Project> list)
    {
        var ids = list.Select(p => p.Id).ToList();
        var logged = await db.TimesheetLines.Where(l => ids.Contains(l.ProjectId))
            .Join(db.Timesheets.Where(t => t.Status != TimesheetStatus.Draft), l => l.TimesheetId, t => t.Id, (l, t) => new { l.ProjectId, l.TaskId, l.Minutes, t.EmployeeId })
            .ToListAsync();
        var people = list.SelectMany(p => p.Members.Select(m => m.EmployeeId).Concat(p.Tasks.Where(t => t.AssigneeEmployeeId != null).Select(t => t.AssigneeEmployeeId!.Value))
                .Concat(p.ManagerEmployeeId is { } m ? [m] : [])).Distinct().ToList();
        var names = await db.Employees.Where(e => people.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        return list.Select(p =>
        {
            var mineLogged = logged.Where(l => l.ProjectId == p.Id).ToList();
            var rates = p.Members.ToDictionary(m => m.EmployeeId, m => m.HourlyRate ?? 0);
            var cost = mineLogged.Sum(l => l.Minutes / 60m * rates.GetValueOrDefault(l.EmployeeId));
            return new ProjectDto(p.Id, p.Name, p.Code, p.Client, p.Description, p.ManagerEmployeeId,
                p.ManagerEmployeeId is { } m ? names.GetValueOrDefault(m) : null, p.StartDate, p.EndDate, p.BudgetHours, p.Billable, p.OpenToAll, p.Status,
                p.Members.Select(x => new ProjectMemberDto(x.EmployeeId, names.GetValueOrDefault(x.EmployeeId), me.IsHr() || p.ManagerEmployeeId == me.EmployeeId ? x.HourlyRate : null)).OrderBy(x => x.Name).ToList(),
                p.Tasks.OrderBy(t => t.IsDone).ThenBy(t => t.DueDate ?? DateOnly.MaxValue).ThenBy(t => t.Name)
                    .Select(t => new ProjectTaskDto(t.Id, t.Name, t.AssigneeEmployeeId, t.AssigneeEmployeeId is { } a ? names.GetValueOrDefault(a) : null, t.DueDate, t.EstimatedHours,
                        t.IsDone, Math.Round(mineLogged.Where(l => l.TaskId == t.Id).Sum(l => l.Minutes) / 60m, 2))).ToList(),
                Math.Round(mineLogged.Sum(l => l.Minutes) / 60m, 2), me.IsHr() || p.ManagerEmployeeId == me.EmployeeId ? Math.Round(cost, 0) : 0, CanManage(p));
        }).ToList();
    }

    private bool CanManage(Project p) => me.IsHr() || (me.EmployeeId is { } mine && p.ManagerEmployeeId == mine);

    [HttpPost("projects"), Authorize(Policy = Policies.Hr)]
    public async Task<ProjectDto> AddProject(SaveProjectRequest req)
    {
        var p = new Project();
        db.Projects.Add(p);
        await ApplyAsync(p, req);
        await db.SaveChangesAsync();
        return await Project(p.Id);
    }

    [HttpPut("projects/{id:guid}")]
    public async Task<ProjectDto> UpdateProject(Guid id, SaveProjectRequest req)
    {
        var p = await db.Projects.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Project");
        if (!CanManage(p)) throw new ForbiddenException("Only HR or the project manager can change this project.");
        if (!me.IsHr() && req.ManagerEmployeeId != p.ManagerEmployeeId) throw new ForbiddenException("Only HR can change the project manager.");
        await ApplyAsync(p, req);
        await db.SaveChangesAsync();
        return await Project(p.Id);
    }

    [HttpDelete("projects/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        var p = await db.Projects.Include(x => x.Members).Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Project");
        if (await db.TimesheetLines.AnyAsync(l => l.ProjectId == id))
            throw new DomainException("Hours are logged on this project. Close it instead.");
        db.Projects.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(Project p, SaveProjectRequest req)
    {
        var name = req.Name.Trim();
        if (await db.Projects.AnyAsync(x => x.Id != p.Id && x.Name == name)) throw new DomainException($"There is already a project called {name}.");
        if (req.StartDate is { } s && req.EndDate is { } e && e < s) throw new DomainException("The project ends before it starts.");
        if (req.ManagerEmployeeId is { } m && !await db.Employees.AnyAsync(x => x.Id == m)) throw new NotFoundException("Employee");
        var members = (req.Members ?? []).GroupBy(x => x.EmployeeId).Select(g => g.First()).ToList();
        var ids = members.Select(x => x.EmployeeId).ToList();
        if (await db.Employees.CountAsync(x => ids.Contains(x.Id)) != ids.Count) throw new NotFoundException("Employee");
        if (members.Any(x => x.HourlyRate is < 0)) throw new DomainException("Hourly rates cannot be negative.");
        p.Name = name;
        p.Code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim();
        p.Client = string.IsNullOrWhiteSpace(req.Client) ? null : req.Client.Trim();
        p.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        p.ManagerEmployeeId = req.ManagerEmployeeId;
        p.StartDate = req.StartDate;
        p.EndDate = req.EndDate;
        p.BudgetHours = req.BudgetHours;
        p.Billable = req.Billable;
        p.OpenToAll = req.OpenToAll;
        p.Status = Enum.IsDefined(req.Status) ? req.Status : ProjectStatus.Active;
        foreach (var gone in p.Members.Where(x => !ids.Contains(x.EmployeeId)).ToList())
            db.ProjectMembers.Remove(gone);
        foreach (var x in members)
        {
            var existing = p.Members.FirstOrDefault(y => y.EmployeeId == x.EmployeeId);
            if (existing is null)
                db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, EmployeeId = x.EmployeeId, HourlyRate = x.HourlyRate });
            else
                existing.HourlyRate = x.HourlyRate;
        }
    }

    // ---------- Tasks ----------

    [HttpPost("projects/{id:guid}/tasks")]
    public async Task<IActionResult> AddTask(Guid id, SaveProjectTaskRequest req)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Project");
        if (!CanManage(p)) throw new ForbiddenException("Only HR or the project manager can add tasks.");
        var t = new ProjectTask { ProjectId = id };
        await ApplyTaskAsync(t, req);
        db.ProjectTasks.Add(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("projects/{id:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid id, Guid taskId, SaveProjectTaskRequest req)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Project");
        var t = await db.ProjectTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.ProjectId == id) ?? throw new NotFoundException("Task");
        // The assignee may tick their own task done; everything else is for the manager.
        if (!CanManage(p))
        {
            if (t.AssigneeEmployeeId != me.EmployeeId) throw new ForbiddenException("Only HR or the project manager can change tasks.");
            t.IsDone = req.IsDone;
        }
        else
            await ApplyTaskAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("projects/{id:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, Guid taskId)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Project");
        if (!CanManage(p)) throw new ForbiddenException("Only HR or the project manager can delete tasks.");
        var t = await db.ProjectTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.ProjectId == id) ?? throw new NotFoundException("Task");
        if (await db.TimesheetLines.AnyAsync(l => l.TaskId == taskId)) throw new DomainException("Hours are logged on this task. Mark it done instead.");
        db.ProjectTasks.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyTaskAsync(ProjectTask t, SaveProjectTaskRequest req)
    {
        if (req.AssigneeEmployeeId is { } a && !await db.Employees.AnyAsync(e => e.Id == a)) throw new NotFoundException("Employee");
        t.Name = req.Name.Trim();
        t.AssigneeEmployeeId = req.AssigneeEmployeeId;
        t.DueDate = req.DueDate;
        t.EstimatedHours = req.EstimatedHours;
        t.IsDone = req.IsDone;
    }

    // ---------- Timesheets ----------

    [HttpGet("timesheets/week")]
    public async Task<TimesheetDto> MyWeek([FromQuery] DateOnly? start) => await WeekAsync(MyId(), TimesheetService.Monday(start ?? await clock.TodayAsync()));

    [HttpPut("timesheets/week")]
    public async Task<TimesheetDto> SaveWeek(SaveTimesheetRequest req)
    {
        await timesheets.SaveAsync(MyId(), req.WeekStart, req.Lines);
        return await WeekAsync(MyId(), TimesheetService.Monday(req.WeekStart));
    }

    [HttpPost("timesheets/week/submit")]
    public async Task<TimesheetDto> Submit([FromQuery] DateOnly start)
    {
        await timesheets.SubmitAsync(MyId(), start);
        return await WeekAsync(MyId(), TimesheetService.Monday(start));
    }

    [HttpPost("timesheets/week/withdraw")]
    public async Task<TimesheetDto> Withdraw([FromQuery] DateOnly start)
    {
        await timesheets.WithdrawAsync(MyId(), start);
        return await WeekAsync(MyId(), TimesheetService.Monday(start));
    }

    [HttpGet("timesheets/my")]
    public async Task<List<TimesheetDto>> MyRecent()
    {
        var id = MyId();
        var weeks = await db.Timesheets.Where(t => t.EmployeeId == id).OrderByDescending(t => t.WeekStart).Take(12).Select(t => t.WeekStart).ToListAsync();
        var list = new List<TimesheetDto>();
        foreach (var w in weeks) list.Add(await WeekAsync(id, w));
        return list;
    }

    [HttpGet("timesheets/approvals"), Authorize(Policy = Policies.Approver)]
    public async Task<List<TimesheetDto>> Approvals([FromQuery] TimesheetStatus status = TimesheetStatus.Submitted)
    {
        var q = db.Timesheets.Where(t => t.Status == status && t.EmployeeId != me.EmployeeId);
        if (!me.IsHr())
        {
            var waiting = await approvals.WaitingForMeAsync(ApprovalRequestType.Timesheet);
            q = q.Where(t => waiting.Contains(t.Id));
        }
        var rows = await q.OrderByDescending(t => t.WeekStart).Take(100).Select(t => new { t.EmployeeId, t.WeekStart }).ToListAsync();
        var list = new List<TimesheetDto>();
        foreach (var r in rows) list.Add(await WeekAsync(r.EmployeeId, r.WeekStart));
        return list;
    }

    [HttpPost("timesheets/{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, TimesheetDecisionRequest? req)
    {
        await timesheets.DecideAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("timesheets/{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, TimesheetDecisionRequest? req)
    {
        await timesheets.DecideAsync(id, false, req?.Note);
        return NoContent();
    }

    /// <summary>The week's lines, plus minutes at work each day from attendance to compare.</summary>
    private async Task<TimesheetDto> WeekAsync(Guid employeeId, DateOnly weekStart)
    {
        var sheet = await db.Timesheets.Include(t => t.Lines).AsNoTracking().FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStart == weekStart);
        var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        var end = weekStart.AddDays(6);
        var attendance = await db.AttendanceRecords.Where(a => a.EmployeeId == employeeId && a.Date >= weekStart && a.Date <= end)
            .ToDictionaryAsync(a => a.Date.ToString("yyyy-MM-dd"), a => a.WorkedMinutes);
        var lines = sheet?.Lines.OrderBy(l => l.Date).Select(l => new TimesheetLineDto(l.Date, l.ProjectId, l.TaskId, l.Minutes, l.Note)).ToList() ?? [];
        return new TimesheetDto(sheet?.Id, employeeId, name, weekStart, sheet?.Status ?? TimesheetStatus.Draft, sheet?.SubmittedAtUtc, sheet?.DecisionNote,
            lines, lines.Sum(l => l.Minutes), attendance);
    }

    private Guid MyId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
}
