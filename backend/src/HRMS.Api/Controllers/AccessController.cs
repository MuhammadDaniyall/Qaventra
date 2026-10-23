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
using Microsoft.Extensions.Caching.Memory;

namespace HRMS.Api.Controllers;

public record AccessModuleDto(string Key, string Label);
public record AccessTemplateDto(Guid Id, string Name, string? Description, List<string> Modules, int Users);
public record SaveAccessTemplateRequest([Required, StringLength(100)] string Name, [StringLength(300)] string? Description, List<string> Modules);
public record AccessUserDto(Guid UserId, string Name, string Email, UserRole Role, bool IsActive, Guid? AccessTemplateId);
public record SetUserTemplateRequest(Guid? AccessTemplateId);
public record CopyAccessRequest(Guid FromUserId, List<Guid> ToUserIds);

public record SuggestionCategoryDto(Guid Id, string Name, List<Guid> ReviewerIds, bool IsActive);
public record SaveSuggestionCategoryRequest([Required, StringLength(100)] string Name, List<Guid>? ReviewerIds, bool IsActive = true);
public record SuggestionDto(Guid Id, string Title, string Details, Guid? CategoryId, string? Category, bool IsAnonymous, string? From, string? FromCode,
    SuggestionStatus Status, string? Response, string? RespondedByName, DateTime? RespondedAtUtc, DateTime CreatedAtUtc, bool Mine);
public record SubmitSuggestionRequest([Required, StringLength(150)] string Title, [Required, StringLength(4000)] string Details, Guid? CategoryId, bool IsAnonymous);
public record RespondSuggestionRequest(SuggestionStatus Status, [StringLength(2000)] string? Response);

public record HistoryEntryDto(DateTime TimestampUtc, string? UserEmail, string Action, string? Changes);

/// <summary>Access templates: which HR modules a user may use. Company admin only.</summary>
[ApiController]
[Route("api/access")]
[Authorize(Policy = Policies.CompanyUser)]
[Authorize(Policy = Policies.Admin)]
public class AccessController(HrmsDbContext db, IMemoryCache cache) : ControllerBase
{
    [HttpGet("modules")]
    public List<AccessModuleDto> Modules() => AccessModules.All.Select(m => new AccessModuleDto(m.Key, m.Label)).ToList();

