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

public record TicketCategoryDto(
    Guid Id, string Name, string? Description, int TargetHours, List<Guid> AgentIds, List<string> Agents, bool IsActive, int OpenTickets);

public record SaveTicketCategoryRequest(
    [Required, StringLength(100)] string Name, [StringLength(500)] string? Description, [Range(1, 2000)] int TargetHours,
    List<Guid>? AgentIds, bool IsActive = true);

public record CreateTicketRequest(
    Guid? ForEmployeeId, Guid CategoryId, [Required, StringLength(200)] string Subject, [Required, StringLength(8000)] string Description,
    TicketPriority Priority = TicketPriority.Medium);

public record TicketCommentRequest([Required, StringLength(8000)] string Body, bool IsInternal = false);

public record UpdateTicketRequest(Guid? AssigneeEmployeeId, TicketPriority Priority, Guid CategoryId);

public record TicketStatusRequest(TicketStatus Status, [StringLength(1000)] string? Note);

public record InvolveRequest([Required, MaxLength(20)] List<Guid> EmployeeIds);

public record CloseTicketRequest([Range(1, 5)] int? Rating, [StringLength(1000)] string? Feedback);

public record ReopenTicketRequest([Required, StringLength(1000)] string Reason);

public record TicketListItemDto(
    Guid Id, string Code, string Subject, Guid CategoryId, string Category, TicketPriority Priority, TicketStatus Status,
    Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Department, Guid? AssigneeEmployeeId, string? Assignee,
    DateTime CreatedAtUtc, DateTime DueAtUtc, DateTime LastActivityAtUtc, bool IsOverdue, int Replies, int? Rating);

public record TicketCommentDto(Guid Id, Guid? AuthorEmployeeId, string AuthorName, string Body, bool IsInternal, bool IsSystem, DateTime CreatedAtUtc);

public record TicketDetailDto(
    TicketListItemDto Ticket, string Description, string RaisedBy, List<Guid> InvolvedIds, List<string> Involved,
    DateTime? FirstResponseAtUtc, DateTime? ResolvedAtUtc, DateTime? ClosedAtUtc, string? Feedback,
    bool IsAgent, bool IsRequester, List<TicketCommentDto> Comments, List<LookupDto> Assignees);

public record HelpdeskSummaryDto(int Open, int InProgress, int Waiting, int Resolved, int Overdue, int Unassigned, int Mine);

[ApiController]
[Route("api/helpdesk")]
[Authorize(Policy = Policies.CompanyUser)]
public class HelpdeskController(HrmsDbContext db, HelpdeskService helpdesk, TenantClock clock, ICurrentUser me) : ControllerBase
{
    // ---------- Categories ----------

