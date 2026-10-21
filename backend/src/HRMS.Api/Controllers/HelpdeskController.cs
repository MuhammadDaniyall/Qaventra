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

public record TicketTypeDto(Guid Id, string Name, int? TargetHours, List<Guid> AgentIds, List<string> Agents, bool IsActive);

public record TicketCategoryDto(
    Guid Id, string Name, string? Description, int TargetHours, List<Guid> AgentIds, List<string> Agents, bool IsActive, int OpenTickets,
    Guid? LeadEmployeeId, string? Lead, TicketAssignment AssignmentMode, bool AgentCanClose, int AutoCloseDays, int ReopenDays,
    List<TicketTypeDto> Types);

public record SaveTicketCategoryRequest(
    [Required, StringLength(100)] string Name, [StringLength(500)] string? Description, [Range(1, 2000)] int TargetHours,
    List<Guid>? AgentIds, bool IsActive = true, Guid? LeadEmployeeId = null, TicketAssignment AssignmentMode = TicketAssignment.Manual,
    bool AgentCanClose = false, [Range(0, 90)] int AutoCloseDays = 3, [Range(0, 365)] int ReopenDays = 7);

public record SaveTicketTypeRequest([Required, StringLength(100)] string Name, [Range(1, 2000)] int? TargetHours, List<Guid>? AgentIds, bool IsActive = true);

public record CreateTicketRequest(
    Guid? ForEmployeeId, Guid CategoryId, [Required, StringLength(200)] string Subject, [Required, StringLength(8000)] string Description,
    TicketPriority Priority = TicketPriority.Medium, Guid? TypeId = null);

public record TicketCommentRequest([Required, StringLength(8000)] string Body, bool IsInternal = false);

public record EditCommentRequest([Required, StringLength(8000)] string Body);

public record UpdateTicketRequest(Guid? AssigneeEmployeeId, TicketPriority Priority, Guid CategoryId, Guid? TypeId = null);

public record TicketStatusRequest(TicketStatus Status, [StringLength(1000)] string? Note);

public record InvolveRequest([Required, MaxLength(20)] List<Guid> EmployeeIds);

public record CloseTicketRequest([Range(1, 5)] int? Rating, [StringLength(1000)] string? Feedback);

public record ReopenTicketRequest([Required, StringLength(1000)] string Reason);

public record HelpdeskLabelsDto(Dictionary<string, string> Statuses, Dictionary<string, string> Priorities);

public record TicketListItemDto(
    Guid Id, string Code, string Subject, Guid CategoryId, string Category, Guid? TypeId, string? Type, TicketPriority Priority, TicketStatus Status,
    Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Department, Guid? AssigneeEmployeeId, string? Assignee,
    DateTime CreatedAtUtc, DateTime DueAtUtc, DateTime LastActivityAtUtc, bool IsOverdue, int Replies, int? Rating);

public record TicketCommentDto(
    Guid Id, Guid? AuthorEmployeeId, string AuthorName, string Body, bool IsInternal, bool IsSystem, DateTime CreatedAtUtc,
    DateTime? EditedAtUtc, bool IsDeleted, bool CanEdit);

public record TicketDetailDto(
    TicketListItemDto Ticket, string Description, string RaisedBy, List<Guid> InvolvedIds, List<string> Involved,
    DateTime? FirstResponseAtUtc, DateTime? ResolvedAtUtc, DateTime? ClosedAtUtc, string? Feedback,
    bool IsAgent, bool IsRequester, bool AgentCanClose, bool CanReopen, List<TicketCommentDto> Comments, List<LookupDto> Assignees);

public record HelpdeskSummaryDto(int Open, int InProgress, int Waiting, int Resolved, int Overdue, int Unassigned, int Mine);

public record HelpdeskReportRowDto(
    Guid? Id, string Name, int Raised, int Resolved, int Open, double? AvgFirstResponseHours, double? AvgResolutionHours,
    int? SlaMetPercent, double? AvgRating, int Rated);

