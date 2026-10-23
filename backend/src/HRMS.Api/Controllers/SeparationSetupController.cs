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

public record ClearanceTemplateItemDto([Required, StringLength(100)] string Department, [Required, StringLength(200)] string Title, [Range(1, 10)] int Level,
    Guid? ResponsibleEmployeeId);
public record ClearanceTemplateDto(Guid Id, string Name, bool IsDefault, List<ClearanceTemplateItemDto> Items);
public record SaveClearanceTemplateRequest([Required, StringLength(150)] string Name, bool IsDefault, List<ClearanceTemplateItemDto> Items);

public record ExitQuestionDto(Guid? Id, [Required, StringLength(500)] string Text, [StringLength(100)] string? Category, ExitQuestionKind Kind);
public record ExitTemplateDto(Guid Id, string Name, bool IsDefault, List<ExitQuestionDto> Questions);
public record SaveExitTemplateRequest([Required, StringLength(150)] string Name, bool IsDefault, List<ExitQuestionDto> Questions);

public record ExitAnswer(Guid QuestionId, string Question, ExitQuestionKind Kind, string? Category, string? Answer);

/// <summary>The extra parts of one separation: levels and owners of clearance items, exit interview questions and panel, settlement approval.</summary>
public record SeparationExtrasDto(Guid SeparationId, List<ClearanceLevelDto> Clearance, Guid? ExitTemplateId, List<ExitAnswer> Answers, List<Guid> InterviewerIds,
    DateTime? InterviewScheduledUtc, string? InterviewNotes, DateTime? SettlementSubmittedAtUtc, DateTime? SettlementApprovedAtUtc, string? SettlementApprovalNote,
    DateTime? SettlementPaidAtUtc, string? SettlementPaidVia);
public record ClearanceLevelDto(Guid ItemId, int Level, Guid? ResponsibleEmployeeId, string? Responsible, bool Open);

public record MyClearanceDto(Guid SeparationId, Guid ItemId, string EmployeeName, string EmployeeCode, DateOnly? LastWorkingDay, string Department, string Title,
    int Level, bool IsCleared, string? Note);

public record ClearMineRequest(bool IsCleared, [StringLength(500)] string? Note);
public record SetClearanceOwnerRequest([Range(1, 10)] int Level, Guid? ResponsibleEmployeeId);
public record ExitPanelRequest(Guid? TemplateId, List<Guid>? InterviewerIds, DateTime? ScheduledUtc, [StringLength(4000)] string? Notes);
public record ExitAnswersRequest(List<ExitAnswerInput> Answers);
public record ExitAnswerInput(Guid QuestionId, [StringLength(2000)] string? Answer);
public record SettlementPaidRequest([StringLength(100)] string? Via);

