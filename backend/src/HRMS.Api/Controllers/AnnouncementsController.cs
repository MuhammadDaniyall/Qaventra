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

public record SaveAnnouncementRequest(
    [Required, StringLength(200)] string Title, [Required, StringLength(8000)] string Body, AnnouncementKind Kind, DateOnly PublishFrom,
    DateOnly? PublishTo, List<Guid>? DepartmentIds, bool IsPinned, [StringLength(500)] string? LinkUrl, bool Notify = true);

/// <summary>ReadCount and AudienceCount are filled for HR only.</summary>
public record AnnouncementDto(
    Guid Id, string Title, string Body, AnnouncementKind Kind, DateOnly PublishFrom, DateOnly? PublishTo, List<Guid> DepartmentIds,
    List<string> Departments, bool IsPinned, string? LinkUrl, string? CreatedByName, DateTime CreatedAtUtc, bool IsRead, int Attachments,
    int? ReadCount, int? AudienceCount);

public record CreatePollRequest(
    [Required, StringLength(300)] string Question, [StringLength(2000)] string? Description, DateOnly OpensOn, DateOnly ClosesOn,
    List<Guid>? DepartmentIds, bool IsAnonymous, PollResultsVisibility ResultsVisibility, [Required] List<string> Options);

public record VoteRequest(Guid OptionId);

/// <summary>Votes are null when results are hidden from the viewer; voter names only for HR on polls that are not anonymous.</summary>
public record PollOptionDto(Guid Id, string Text, int? Votes, List<string>? Voters);

public record PollDto(
    Guid Id, string Question, string? Description, DateOnly OpensOn, DateOnly ClosesOn, bool IsOpen, bool IsAnonymous,
    PollResultsVisibility ResultsVisibility, List<string> Departments, string? CreatedByName, List<PollOptionDto> Options,
    Guid? MyOptionId, bool ResultsShown, int? TotalVotes, int? AudienceCount);

[ApiController]
[Route("api/announcements")]
[Authorize(Policy = Policies.CompanyUser)]
public class AnnouncementsController(HrmsDbContext db, AnnouncementService service, TenantClock clock, ICurrentUser me) : ControllerBase
{
    [HttpGet]
    public async Task<List<AnnouncementDto>> Feed() => await ProjectAsync(await service.FeedAsync(), stats: false);

    /// <summary>HR: everything, including scheduled and ended ones, with how many have read each.</summary>
    [HttpGet("manage"), Authorize(Policy = Policies.Hr)]
    public async Task<List<AnnouncementDto>> Manage() =>
        await ProjectAsync(await db.Announcements.OrderByDescending(a => a.PublishFrom).ThenByDescending(a => a.CreatedAtUtc).Take(300).ToListAsync(), stats: true);

