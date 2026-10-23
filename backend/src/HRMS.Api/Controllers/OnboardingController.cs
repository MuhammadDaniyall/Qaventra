using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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

public record TemplateTaskDto(
    Guid? Id, [Required, StringLength(200)] string Title, [StringLength(1000)] string? Description, OnboardingTaskCategory Category,
    OnboardingAssignee AssigneeKind, Guid? AssigneeEmployeeId, [Range(-60, 365)] int DueOffsetDays);

public record OnboardingTemplateDto(
    Guid Id, string Name, string? Description, bool IsDefault, Guid? AssetKitId, string? WelcomeMessage, List<TemplateTaskDto> Tasks);

public record SaveOnboardingTemplateRequest(
    [Required, StringLength(100)] string Name, [StringLength(500)] string? Description, bool IsDefault, Guid? AssetKitId,
    [StringLength(4000)] string? WelcomeMessage, [MaxLength(60)] List<TemplateTaskDto> Tasks);

public record StartOnboardingRequest(Guid EmployeeId, Guid? TemplateId, TimeOnly? ReportingTime, [StringLength(300)] string? ReportingPlace,
    Guid? BuddyEmployeeId, [StringLength(4000)] string? Instructions);

public record AddOnboardingTaskRequest([Required, StringLength(200)] string Title, OnboardingTaskCategory Category, OnboardingAssignee AssigneeKind,
    Guid? AssigneeEmployeeId, DateOnly DueOn);

public record SetTaskRequest(bool Done, [StringLength(1000)] string? Note);

public record IntroduceRequest([Required] Dictionary<string, string?> Answers);

public record OnboardingTaskDto(
    Guid Id, Guid OnboardingId, string Title, string? Description, OnboardingTaskCategory Category, OnboardingAssignee AssigneeKind,
    Guid? AssigneeEmployeeId, string? Assignee, DateOnly DueOn, bool IsOverdue, DateTime? CompletedAtUtc, string? CompletedByName, string? Note, bool CanDo,
    string? ForEmployee);

public record OnboardingListItemDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Department, string? Designation, DateOnly JoiningDate, string TemplateName,
    OnboardingStatus Status, int Total, int Done, int Overdue, bool Introduced);

public record OnboardingDetailDto(
    OnboardingListItemDto Summary, TimeOnly? ReportingTime, string? ReportingPlace, Guid? BuddyEmployeeId, string? Buddy, string? BuddyPhone,
    string? Manager, string? Instructions, string? StartedByName, Dictionary<string, string?>? Introduction, List<OnboardingTaskDto> Tasks);

public record WelcomeDto(OnboardingDetailDto? Onboarding, List<IntroQuestionDto> Questions, bool HasPhoto, bool ProfileComplete, int EmergencyContacts);

public record IntroQuestionDto(string Key, string Question);

