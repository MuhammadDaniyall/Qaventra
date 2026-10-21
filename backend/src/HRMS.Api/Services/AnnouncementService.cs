using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record AnnouncementInput(
    string Title, string Body, AnnouncementKind Kind, DateOnly PublishFrom, DateOnly? PublishTo, IReadOnlyCollection<Guid>? DepartmentIds,
    bool IsPinned, string? LinkUrl, bool Notify);

public record PollInput(
    string Question, string? Description, DateOnly OpensOn, DateOnly ClosesOn, IReadOnlyCollection<Guid>? DepartmentIds, bool IsAnonymous,
    PollResultsVisibility ResultsVisibility, IReadOnlyList<string> Options);

/// <summary>Announcements and polls for everyone or chosen departments.</summary>
public class AnnouncementService(HrmsDbContext db, ICurrentUser me, TenantClock clock, NotificationService notify)
{
    /// <summary>True when the audience (comma-separated department ids) is everyone or includes the department.</summary>
    public static bool InAudience(string departmentIds, Guid? departmentId) =>
        string.IsNullOrEmpty(departmentIds) || (departmentId is { } d && departmentIds.Contains(d.ToString(), StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<Guid> Departments(string departmentIds) =>
        departmentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    public async Task<Guid?> MyDepartmentAsync() =>
        me.EmployeeId is { } mine ? await db.Employees.Where(e => e.Id == mine).Select(e => e.DepartmentId).FirstOrDefaultAsync() : null;

    /// <summary>Current employees the audience reaches.</summary>
    public async Task<List<Guid>> AudienceAsync(string departmentIds)
    {
        var q = db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
        if (!string.IsNullOrEmpty(departmentIds))
        {
            var depts = Departments(departmentIds);
            q = q.Where(e => e.DepartmentId != null && depts.Contains(e.DepartmentId.Value));
        }
        return await q.Select(e => e.Id).ToListAsync();
    }

    // ---------- Announcements ----------

    public async Task<Announcement> SaveAnnouncementAsync(Guid? id, AnnouncementInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Body))
            throw new DomainException("An announcement needs a title and a message.");
        if (input.PublishTo is { } to && to < input.PublishFrom)
            throw new DomainException("The end date is before the start date.");
        if (input.LinkUrl is { Length: > 0 } url && !(Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http")))
            throw new DomainException("The link must start with https://.");
        var audience = await AudienceTextAsync(input.DepartmentIds);

        Announcement a;
        if (id is { } existing)
            a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == existing) ?? throw new NotFoundException("Announcement");
        else
        {
            a = new Announcement { CreatedByName = await MyNameAsync() };
            db.Announcements.Add(a);
        }
        a.Title = input.Title.Trim();
        a.Body = input.Body.Trim();
        a.Kind = input.Kind;
        a.PublishFrom = input.PublishFrom;
        a.PublishTo = input.PublishTo;
        a.DepartmentIds = audience;
        a.IsPinned = input.IsPinned;
        a.LinkUrl = string.IsNullOrWhiteSpace(input.LinkUrl) ? null : input.LinkUrl.Trim();
        if (!input.Notify)
            a.NotifiedAtUtc ??= clock.UtcNow;
        await NotifyIfLiveAsync(a);
        await db.SaveChangesAsync();
        return a;
    }

    /// <summary>
    /// Tells the audience about an announcement once it is live. Future ones are picked up by the first feed
    /// request on or after their start date.
    /// </summary>
    public async Task NotifyIfLiveAsync(Announcement a)
    {
        if (a.NotifiedAtUtc is not null || a.PublishFrom > await clock.TodayAsync())
            return;
        a.NotifiedAtUtc = clock.UtcNow;
        var label = a.Kind switch { AnnouncementKind.Policy => "New policy", AnnouncementKind.Event => "Event", _ => "Announcement" };
        var body = a.Body.Length > 200 ? a.Body[..200] + "…" : a.Body;
        await notify.ToEmployeesAsync(await AudienceAsync(a.DepartmentIds), NotificationKind.Announcement, $"{label}: {a.Title}", body, "/announcements");
    }

    /// <summary>Live announcements for the signed-in user, pinned first. Also sends any due notifications.</summary>
    public async Task<List<Announcement>> FeedAsync()
    {
        var today = await clock.TodayAsync();
        var live = await db.Announcements.Where(a => a.PublishFrom <= today && (a.PublishTo == null || a.PublishTo >= today))
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishFrom).ThenByDescending(a => a.CreatedAtUtc)
            .Take(200).ToListAsync();
        var due = live.Where(a => a.NotifiedAtUtc is null).ToList();
        foreach (var a in due)
            await NotifyIfLiveAsync(a);
        if (due.Count > 0)
            await db.SaveChangesAsync();
        if (me.IsHr())
            return live;
        var dept = await MyDepartmentAsync();
        return live.Where(a => InAudience(a.DepartmentIds, dept)).ToList();
    }