    [HttpPost, Authorize(Policy = Policies.Hr)]
    public async Task<AnnouncementDto> Create(SaveAnnouncementRequest req) =>
        (await ProjectAsync([await service.SaveAnnouncementAsync(null, Input(req))], stats: true)).Single();

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<AnnouncementDto> Update(Guid id, SaveAnnouncementRequest req) =>
        (await ProjectAsync([await service.SaveAnnouncementAsync(id, Input(req))], stats: true)).Single();

    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Announcement");
        db.Attachments.RemoveRange(await db.Attachments.Where(x => x.OwnerType == AttachmentOwner.Announcement && x.OwnerId == id).ToListAsync());
        db.Announcements.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id)
    {
        await service.MarkReadAsync(id);
        return NoContent();
    }

    // ---------- Polls ----------

    /// <summary>Polls that are open, or closed in the last 60 days, for the signed-in user.</summary>
    [HttpGet("polls")]
    public async Task<List<PollDto>> Polls()
    {
        var today = await clock.TodayAsync();
        var polls = await db.Polls.Include(p => p.Options).Where(p => p.OpensOn <= today && p.ClosesOn >= today.AddDays(-60))
            .OrderByDescending(p => p.ClosesOn >= today).ThenBy(p => p.ClosesOn).ToListAsync();
        if (!me.IsHr())
        {
            var dept = await service.MyDepartmentAsync();
            polls = polls.Where(p => AnnouncementService.InAudience(p.DepartmentIds, dept)).ToList();
        }
        return await ProjectPollsAsync(polls, manage: false);
    }

    [HttpGet("polls/manage"), Authorize(Policy = Policies.Hr)]
    public async Task<List<PollDto>> ManagePolls() =>
        await ProjectPollsAsync(await db.Polls.Include(p => p.Options).OrderByDescending(p => p.OpensOn).Take(200).ToListAsync(), manage: true);

    [HttpPost("polls"), Authorize(Policy = Policies.Hr)]
    public async Task<PollDto> CreatePoll(CreatePollRequest req)
    {
        var poll = await service.CreatePollAsync(new PollInput(req.Question, req.Description, req.OpensOn, req.ClosesOn, req.DepartmentIds,
            req.IsAnonymous, req.ResultsVisibility, req.Options));
        return (await ProjectPollsAsync([poll], manage: true)).Single();
    }

    [HttpDelete("polls/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeletePoll(Guid id)
    {
        var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Poll");
        db.Polls.Remove(poll);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("polls/{id:guid}/vote")]
    public async Task<PollDto> Vote(Guid id, VoteRequest req)
    {
        await service.VoteAsync(id, req.OptionId);
        var poll = await db.Polls.Include(p => p.Options).FirstAsync(p => p.Id == id);
        return (await ProjectPollsAsync([poll], manage: false)).Single();
    }

    private static AnnouncementInput Input(SaveAnnouncementRequest r) =>
        new(r.Title, r.Body, r.Kind, r.PublishFrom, r.PublishTo, r.DepartmentIds, r.IsPinned, r.LinkUrl, r.Notify);

    private async Task<Dictionary<Guid, string>> DepartmentNamesAsync() => await db.Departments.ToDictionaryAsync(d => d.Id, d => d.Name);

    private async Task<List<AnnouncementDto>> ProjectAsync(List<Announcement> list, bool stats)
    {
        var ids = list.Select(a => a.Id).ToList();
        var names = await DepartmentNamesAsync();
        var mine = me.EmployeeId;
        var read = (await db.AnnouncementReads.Where(r => ids.Contains(r.AnnouncementId) && r.EmployeeId == mine).Select(r => r.AnnouncementId).ToListAsync()).ToHashSet();
        var files = await db.Attachments.Where(x => x.OwnerType == AttachmentOwner.Announcement && ids.Contains(x.OwnerId))
            .GroupBy(x => x.OwnerId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        Dictionary<Guid, int> reads = [];
        Dictionary<Guid?, int> perDept = [];
        var everyone = 0;
        if (stats)
        {
            reads = await db.AnnouncementReads.Where(r => ids.Contains(r.AnnouncementId)).GroupBy(r => r.AnnouncementId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var current = db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
            // People without a department count towards "everyone" only; a null key can't go in the dictionary.
            perDept = (await current.GroupBy(e => e.DepartmentId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => (Guid?)(x.Key ?? Guid.Empty), x => x.Count);
            everyone = perDept.Values.Sum();
        }
        return list.Select(a =>
        {
            var depts = AnnouncementService.Departments(a.DepartmentIds).ToList();
            int? audience = stats ? depts.Count == 0 ? everyone : depts.Sum(d => perDept.GetValueOrDefault(d)) : null;
            return new AnnouncementDto(a.Id, a.Title, a.Body, a.Kind, a.PublishFrom, a.PublishTo, depts,
                depts.Select(d => names.GetValueOrDefault(d) ?? "Removed department").ToList(), a.IsPinned, a.LinkUrl, a.CreatedByName,
                a.CreatedAtUtc, read.Contains(a.Id), files.GetValueOrDefault(a.Id), stats ? reads.GetValueOrDefault(a.Id) : null, audience);
        }).ToList();
    }

    private async Task<List<PollDto>> ProjectPollsAsync(List<Poll> polls, bool manage)
    {
        var today = await clock.TodayAsync();
        var hr = me.IsHr();
        var ids = polls.Select(p => p.Id).ToList();
        var votes = await db.PollVotes.Where(v => ids.Contains(v.PollId)).Select(v => new { v.PollId, v.OptionId, v.EmployeeId }).ToListAsync();
        var names = await DepartmentNamesAsync();
        var voterIds = votes.Select(v => v.EmployeeId).Distinct().ToList();
        var voterNames = hr
            ? await db.Employees.Where(e => voterIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName)
            : [];
        var audienceSizes = new Dictionary<Guid, int>();
        if (manage)
            foreach (var p in polls)
                audienceSizes[p.Id] = (await service.AudienceAsync(p.DepartmentIds)).Count;

        return polls.Select(p =>
        {
            var mine = votes.FirstOrDefault(v => v.PollId == p.Id && v.EmployeeId == me.EmployeeId)?.OptionId;
            var open = today >= p.OpensOn && today <= p.ClosesOn;
            var shown = hr || p.ResultsVisibility switch
            {
                PollResultsVisibility.AfterVoting => mine is not null || !open,
                PollResultsVisibility.AfterClose => !open,
                _ => false,
            };
            var pollVotes = votes.Where(v => v.PollId == p.Id).ToList();
            var options = p.Options.OrderBy(o => o.Order).Select(o => new PollOptionDto(o.Id, o.Text,
                shown ? pollVotes.Count(v => v.OptionId == o.Id) : null,
                hr && !p.IsAnonymous ? pollVotes.Where(v => v.OptionId == o.Id).Select(v => voterNames.GetValueOrDefault(v.EmployeeId) ?? "").OrderBy(n => n).ToList() : null)).ToList();
            return new PollDto(p.Id, p.Question, p.Description, p.OpensOn, p.ClosesOn, open, p.IsAnonymous, p.ResultsVisibility,
                AnnouncementService.Departments(p.DepartmentIds).Select(d => names.GetValueOrDefault(d) ?? "Removed department").ToList(),
                p.CreatedByName, options, mine, shown, shown ? pollVotes.Count : null, manage ? audienceSizes[p.Id] : null);
        }).ToList();
    }
}