    /// <summary>Active categories for raising tickets; HR gets all of them with agents.</summary>
    [HttpGet("categories")]
    public async Task<List<TicketCategoryDto>> Categories()
    {
        var list = await db.TicketCategories.Where(c => me.IsHr() || c.IsActive).OrderBy(c => c.Name).ToListAsync();
        var agentIds = list.SelectMany(c => HelpdeskService.Ids(c.AgentIds)).Distinct().ToList();
        var names = await db.Employees.Where(e => agentIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        var open = await db.Tickets.Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled && t.Status != TicketStatus.Resolved)
            .GroupBy(t => t.CategoryId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return list.Select(c =>
        {
            var ids = HelpdeskService.Ids(c.AgentIds);
            return new TicketCategoryDto(c.Id, c.Name, c.Description, c.TargetHours, ids, ids.Select(i => names.GetValueOrDefault(i) ?? "").ToList(),
                c.IsActive, open.GetValueOrDefault(c.Id));
        }).ToList();
    }

    [HttpPost("categories"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> CreateCategory(SaveTicketCategoryRequest req)
    {
        var c = new TicketCategory();
        await ApplyAsync(c, req);
        db.TicketCategories.Add(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("categories/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateCategory(Guid id, SaveTicketCategoryRequest req)
    {
        var c = await db.TicketCategories.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Category");
        await ApplyAsync(c, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("categories/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var c = await db.TicketCategories.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Category");
        if (await db.Tickets.AnyAsync(t => t.CategoryId == id))
            throw new DomainException("This category has tickets. Turn it off instead so nobody can pick it.");
        db.TicketCategories.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Tickets ----------

    /// <summary>Whether to show the agent queue to the signed-in user.</summary>
    [HttpGet("me")]
    public async Task<object> Me() => new { isAgent = await helpdesk.IsAnyAgentAsync() };

    /// <summary>Tickets the signed-in employee raised, are the subject of, or were involved in.</summary>
    [HttpGet("tickets/my")]
    public async Task<List<TicketListItemDto>> MyTickets()
    {
        await helpdesk.AutoCloseAsync();
        var mine = me.EmployeeId ?? Guid.Empty;
        var text = mine.ToString();
        return await ProjectAsync(db.Tickets.Where(t => t.EmployeeId == mine || t.RaisedByEmployeeId == mine || t.InvolvedIds.Contains(text)));
    }

    /// <summary>
    /// The agent queue: HR sees everything, agents their categories and tickets assigned to them.
    /// scope = all | mine | unassigned | overdue; status = open (not resolved/closed) | resolved | closed | any.
    /// </summary>
    [HttpGet("tickets")]
    public async Task<List<TicketListItemDto>> Queue([FromQuery] string scope = "all", [FromQuery] string status = "open",
        [FromQuery] Guid? categoryId = null, [FromQuery] string? search = null)
    {
        await helpdesk.AutoCloseAsync();
        var q = AgentScope();
        q = status switch
        {
            "resolved" => q.Where(t => t.Status == TicketStatus.Resolved),
            "closed" => q.Where(t => t.Status == TicketStatus.Closed || t.Status == TicketStatus.Cancelled),
            "any" => q,
            _ => q.Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingOnEmployee),
        };
        var now = clock.UtcNow;
        q = scope switch
        {
            "mine" => q.Where(t => t.AssigneeEmployeeId == me.EmployeeId),
            "unassigned" => q.Where(t => t.AssigneeEmployeeId == null),
            "overdue" => q.Where(t => t.DueAtUtc < now && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingOnEmployee)),
            _ => q,
        };
        if (categoryId is { } c)
            q = q.Where(t => t.CategoryId == c);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var number = s.StartsWith("HD-", StringComparison.OrdinalIgnoreCase) && int.TryParse(s[3..], out var n) ? n : int.TryParse(s, out var m) ? m : -1;
            q = q.Where(t => t.Number == number || t.Subject.Contains(s) || t.Employee!.FirstName.Contains(s) || t.Employee.LastName.Contains(s));
        }
        return await ProjectAsync(q);
    }

    [HttpGet("summary")]
    public async Task<HelpdeskSummaryDto> Summary()
    {
        var q = AgentScope();
        var now = clock.UtcNow;
        var active = q.Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingOnEmployee);
        return new HelpdeskSummaryDto(
            await q.CountAsync(t => t.Status == TicketStatus.Open),
            await q.CountAsync(t => t.Status == TicketStatus.InProgress),
            await q.CountAsync(t => t.Status == TicketStatus.WaitingOnEmployee),
            await q.CountAsync(t => t.Status == TicketStatus.Resolved),
            await active.CountAsync(t => t.DueAtUtc < now),
            await active.CountAsync(t => t.AssigneeEmployeeId == null),
            await active.CountAsync(t => t.AssigneeEmployeeId == me.EmployeeId));
    }

    [HttpGet("tickets/{id:guid}")]
    public async Task<TicketDetailDto> Get(Guid id)
    {
        var (t, c) = await helpdesk.LoadAsync(id);
        var agent = helpdesk.IsAgent(t, c);
        var comments = await db.TicketComments.Where(x => x.TicketId == id && (agent || !x.IsInternal)).OrderBy(x => x.CreatedAtUtc)
            .Select(x => new TicketCommentDto(x.Id, x.AuthorEmployeeId, x.AuthorName, x.Body, x.IsInternal, x.IsSystem, x.CreatedAtUtc)).ToListAsync();
        var involved = HelpdeskService.Ids(t.InvolvedIds);
        var names = await db.Employees.Where(e => involved.Contains(e.Id) || e.Id == t.RaisedByEmployeeId)
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        var assignees = new List<LookupDto>();
        if (agent)
        {
            var agents = HelpdeskService.Ids(c.AgentIds);
            var hr = db.Users.Where(u => u.Role == UserRole.HrManager || u.Role == UserRole.CompanyAdmin).Select(u => u.EmployeeId);
            assignees = await db.Employees.Where(e => (agents.Contains(e.Id) || hr.Contains(e.Id)) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation))
                .OrderBy(e => e.FirstName).Select(e => new LookupDto(e.Id, e.FirstName + " " + e.LastName)).ToListAsync();
        }
        var item = (await ProjectAsync(db.Tickets.Where(x => x.Id == id))).Single();
        return new TicketDetailDto(item, t.Description, names.GetValueOrDefault(t.RaisedByEmployeeId) ?? "", involved,
            involved.Select(i => names.GetValueOrDefault(i) ?? "").ToList(), t.FirstResponseAtUtc, t.ResolvedAtUtc, t.ClosedAtUtc, t.Feedback,
            agent, helpdesk.IsRequester(t), comments, assignees);
    }

    [HttpPost("tickets")]
    public async Task<TicketListItemDto> Create(CreateTicketRequest req)
    {
        var t = await helpdesk.CreateAsync(new NewTicket(req.ForEmployeeId, req.CategoryId, req.Subject, req.Description, req.Priority));
        return (await ProjectAsync(db.Tickets.Where(x => x.Id == t.Id))).Single();
    }

    [HttpPost("tickets/{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id, TicketCommentRequest req)
    {
        await helpdesk.CommentAsync(id, req.Body, req.IsInternal);
        return NoContent();
    }

    [HttpPut("tickets/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTicketRequest req)
    {
        await helpdesk.UpdateAsync(id, req.AssigneeEmployeeId, req.Priority, req.CategoryId);
        return NoContent();
    }

    [HttpPost("tickets/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, TicketStatusRequest req)
    {
        await helpdesk.SetStatusAsync(id, req.Status, req.Note);
        return NoContent();
    }

    [HttpPost("tickets/{id:guid}/involve")]
    public async Task<IActionResult> Involve(Guid id, InvolveRequest req)
    {
        await helpdesk.InvolveAsync(id, req.EmployeeIds ?? []);
        return NoContent();
    }

    [HttpPost("tickets/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CloseTicketRequest req)
    {
        await helpdesk.CloseAsync(id, req.Rating, req.Feedback);
        return NoContent();
    }

    [HttpPost("tickets/{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, ReopenTicketRequest req)
    {
        await helpdesk.ReopenAsync(id, req.Reason);
        return NoContent();
    }

    [HttpPost("tickets/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await helpdesk.CancelAsync(id);
        return NoContent();
    }

    /// <summary>Tickets the signed-in user handles: all for HR; otherwise their categories and tickets assigned to them.</summary>
    private IQueryable<Ticket> AgentScope()
    {
        if (me.IsHr())
            return db.Tickets;
        var mine = me.EmployeeId ?? Guid.Empty;
        var text = mine.ToString();
        var categories = db.TicketCategories.Where(c => c.AgentIds.Contains(text)).Select(c => c.Id);
        return db.Tickets.Where(t => categories.Contains(t.CategoryId) || t.AssigneeEmployeeId == mine);
    }

    private async Task<List<TicketListItemDto>> ProjectAsync(IQueryable<Ticket> q)
    {
        var now = clock.UtcNow;
        var rows = await q.OrderByDescending(t => t.LastActivityAtUtc).Take(500).Select(t => new
        {
            t.Id, t.Number, t.Subject, t.CategoryId, Category = t.Category!.Name, t.Priority, t.Status, t.EmployeeId,
            t.Employee!.FirstName, t.Employee.LastName, t.Employee.EmployeeCode,
            Department = t.Employee.Department != null ? t.Employee.Department.Name : null,
            t.AssigneeEmployeeId,
            Assignee = db.Employees.Where(e => e.Id == t.AssigneeEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            t.CreatedAtUtc, t.DueAtUtc, t.LastActivityAtUtc, t.Rating,
            Replies = db.TicketComments.Count(c => c.TicketId == t.Id && !c.IsSystem && !c.IsInternal),
        }).ToListAsync();
        return rows.Select(r => new TicketListItemDto(r.Id, HelpdeskService.Code(r.Number), r.Subject, r.CategoryId, r.Category, r.Priority, r.Status,
            r.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, r.Department, r.AssigneeEmployeeId, r.Assignee, r.CreatedAtUtc, r.DueAtUtc,
            r.LastActivityAtUtc, r.DueAtUtc < now && r.Status is TicketStatus.Open or TicketStatus.InProgress or TicketStatus.WaitingOnEmployee,
            r.Replies, r.Rating)).ToList();
    }

    private async Task ApplyAsync(TicketCategory c, SaveTicketCategoryRequest req)
    {
        var name = req.Name.Trim();
        if (await db.TicketCategories.AnyAsync(x => x.Id != c.Id && x.Name == name))
            throw new DomainException($"There is already a {name} category.");
        var agents = (req.AgentIds ?? []).Distinct().ToList();
        if (agents.Count > 30)
            throw new DomainException("Add at most 30 agents to a category.");
        if (await db.Employees.CountAsync(e => agents.Contains(e.Id)) != agents.Count)
            throw new NotFoundException("Employee");
        c.Name = name;
        c.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        c.TargetHours = req.TargetHours;
        c.AgentIds = string.Join(',', agents);
        c.IsActive = req.IsActive;
    }
}
