using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record NewTicket(Guid? ForEmployeeId, Guid CategoryId, Guid? TypeId, string Subject, string Description, TicketPriority Priority);

/// <summary>
/// Help desk tickets. The requester (and whoever raised it for them) and involved colleagues follow a ticket;
/// the agents of its type (or else its category) and HR handle it.
/// </summary>
public class HelpdeskService(HrmsDbContext db, ICurrentUser me, TenantClock clock, NotificationService notify)
{
    /// <summary>Authors can change or remove their own reply for this long.</summary>
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(30);

    /// <summary>How long before the target the "due soon" reminder goes out.</summary>
    public static readonly TimeSpan DueSoon = TimeSpan.FromHours(1);

    public static string Code(int number) => $"HD-{number:D4}";

    public static bool Has(string ids, Guid? id) => id is { } x && ids.Contains(x.ToString(), StringComparison.OrdinalIgnoreCase);

    public static List<Guid> Ids(string ids) => ids.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    /// <summary>The agents of a ticket: its type's when the type names any, otherwise its category's.</summary>
    public static string AgentIdsOf(TicketCategory c, TicketType? type) => type is { AgentIds.Length: > 0 } ? type.AgentIds : c.AgentIds;

    public bool IsAgent(Ticket t, TicketCategory c) =>
        me.IsHr() || Has(c.AgentIds, me.EmployeeId) || Has(t.Type?.AgentIds ?? "", me.EmployeeId)
        || (me.EmployeeId is { } m && (t.AssigneeEmployeeId == m || c.LeadEmployeeId == m));

    public bool IsRequester(Ticket t) => me.EmployeeId is { } m && (t.EmployeeId == m || t.RaisedByEmployeeId == m);

    public bool CanView(Ticket t, TicketCategory c) => IsAgent(t, c) || IsRequester(t) || Has(t.InvolvedIds, me.EmployeeId);

    /// <summary>Is the signed-in user an agent or lead anywhere (or HR)?</summary>
    public async Task<bool> IsAnyAgentAsync()
    {
        if (me.IsHr())
            return true;
        if (me.EmployeeId is not { } mine)
            return false;
        var text = mine.ToString();
        return await db.TicketCategories.AnyAsync(c => c.AgentIds.Contains(text) || c.LeadEmployeeId == mine)
               || await db.TicketTypes.AnyAsync(t => t.AgentIds.Contains(text))
               || await db.Tickets.AnyAsync(t => t.AssigneeEmployeeId == mine);
    }

    public async Task<Ticket> CreateAsync(NewTicket input)
    {
        var mine = MyId();
        var forId = input.ForEmployeeId ?? mine;
        if (forId != mine && !me.IsHr())
            throw new ForbiddenException("You can only raise tickets for yourself.");
        if (!await db.Employees.AnyAsync(e => e.Id == forId && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new NotFoundException("Employee");
        var category = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == input.CategoryId && c.IsActive) ?? throw new NotFoundException("Category");
        TicketType? type = null;
        if (input.TypeId is { } typeId)
            type = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == typeId && t.CategoryId == category.Id && t.IsActive) ?? throw new NotFoundException("Ticket type");
        else if (await db.TicketTypes.AnyAsync(t => t.CategoryId == category.Id && t.IsActive))
            throw new DomainException($"Choose what kind of {category.Name} ticket it is.");
        if (string.IsNullOrWhiteSpace(input.Subject) || string.IsNullOrWhiteSpace(input.Description))
            throw new DomainException("Add a subject and describe the problem.");
        if (!Enum.IsDefined(input.Priority))
            throw new DomainException("Choose a priority.");

        var now = clock.UtcNow;
        var ticket = new Ticket
        {
            Number = (await db.Tickets.MaxAsync(t => (int?)t.Number) ?? 0) + 1,
            EmployeeId = forId,
            RaisedByEmployeeId = mine,
            CategoryId = category.Id,
            TypeId = type?.Id,
            Type = type,
            Subject = input.Subject.Trim(),
            Description = input.Description.Trim(),
            Priority = input.Priority,
            DueAtUtc = now.AddHours(type?.TargetHours ?? category.TargetHours),
            LastActivityAtUtc = now,
        };
        db.Tickets.Add(ticket);

        var link = $"/helpdesk/{ticket.Id}";
        var what = type is null ? category.Name : $"{category.Name} · {type.Name}";
        var assignee = await AutoAssigneeAsync(category, type);
        if (assignee is { } a)
        {
            ticket.AssigneeEmployeeId = a;
            await AddCommentAsync(ticket, $"Assigned to {await NameAsync(a)} automatically", false, system: true, byQaventra: true);
            await notify.ToEmployeesAsync([a], NotificationKind.Ticket, $"New {what} ticket {Code(ticket.Number)} assigned to you", ticket.Subject, link);
        }
        else
        {
            var title = $"New {what} ticket {Code(ticket.Number)}: {ticket.Subject}";
            var agents = Ids(AgentIdsOf(category, type));
            if (agents.Count > 0)
                await notify.ToEmployeesAsync(agents, NotificationKind.Ticket, title, $"{ticket.Priority} priority", link);
            else
                await notify.ToHrAsync(NotificationKind.Ticket, title, $"{ticket.Priority} priority", link);
        }
        if (forId != mine)
            await notify.ToEmployeesAsync([forId], NotificationKind.Ticket, $"HR opened ticket {Code(ticket.Number)} for you", ticket.Subject, link);
        await db.SaveChangesAsync();
        return ticket;
    }

