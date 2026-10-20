using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record LetterTemplateDto(Guid Id, string Name, LetterCategory Category, string Subject, string Body, bool IsActive);

public record SaveLetterTemplateRequest(
    [Required, StringLength(150)] string Name,
    LetterCategory Category,
    [Required, StringLength(300)] string Subject,
    [Required, StringLength(20000)] string Body,
    bool IsActive);

public record PreviewLetterRequest(Guid EmployeeId, [StringLength(300)] string Subject, [StringLength(20000)] string Body);

public record PreviewLetterDto(string Subject, string Body, IReadOnlyList<string> UnknownPlaceholders);

public record IssueLetterRequest(
    Guid EmployeeId,
    LetterCategory Category,
    [Required, StringLength(300)] string Subject,
    [Required, StringLength(20000)] string Body,
    DateOnly? IssuedOn,
    bool ShareWithEmployee = true);

public record IssuedLetterDto(
    Guid Id, string ReferenceNo, LetterCategory Category, string Subject, DateOnly IssuedOn,
    Guid EmployeeId, string EmployeeName, string EmployeeCode, string? IssuedByName, bool SharedWithEmployee);

public record LetterDocumentDto(
    Guid Id, string ReferenceNo, LetterCategory Category, string Subject, string Body, DateOnly IssuedOn,
    Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Designation, string? IssuedByName,
    bool SharedWithEmployee, string CompanyName, string? CompanyAddress, string? CompanyPhone);

[ApiController]
[Route("api/letters")]
[Authorize(Policy = Policies.CompanyUser)]
public class LettersController(HrmsDbContext db, LetterService letters, ICurrentUser me) : ControllerBase
{
    [HttpGet("placeholders"), Authorize(Policy = Policies.Hr)]
    public IReadOnlyList<LetterPlaceholder> Placeholders() => LetterRenderer.Placeholders;

    [HttpGet("templates"), Authorize(Policy = Policies.Hr)]
    public async Task<List<LetterTemplateDto>> Templates()
    {
        await letters.EnsureDefaultTemplatesAsync();
        return await db.LetterTemplates.OrderBy(t => t.Category).ThenBy(t => t.Name)
            .Select(t => new LetterTemplateDto(t.Id, t.Name, t.Category, t.Subject, t.Body, t.IsActive)).ToListAsync();
    }

    [HttpPost("templates"), Authorize(Policy = Policies.Hr)]
    public async Task<LetterTemplateDto> CreateTemplate(SaveLetterTemplateRequest req)
    {
        RequireKnownPlaceholders(req.Subject, req.Body);
        var t = new LetterTemplate { Name = req.Name.Trim(), Category = req.Category, Subject = req.Subject.Trim(), Body = req.Body, IsActive = req.IsActive };
        db.LetterTemplates.Add(t);
        await db.SaveChangesAsync();
        return new LetterTemplateDto(t.Id, t.Name, t.Category, t.Subject, t.Body, t.IsActive);
    }

    [HttpPut("templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateTemplate(Guid id, SaveLetterTemplateRequest req)
    {
        RequireKnownPlaceholders(req.Subject, req.Body);
        var t = await db.LetterTemplates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        t.Name = req.Name.Trim();
        t.Category = req.Category;
        t.Subject = req.Subject.Trim();
        t.Body = req.Body;
        t.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var t = await db.LetterTemplates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        db.LetterTemplates.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("preview"), Authorize(Policy = Policies.Hr)]
    public async Task<PreviewLetterDto> Preview(PreviewLetterRequest req)
    {
        var (subject, body) = await letters.RenderAsync(req.EmployeeId, req.Subject ?? "", req.Body ?? "");
        return new PreviewLetterDto(subject, body, LetterRenderer.UnknownKeys((req.Subject ?? "") + "\n" + (req.Body ?? "")));
    }

    [HttpGet, Authorize(Policy = Policies.Hr)]
    public Task<List<IssuedLetterDto>> List([FromQuery] Guid? employeeId, [FromQuery] LetterCategory? category, [FromQuery] string? search)
    {
        var q = db.IssuedLetters.AsQueryable();
        if (employeeId is { } eid)
            q = q.Where(l => l.EmployeeId == eid);
        if (category is { } c)
            q = q.Where(l => l.Category == c);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(l => l.ReferenceNo.Contains(s) || l.Subject.Contains(s) || l.Employee!.FirstName.Contains(s)
                             || l.Employee.LastName.Contains(s) || l.Employee.EmployeeCode.Contains(s));
        }
        return Project(q).Take(500).ToListAsync();
    }

    [HttpGet("my")]
    public Task<List<IssuedLetterDto>> My() =>
        Project(db.IssuedLetters.Where(l => l.EmployeeId == me.EmployeeId && l.SharedWithEmployee)).ToListAsync();

    [HttpPost, Authorize(Policy = Policies.Hr)]
    public async Task<IssuedLetterDto> Issue(IssueLetterRequest req)
    {
        var letter = await letters.IssueAsync(req.EmployeeId, req.Category, req.Subject, req.Body, req.IssuedOn, req.ShareWithEmployee);
        return await Project(db.IssuedLetters.Where(l => l.Id == letter.Id)).FirstAsync();
    }

    [HttpGet("{id:guid}")]
    public async Task<LetterDocumentDto> Get(Guid id)
    {
        var l = await db.IssuedLetters.Include(x => x.Employee).ThenInclude(e => e!.Designation)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Letter");
        if (!me.IsHr() && !(l.EmployeeId == me.EmployeeId && l.SharedWithEmployee))
            throw new NotFoundException("Letter");

        var company = await db.Tenants.Where(t => t.Id == db.CurrentTenantId)
            .Select(t => new { t.Name, t.Address, t.City, t.Phone }).FirstAsync();
        return new LetterDocumentDto(
            l.Id, l.ReferenceNo, l.Category, l.Subject, l.Body, l.IssuedOn, l.EmployeeId, l.Employee!.FullName,
            l.Employee.EmployeeCode, l.Employee.Designation?.Title, l.IssuedByName, l.SharedWithEmployee,
            company.Name, LetterService.FullAddress(company.Address, company.City), company.Phone);
    }

    [HttpPut("{id:guid}/sharing"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SetSharing(Guid id, [FromQuery] bool shared)
    {
        var l = await db.IssuedLetters.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Letter");
        l.SharedWithEmployee = shared;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var l = await db.IssuedLetters.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Letter");
        db.IssuedLetters.Remove(l);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void RequireKnownPlaceholders(string subject, string body)
    {
        var unknown = LetterRenderer.UnknownKeys(subject + "\n" + body);
        if (unknown.Count > 0)
            throw new Domain.Common.DomainException($"Unknown placeholder(s): {string.Join(", ", unknown.Select(k => "{{" + k + "}}"))}.");
    }

    private static IQueryable<IssuedLetterDto> Project(IQueryable<IssuedLetter> q) =>
        q.OrderByDescending(l => l.IssuedOn).ThenByDescending(l => l.CreatedAtUtc).Select(l => new IssuedLetterDto(
            l.Id, l.ReferenceNo, l.Category, l.Subject, l.IssuedOn, l.EmployeeId,
            l.Employee!.FirstName + " " + l.Employee.LastName, l.Employee.EmployeeCode, l.IssuedByName, l.SharedWithEmployee));
}
