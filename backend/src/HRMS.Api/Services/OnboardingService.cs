using System.Text.Json;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record StartOnboarding(Guid? TemplateId, TimeOnly? ReportingTime, string? ReportingPlace, Guid? BuddyEmployeeId, string? Instructions);

/// <summary>
/// A new joiner's first days: tasks for HR, their manager, their buddy and themselves, a welcome page with first-day
/// details, and an introduction to the team.
/// </summary>
public class OnboardingService(
    HrmsDbContext db, ICurrentUser me, TenantClock clock, NotificationService notify, AssetService assets, AnnouncementService announcements, IServiceProvider services)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The questions a new joiner answers to introduce themselves (key, question).</summary>
    public static readonly (string Key, string Question)[] IntroQuestions =
    [
        ("about", "A few lines about you"),
        ("previous", "Where you worked or studied before"),
        ("hobbies", "What you enjoy outside work"),
        ("funFact", "A fun fact about you"),
    ];

    public async Task<Onboarding> StartAsync(Guid employeeId, StartOnboarding input)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId) ?? throw new NotFoundException("Employee");
        if (!employee.IsCurrent)
            throw new DomainException($"{employee.FullName} does not work here.");
        if (await db.Onboardings.AnyAsync(o => o.EmployeeId == employeeId && o.Status == OnboardingStatus.InProgress))
            throw new DomainException($"{employee.FirstName} is already being onboarded.");
        if (input.BuddyEmployeeId is { } b && (b == employeeId || !await db.Employees.AnyAsync(e => e.Id == b && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation))))
            throw new DomainException("Choose a buddy who works here.");
        OnboardingTemplate? template = null;
        if (input.TemplateId is { } tid)
            template = await db.OnboardingTemplates.Include(t => t.Tasks).FirstOrDefaultAsync(t => t.Id == tid) ?? throw new NotFoundException("Template");
        // A named person who has since left can't do the task; it falls to HR instead.
        var named = template?.Tasks.Where(t => t.AssigneeKind == OnboardingAssignee.SpecificEmployee && t.AssigneeEmployeeId != null)
            .Select(t => t.AssigneeEmployeeId!.Value).Distinct().ToList() ?? [];
        var current = await db.Employees
            .Where(e => named.Contains(e.Id) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)).Select(e => e.Id).ToListAsync();

        var onboarding = new Onboarding
        {
            EmployeeId = employee.Id,
            TemplateName = template?.Name ?? "Custom",
            JoiningDate = employee.JoiningDate,
            ReportingTime = input.ReportingTime,
            ReportingPlace = Clean(input.ReportingPlace, 300),
            BuddyEmployeeId = input.BuddyEmployeeId,
            Instructions = Clean(input.Instructions, 4000) ?? template?.WelcomeMessage,
            StartedByName = await MyNameAsync(),
            RequiresApproval = template?.RequiresApproval ?? false,
        };
        foreach (var t in template?.Tasks.OrderBy(t => t.Order) ?? Enumerable.Empty<OnboardingTemplateTask>())
            onboarding.Tasks.Add(new OnboardingTask
            {
                Title = t.Title, Description = t.Description, Category = t.Category, AssigneeKind = t.AssigneeKind,
                AssigneeEmployeeId = t.AssigneeKind == OnboardingAssignee.SpecificEmployee && t.AssigneeEmployeeId is { } who && !current.Contains(who)
                    ? null : Resolve(t.AssigneeKind, t.AssigneeEmployeeId, employee, input.BuddyEmployeeId),
                DueOn = employee.JoiningDate.AddDays(t.DueOffsetDays), Order = t.Order,
            });
        db.Onboardings.Add(onboarding);

        foreach (var g in onboarding.Tasks.Where(t => t.AssigneeEmployeeId != employee.Id).GroupBy(t => t.AssigneeEmployeeId))
        {
            var title = $"{g.Count()} onboarding task{(g.Count() == 1 ? "" : "s")} for {employee.FullName}";
            var body = string.Join(", ", g.Select(t => t.Title));
            if (g.Key is { } person)
                await notify.ToEmployeesAsync([person], NotificationKind.Onboarding, title, body, "/onboarding?tab=tasks");
            else
                await notify.ToHrAsync(NotificationKind.Onboarding, title, body, "/onboarding?tab=tasks");
        }
        if (input.BuddyEmployeeId is { } buddy)
            await notify.ToEmployeesAsync([buddy], NotificationKind.Onboarding, $"You are {employee.FirstName}'s onboarding buddy",
                $"{employee.FullName} joins on {employee.JoiningDate:d MMM}. Show them around!", "/onboarding?tab=tasks");
        await db.SaveChangesAsync();
        await WelcomeAsync([employee.Id]);

        if (template?.AssetKitId is { } kit && me.IsHr())
            await assets.ApplyKitAsync(kit, employee.Id);
        return onboarding;
    }

    /// <summary>
    /// Greets people being onboarded once they can sign in (a login usually comes after the employee record), and also
    /// right away when onboarding starts for someone who already has one.
    /// </summary>
    public async Task WelcomeAsync(IReadOnlyCollection<Guid> employeeIds)
    {
        var people = await db.Onboardings.Where(o => employeeIds.Contains(o.EmployeeId) && o.Status == OnboardingStatus.InProgress)
            .Select(o => new { o.EmployeeId, o.Employee!.FirstName, o.JoiningDate }).ToListAsync();
        foreach (var p in people)
            await notify.ToEmployeesAsync([p.EmployeeId], NotificationKind.Onboarding, $"Welcome to the team, {p.FirstName}!",
                $"Your first day is {p.JoiningDate:dddd d MMMM}. Everything you need is on your welcome page.", "/welcome");
        if (people.Count > 0)
            await db.SaveChangesAsync();
    }

    /// <summary>Starts the company's default template for someone just added, if there is one.</summary>
    public async Task AutoStartAsync(Guid employeeId)
    {
        var template = await db.OnboardingTemplates.Where(t => t.IsDefault).Select(t => (Guid?)t.Id).FirstOrDefaultAsync();
        // Someone added as already gone (resigned, terminated) has nothing to onboard; don't fail their creation over it.
        if (template is not null
            && await db.Employees.AnyAsync(e => e.Id == employeeId && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation))
            && !await db.Onboardings.AnyAsync(o => o.EmployeeId == employeeId))
            await StartAsync(employeeId, new StartOnboarding(template, null, null, null, null));
    }

    private static Guid? Resolve(OnboardingAssignee kind, Guid? specific, Employee employee, Guid? buddy) => kind switch
    {
        OnboardingAssignee.NewJoiner => employee.Id,
        OnboardingAssignee.LineManager => employee.ManagerId,
        OnboardingAssignee.Buddy => buddy ?? employee.ManagerId,
        OnboardingAssignee.SpecificEmployee => specific,
        _ => null,
    };

    public bool CanDo(OnboardingTask t) => me.IsHr() || (t.AssigneeEmployeeId is { } a && a == me.EmployeeId);

    /// <summary>Marks a task done (or not). When every task is done, the onboarding is complete.</summary>
    public async Task SetTaskAsync(Guid taskId, bool done, string? note)
    {
        var task = await FindTaskAsync(taskId);
        var onboarding = await db.Onboardings.Include(o => o.Tasks).Include(o => o.Employee).FirstOrDefaultAsync(o => o.Id == task.OnboardingId)
                         ?? throw new NotFoundException("Onboarding");
        if (!CanDo(task))
            throw new ForbiddenException("This task is someone else's.");
        if (onboarding.Status == OnboardingStatus.Cancelled)
            throw new DomainException("This onboarding was cancelled.");
        task.CompletedAtUtc = done ? clock.UtcNow : null;
        task.CompletedByName = done ? await MyNameAsync() : null;
        if (note is not null)
            task.Note = Clean(note, 1000);

        await RefreshStatusAsync(onboarding);
        await db.SaveChangesAsync();
    }

    /// <summary>HR removes a task; if it was the last one open, the onboarding is complete.</summary>
    public async Task DeleteTaskAsync(Guid taskId)
    {
        var task = await FindTaskAsync(taskId);
        var onboarding = await db.Onboardings.Include(o => o.Tasks).Include(o => o.Employee).FirstOrDefaultAsync(o => o.Id == task.OnboardingId)
                         ?? throw new NotFoundException("Onboarding");
        if (onboarding.Status != OnboardingStatus.InProgress)
            throw new DomainException("This onboarding is not in progress.");
        onboarding.Tasks.Remove(task);
        db.OnboardingTasks.Remove(task);
        await RefreshStatusAsync(onboarding);
        await db.SaveChangesAsync();
    }

    /// <summary>Tasks aren't tenant entities themselves; only find one whose onboarding is in this company.</summary>
    private async Task<OnboardingTask> FindTaskAsync(Guid taskId) =>
        await db.OnboardingTasks.FirstOrDefaultAsync(t => t.Id == taskId && db.Onboardings.Any(o => o.Id == t.OnboardingId))
        ?? throw new NotFoundException("Task");

    private ApprovalEngine Approvals => services.GetRequiredService<ApprovalEngine>();

    /// <summary>
    /// Completes the onboarding when every task is done (or sends it for approval when the template asks for it),
    /// and reopens it when one is undone.
    /// </summary>
    private async Task RefreshStatusAsync(Onboarding onboarding)
    {
        var allDone = onboarding.Tasks.All(t => t.CompletedAtUtc is not null);
        if (allDone && onboarding.Status == OnboardingStatus.InProgress)
            await FinishAsync(onboarding);
        else if (!allDone && onboarding.Status is OnboardingStatus.Completed or OnboardingStatus.PendingApproval)
        {
            if (onboarding.Status == OnboardingStatus.PendingApproval)
                await Approvals.CancelAsync(ApprovalRequestType.Onboarding, onboarding.Id);
            onboarding.Status = OnboardingStatus.InProgress;
            onboarding.CompletedAtUtc = null;
        }
    }

    private async Task FinishAsync(Onboarding onboarding)
    {
        if (onboarding.RequiresApproval)
        {
            var approval = await Approvals.StartAsync(ApprovalRequestType.Onboarding, onboarding.Id, onboarding.EmployeeId,
                $"Onboarding of {onboarding.Employee!.FullName} is done ({onboarding.Tasks.Count} tasks)");
            if (approval.Status != ApprovalStatus.Approved)
            {
                onboarding.Status = OnboardingStatus.PendingApproval;
                return;
            }
        }
        Complete(onboarding);
        await notify.ToHrAsync(NotificationKind.Onboarding, $"{onboarding.Employee!.FullName}'s onboarding is complete", null, $"/onboarding/{onboarding.Id}");
    }

    private void Complete(Onboarding onboarding)
    {
        onboarding.Status = OnboardingStatus.Completed;
        onboarding.CompletedAtUtc = clock.UtcNow;
    }

    /// <summary>HR sends a finished onboarding for approval again after it was sent back.</summary>
    public async Task SubmitAsync(Guid id)
    {
        var o = await db.Onboardings.Include(x => x.Tasks).Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Onboarding");
        if (o.Status != OnboardingStatus.InProgress || !o.RequiresApproval)
            throw new DomainException("This onboarding is not waiting to be sent for approval.");
        if (o.Tasks.Any(t => t.CompletedAtUtc is null))
            throw new DomainException("Finish every task first.");
        await FinishAsync(o);
        await db.SaveChangesAsync();
    }

    /// <summary>An approver accepts the finished onboarding (complete) or sends it back to HR with a note.</summary>
    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var o = await db.Onboardings.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Onboarding");
        if (o.Status != OnboardingStatus.PendingApproval)
            throw new DomainException("This onboarding is not waiting for approval.");
        var outcome = await Approvals.DecideAsync(ApprovalRequestType.Onboarding, o.Id, o.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Pending)
        {
            await db.SaveChangesAsync();
            return;
        }
        o.ApprovalNote = Clean(note, 1000);
        if (outcome == ApprovalStatus.Approved)
        {
            Complete(o);
            await notify.ToHrAsync(NotificationKind.Onboarding, $"{o.Employee!.FullName}'s onboarding is approved", null, $"/onboarding/{o.Id}");
        }
        else
        {
            o.Status = OnboardingStatus.InProgress;
            await notify.ToHrAsync(NotificationKind.Onboarding, $"{o.Employee!.FullName}'s onboarding was sent back", o.ApprovalNote, $"/onboarding/{o.Id}");
        }
        await db.SaveChangesAsync();
    }

    public async Task<OnboardingTask> AddTaskAsync(Guid onboardingId, string title, OnboardingTaskCategory category, OnboardingAssignee kind, Guid? employeeId, DateOnly dueOn)
    {
        var onboarding = await db.Onboardings.Include(o => o.Employee).Include(o => o.Tasks).FirstOrDefaultAsync(o => o.Id == onboardingId) ?? throw new NotFoundException("Onboarding");
        if (onboarding.Status != OnboardingStatus.InProgress)
            throw new DomainException("This onboarding is not in progress.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Name the task.");
        if (kind == OnboardingAssignee.SpecificEmployee && (employeeId is not { } e || !await db.Employees.AnyAsync(x => x.Id == e && (x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation))))
            throw new DomainException("Choose who does it.");
        var task = new OnboardingTask
        {
            OnboardingId = onboarding.Id, Title = title.Trim(), Category = category, AssigneeKind = kind,
            AssigneeEmployeeId = Resolve(kind, employeeId, onboarding.Employee!, onboarding.BuddyEmployeeId), DueOn = dueOn,
            Order = onboarding.Tasks.Count == 0 ? 1 : onboarding.Tasks.Max(t => t.Order) + 1,
        };
        onboarding.Tasks.Add(task);
        db.OnboardingTasks.Add(task);
        if (task.AssigneeEmployeeId is { } who && who != me.EmployeeId)
            await notify.ToEmployeesAsync([who], NotificationKind.Onboarding, $"Onboarding task for {onboarding.Employee!.FullName}", task.Title,
                who == onboarding.EmployeeId ? "/welcome" : "/onboarding?tab=tasks");
        await db.SaveChangesAsync();
        return task;
    }

    /// <summary>The new joiner introduces themselves; their department (or everyone) sees a welcome announcement.</summary>
    public async Task IntroduceAsync(IReadOnlyDictionary<string, string?> answers)
    {
        var mine = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var onboarding = await db.Onboardings.Include(o => o.Employee).ThenInclude(e => e!.Designation)
                             .Where(o => o.EmployeeId == mine).OrderByDescending(o => o.CreatedAtUtc).FirstOrDefaultAsync()
                         ?? throw new NotFoundException("Onboarding");
        if (onboarding.IntroducedAtUtc is not null)
            throw new DomainException("You have already introduced yourself.");
        var clean = IntroQuestions.ToDictionary(q => q.Key, q => Clean(answers.GetValueOrDefault(q.Key), 1000));
        if (clean.Values.All(v => v is null))
            throw new DomainException("Answer at least one question.");
        onboarding.IntroductionJson = JsonSerializer.Serialize(clean, Json);
        onboarding.IntroducedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();

        var e = onboarding.Employee!;
        var lines = IntroQuestions.Where(q => clean[q.Key] is not null).Select(q => $"{q.Question}: {clean[q.Key]}");
        var role = e.Designation is { } d ? $" joins us as {d.Title}" : " joins the team";
        await announcements.SaveAnnouncementAsync(null, new AnnouncementInput(
            $"Welcome {e.FullName}!", $"{e.FirstName}{role}.\n\n{string.Join("\n", lines)}", AnnouncementKind.News, await clock.TodayAsync(),
            (await clock.TodayAsync()).AddDays(14), e.DepartmentId is { } dep ? [dep] : [], false, null, true));
    }

    public async Task CancelAsync(Guid id)
    {
        var o = await db.Onboardings.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Onboarding");
        if (o.Status is not (OnboardingStatus.InProgress or OnboardingStatus.PendingApproval))
            throw new DomainException("This onboarding is not in progress.");
        if (o.Status == OnboardingStatus.PendingApproval)
            await Approvals.CancelAsync(ApprovalRequestType.Onboarding, o.Id);
        o.Status = OnboardingStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    private Task<string?> MyNameAsync() =>
        me.UserId is { } uid ? db.Users.IgnoreQueryFilters().Where(u => u.Id == uid).Select(u => (string?)u.FullName).FirstOrDefaultAsync() : Task.FromResult<string?>("Qaventra");

    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();
}