    [HttpGet("templates")]
    public async Task<List<AccessTemplateDto>> Templates() =>
        (await db.AccessTemplates.OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name, t.Description, t.Modules, Users = db.Users.Count(u => u.AccessTemplateId == t.Id) }).ToListAsync())
        .Select(t => new AccessTemplateDto(t.Id, t.Name, t.Description, AccessModules.Parse(t.Modules), t.Users)).ToList();

    [HttpPost("templates")]
    public async Task<IActionResult> AddTemplate(SaveAccessTemplateRequest req)
    {
        var t = new AccessTemplate();
        await ApplyAsync(t, req);
        db.AccessTemplates.Add(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, SaveAccessTemplateRequest req)
    {
        var t = await db.AccessTemplates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        await ApplyAsync(t, req);
        await db.SaveChangesAsync();
        foreach (var u in await db.Users.Where(u => u.AccessTemplateId == id).Select(u => u.Id).ToListAsync())
            ModuleRoleHandler.Forget(cache, u);
        return NoContent();
    }

    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var t = await db.AccessTemplates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        if (await db.Users.AnyAsync(u => u.AccessTemplateId == id))
            throw new DomainException("Users still have this template. Take it off them first.");
        db.AccessTemplates.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(AccessTemplate t, SaveAccessTemplateRequest req)
    {
        var name = req.Name.Trim();
        if (await db.AccessTemplates.AnyAsync(x => x.Id != t.Id && x.Name == name)) throw new DomainException($"There is already a template called {name}.");
        var modules = AccessModules.Parse(string.Join(',', req.Modules ?? []));
        if (modules.Count == 0) throw new DomainException("Tick at least one module.");
        t.Name = name;
        t.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        t.Modules = string.Join(',', modules.Distinct());
    }

    [HttpGet("users")]
    public Task<List<AccessUserDto>> Users() =>
        db.Users.Where(u => u.Role != UserRole.CompanyAdmin && u.Role != UserRole.SuperAdmin).OrderBy(u => u.FullName)
            .Select(u => new AccessUserDto(u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.AccessTemplateId)).ToListAsync();

    [HttpPut("users/{userId:guid}")]
    public async Task<IActionResult> SetTemplate(Guid userId, SetUserTemplateRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId) ?? throw new NotFoundException("User");
        if (user.Role is UserRole.CompanyAdmin or UserRole.SuperAdmin) throw new DomainException("The company admin always has full access.");
        if (req.AccessTemplateId is { } t && !await db.AccessTemplates.AnyAsync(x => x.Id == t)) throw new NotFoundException("Template");
        user.AccessTemplateId = req.AccessTemplateId;
        await db.SaveChangesAsync();
        ModuleRoleHandler.Forget(cache, userId);
        return NoContent();
    }

    /// <summary>Gives several users the same template as another user.</summary>
    [HttpPost("copy")]
    public async Task<IActionResult> Copy(CopyAccessRequest req)
    {
        var from = await db.Users.FirstOrDefaultAsync(u => u.Id == req.FromUserId) ?? throw new NotFoundException("User");
        var targets = await db.Users.Where(u => (req.ToUserIds ?? new()).Contains(u.Id) && u.Role != UserRole.CompanyAdmin && u.Role != UserRole.SuperAdmin).ToListAsync();
        foreach (var u in targets)
        {
            u.AccessTemplateId = from.AccessTemplateId;
            ModuleRoleHandler.Forget(cache, u.Id);
        }
        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Suggestion box: anyone submits (optionally anonymous); the category's reviewers or HR respond.</summary>
[ApiController]
[Route("api/suggestions")]
[Authorize(Policy = Policies.CompanyUser)]
public class SuggestionsController(HrmsDbContext db, ICurrentUser me, TenantClock clock, NotificationService notify) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<List<SuggestionCategoryDto>> Categories() =>
        (await db.SuggestionCategories.OrderBy(c => c.Name).ToListAsync())
        .Select(c => new SuggestionCategoryDto(c.Id, c.Name, c.ReviewerIds is { } r ? HelpdeskService.Ids(r) : [], c.IsActive)).ToList();

    [HttpPost("categories"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddCategory(SaveSuggestionCategoryRequest req)
    {
        var c = new SuggestionCategory();
        await ApplyAsync(c, req);
        db.SuggestionCategories.Add(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("categories/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateCategory(Guid id, SaveSuggestionCategoryRequest req)
    {
        await ApplyAsync(await db.SuggestionCategories.FirstOrDefaultAsync(c => c.Id == id) ?? throw new NotFoundException("Category"), req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("categories/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var c = await db.SuggestionCategories.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Category");
        if (await db.Suggestions.AnyAsync(s => s.CategoryId == id)) throw new DomainException("Suggestions use this category. Turn it off instead.");
        db.SuggestionCategories.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(SuggestionCategory c, SaveSuggestionCategoryRequest req)
    {
        var ids = (req.ReviewerIds ?? []).Distinct().ToList();
        if (ids.Count > 0 && await db.Employees.CountAsync(e => ids.Contains(e.Id)) != ids.Count) throw new NotFoundException("Employee");
        c.Name = req.Name.Trim();
        c.ReviewerIds = ids.Count > 0 ? string.Join(',', ids) : null;
        c.IsActive = req.IsActive;
    }

    [HttpGet("my")]
    public Task<List<SuggestionDto>> My() => ProjectAsync(db.Suggestions.Where(s => s.EmployeeId == me.EmployeeId), reviewer: false);

    [HttpPost]
    public async Task<IActionResult> Submit(SubmitSuggestionRequest req)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        SuggestionCategory? category = null;
        if (req.CategoryId is { } cid)
            category = await db.SuggestionCategories.FirstOrDefaultAsync(c => c.Id == cid && c.IsActive) ?? throw new NotFoundException("Category");
        var s = new Suggestion
        {
            EmployeeId = employeeId, CategoryId = req.CategoryId, IsAnonymous = req.IsAnonymous, Title = req.Title.Trim(), Details = req.Details.Trim(),
        };
        db.Suggestions.Add(s);
        await db.SaveChangesAsync();
        var title = $"New suggestion{(category is null ? "" : $" ({category.Name})")}";
        if (category?.ReviewerIds is { } r)
            await notify.ToEmployeesAsync(HelpdeskService.Ids(r), NotificationKind.ApprovalNeeded, title, s.Title, "/suggestions?tab=review", includeMe: false);
        else
            await notify.ToHrAsync(NotificationKind.ApprovalNeeded, title, s.Title, "/suggestions?tab=review");
        return NoContent();
    }

    /// <summary>HR sees all; a reviewer sees their categories. Anonymous ones never show who sent them.</summary>
    [HttpGet("review")]
    public async Task<List<SuggestionDto>> Review([FromQuery] SuggestionStatus? status)
    {
        var q = db.Suggestions.AsQueryable();
        if (!me.IsHr())
        {
            var mine = await ReviewCategoriesAsync();
            if (mine.Count == 0) throw new ForbiddenException("You do not review suggestions.");
            q = q.Where(s => s.CategoryId != null && mine.Contains(s.CategoryId.Value));
        }
        if (status is { } st) q = q.Where(s => s.Status == st);
        return await ProjectAsync(q, reviewer: true);
    }

    [HttpPut("{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, RespondSuggestionRequest req)
    {
        var s = await db.Suggestions.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Suggestion");
        if (!me.IsHr() && (s.CategoryId is not { } cid || !(await ReviewCategoriesAsync()).Contains(cid)))
            throw new ForbiddenException("You do not review this suggestion.");
        if (!Enum.IsDefined(req.Status)) throw new DomainException("Choose a status.");
        s.Status = req.Status;
        s.Response = string.IsNullOrWhiteSpace(req.Response) ? null : req.Response.Trim();
        s.RespondedByName = await LetterService.CurrentUserNameAsync(db, me);
        s.RespondedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
        await notify.ToEmployeesAsync([s.EmployeeId], NotificationKind.RequestDecided, $"Your suggestion: {Humanize(s.Status)}",
            s.Response ?? s.Title, "/suggestions", includeMe: false);
        return NoContent();
    }

    [HttpGet("can-review")]
    public async Task<bool> CanReview() => me.IsHr() || (await ReviewCategoriesAsync()).Count > 0;

    private async Task<List<Guid>> ReviewCategoriesAsync()
    {
        var mine = me.EmployeeId;
        if (mine is null) return [];
        return (await db.SuggestionCategories.Where(c => c.ReviewerIds != null).ToListAsync())
            .Where(c => HelpdeskService.Has(c.ReviewerIds!, mine)).Select(c => c.Id).ToList();
    }

    private static string Humanize(SuggestionStatus s) => s switch
    {
        SuggestionStatus.UnderReview => "under review",
        _ => s.ToString().ToLowerInvariant(),
    };

    private async Task<List<SuggestionDto>> ProjectAsync(IQueryable<Suggestion> q, bool reviewer)
    {
        var rows = await q.OrderByDescending(s => s.CreatedAtUtc).Take(300)
            .Select(s => new
            {
                S = s, Name = s.Employee!.FirstName + " " + s.Employee.LastName, s.Employee.EmployeeCode,
                Category = db.SuggestionCategories.Where(c => c.Id == s.CategoryId).Select(c => c.Name).FirstOrDefault(),
            }).ToListAsync();
        return rows.Select(r => new SuggestionDto(r.S.Id, r.S.Title, r.S.Details, r.S.CategoryId, r.Category, r.S.IsAnonymous,
            reviewer && r.S.IsAnonymous ? null : r.Name, reviewer && r.S.IsAnonymous ? null : r.EmployeeCode, r.S.Status, r.S.Response, r.S.RespondedByName,
            r.S.RespondedAtUtc, r.S.CreatedAtUtc, r.S.EmployeeId == me.EmployeeId)).ToList();
    }
}

/// <summary>Change history of one record, from the audit log: who changed what and when.</summary>
[ApiController]
[Route("api/history")]
[Authorize(Policy = Policies.CompanyUser)]
[Authorize(Policy = Policies.Hr)]
public class HistoryController(HrmsDbContext db, ICurrentUser me) : ControllerBase
{
    [HttpGet("{entityType}/{entityId}")]
    public Task<List<HistoryEntryDto>> Get(string entityType, string entityId) =>
        db.AuditLogs.AsNoTracking()
            .Where(l => l.TenantId == me.TenantId && l.Category == AuditCategory.Data && l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.Id).Take(100)
            .Select(l => new HistoryEntryDto(l.TimestampUtc, l.UserEmail, l.Action, l.Changes)).ToListAsync();
}