public record HelpdeskReportDto(
    DateOnly From, DateOnly To, HelpdeskReportRowDto Total, List<HelpdeskReportRowDto> Categories, List<HelpdeskReportRowDto> Agents,
    Dictionary<string, int> ByPriority, Dictionary<string, int> ByStatus);

[ApiController]
[Route("api/helpdesk")]
[Authorize(Policy = Policies.CompanyUser)]
public class HelpdeskController(HrmsDbContext db, HelpdeskService helpdesk, TenantClock clock, ICurrentUser me) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------- Categories and types ----------

    /// <summary>Active categories and types for raising tickets; HR gets everything with agents.</summary>
    [HttpGet("categories")]
    public async Task<List<TicketCategoryDto>> Categories()
    {
        var hr = me.IsHr();
        var list = await db.TicketCategories.Where(c => hr || c.IsActive).OrderBy(c => c.Name).ToListAsync();
        var types = (await db.TicketTypes.Where(t => hr || t.IsActive).OrderBy(t => t.Name).ToListAsync()).ToLookup(t => t.CategoryId);
        var people = list.SelectMany(c => HelpdeskService.Ids(c.AgentIds).Concat(c.LeadEmployeeId is { } l ? [l] : []))
            .Concat(types.SelectMany(g => g).SelectMany(t => HelpdeskService.Ids(t.AgentIds))).Distinct().ToList();
        var names = await db.Employees.Where(e => people.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        var open = await db.Tickets.Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingOnEmployee)
            .GroupBy(t => t.CategoryId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        List<string> Names(List<Guid> ids) => ids.Select(i => names.GetValueOrDefault(i) ?? "").ToList();
        return list.Select(c =>
        {
            var ids = HelpdeskService.Ids(c.AgentIds);
            var typeDtos = types[c.Id].Select(t =>
            {
                var tIds = HelpdeskService.Ids(t.AgentIds);
                return new TicketTypeDto(t.Id, t.Name, t.TargetHours, tIds, Names(tIds), t.IsActive);
            }).ToList();
            return new TicketCategoryDto(c.Id, c.Name, c.Description, c.TargetHours, ids, Names(ids), c.IsActive, open.GetValueOrDefault(c.Id),
                c.LeadEmployeeId, c.LeadEmployeeId is { } lead ? names.GetValueOrDefault(lead) : null, c.AssignmentMode, c.AgentCanClose,
                c.AutoCloseDays, c.ReopenDays, typeDtos);
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

    [HttpPost("categories/{id:guid}/types"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> CreateType(Guid id, SaveTicketTypeRequest req)
    {
        if (!await db.TicketCategories.AnyAsync(c => c.Id == id))
            throw new NotFoundException("Category");
        var t = new TicketType { CategoryId = id };
        await ApplyTypeAsync(t, req);
        db.TicketTypes.Add(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateType(Guid id, SaveTicketTypeRequest req)
    {
        var t = await db.TicketTypes.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Ticket type");
        await ApplyTypeAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteType(Guid id)
    {
        var t = await db.TicketTypes.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Ticket type");
        if (await db.Tickets.AnyAsync(x => x.TypeId == id))
            throw new DomainException("This type has tickets. Turn it off instead.");
        db.TicketTypes.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Settings ----------

    /// <summary>The company's names for statuses and priorities, with defaults filled in.</summary>
    [HttpGet("settings")]
    public async Task<HelpdeskLabelsDto> Settings()
    {
        var s = await db.HelpdeskSettings.FirstOrDefaultAsync();
        var statuses = Enum.GetValues<TicketStatus>().ToDictionary(x => x.ToString(), HelpdeskService.Label);
        var priorities = Enum.GetValues<TicketPriority>().ToDictionary(x => x.ToString(), x => x.ToString());
        if (s is not null)
        {
            foreach (var (k, v) in JsonSerializer.Deserialize<Dictionary<string, string>>(s.StatusLabels, Json) ?? [])
                if (statuses.ContainsKey(k) && !string.IsNullOrWhiteSpace(v)) statuses[k] = v;
            foreach (var (k, v) in JsonSerializer.Deserialize<Dictionary<string, string>>(s.PriorityLabels, Json) ?? [])
                if (priorities.ContainsKey(k) && !string.IsNullOrWhiteSpace(v)) priorities[k] = v;
        }
        return new HelpdeskLabelsDto(statuses, priorities);
    }

    [HttpPut("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<HelpdeskLabelsDto> SaveSettings(HelpdeskLabelsDto req)
    {
        static Dictionary<string, string> Clean<T>(Dictionary<string, string>? input) where T : struct, Enum =>
            (input ?? []).Where(kv => Enum.TryParse<T>(kv.Key, out _) && !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value.Trim().Length > 40 ? throw new DomainException("Keep each name under 40 characters.") : kv.Value.Trim());
        var s = await db.HelpdeskSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new HelpdeskSettings();
            db.HelpdeskSettings.Add(s);
        }
        s.StatusLabels = JsonSerializer.Serialize(Clean<TicketStatus>(req.Statuses), Json);
        s.PriorityLabels = JsonSerializer.Serialize(Clean<TicketPriority>(req.Priorities), Json);
        await db.SaveChangesAsync();
        return await Settings();
    }

    // ---------- Tickets ----------

    /// <summary>Whether to show the agent queue to the signed-in user.</summary>
    [HttpGet("me")]
    public async Task<object> Me() => new { isAgent = await helpdesk.IsAnyAgentAsync() };

    /// <summary>Tickets the signed-in employee raised, are the subject of, or were involved in.</summary>
    [HttpGet("tickets/my")]
    public async Task<List<TicketListItemDto>> MyTickets()
    {
        await helpdesk.RunDueWorkAsync();
        var mine = me.EmployeeId ?? Guid.Empty;
        var text = mine.ToString();
        return await ProjectAsync(db.Tickets.Where(t => t.EmployeeId == mine || t.RaisedByEmployeeId == mine || t.InvolvedIds.Contains(text)));
    }

    /// <summary>
    /// The agent queue: HR sees everything; agents their categories and types, leads their categories, and tickets assigned to them.
    /// scope = all | mine | unassigned | overdue; status = open (not resolved/closed) | resolved | closed | any.
    /// </summary>
    [HttpGet("tickets")]
    public async Task<List<TicketListItemDto>> Queue([FromQuery] string scope = "all", [FromQuery] string status = "open",
        [FromQuery] Guid? categoryId = null, [FromQuery] string? search = null)
    {
        await helpdesk.RunDueWorkAsync();
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
        await helpdesk.RunDueWorkAsync();
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
        var now = clock.UtcNow;
        var open = t.Status is not (TicketStatus.Closed or TicketStatus.Cancelled);
        var comments = (await db.TicketComments.Where(x => x.TicketId == id && (agent || !x.IsInternal)).OrderBy(x => x.CreatedAtUtc).ToListAsync())
            .Select(x => new TicketCommentDto(x.Id, x.AuthorEmployeeId, x.AuthorName, x.IsDeleted ? "" : x.Body, x.IsInternal, x.IsSystem, x.CreatedAtUtc,
                x.EditedAtUtc, x.IsDeleted,
                open && !x.IsSystem && !x.IsDeleted && x.AuthorEmployeeId == me.EmployeeId && now - x.CreatedAtUtc <= HelpdeskService.EditWindow))
            .ToList();
        var involved = HelpdeskService.Ids(t.InvolvedIds);
        var names = await db.Employees.Where(e => involved.Contains(e.Id) || e.Id == t.RaisedByEmployeeId)
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        var assignees = new List<LookupDto>();
        if (agent)
        {
            var agents = HelpdeskService.Ids(c.AgentIds).Concat(HelpdeskService.Ids(t.Type?.AgentIds ?? "")).Concat(c.LeadEmployeeId is { } l ? [l] : []).ToList();
            var hr = db.Users.Where(u => u.Role == UserRole.HrManager || u.Role == UserRole.CompanyAdmin).Select(u => u.EmployeeId);
            assignees = await db.Employees.Where(e => (agents.Contains(e.Id) || hr.Contains(e.Id)) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation))
                .OrderBy(e => e.FirstName).Select(e => new LookupDto(e.Id, e.FirstName + " " + e.LastName)).ToListAsync();
        }
        var canReopen = helpdesk.IsRequester(t) && (t.Status == TicketStatus.Resolved
            || (t.Status == TicketStatus.Closed && c.ReopenDays > 0 && t.ClosedAtUtc is { } closed && now - closed <= TimeSpan.FromDays(c.ReopenDays)));
        var item = (await ProjectAsync(db.Tickets.Where(x => x.Id == id))).Single();
        return new TicketDetailDto(item, t.Description, names.GetValueOrDefault(t.RaisedByEmployeeId) ?? "", involved,
            involved.Select(i => names.GetValueOrDefault(i) ?? "").ToList(), t.FirstResponseAtUtc, t.ResolvedAtUtc, t.ClosedAtUtc, t.Feedback,
            agent, helpdesk.IsRequester(t), c.AgentCanClose, canReopen, comments, assignees);
    }

    [HttpPost("tickets")]
    public async Task<TicketListItemDto> Create(CreateTicketRequest req)
    {
        var t = await helpdesk.CreateAsync(new NewTicket(req.ForEmployeeId, req.CategoryId, req.TypeId, req.Subject, req.Description, req.Priority));
        return (await ProjectAsync(db.Tickets.Where(x => x.Id == t.Id))).Single();
    }

    [HttpPost("tickets/{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id, TicketCommentRequest req)
    {
        await helpdesk.CommentAsync(id, req.Body, req.IsInternal);
        return NoContent();
    }

    [HttpPut("tickets/{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> EditComment(Guid id, Guid commentId, EditCommentRequest req)
    {
        await helpdesk.EditCommentAsync(id, commentId, req.Body, delete: false);
        return NoContent();
    }

    [HttpDelete("tickets/{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        await helpdesk.EditCommentAsync(id, commentId, null, delete: true);
        return NoContent();
    }

    [HttpPut("tickets/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTicketRequest req)
    {
        await helpdesk.UpdateAsync(id, req.AssigneeEmployeeId, req.Priority, req.CategoryId, req.TypeId);
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

    // ---------- Reports ----------

    /// <summary>
    /// Tickets raised in the range, by category and by assignee: how many, how fast the first answer and the fix came,
    /// how many met their target, and the ratings. HR sees all; agents and leads their own scope.
    /// </summary>
    [HttpGet("report")]
    public async Task<HelpdeskReportDto> Report([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var rows = await ReportRowsAsync(from, to);
        HelpdeskReportRowDto Row(Guid? id, string name, List<ReportTicket> list)
        {
            var resolved = list.Where(t => t.ResolvedAtUtc is not null).ToList();
            var rated = list.Where(t => t.Rating is not null).ToList();
            return new HelpdeskReportRowDto(id, name, list.Count, resolved.Count,
                list.Count(t => t.Status is TicketStatus.Open or TicketStatus.InProgress or TicketStatus.WaitingOnEmployee),
                Avg(list.Where(t => t.FirstResponseAtUtc is not null).Select(t => (t.FirstResponseAtUtc!.Value - t.CreatedAtUtc).TotalHours)),
                Avg(resolved.Select(t => (t.ResolvedAtUtc!.Value - t.CreatedAtUtc).TotalHours)),
                resolved.Count == 0 ? null : (int)Math.Round(100.0 * resolved.Count(t => t.ResolvedAtUtc <= t.DueAtUtc) / resolved.Count),
                rated.Count == 0 ? null : Math.Round(rated.Average(t => t.Rating!.Value), 1), rated.Count);
        }
        return new HelpdeskReportDto(from, to, Row(null, "All tickets", rows),
            rows.GroupBy(t => (t.CategoryId, t.Category)).OrderByDescending(g => g.Count()).Select(g => Row(g.Key.CategoryId, g.Key.Category, g.ToList())).ToList(),
            rows.Where(t => t.AssigneeEmployeeId is not null).GroupBy(t => (t.AssigneeEmployeeId, t.Assignee))
                .OrderByDescending(g => g.Count()).Select(g => Row(g.Key.AssigneeEmployeeId, g.Key.Assignee ?? "", g.ToList())).ToList(),
            Enum.GetValues<TicketPriority>().ToDictionary(p => p.ToString(), p => rows.Count(t => t.Priority == p)),
            Enum.GetValues<TicketStatus>().ToDictionary(s => s.ToString(), s => rows.Count(t => t.Status == s)));
    }

    [HttpGet("export")]
    public async Task<FileContentResult> Export([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var rows = await ReportRowsAsync(from, to);
        var bytes = Excel.Write("Tickets", rows.OrderBy(r => r.Number),
            new("Ticket", r => HelpdeskService.Code(r.Number)), new("Subject", r => r.Subject), new("Category", r => r.Category), new("Type", r => r.Type),
            new("Priority", r => r.Priority.ToString()), new("Status", r => HelpdeskService.Label(r.Status)), new("Employee", r => r.Employee),
            new("Department", r => r.Department), new("Assignee", r => r.Assignee), new("Raised (UTC)", r => r.CreatedAtUtc, "yyyy-mm-dd hh:mm"),
            new("Due (UTC)", r => r.DueAtUtc, "yyyy-mm-dd hh:mm"), new("First response (UTC)", r => r.FirstResponseAtUtc, "yyyy-mm-dd hh:mm"),
            new("Resolved (UTC)", r => r.ResolvedAtUtc, "yyyy-mm-dd hh:mm"),
            new("Within target", r => r.ResolvedAtUtc is { } x ? x <= r.DueAtUtc ? "Yes" : "No" : null), new("Rating", r => r.Rating));
        return File(bytes, Excel.ContentType, $"helpdesk-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.xlsx");
    }

    private record ReportTicket(
        int Number, string Subject, Guid CategoryId, string Category, string? Type, TicketPriority Priority, TicketStatus Status, string Employee,
        string? Department, Guid? AssigneeEmployeeId, string? Assignee, DateTime CreatedAtUtc, DateTime DueAtUtc, DateTime? FirstResponseAtUtc,
        DateTime? ResolvedAtUtc, int? Rating);

    private async Task<List<ReportTicket>> ReportRowsAsync(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 366)
            throw new DomainException("Choose a range of up to one year.");
        if (!await helpdesk.IsAnyAgentAsync())
            throw new ForbiddenException("Help desk reports are for agents and HR.");
        var start = await clock.ToUtcAsync(from.ToDateTime(TimeOnly.MinValue));
        var end = await clock.ToUtcAsync(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        return await AgentScope().Where(t => t.CreatedAtUtc >= start && t.CreatedAtUtc < end)
            .Select(t => new ReportTicket(t.Number, t.Subject, t.CategoryId, t.Category!.Name, t.Type != null ? t.Type.Name : null, t.Priority, t.Status,
                t.Employee!.FirstName + " " + t.Employee.LastName, t.Employee.Department != null ? t.Employee.Department.Name : null, t.AssigneeEmployeeId,
                db.Employees.Where(e => e.Id == t.AssigneeEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                t.CreatedAtUtc, t.DueAtUtc, t.FirstResponseAtUtc, t.ResolvedAtUtc, t.Rating))
            .ToListAsync();
    }

    private static double? Avg(IEnumerable<double> hours)
    {
        var list = hours.ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 1);
    }

    /// <summary>Tickets the signed-in user handles: all for HR; otherwise their categories and types, categories they lead, and tickets assigned to them.</summary>
    private IQueryable<Ticket> AgentScope()
    {
        if (me.IsHr())
            return db.Tickets;
        var mine = me.EmployeeId ?? Guid.Empty;
        var text = mine.ToString();
        var categories = db.TicketCategories.Where(c => c.AgentIds.Contains(text) || c.LeadEmployeeId == mine).Select(c => c.Id);
        var types = db.TicketTypes.Where(t => t.AgentIds.Contains(text)).Select(t => t.Id);
        return db.Tickets.Where(t => categories.Contains(t.CategoryId) || (t.TypeId != null && types.Contains(t.TypeId.Value)) || t.AssigneeEmployeeId == mine);
    }

    private async Task<List<TicketListItemDto>> ProjectAsync(IQueryable<Ticket> q)
    {
        var now = clock.UtcNow;
        var rows = await q.OrderByDescending(t => t.LastActivityAtUtc).Take(500).Select(t => new
        {
            t.Id, t.Number, t.Subject, t.CategoryId, Category = t.Category!.Name, t.TypeId, Type = t.Type != null ? t.Type.Name : null, t.Priority, t.Status,
            t.EmployeeId, t.Employee!.FirstName, t.Employee.LastName, t.Employee.EmployeeCode,
            Department = t.Employee.Department != null ? t.Employee.Department.Name : null,
            t.AssigneeEmployeeId,
            Assignee = db.Employees.Where(e => e.Id == t.AssigneeEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            t.CreatedAtUtc, t.DueAtUtc, t.LastActivityAtUtc, t.Rating,
            Replies = db.TicketComments.Count(c => c.TicketId == t.Id && !c.IsSystem && !c.IsInternal && !c.IsDeleted),
        }).ToListAsync();
        return rows.Select(r => new TicketListItemDto(r.Id, HelpdeskService.Code(r.Number), r.Subject, r.CategoryId, r.Category, r.TypeId, r.Type,
            r.Priority, r.Status, r.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, r.Department, r.AssigneeEmployeeId, r.Assignee,
            r.CreatedAtUtc, r.DueAtUtc, r.LastActivityAtUtc,
            r.DueAtUtc < now && r.Status is TicketStatus.Open or TicketStatus.InProgress or TicketStatus.WaitingOnEmployee, r.Replies, r.Rating)).ToList();
    }

    private async Task<List<Guid>> ValidAgentsAsync(List<Guid>? ids)
    {
        var agents = (ids ?? []).Distinct().ToList();
        if (agents.Count > 30)
            throw new DomainException("Add at most 30 agents.");
        if (await db.Employees.CountAsync(e => agents.Contains(e.Id)) != agents.Count)
            throw new NotFoundException("Employee");
        return agents;
    }

    private async Task ApplyAsync(TicketCategory c, SaveTicketCategoryRequest req)
    {
        var name = req.Name.Trim();
        if (await db.TicketCategories.AnyAsync(x => x.Id != c.Id && x.Name == name))
            throw new DomainException($"There is already a {name} category.");
        if (req.LeadEmployeeId is { } lead && !await db.Employees.AnyAsync(e => e.Id == lead && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new DomainException("Choose a lead who currently works here.");
        if (!Enum.IsDefined(req.AssignmentMode))
            throw new DomainException("Choose how tickets are assigned.");
        var agents = await ValidAgentsAsync(req.AgentIds);
        if (req.AssignmentMode != TicketAssignment.Manual && agents.Count == 0)
            throw new DomainException("Automatic assignment needs at least one agent.");
        c.Name = name;
        c.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        c.TargetHours = req.TargetHours;
        c.AgentIds = string.Join(',', agents);
        c.IsActive = req.IsActive;
        c.LeadEmployeeId = req.LeadEmployeeId;
        c.AssignmentMode = req.AssignmentMode;
        c.AgentCanClose = req.AgentCanClose;
        c.AutoCloseDays = req.AutoCloseDays;
        c.ReopenDays = req.ReopenDays;
    }

    private async Task ApplyTypeAsync(TicketType t, SaveTicketTypeRequest req)
    {
        var name = req.Name.Trim();
        if (await db.TicketTypes.AnyAsync(x => x.Id != t.Id && x.CategoryId == t.CategoryId && x.Name == name))
            throw new DomainException($"This category already has a {name} type.");
        t.Name = name;
        t.TargetHours = req.TargetHours;
        t.AgentIds = string.Join(',', await ValidAgentsAsync(req.AgentIds));
        t.IsActive = req.IsActive;
    }
}