    /// <summary>Round robin: the agent after whoever got the category's last auto-assigned ticket. Least busy: fewest open tickets.</summary>
    private async Task<Guid?> AutoAssigneeAsync(TicketCategory category, TicketType? type)
    {
        if (category.AssignmentMode == TicketAssignment.Manual)
            return null;
        var agents = Ids(AgentIdsOf(category, type));
        var current = await db.Employees.Where(e => agents.Contains(e.Id) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation))
            .Select(e => e.Id).ToListAsync();
        // Keep the configured order so round robin is predictable.
        var candidates = agents.Where(current.Contains).ToList();
        if (candidates.Count == 0)
            return null;
        if (category.AssignmentMode == TicketAssignment.LeastBusy)
        {
            var load = await db.Tickets.Where(t => t.AssigneeEmployeeId != null && candidates.Contains(t.AssigneeEmployeeId.Value)
                                                   && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingOnEmployee))
                .GroupBy(t => t.AssigneeEmployeeId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            return candidates.OrderBy(c => load.GetValueOrDefault(c)).First();
        }
        var last = await db.Tickets.Where(t => t.CategoryId == category.Id && t.AssigneeEmployeeId != null && candidates.Contains(t.AssigneeEmployeeId.Value))
            .OrderByDescending(t => t.Number).Select(t => t.AssigneeEmployeeId).FirstOrDefaultAsync();
        var i = last is { } l ? candidates.IndexOf(l) : -1;
        return candidates[(i + 1) % candidates.Count];
    }

    public async Task CommentAsync(Guid id, string body, bool isInternal)
    {
        var (t, c) = await LoadAsync(id);
        if (string.IsNullOrWhiteSpace(body))
            throw new DomainException("Write a reply.");
        var agent = IsAgent(t, c);
        if (isInternal && !agent)
            throw new ForbiddenException("Only agents can add internal notes.");
        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw new DomainException("This ticket is closed. Raise a new one if you still need help.");

        await AddCommentAsync(t, body.Trim(), isInternal, system: false);
        var link = $"/helpdesk/{t.Id}";
        if (isInternal)
        {
            // Internal notes reach the other agents only.
            if (t.AssigneeEmployeeId is { } a && a != me.EmployeeId)
                await notify.ToEmployeesAsync([a], NotificationKind.Ticket, $"Note on {Code(t.Number)}", Trim(body), link);
        }
        else if (agent && !IsRequester(t))
        {
            t.FirstResponseAtUtc ??= clock.UtcNow;
            if (t.Status == TicketStatus.Open)
                t.Status = TicketStatus.InProgress;
            await notify.ToEmployeesAsync([t.EmployeeId, t.RaisedByEmployeeId], NotificationKind.Ticket, $"Reply on {Code(t.Number)}: {t.Subject}", Trim(body), link);
        }
        else
        {
            // The requester (or someone involved) answered: back to the agents.
            if (t.Status is TicketStatus.WaitingOnEmployee or TicketStatus.Resolved && IsRequester(t))
            {
                if (t.Status == TicketStatus.Resolved)
                    await AddCommentAsync(t, "Reopened by the reply", false, system: true);
                t.Status = TicketStatus.InProgress;
                t.ResolvedAtUtc = null;
            }
            await NotifyHandlersAsync(t, c, $"New reply on {Code(t.Number)}: {t.Subject}", Trim(body));
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Authors change their own reply or note shortly after writing it.</summary>
    public async Task EditCommentAsync(Guid id, Guid commentId, string? body, bool delete)
    {
        var (t, _) = await LoadAsync(id);
        var comment = await db.TicketComments.FirstOrDefaultAsync(x => x.Id == commentId && x.TicketId == id) ?? throw new NotFoundException("Reply");
        if (comment.IsSystem || comment.IsDeleted || comment.AuthorEmployeeId != me.EmployeeId)
            throw new ForbiddenException("You can only change your own replies.");
        if (clock.UtcNow - comment.CreatedAtUtc > EditWindow)
            throw new DomainException($"Replies can be changed for {EditWindow.TotalMinutes:0} minutes after sending.");
        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw new DomainException("This ticket is closed.");
        if (delete)
        {
            comment.IsDeleted = true;
            comment.Body = "";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(body))
                throw new DomainException("Write a reply.");
            comment.Body = body.Trim();
            comment.EditedAtUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Agents: who handles it (null to unassign), its priority, category and type.</summary>
    public async Task UpdateAsync(Guid id, Guid? assigneeId, TicketPriority priority, Guid categoryId, Guid? typeId)
    {
        var (t, c) = await LoadAsync(id);
        RequireAgent(t, c);
        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw new DomainException("This ticket is closed.");
        if (!Enum.IsDefined(priority))
            throw new DomainException("Choose a priority.");
        var category = c;
        if (categoryId != t.CategoryId)
        {
            category = await db.TicketCategories.FirstOrDefaultAsync(x => x.Id == categoryId && x.IsActive) ?? throw new NotFoundException("Category");
            t.CategoryId = category.Id;
            t.Category = category;
            await AddCommentAsync(t, $"Moved to {category.Name}", false, system: true);
        }
        if (typeId != t.TypeId)
        {
            var type = typeId is { } tid
                ? await db.TicketTypes.FirstOrDefaultAsync(x => x.Id == tid && x.CategoryId == category.Id) ?? throw new NotFoundException("Ticket type")
                : null;
            t.TypeId = type?.Id;
            t.Type = type;
            if (type is not null)
                await AddCommentAsync(t, $"Type set to {type.Name}", false, system: true);
        }
        else if (t.Type is { } current && current.CategoryId != category.Id)
        {
            t.TypeId = null;
            t.Type = null;
        }
        if (priority != t.Priority)
        {
            await AddCommentAsync(t, $"Priority changed from {t.Priority} to {priority}", false, system: true);
            t.Priority = priority;
        }
        if (assigneeId != t.AssigneeEmployeeId)
        {
            if (assigneeId is { } a)
            {
                var hrPeople = db.Users.Where(u => u.Role == UserRole.HrManager || u.Role == UserRole.CompanyAdmin).Select(u => u.EmployeeId);
                if (!Has(AgentIdsOf(category, t.Type), a) && !Has(category.AgentIds, a) && category.LeadEmployeeId != a && !await hrPeople.AnyAsync(e => e == a))
                    throw new DomainException($"Assign it to one of the {category.Name} agents or someone in HR.");
                var name = await NameAsync(a);
                await AddCommentAsync(t, a == me.EmployeeId ? $"{name} took the ticket" : $"Assigned to {name}", false, system: true);
                if (a != me.EmployeeId)
                    await notify.ToEmployeesAsync([a], NotificationKind.Ticket, $"Ticket {Code(t.Number)} assigned to you", t.Subject, $"/helpdesk/{t.Id}");
            }
            else
                await AddCommentAsync(t, "Unassigned", false, system: true);
            t.AssigneeEmployeeId = assigneeId;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Agents move a ticket along: in progress, waiting on the employee, resolved, or closed where the category allows it.</summary>
    public async Task SetStatusAsync(Guid id, TicketStatus status, string? note)
    {
        var (t, c) = await LoadAsync(id);
        RequireAgent(t, c);
        if (status == TicketStatus.Closed && !c.AgentCanClose)
            throw new DomainException($"In {c.Name}, the employee closes the ticket after you resolve it.");
        if (status is not (TicketStatus.InProgress or TicketStatus.WaitingOnEmployee or TicketStatus.Resolved or TicketStatus.Closed))
            throw new DomainException("Agents can set in progress, waiting on employee, resolved or closed.");
        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw new DomainException("This ticket is closed.");
        if (status == t.Status)
            return;
        var now = clock.UtcNow;
        t.Status = status;
        t.FirstResponseAtUtc ??= now;
        if (status is TicketStatus.Resolved or TicketStatus.Closed)
            t.ResolvedAtUtc ??= now;
        else
            t.ResolvedAtUtc = null;
        if (status == TicketStatus.Closed)
            t.ClosedAtUtc = now;
        var label = Label(status);
        await AddCommentAsync(t, string.IsNullOrWhiteSpace(note) ? label : $"{label}: {note.Trim()}", false, system: true);
        await notify.ToEmployeesAsync([t.EmployeeId, t.RaisedByEmployeeId], NotificationKind.Ticket,
            status is TicketStatus.Resolved or TicketStatus.Closed ? $"{Code(t.Number)} is {label.ToLowerInvariant()}" : $"{Code(t.Number)}: {label.ToLowerInvariant()}",
            string.IsNullOrWhiteSpace(note) ? t.Subject : Trim(note), $"/helpdesk/{t.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>Agents pull colleagues in; they can then read and reply.</summary>
    public async Task InvolveAsync(Guid id, IReadOnlyCollection<Guid> employeeIds)
    {
        var (t, c) = await LoadAsync(id);
        RequireAgent(t, c);
        var ids = employeeIds.Distinct().Where(e => !Has(t.InvolvedIds, e)).ToList();
        if (ids.Count == 0)
            return;
        var names = await db.Employees.Where(e => ids.Contains(e.Id) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName }).ToListAsync();
        if (names.Count != ids.Count)
            throw new NotFoundException("Employee");
        t.InvolvedIds = string.Join(',', Ids(t.InvolvedIds).Concat(ids));
        await AddCommentAsync(t, $"Involved {string.Join(", ", names.Select(n => n.Name))}", false, system: true);
        await notify.ToEmployeesAsync(ids, NotificationKind.Ticket, $"You were involved in {Code(t.Number)}", t.Subject, $"/helpdesk/{t.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>The requester confirms the fix and rates the help.</summary>
    public async Task CloseAsync(Guid id, int? rating, string? feedback)
    {
        var (t, _) = await LoadAsync(id);
        if (!IsRequester(t))
            throw new ForbiddenException("Only the person who raised it can close this ticket.");
        if (t.Status != TicketStatus.Resolved)
            throw new DomainException("A ticket can be closed once it is resolved.");
        if (rating is { } r && r is < 1 or > 5)
            throw new DomainException("Rate from 1 to 5.");
        t.Status = TicketStatus.Closed;
        t.ClosedAtUtc = clock.UtcNow;
        t.Rating = rating;
        t.Feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        await AddCommentAsync(t, rating is { } x ? $"Closed with a {x}★ rating" : "Closed", false, system: true);
        if (t.AssigneeEmployeeId is { } a)
            await notify.ToEmployeesAsync([a], NotificationKind.Ticket, $"{Code(t.Number)} closed" + (rating is { } s ? $" · {s}★" : ""), t.Feedback, $"/helpdesk/{t.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>A resolved ticket can always be reopened; a closed one within the category's reopen window.</summary>
    public async Task ReopenAsync(Guid id, string reason)
    {
        var (t, c) = await LoadAsync(id);
        if (!IsRequester(t))
            throw new ForbiddenException("Only the person who raised it can reopen this ticket.");
        var closedRecently = t.Status == TicketStatus.Closed && c.ReopenDays > 0 && t.ClosedAtUtc is { } closed
                             && clock.UtcNow - closed <= TimeSpan.FromDays(c.ReopenDays);
        if (t.Status != TicketStatus.Resolved && !closedRecently)
            throw new DomainException(t.Status == TicketStatus.Closed
                ? c.ReopenDays > 0 ? $"Closed tickets can be reopened for {c.ReopenDays} days. Raise a new one." : "Closed tickets cannot be reopened. Raise a new one."
                : "Only a resolved or recently closed ticket can be reopened.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Say what is still wrong.");
        t.Status = TicketStatus.InProgress;
        t.ResolvedAtUtc = null;
        t.ClosedAtUtc = null;
        t.Rating = null;
        t.Feedback = null;
        await AddCommentAsync(t, $"Reopened: {reason.Trim()}", false, system: true);
        await NotifyHandlersAsync(t, c, $"{Code(t.Number)} was reopened", reason.Trim());
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid id)
    {
        var (t, _) = await LoadAsync(id);
        if (!IsRequester(t) && !me.IsHr())
            throw new ForbiddenException("Only the person who raised it can cancel this ticket.");
        if (t.Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled)
            throw new DomainException("This ticket can no longer be cancelled.");
        t.Status = TicketStatus.Cancelled;
        t.ClosedAtUtc = clock.UtcNow;
        await AddCommentAsync(t, "Cancelled", false, system: true);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Time-based work for the current company: close resolved tickets past their category's auto-close days, remind
    /// assignees shortly before the target, and escalate overdue tickets to the assignee and the category lead (or HR).
    /// Runs in the background and on queue requests, so it also catches up after the site has been idle.
    /// </summary>
    public async Task RunDueWorkAsync()
    {
        var now = clock.UtcNow;
        var resolved = await db.Tickets.Include(t => t.Category).Where(t => t.Status == TicketStatus.Resolved && t.ResolvedAtUtc != null).ToListAsync();
        foreach (var t in resolved.Where(t => t.Category!.AutoCloseDays > 0 && now - t.ResolvedAtUtc!.Value > TimeSpan.FromDays(t.Category.AutoCloseDays)))
        {
            t.Status = TicketStatus.Closed;
            t.ClosedAtUtc = now;
            db.TicketComments.Add(new TicketComment
            {
                TicketId = t.Id, AuthorName = "Qaventra", IsSystem = true,
                Body = $"Closed automatically {t.Category!.AutoCloseDays} day{(t.Category.AutoCloseDays == 1 ? "" : "s")} after it was resolved",
            });
        }

        var active = await db.Tickets.Include(t => t.Category).Include(t => t.Type)
            .Where(t => (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingOnEmployee)
                        && ((t.DueSoonRemindedAtUtc == null && t.DueAtUtc <= now + DueSoon) || (t.EscalatedAtUtc == null && t.DueAtUtc < now)))
            .ToListAsync();
        foreach (var t in active)
        {
            var link = $"/helpdesk/{t.Id}";
            var handlers = t.AssigneeEmployeeId is { } a ? [a] : Ids(AgentIdsOf(t.Category!, t.Type));
            if (t.DueAtUtc < now && t.EscalatedAtUtc is null)
            {
                t.EscalatedAtUtc = now;
                t.DueSoonRemindedAtUtc ??= now;
                var title = $"Overdue: {Code(t.Number)} {t.Subject}";
                var body = $"{t.Category!.Name} · {t.Priority} priority · " + (t.AssigneeEmployeeId is null ? "nobody has taken it" : "still not resolved");
                var to = handlers.Concat(t.Category.LeadEmployeeId is { } lead ? [lead] : []).Distinct().ToList();
                if (to.Count > 0)
                    await notify.ToEmployeesAsync(to, NotificationKind.Ticket, title, body, link, includeMe: true);
                if (t.Category.LeadEmployeeId is null)
                    await notify.ToHrAsync(NotificationKind.Ticket, title, body, link, includeMe: true);
                db.TicketComments.Add(new TicketComment { TicketId = t.Id, AuthorName = "Qaventra", IsSystem = true, Body = "Overdue: escalated" });
            }
            else if (t.DueSoonRemindedAtUtc is null)
            {
                t.DueSoonRemindedAtUtc = now;
                var title = $"Due soon: {Code(t.Number)} {t.Subject}";
                if (handlers.Count > 0)
                    await notify.ToEmployeesAsync(handlers, NotificationKind.Ticket, title, $"Due {t.DueAtUtc:HH:mm} UTC", link, includeMe: true);
                else
                    await notify.ToHrAsync(NotificationKind.Ticket, title, $"Due {t.DueAtUtc:HH:mm} UTC", link, includeMe: true);
            }
        }
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
    }

    public static string Label(TicketStatus s) => s switch
    {
        TicketStatus.InProgress => "In progress",
        TicketStatus.WaitingOnEmployee => "Waiting on employee",
        _ => s.ToString(),
    };

    public async Task<(Ticket Ticket, TicketCategory Category)> LoadAsync(Guid id)
    {
        var t = await db.Tickets.Include(x => x.Category).Include(x => x.Type).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Ticket");
        if (!CanView(t, t.Category!))
            throw new NotFoundException("Ticket");
        return (t, t.Category!);
    }

    private async Task NotifyHandlersAsync(Ticket t, TicketCategory c, string title, string body)
    {
        var to = t.AssigneeEmployeeId is { } a ? [a] : Ids(AgentIdsOf(c, t.Type));
        if (to.Count > 0)
            await notify.ToEmployeesAsync(to, NotificationKind.Ticket, title, body, $"/helpdesk/{t.Id}");
        else
            await notify.ToHrAsync(NotificationKind.Ticket, title, body, $"/helpdesk/{t.Id}");
    }

    private void RequireAgent(Ticket t, TicketCategory c)
    {
        if (!IsAgent(t, c))
            throw new ForbiddenException($"Only {c.Name} agents and HR can do this.");
    }

    /// <param name="byQaventra">The system did it (e.g. automatic assignment), not the signed-in user.</param>
    private async Task AddCommentAsync(Ticket t, string body, bool isInternal, bool system, bool byQaventra = false)
    {
        db.TicketComments.Add(new TicketComment
        {
            TicketId = t.Id,
            AuthorEmployeeId = byQaventra ? null : me.EmployeeId,
            AuthorName = byQaventra ? "Qaventra" : me.EmployeeId is { } m ? await NameAsync(m) : "HR",
            Body = body.Length > 8000 ? body[..8000] : body,
            IsInternal = isInternal,
            IsSystem = system,
        });
        t.LastActivityAtUtc = clock.UtcNow;
    }

    private Task<string> NameAsync(Guid employeeId) =>
        db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();

    private Guid MyId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");

    private static string Trim(string s) => s.Length > 200 ? s[..200] + "…" : s;
}

/// <summary>Every 10 minutes, runs <see cref="HelpdeskService.RunDueWorkAsync"/> for each company with open tickets.</summary>
public class HelpdeskReminderService(IServiceProvider services, IHostEnvironment env, ILogger<HelpdeskReminderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (env.IsEnvironment("Testing"))
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try
            {
                List<Guid> tenants;
                using (var scope = services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
                    tenants = await db.Tickets.IgnoreQueryFilters()
                        .Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled)
                        .Select(t => t.TenantId).Distinct().ToListAsync(stoppingToken);
                }
                foreach (var tenant in tenants)
                {
                    using var scope = services.CreateScope();
                    scope.ServiceProvider.GetRequiredService<HrmsDbContext>().UseTenant(tenant);
                    await scope.ServiceProvider.GetRequiredService<HelpdeskService>().RunDueWorkAsync();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Help desk reminders failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
