using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record LetterTemplateDto(Guid Id, string Name, LetterCategory Category, string Subject, string Body, bool IsActive,
    bool RequiresApproval = false, bool SelfService = false, LetterAutoUse? AutoUse = null, int MarginTopMm = 25, int MarginSideMm = 20, string? Footer = null);

public record SaveLetterTemplateRequest(
    [Required, StringLength(150)] string Name,
    LetterCategory Category,
    [Required, StringLength(300)] string Subject,
    [Required, StringLength(20000)] string Body,
    bool IsActive,
    bool RequiresApproval = false,
    bool SelfService = false,
    LetterAutoUse? AutoUse = null,
    [Range(5, 80)] int MarginTopMm = 25,
    [Range(5, 60)] int MarginSideMm = 20,
    [StringLength(500)] string? Footer = null);

public record RequestLetterRequest(Guid TemplateId, [StringLength(500)] string? Reason);

public record SelfServiceLetterDto(Guid Id, string Name, LetterCategory Category);

public record CandidateLetterDto(Guid Id, Guid ApplicationId, string CandidateName, string ReferenceNo, string Subject, string Body, DateOnly IssuedOn,
    string? IssuedByName, int MarginTopMm, int MarginSideMm, string? Footer, string CompanyName, string? CompanyAddress, string? CompanyPhone);

public record PreviewLetterRequest(Guid EmployeeId, [StringLength(300)] string Subject, [StringLength(20000)] string Body);

public record PreviewLetterDto(string Subject, string Body, IReadOnlyList<string> UnknownPlaceholders);

public record IssueLetterRequest(
    Guid EmployeeId,
    LetterCategory Category,
    [Required, StringLength(300)] string Subject,
    [Required, StringLength(20000)] string Body,
    DateOnly? IssuedOn,
    bool ShareWithEmployee = true,
    Guid? TemplateId = null);

public record IssuedLetterDto(
    Guid Id, string ReferenceNo, LetterCategory Category, string Subject, DateOnly IssuedOn,
    Guid EmployeeId, string EmployeeName, string EmployeeCode, string? IssuedByName, bool SharedWithEmployee,
    LetterStatus Status = LetterStatus.Issued, bool Requested = false, string? RequestReason = null);

public record LetterDocumentDto(
    Guid Id, string ReferenceNo, LetterCategory Category, string Subject, string Body, DateOnly IssuedOn,
    Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Designation, string? IssuedByName,
    bool SharedWithEmployee, string CompanyName, string? CompanyAddress, string? CompanyPhone,
    LetterStatus Status = LetterStatus.Issued, int MarginTopMm = 25, int MarginSideMm = 20, string? Footer = null);

[ApiController]
[Route("api/letters")]
[Authorize(Policy = Policies.CompanyUser)]
public class LettersController(HrmsDbContext db, LetterService letters, ICurrentUser me, ApprovalEngine approvals) : ControllerBase
{
    [HttpGet("placeholders"), Authorize(Policy = Policies.Hr)]
    public IReadOnlyList<LetterPlaceholder> Placeholders() => LetterRenderer.Placeholders;

    [HttpGet("templates"), Authorize(Policy = Policies.Hr)]
    public async Task<List<LetterTemplateDto>> Templates()
    {
        await letters.EnsureDefaultTemplatesAsync();
        return await db.LetterTemplates.OrderBy(t => t.Category).ThenBy(t => t.Name)
            .Select(t => new LetterTemplateDto(t.Id, t.Name, t.Category, t.Subject, t.Body, t.IsActive, t.RequiresApproval, t.SelfService, t.AutoUse,
                t.MarginTopMm, t.MarginSideMm, t.Footer)).ToListAsync();
    }