    public async Task MarkReadAsync(Guid id)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        if (!await db.Announcements.AnyAsync(a => a.Id == id))
            throw new NotFoundException("Announcement");
        if (await db.AnnouncementReads.AnyAsync(r => r.AnnouncementId == id && r.EmployeeId == employeeId))
            return;
        db.AnnouncementReads.Add(new AnnouncementRead { AnnouncementId = id, EmployeeId = employeeId, ReadAtUtc = clock.UtcNow });
        await db.SaveChangesAsync();
    }

    // ---------- Polls ----------

    public async Task<Poll> CreatePollAsync(PollInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Question))
            throw new DomainException("Write the question.");
        var options = (input.Options ?? []).Select(o => o?.Trim() ?? "").Where(o => o.Length > 0).ToList();
        if (options.Count is < 2 or > 6)
            throw new DomainException("Give between 2 and 6 answers.");
        if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
            throw new DomainException("Each answer must be different.");
        if (options.Any(o => o.Length > 200))
            throw new DomainException("Keep each answer under 200 characters.");
        if (input.ClosesOn < input.OpensOn)
            throw new DomainException("The poll must close on or after the day it opens.");
        if (input.ClosesOn < await clock.TodayAsync())
            throw new DomainException("The closing date has already passed.");

        var poll = new Poll
        {
            Question = input.Question.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            OpensOn = input.OpensOn,
            ClosesOn = input.ClosesOn,
            DepartmentIds = await AudienceTextAsync(input.DepartmentIds),
            IsAnonymous = input.IsAnonymous,
            ResultsVisibility = input.ResultsVisibility,
            CreatedByName = await MyNameAsync(),
            Options = options.Select((t, i) => new PollOption { Text = t, Order = i + 1 }).ToList(),
        };
        db.Polls.Add(poll);
        await notify.ToEmployeesAsync(await AudienceAsync(poll.DepartmentIds), NotificationKind.Poll, $"New poll: {poll.Question}",
            $"Open {poll.OpensOn:d MMM} – {poll.ClosesOn:d MMM}.", "/announcements?tab=polls");
        await db.SaveChangesAsync();
        return poll;
    }

    /// <summary>One vote per person; it can be changed while the poll is open.</summary>
    public async Task VoteAsync(Guid pollId, Guid optionId)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var poll = await db.Polls.Include(p => p.Options).FirstOrDefaultAsync(p => p.Id == pollId) ?? throw new NotFoundException("Poll");
        if (!me.IsHr() && !InAudience(poll.DepartmentIds, await MyDepartmentAsync()))
            throw new NotFoundException("Poll");
        var today = await clock.TodayAsync();
        if (today < poll.OpensOn)
            throw new DomainException($"This poll opens on {poll.OpensOn:d MMM}.");
        if (today > poll.ClosesOn)
            throw new DomainException("This poll has closed.");
        if (poll.Options.All(o => o.Id != optionId))
            throw new DomainException("Choose one of the answers.");
        var vote = await db.PollVotes.FirstOrDefaultAsync(v => v.PollId == pollId && v.EmployeeId == employeeId);
        if (vote is null)
            db.PollVotes.Add(new PollVote { PollId = pollId, OptionId = optionId, EmployeeId = employeeId });
        else
            vote.OptionId = optionId;
        await db.SaveChangesAsync();
    }

    private async Task<string> AudienceTextAsync(IReadOnlyCollection<Guid>? departmentIds)
    {
        var ids = (departmentIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
            return "";
        if (await db.Departments.CountAsync(d => ids.Contains(d.Id)) != ids.Count)
            throw new NotFoundException("Department");
        return string.Join(',', ids);
    }

    private async Task<string?> MyNameAsync() =>
        me.UserId is { } uid ? await db.Users.IgnoreQueryFilters().Where(u => u.Id == uid).Select(u => u.FullName).FirstOrDefaultAsync() : null;
}
