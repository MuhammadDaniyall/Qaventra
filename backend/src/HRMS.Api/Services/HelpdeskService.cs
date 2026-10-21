using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record NewTicket(Guid? ForEmployeeId, Guid CategoryId, string Subject, string Description, TicketPriority Priority);

/// <summary>
/// Help desk tickets. The requester (and whoever raised it for them) and involved colleagues follow a ticket;
/// the category's agents and HR handle it.
/// </summary>
public class HelpdeskService(HrmsDbContext db, ICurrentUser me, TenantClock clock, NotificationService notify)
{
    /// <summary>Resolved tickets the employee has not closed or reopened are closed after this many days.</summary>
    public const int AutoCloseDays = 3;

    public static string Code(int number) => $"HD-{number:D4}";

    public static bool Has(string ids, Guid? id) => id is { } x && ids.Contains(x.ToString(), StringComparison.OrdinalIgnoreCase);

    public static List<Guid> Ids(string ids) => ids.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    public bool IsAgent(Ticket t, TicketCategory c) => me.IsHr() || Has(c.AgentIds, me.EmployeeId) || (me.EmployeeId is { } m && t.AssigneeEmployeeId == m);

    public bool IsRequester(Ticket t) => me.EmployeeId is { } m && (t.EmployeeId == m || t.RaisedByEmployeeId == m);

    public bool CanView(Ticket t, TicketCategory c) => IsAgent(t, c) || IsRequester(t) || Has(t.InvolvedIds, me.EmployeeId);

    /// <summary>Is the signed-in user an agent of any category (or HR)?</summary>
    public async Task<bool> IsAnyAgentAsync()
    {
        if (me.IsHr())
            return true;
        if (me.EmployeeId is not { } mine)
            return false;
        var text = mine.ToString();
        return await db.TicketCategories.AnyAsync(c => c.AgentIds.Contains(text))
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
            Subject = input.Subject.Trim(),
            Description = input.Description.Trim(),
            Priority = input.Priority,
            DueAtUtc = now.AddHours(category.TargetHours),
            LastActivityAtUtc = now,
        };
        db.Tickets.Add(ticket);
        var title = $"New {category.Name} ticket {Code(ticket.Number)}: {ticket.Subject}";
        var agents = Ids(category.AgentIds);
        if (agents.Count > 0)
            await notify.ToEmployeesAsync(agents, NotificationKind.Ticket, title, $"{ticket.Priority} priority", $"/helpdesk/{ticket.Id}");
        else
            await notify.ToHrAsync(NotificationKind.Ticket, title, $"{ticket.Priority} priority", $"/helpdesk/{ticket.Id}");
        if (forId != mine)
            await notify.ToEmployeesAsync([forId], NotificationKind.Ticket, $"HR opened ticket {Code(ticket.Number)} for you", ticket.Subject, $"/helpdesk/{ticket.Id}");
        await db.SaveChangesAsync();
        return ticket;
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
            var to = t.AssigneeEmployeeId is { } a ? [a] : Ids(c.AgentIds);
            if (to.Count > 0)
                await notify.ToEmployeesAsync(to, NotificationKind.Ticket, $"New reply on {Code(t.Number)}: {t.Subject}", Trim(body), link);
            else
                await notify.ToHrAsync(NotificationKind.Ticket, $"New reply on {Code(t.Number)}: {t.Subject}", Trim(body), link);
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Agents: who handles it (null to unassign), its priority and category.</summary>
    public async Task UpdateAsync(Guid id, Guid? assigneeId, TicketPriority priority, Guid categoryId)
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
            await AddCommentAsync(t, $"Moved to {category.Name}", false, system: true);
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
                if (!Has(category.AgentIds, a) && !await hrPeople.AnyAsync(e => e == a))
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

    /// <summary>Agents move a ticket along: in progress, waiting on the employee, or resolved.</summary>
    public async Task SetStatusAsync(Guid id, TicketStatus status, string? note)
    {
        var (t, c) = await LoadAsync(id);
        RequireAgent(t, c);
        if (status is not (TicketStatus.InProgress or TicketStatus.WaitingOnEmployee or TicketStatus.Resolved))
            throw new DomainException("Agents can set in progress, waiting on employee or resolved.");
        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw new DomainException("This ticket is closed.");
        if (status == t.Status)
            return;
        t.Status = status;
        t.ResolvedAtUtc = status == TicketStatus.Resolved ? clock.UtcNow : null;
        t.FirstResponseAtUtc ??= clock.UtcNow;
        var label = Label(status);
        await AddCommentAsync(t, string.IsNullOrWhiteSpace(note) ? label : $"{label}: {note.Trim()}", false, system: true);
        await notify.ToEmployeesAsync([t.EmployeeId, t.RaisedByEmployeeId], NotificationKind.Ticket,
            status == TicketStatus.Resolved ? $"{Code(t.Number)} is resolved" : $"{Code(t.Number)}: {label.ToLowerInvariant()}",
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

    public async Task ReopenAsync(Guid id, string reason)
    {
        var (t, c) = await LoadAsync(id);
        if (!IsRequester(t))
            throw new ForbiddenException("Only the person who raised it can reopen this ticket.");
        if (t.Status != TicketStatus.Resolved)
            throw new DomainException("Only a resolved ticket can be reopened.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Say what is still wrong.");
        t.Status = TicketStatus.InProgress;
        t.ResolvedAtUtc = null;
        await AddCommentAsync(t, $"Reopened: {reason.Trim()}", false, system: true);
        var to = t.AssigneeEmployeeId is { } a ? [a] : Ids(c.AgentIds);
        if (to.Count > 0)
            await notify.ToEmployeesAsync(to, NotificationKind.Ticket, $"{Code(t.Number)} was reopened", reason.Trim(), $"/helpdesk/{t.Id}");
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

    /// <summary>Closes resolved tickets nobody answered within <see cref="AutoCloseDays"/> days.</summary>
    public async Task AutoCloseAsync()
    {
        var cutoff = clock.UtcNow.AddDays(-AutoCloseDays);
        var stale = await db.Tickets.Where(t => t.Status == TicketStatus.Resolved && t.ResolvedAtUtc < cutoff).ToListAsync();
        foreach (var t in stale)
        {
            t.Status = TicketStatus.Closed;
            t.ClosedAtUtc = clock.UtcNow;
            db.TicketComments.Add(new TicketComment { TicketId = t.Id, AuthorName = "Qaventra", Body = $"Closed automatically {AutoCloseDays} days after it was resolved", IsSystem = true });
        }
        if (stale.Count > 0)
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
        var t = await db.Tickets.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Ticket");
        if (!CanView(t, t.Category!))
            throw new NotFoundException("Ticket");
        return (t, t.Category!);
    }

    private void RequireAgent(Ticket t, TicketCategory c)
    {
        if (!IsAgent(t, c))
            throw new ForbiddenException($"Only {c.Name} agents and HR can do this.");
    }

    private async Task AddCommentAsync(Ticket t, string body, bool isInternal, bool system)
    {
        db.TicketComments.Add(new TicketComment
        {
            TicketId = t.Id,
            AuthorEmployeeId = me.EmployeeId,
            AuthorName = me.EmployeeId is { } m ? await NameAsync(m) : "HR",
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