[ApiController]
[Route("api/onboarding")]
[Authorize(Policy = Policies.CompanyUser)]
public class OnboardingController(HrmsDbContext db, OnboardingService onboarding, TenantClock clock, ICurrentUser me) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------- Templates ----------

    [HttpGet("templates"), Authorize(Policy = Policies.Hr)]
    public async Task<List<OnboardingTemplateDto>> Templates() =>
        (await db.OnboardingTemplates.Include(t => t.Tasks).OrderBy(t => t.Name).ToListAsync()).Select(ToDto).ToList();

    [HttpPost("templates"), Authorize(Policy = Policies.Hr)]
    public async Task<OnboardingTemplateDto> CreateTemplate(SaveOnboardingTemplateRequest req)
    {
        var t = new OnboardingTemplate();
        await ApplyAsync(t, req);
        db.OnboardingTemplates.Add(t);
        await db.SaveChangesAsync();
        return ToDto(t);
    }

    [HttpPut("templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<OnboardingTemplateDto> UpdateTemplate(Guid id, SaveOnboardingTemplateRequest req)
    {
        var t = await db.OnboardingTemplates.Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        await ApplyAsync(t, req);
        await db.SaveChangesAsync();
        return ToDto(t);
    }

    [HttpDelete("templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var t = await db.OnboardingTemplates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        db.OnboardingTemplates.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Onboardings ----------

    [HttpGet, Authorize(Policy = Policies.Hr)]
    public async Task<List<OnboardingListItemDto>> List([FromQuery] OnboardingStatus status = OnboardingStatus.InProgress) =>
        await ProjectAsync(db.Onboardings.Where(o => o.Status == status).OrderBy(o => o.JoiningDate));

    [HttpPost, Authorize(Policy = Policies.Hr)]
    public async Task<OnboardingDetailDto> Start(StartOnboardingRequest req)
    {
        var o = await onboarding.StartAsync(req.EmployeeId, new StartOnboarding(req.TemplateId, req.ReportingTime, req.ReportingPlace, req.BuddyEmployeeId, req.Instructions));
        return await DetailAsync(o.Id);
    }

    [HttpGet("{id:guid}")]
    public async Task<OnboardingDetailDto> Get(Guid id)
    {
        var o = await db.Onboardings.Include(x => x.Employee).Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Onboarding");
        var mine = me.EmployeeId;
        var involved = me.IsHr() || o.EmployeeId == mine || o.BuddyEmployeeId == mine || o.Employee!.ManagerId == mine || o.Tasks.Any(t => t.AssigneeEmployeeId == mine);
        if (!involved)
            throw new NotFoundException("Onboarding");
        return await DetailAsync(id);
    }

    [HttpGet("employee/{employeeId:guid}"), Authorize(Policy = Policies.Approver)]
    public async Task<ActionResult<OnboardingDetailDto>> ForEmployee(Guid employeeId)
    {
        var id = await db.Onboardings.Where(o => o.EmployeeId == employeeId).OrderByDescending(o => o.CreatedAtUtc).Select(o => (Guid?)o.Id).FirstOrDefaultAsync();
        return id is { } x ? await Get(x) : NoContent();
    }

    [HttpPost("{id:guid}/tasks"), Authorize(Policy = Policies.Hr)]
    public async Task<OnboardingDetailDto> AddTask(Guid id, AddOnboardingTaskRequest req)
    {
        await onboarding.AddTaskAsync(id, req.Title, req.Category, req.AssigneeKind, req.AssigneeEmployeeId, req.DueOn);
        return await DetailAsync(id);
    }

    [HttpDelete("tasks/{taskId:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteTask(Guid taskId)
    {
        var t = await db.OnboardingTasks.FirstOrDefaultAsync(x => x.Id == taskId) ?? throw new NotFoundException("Task");
        db.OnboardingTasks.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("tasks/{taskId:guid}")]
    public async Task<IActionResult> SetTask(Guid taskId, SetTaskRequest req)
    {
        await onboarding.SetTaskAsync(taskId, req.Done, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await onboarding.CancelAsync(id);
        return NoContent();
    }

    /// <summary>Onboarding tasks waiting on the signed-in user (HR also gets tasks for HR), across everyone being onboarded.</summary>
    [HttpGet("my-tasks")]
    public async Task<List<OnboardingTaskDto>> MyTasks([FromQuery] bool done = false)
    {
        var mine = me.EmployeeId;
        var hr = me.IsHr();
        var q = db.OnboardingTasks.Where(t => db.Onboardings.Any(o => o.Id == t.OnboardingId && o.Status != OnboardingStatus.Cancelled && o.EmployeeId != mine)
                                             && (t.AssigneeEmployeeId == mine || (hr && t.AssigneeEmployeeId == null))
                                             && (done ? t.CompletedAtUtc != null : t.CompletedAtUtc == null));
        return await ProjectTasksAsync(q.OrderBy(t => t.DueOn).Take(300), withEmployee: true);
    }

    // ---------- Welcome page ----------

    /// <summary>The signed-in employee's own onboarding, the introduction questions and what is left of their profile.</summary>
    [HttpGet("welcome")]
    public async Task<WelcomeDto> Welcome()
    {
        var mine = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var id = await db.Onboardings.Where(o => o.EmployeeId == mine && o.Status != OnboardingStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAtUtc).Select(o => (Guid?)o.Id).FirstOrDefaultAsync();
        var e = await db.Employees.FirstAsync(x => x.Id == mine);
        var complete = e.Phone != null && e.Cnic != null && e.DateOfBirth != null && e.Address != null && e.BankAccount != null;
        return new WelcomeDto(id is { } x ? await DetailAsync(x) : null, OnboardingService.IntroQuestions.Select(q => new IntroQuestionDto(q.Key, q.Question)).ToList(),
            await db.Attachments.AnyAsync(a => a.OwnerType == AttachmentOwner.EmployeeDocument && a.OwnerId == mine && a.Category == DocumentCategory.Photo),
            complete, await db.EmergencyContacts.CountAsync(c => c.EmployeeId == mine));
    }

    [HttpPost("introduce")]
    public async Task<IActionResult> Introduce(IntroduceRequest req)
    {
        await onboarding.IntroduceAsync(req.Answers);
        return NoContent();
    }

    // ---------- Helpers ----------

    private static OnboardingTemplateDto ToDto(OnboardingTemplate t) => new(t.Id, t.Name, t.Description, t.IsDefault, t.AssetKitId, t.WelcomeMessage,
        t.Tasks.OrderBy(x => x.Order).Select(x => new TemplateTaskDto(x.Id, x.Title, x.Description, x.Category, x.AssigneeKind, x.AssigneeEmployeeId, x.DueOffsetDays)).ToList());

    private async Task ApplyAsync(OnboardingTemplate t, SaveOnboardingTemplateRequest req)
    {
        var name = req.Name.Trim();
        if (await db.OnboardingTemplates.AnyAsync(x => x.Id != t.Id && x.Name == name))
            throw new DomainException($"There is already a {name} template.");
        if (req.AssetKitId is { } kit && !await db.AssetKits.AnyAsync(k => k.Id == kit))
            throw new NotFoundException("Starter kit");
        var tasks = req.Tasks ?? [];
        if (tasks.Any(x => string.IsNullOrWhiteSpace(x.Title)))
            throw new DomainException("Every task needs a title.");
        foreach (var x in tasks.Where(x => x.AssigneeKind == OnboardingAssignee.SpecificEmployee))
            if (x.AssigneeEmployeeId is not { } e || !await db.Employees.AnyAsync(y => y.Id == e))
                throw new DomainException($"Choose who does \"{x.Title}\".");
        if (req.IsDefault)
            foreach (var other in await db.OnboardingTemplates.Where(x => x.Id != t.Id && x.IsDefault).ToListAsync())
                other.IsDefault = false;
        t.Name = name;
        t.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        t.IsDefault = req.IsDefault;
        t.AssetKitId = req.AssetKitId;
        t.WelcomeMessage = string.IsNullOrWhiteSpace(req.WelcomeMessage) ? null : req.WelcomeMessage.Trim();
        db.OnboardingTemplateTasks.RemoveRange(t.Tasks);
        t.Tasks.Clear();
        var order = 0;
        foreach (var x in tasks)
        {
            var task = new OnboardingTemplateTask
            {
                TemplateId = t.Id, Title = x.Title.Trim(), Description = string.IsNullOrWhiteSpace(x.Description) ? null : x.Description.Trim(),
                Category = x.Category, AssigneeKind = x.AssigneeKind,
                AssigneeEmployeeId = x.AssigneeKind == OnboardingAssignee.SpecificEmployee ? x.AssigneeEmployeeId : null,
                DueOffsetDays = x.DueOffsetDays, Order = ++order,
            };
            t.Tasks.Add(task);
            db.OnboardingTemplateTasks.Add(task);
        }
    }

    private async Task<List<OnboardingListItemDto>> ProjectAsync(IQueryable<Onboarding> q)
    {
        var today = await clock.TodayAsync();
        return await q.Select(o => new OnboardingListItemDto(o.Id, o.EmployeeId, o.Employee!.FirstName + " " + o.Employee.LastName, o.Employee.EmployeeCode,
            o.Employee.Department != null ? o.Employee.Department.Name : null, o.Employee.Designation != null ? o.Employee.Designation.Title : null,
            o.JoiningDate, o.TemplateName, o.Status, o.Tasks.Count, o.Tasks.Count(t => t.CompletedAtUtc != null),
            o.Tasks.Count(t => t.CompletedAtUtc == null && t.DueOn < today), o.IntroducedAtUtc != null)).ToListAsync();
    }

    private async Task<List<OnboardingTaskDto>> ProjectTasksAsync(IQueryable<OnboardingTask> q, bool withEmployee)
    {
        var today = await clock.TodayAsync();
        var rows = await q.Select(t => new
        {
            Task = t,
            Assignee = db.Employees.Where(e => e.Id == t.AssigneeEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            For = db.Onboardings.Where(o => o.Id == t.OnboardingId).Select(o => o.Employee!.FirstName + " " + o.Employee.LastName).FirstOrDefault(),
        }).ToListAsync();
        return rows.Select(r => new OnboardingTaskDto(r.Task.Id, r.Task.OnboardingId, r.Task.Title, r.Task.Description, r.Task.Category, r.Task.AssigneeKind,
            r.Task.AssigneeEmployeeId, r.Assignee ?? (r.Task.AssigneeEmployeeId is null ? "HR" : null), r.Task.DueOn,
            r.Task.CompletedAtUtc is null && r.Task.DueOn < today, r.Task.CompletedAtUtc, r.Task.CompletedByName, r.Task.Note, onboarding.CanDo(r.Task),
            withEmployee ? r.For : null)).ToList();
    }

    private async Task<OnboardingDetailDto> DetailAsync(Guid id)
    {
        var summary = (await ProjectAsync(db.Onboardings.Where(o => o.Id == id))).Single();
        var o = await db.Onboardings.Include(x => x.Employee).FirstAsync(x => x.Id == id);
        var buddy = o.BuddyEmployeeId is { } b ? await db.Employees.Where(e => e.Id == b).Select(e => new { Name = e.FirstName + " " + e.LastName, e.Phone }).FirstOrDefaultAsync() : null;
        var manager = o.Employee!.ManagerId is { } m ? await db.Employees.Where(e => e.Id == m).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync() : null;
        var tasks = await ProjectTasksAsync(db.OnboardingTasks.Where(t => t.OnboardingId == id).OrderBy(t => t.DueOn).ThenBy(t => t.Order), withEmployee: false);
        return new OnboardingDetailDto(summary, o.ReportingTime, o.ReportingPlace, o.BuddyEmployeeId, buddy?.Name, buddy?.Phone, manager, o.Instructions, o.StartedByName,
            o.IntroductionJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, string?>>(o.IntroductionJson, Json), tasks);
    }
}