    [HttpPost("templates"), Authorize(Policy = Policies.Hr)]
    public async Task<LetterTemplateDto> CreateTemplate(SaveLetterTemplateRequest req)
    {
        RequireKnownPlaceholders(req.Subject, req.Body);
        var t = new LetterTemplate { Name = req.Name.Trim(), Category = req.Category, Subject = req.Subject.Trim(), Body = req.Body, IsActive = req.IsActive };
        await ApplyExtrasAsync(t, req);
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
        await ApplyExtrasAsync(t, req);
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
        var letter = await letters.IssueAsync(req.EmployeeId, req.Category, req.Subject, req.Body, req.IssuedOn, req.ShareWithEmployee, req.TemplateId);
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
            company.Name, LetterService.FullAddress(company.Address, company.City), company.Phone, l.Status, l.MarginTopMm, l.MarginSideMm, l.Footer);
    }

    // ---------- Self-service requests and approvals ----------

    [HttpGet("self-service")]
    public Task<List<SelfServiceLetterDto>> SelfService() =>
        db.LetterTemplates.Where(t => t.IsActive && t.SelfService).OrderBy(t => t.Name).Select(t => new SelfServiceLetterDto(t.Id, t.Name, t.Category)).ToListAsync();

    [HttpPost("request")]
    public async Task<IssuedLetterDto> RequestLetter(RequestLetterRequest req)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var letter = await letters.RequestAsync(employeeId, req.TemplateId, req.Reason ?? "");
        return await Project(db.IssuedLetters.Where(l => l.Id == letter.Id)).FirstAsync();
    }

    [HttpGet("requests/my")]
    public Task<List<IssuedLetterDto>> MyRequests() =>
        Project(db.IssuedLetters.Where(l => l.EmployeeId == me.EmployeeId && (l.Requested || l.Status != LetterStatus.Issued))).ToListAsync();

    [HttpGet("pending"), Authorize(Policy = Policies.Approver)]
    public async Task<List<IssuedLetterDto>> Pending()
    {
        var q = db.IssuedLetters.Where(l => l.Status == LetterStatus.Pending);
        if (!me.IsHr())
        {
            var waiting = await approvals.WaitingForMeAsync(ApprovalRequestType.Letter);
            q = q.Where(l => waiting.Contains(l.Id));
        }
        return await Project(q).ToListAsync();
    }

    [HttpPost("{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, ExemptionDecisionRequest? req)
    {
        await letters.DecideAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, ExemptionDecisionRequest? req)
    {
        await letters.DecideAsync(id, false, req?.Note);
        return NoContent();
    }

    // ---------- Candidate letters ----------

    [HttpGet("candidate/{applicationId:guid}")]
    public async Task<ActionResult<CandidateLetterDto>> CandidateLetter(Guid applicationId)
    {
        var l = await db.CandidateLetters.FirstOrDefaultAsync(x => x.ApplicationId == applicationId);
        if (l is null) return NoContent();
        // HR, or whoever runs the job post.
        if (!me.IsHr() && !await db.JobApplications.AnyAsync(a => a.Id == applicationId
                && db.JobPosts.Any(p => p.Id == a.JobPostId && (p.RecruiterEmployeeId == me.EmployeeId || p.HiringManagerEmployeeId == me.EmployeeId))))
            throw new NotFoundException("Letter");
        var company = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => new { t.Name, t.Address, t.City, t.Phone }).FirstAsync();
        return new CandidateLetterDto(l.Id, l.ApplicationId, l.CandidateName, l.ReferenceNo, l.Subject, l.Body, l.IssuedOn, l.IssuedByName, l.MarginTopMm, l.MarginSideMm,
            l.Footer, company.Name, LetterService.FullAddress(company.Address, company.City), company.Phone);
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

    /// <summary>Approval, self-service, automatic use (one template per event) and page layout.</summary>
    private async Task ApplyExtrasAsync(LetterTemplate t, SaveLetterTemplateRequest req)
    {
        if (req.AutoUse is { } use && req.IsActive && await db.LetterTemplates.AnyAsync(x => x.Id != t.Id && x.IsActive && x.AutoUse == use))
            throw new DomainException("Another template is already used automatically for that event.");
        t.RequiresApproval = req.RequiresApproval;
        t.SelfService = req.SelfService;
        t.AutoUse = req.AutoUse;
        t.MarginTopMm = req.MarginTopMm;
        t.MarginSideMm = req.MarginSideMm;
        t.Footer = string.IsNullOrWhiteSpace(req.Footer) ? null : req.Footer.Trim();
    }

    private static IQueryable<IssuedLetterDto> Project(IQueryable<IssuedLetter> q) =>
        q.OrderByDescending(l => l.IssuedOn).ThenByDescending(l => l.CreatedAtUtc).Select(l => new IssuedLetterDto(
            l.Id, l.ReferenceNo, l.Category, l.Subject, l.IssuedOn, l.EmployeeId,
            l.Employee!.FirstName + " " + l.Employee.LastName, l.Employee.EmployeeCode, l.IssuedByName, l.SharedWithEmployee, l.Status, l.Requested, l.RequestReason));
}