/// <summary>Final settlement approval (through the FinalSettlement approval chain); also used by the approvals inbox.</summary>
public class SettlementApprovalService(HrmsDbContext db, TenantClock clock, ApprovalEngine approvals, SeparationService separations)
{
    public async Task SubmitAsync(Guid id)
    {
        var s = await separations.LoadAsync(id);
        if (s.Status != SeparationStatus.InProgress) throw new DomainException("Only an open separation's settlement can be sent for approval.");
        if (s.SettlementSavedAtUtc is null) throw new DomainException("Save the settlement first.");
        if (s.SettlementSubmittedAtUtc is not null && s.SettlementApprovedAtUtc is null) throw new DomainException("It is already waiting for approval.");
        var net = (await separations.SettlementAsync(s)).Result.Net;
        var name = await db.Employees.Where(e => e.Id == s.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        s.SettlementSubmittedAtUtc = clock.UtcNow;
        s.SettlementApprovedAtUtc = null;
        s.SettlementApprovalNote = null;
        var approval = await approvals.StartAsync(ApprovalRequestType.FinalSettlement, s.Id, s.EmployeeId, $"Final settlement for {name}: Rs {net:N0}");
        if (approval.Status == ApprovalStatus.Approved)
            s.SettlementApprovedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var s = await separations.LoadAsync(id);
        if (s.SettlementSubmittedAtUtc is null || s.SettlementApprovedAtUtc is not null) throw new DomainException("This settlement is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.FinalSettlement, s.Id, s.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
        {
            s.SettlementApprovedAtUtc = clock.UtcNow;
            s.SettlementApprovalNote = note;
        }
        else if (outcome == ApprovalStatus.Rejected)
        {
            s.SettlementSubmittedAtUtc = null;
            s.SettlementApprovalNote = note;
        }
        await db.SaveChangesAsync();
    }
}

[ApiController]
[Route("api/separation-setup")]
[Authorize(Policy = Policies.CompanyUser)]
public class SeparationSetupController(HrmsDbContext db, SeparationService separations, SettlementApprovalService settlements, TenantClock clock, ICurrentUser me)
    : ControllerBase
{
    // ---------- Clearance templates ----------

    [HttpGet("clearance-templates"), Authorize(Policy = Policies.Hr)]
    public async Task<List<ClearanceTemplateDto>> ClearanceTemplates() =>
        (await db.ClearanceTemplates.Include(t => t.Items).OrderBy(t => t.Name).ToListAsync())
        .Select(t => new ClearanceTemplateDto(t.Id, t.Name, t.IsDefault,
            t.Items.OrderBy(i => i.Level).ThenBy(i => i.Order).Select(i => new ClearanceTemplateItemDto(i.Department, i.Title, i.Level, i.ResponsibleEmployeeId)).ToList()))
        .ToList();

    [HttpPost("clearance-templates"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddClearanceTemplate(SaveClearanceTemplateRequest req)
    {
        var t = new ClearanceTemplate();
        db.ClearanceTemplates.Add(t);
        await ApplyAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("clearance-templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateClearanceTemplate(Guid id, SaveClearanceTemplateRequest req)
    {
        var t = await db.ClearanceTemplates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        await ApplyAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("clearance-templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteClearanceTemplate(Guid id)
    {
        db.ClearanceTemplates.Remove(await db.ClearanceTemplates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(ClearanceTemplate t, SaveClearanceTemplateRequest req)
    {
        var items = req.Items ?? [];
        if (items.Count is 0 or > 60) throw new DomainException("A template needs between 1 and 60 items.");
        var owners = items.Where(i => i.ResponsibleEmployeeId != null).Select(i => i.ResponsibleEmployeeId!.Value).Distinct().ToList();
        if (await db.Employees.CountAsync(e => owners.Contains(e.Id)) != owners.Count) throw new NotFoundException("Employee");
        t.Name = req.Name.Trim();
        if (req.IsDefault && !t.IsDefault)
            await db.ClearanceTemplates.Where(x => x.IsDefault && x.Id != t.Id).ForEachAsync(x => x.IsDefault = false);
        t.IsDefault = req.IsDefault;
        db.ClearanceTemplateItems.RemoveRange(t.Items);
        var order = 0;
        foreach (var i in items)
            db.ClearanceTemplateItems.Add(new ClearanceTemplateItem
            {
                TemplateId = t.Id, Department = i.Department.Trim(), Title = i.Title.Trim(), Level = Math.Clamp(i.Level, 1, 10), ResponsibleEmployeeId = i.ResponsibleEmployeeId,
                Order = ++order,
            });
    }

    // ---------- Exit interview templates ----------

    [HttpGet("exit-templates")]
    public async Task<List<ExitTemplateDto>> ExitTemplates() =>
        (await db.ExitInterviewTemplates.Include(t => t.Questions).OrderBy(t => t.Name).ToListAsync())
        .Select(t => new ExitTemplateDto(t.Id, t.Name, t.IsDefault,
            t.Questions.OrderBy(q => q.Order).Select(q => new ExitQuestionDto(q.Id, q.Text, q.Category, q.Kind)).ToList()))
        .ToList();

    [HttpPost("exit-templates"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddExitTemplate(SaveExitTemplateRequest req)
    {
        var t = new ExitInterviewTemplate();
        db.ExitInterviewTemplates.Add(t);
        await ApplyAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("exit-templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateExitTemplate(Guid id, SaveExitTemplateRequest req)
    {
        var t = await db.ExitInterviewTemplates.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        await ApplyAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("exit-templates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteExitTemplate(Guid id)
    {
        if (await db.Separations.AnyAsync(s => s.ExitTemplateId == id)) throw new DomainException("Exit interviews used this template.");
        db.ExitInterviewTemplates.Remove(await db.ExitInterviewTemplates.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(ExitInterviewTemplate t, SaveExitTemplateRequest req)
    {
        var qs = req.Questions ?? [];
        if (qs.Count is 0 or > 50) throw new DomainException("A template needs between 1 and 50 questions.");
        t.Name = req.Name.Trim();
        if (req.IsDefault && !t.IsDefault)
            await db.ExitInterviewTemplates.Where(x => x.IsDefault && x.Id != t.Id).ForEachAsync(x => x.IsDefault = false);
        t.IsDefault = req.IsDefault;
        // Keep question ids that stay, so earlier answers still line up.
        var keep = qs.Where(q => q.Id != null).Select(q => q.Id!.Value).ToHashSet();
        db.ExitQuestions.RemoveRange(t.Questions.Where(q => !keep.Contains(q.Id)));
        var order = 0;
        foreach (var q in qs)
        {
            var existing = q.Id is { } qid ? t.Questions.FirstOrDefault(x => x.Id == qid) : null;
            if (existing is null)
                db.ExitQuestions.Add(new ExitQuestion { TemplateId = t.Id, Text = q.Text.Trim(), Category = q.Category?.Trim(), Kind = q.Kind, Order = ++order });
            else
            {
                existing.Text = q.Text.Trim();
                existing.Category = q.Category?.Trim();
                existing.Kind = q.Kind;
                existing.Order = ++order;
            }
        }
    }

    // ---------- One separation ----------

    [HttpGet("separations/{id:guid}")]
    public async Task<SeparationExtrasDto> Extras(Guid id)
    {
        var s = await separations.LoadAsync(id);
        var hr = me.IsHr();
        var panel = s.ExitInterviewerIds is { } p ? HelpdeskService.Ids(p) : [];
        if (!hr && s.EmployeeId != me.EmployeeId && !panel.Contains(me.EmployeeId ?? Guid.Empty))
            throw new NotFoundException("Separation");
        var owners = s.Clearance.Where(c => c.ResponsibleEmployeeId != null).Select(c => c.ResponsibleEmployeeId!.Value).Distinct().ToList();
        var names = await db.Employees.Where(e => owners.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        var answers = await AnswersAsync(s);
        return new SeparationExtrasDto(s.Id,
            s.Clearance.OrderBy(c => c.Level).ThenBy(c => c.SortOrder).Select(c => new ClearanceLevelDto(c.Id, c.Level, c.ResponsibleEmployeeId,
                c.ResponsibleEmployeeId is { } r ? names.GetValueOrDefault(r) : null, !s.Clearance.Any(x => x.Level < c.Level && !x.IsCleared))).ToList(),
            s.ExitTemplateId, answers, panel, s.ExitInterviewScheduledUtc, hr || panel.Contains(me.EmployeeId ?? Guid.Empty) ? s.ExitInterviewNotes : null,
            s.SettlementSubmittedAtUtc, s.SettlementApprovedAtUtc, s.SettlementApprovalNote, s.SettlementPaidAtUtc, s.SettlementPaidVia);
    }

    /// <summary>The template's questions with any answers given; the default template when none was chosen.</summary>
    private async Task<List<ExitAnswer>> AnswersAsync(Separation s)
    {
        var given = s.ExitAnswersJson is { } j ? JsonSerializer.Deserialize<List<ExitAnswer>>(j) ?? [] : [];
        var templateId = s.ExitTemplateId ?? await db.ExitInterviewTemplates.Where(t => t.IsDefault).Select(t => (Guid?)t.Id).FirstOrDefaultAsync();
        if (templateId is null) return given;
        var questions = await db.ExitQuestions.Where(q => q.TemplateId == templateId).OrderBy(q => q.Order).ToListAsync();
        return questions.Select(q => new ExitAnswer(q.Id, q.Text, q.Kind, q.Category, given.FirstOrDefault(a => a.QuestionId == q.Id)?.Answer))
            .Concat(given.Where(a => questions.All(q => q.Id != a.QuestionId))).ToList();
    }

    /// <summary>The leaving employee (or a panel member, or HR) answers the template questions.</summary>
    [HttpPut("separations/{id:guid}/answers")]
    public async Task<IActionResult> Answer(Guid id, ExitAnswersRequest req)
    {
        var s = await separations.LoadAsync(id);
        var panel = s.ExitInterviewerIds is { } p ? HelpdeskService.Ids(p) : [];
        if (s.EmployeeId != me.EmployeeId && !me.IsHr() && !panel.Contains(me.EmployeeId ?? Guid.Empty))
            throw new ForbiddenException("Only the leaving employee, the interview panel or HR answer these.");
        if (s.Status != SeparationStatus.InProgress) throw new DomainException("The exit interview is open while the separation is in progress.");
        var questions = await AnswersAsync(s);
        foreach (var a in req.Answers ?? [])
        {
            var q = questions.FirstOrDefault(x => x.QuestionId == a.QuestionId) ?? throw new NotFoundException("Question");
            if (q.Kind == ExitQuestionKind.Rating && a.Answer is { Length: > 0 } r && (!int.TryParse(r, out var n) || n is < 1 or > 5))
                throw new DomainException("Ratings are 1 to 5.");
        }
        var merged = questions.Select(q => q with { Answer = req.Answers?.FirstOrDefault(a => a.QuestionId == q.QuestionId)?.Answer?.Trim() ?? q.Answer }).ToList();
        s.ExitAnswersJson = JsonSerializer.Serialize(merged);
        s.ExitTemplateId ??= await db.ExitInterviewTemplates.Where(t => t.IsDefault).Select(t => (Guid?)t.Id).FirstOrDefaultAsync();
        s.ExitInterviewAtUtc ??= clock.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>HR picks the questions, the panel and the time; the panel keeps notes.</summary>
    [HttpPut("separations/{id:guid}/panel")]
    public async Task<IActionResult> Panel(Guid id, ExitPanelRequest req)
    {
        var s = await separations.LoadAsync(id);
        var panel = s.ExitInterviewerIds is { } p ? HelpdeskService.Ids(p) : [];
        if (!me.IsHr())
        {
            if (!panel.Contains(me.EmployeeId ?? Guid.Empty)) throw new ForbiddenException("Only HR or the panel.");
            s.ExitInterviewNotes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
            await db.SaveChangesAsync();
            return NoContent();
        }
        if (req.TemplateId is { } t && !await db.ExitInterviewTemplates.AnyAsync(x => x.Id == t)) throw new NotFoundException("Template");
        var ids = (req.InterviewerIds ?? []).Distinct().ToList();
        if (await db.Employees.CountAsync(e => ids.Contains(e.Id)) != ids.Count) throw new NotFoundException("Employee");
        s.ExitTemplateId = req.TemplateId;
        s.ExitInterviewerIds = ids.Count > 0 ? string.Join(',', ids) : null;
        s.ExitInterviewScheduledUtc = req.ScheduledUtc;
        s.ExitInterviewNotes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("separations/{id:guid}/clearance/{itemId:guid}/owner"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SetOwner(Guid id, Guid itemId, SetClearanceOwnerRequest req)
    {
        var s = await separations.LoadAsync(id);
        var item = s.Clearance.FirstOrDefault(c => c.Id == itemId) ?? throw new NotFoundException("Checklist item");
        if (req.ResponsibleEmployeeId is { } r && !await db.Employees.AnyAsync(e => e.Id == r)) throw new NotFoundException("Employee");
        item.Level = req.Level;
        item.ResponsibleEmployeeId = req.ResponsibleEmployeeId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Clearance items waiting for the signed-in person (their level is open).</summary>
    [HttpGet("my-clearances")]
    public async Task<List<MyClearanceDto>> MyClearances()
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        var open = await db.Separations.Include(s => s.Clearance).Include(s => s.Employee)
            .Where(s => s.Status == SeparationStatus.InProgress && s.Clearance.Any(c => c.ResponsibleEmployeeId == mine)).ToListAsync();
        return open.SelectMany(s => s.Clearance.Where(c => c.ResponsibleEmployeeId == mine && !s.Clearance.Any(x => x.Level < c.Level && !x.IsCleared))
                .Select(c => new MyClearanceDto(s.Id, c.Id, s.Employee!.FullName, s.Employee.EmployeeCode, s.LastWorkingDay, c.Department, c.Title, c.Level, c.IsCleared, c.Note)))
            .OrderBy(x => x.LastWorkingDay).ToList();
    }

    [HttpPut("separations/{id:guid}/clearance/{itemId:guid}/mine")]
    public async Task<IActionResult> ClearMine(Guid id, Guid itemId, ClearMineRequest req)
    {
        var s = await separations.LoadAsync(id);
        if (s.Status != SeparationStatus.InProgress) throw new DomainException("This separation is not in progress.");
        var item = s.Clearance.FirstOrDefault(c => c.Id == itemId) ?? throw new NotFoundException("Checklist item");
        if (item.ResponsibleEmployeeId != me.EmployeeId) throw new ForbiddenException("This item is not yours to clear.");
        await separations.SetClearedAsync(s, item, req.IsCleared, req.Note);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Settlement approval and payment ----------

    [HttpPost("separations/{id:guid}/settlement/submit"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Submit(Guid id)
    {
        await settlements.SubmitAsync(id);
        return NoContent();
    }

    [HttpPost("separations/{id:guid}/settlement/paid"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Paid(Guid id, SettlementPaidRequest req)
    {
        var s = await separations.LoadAsync(id);
        if (s.Status != SeparationStatus.Completed) throw new DomainException("Complete the separation first.");
        s.SettlementPaidAtUtc = clock.UtcNow;
        s.SettlementPaidVia = string.IsNullOrWhiteSpace(req.Via) ? "Bank transfer" : req.Via.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }
}
