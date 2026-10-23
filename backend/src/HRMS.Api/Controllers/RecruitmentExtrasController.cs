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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record InterviewQuestionDto(Guid? Id, [Required, StringLength(500)] string Text, [StringLength(100)] string? Category, [Range(1, 100)] int MaxMarks);
public record InterviewTemplateDto(Guid Id, string Name, List<InterviewQuestionDto> Questions);
public record SaveInterviewTemplateRequest([Required, StringLength(150)] string Name, List<InterviewQuestionDto> Questions);

public record QuestionMark(Guid QuestionId, int Marks);
public record ScorecardRow(Guid InterviewerEmployeeId, string Interviewer, List<QuestionMark> Marks, int Total, int Max);
public record ScorecardDto(Guid InterviewId, Guid? TemplateId, List<InterviewQuestionDto> Questions, List<QuestionMark> Mine, List<ScorecardRow> All, bool CanSeeAll);
public record SaveMarksRequest(List<QuestionMark> Marks);
public record SetInterviewTemplateRequest(Guid? TemplateId);

public record ScreeningCriteriaDto([Range(0, 60)] decimal? MinExperienceYears, [Range(0, 100_000_000)] decimal? MaxExpectedSalary, [StringLength(500)] string? Cities,
    [StringLength(1000)] string? Keywords);
public record ScreeningScoreDto(Guid ApplicationId, Guid CandidateId, string Candidate, ApplicationStage Stage, int Score, List<string> Reasons);

public record PortalApplicationDto(Guid Id, string Job, string Stage, DateTime AppliedAtUtc, List<PortalInterviewDto> Interviews, decimal? OfferedSalary,
    DateOnly? OfferedJoiningDate, OfferStatus? OfferStatus, bool HasOfferLetter, bool CanWithdraw);
public record PortalInterviewDto(string Title, DateTime StartsAtUtc, int DurationMinutes, InterviewMode Mode, string? Where, InterviewStatus Status);
public record PortalDto(string Company, string Name, string Email, List<PortalApplicationDto> Applications);
public record PortalOfferRequest(bool Accepted, [StringLength(500)] string? Note);

/// <summary>Interview templates and scorecards, and shortlisting scores for a job.</summary>
[ApiController]
[Route("api/recruitment-extras")]
[Authorize(Policy = Policies.CompanyUser)]
public class RecruitmentExtrasController(HrmsDbContext db, RecruitmentService hiring, ICurrentUser me) : ControllerBase
{
    // ---------- Interview templates ----------

    [HttpGet("interview-templates")]
    public async Task<List<InterviewTemplateDto>> Templates() =>
        (await db.InterviewTemplates.Include(t => t.Questions).OrderBy(t => t.Name).ToListAsync())
        .Select(t => new InterviewTemplateDto(t.Id, t.Name, t.Questions.OrderBy(q => q.Order).Select(q => new InterviewQuestionDto(q.Id, q.Text, q.Category, q.MaxMarks)).ToList()))
        .ToList();

    [HttpPost("interview-templates")]
    public async Task<IActionResult> AddTemplate(SaveInterviewTemplateRequest req)
    {
        await RequireRecruiterAsync();
        var t = new InterviewTemplate();
        db.InterviewTemplates.Add(t);
        Apply(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("interview-templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, SaveInterviewTemplateRequest req)
    {
        await RequireRecruiterAsync();
        var t = await db.InterviewTemplates.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template");
        Apply(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("interview-templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        await RequireRecruiterAsync();
        if (await db.Interviews.AnyAsync(i => i.TemplateId == id)) throw new DomainException("Interviews use this template.");
        db.InterviewTemplates.Remove(await db.InterviewTemplates.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Template"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    private void Apply(InterviewTemplate t, SaveInterviewTemplateRequest req)
    {
        var qs = req.Questions ?? [];
        if (qs.Count is 0 or > 40) throw new DomainException("A template needs between 1 and 40 questions.");
        t.Name = req.Name.Trim();
        var keep = qs.Where(q => q.Id != null).Select(q => q.Id!.Value).ToHashSet();
        db.InterviewQuestions.RemoveRange(t.Questions.Where(q => !keep.Contains(q.Id)));
        var order = 0;
        foreach (var q in qs)
        {
            var existing = q.Id is { } id ? t.Questions.FirstOrDefault(x => x.Id == id) : null;
            if (existing is null)
                db.InterviewQuestions.Add(new InterviewQuestion { TemplateId = t.Id, Text = q.Text.Trim(), Category = q.Category?.Trim(), MaxMarks = q.MaxMarks, Order = ++order });
            else
            {
                existing.Text = q.Text.Trim();
                existing.Category = q.Category?.Trim();
                existing.MaxMarks = q.MaxMarks;
                existing.Order = ++order;
            }
        }
    }

    private async Task RequireRecruiterAsync()
    {
        if (!await hiring.IsRecruiterAsync()) throw new ForbiddenException("Only HR and recruiters manage interview templates.");
    }

    // ---------- Scorecards ----------

    [HttpPut("interviews/{id:guid}/template")]
    public async Task<IActionResult> SetTemplate(Guid id, SetInterviewTemplateRequest req)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == id) ?? throw new NotFoundException("Interview");
        var (_, canRun) = await hiring.LoadAsync(interview.ApplicationId);
        if (!canRun) throw new ForbiddenException("Only the recruiter or HR.");
        if (req.TemplateId is { } t && !await db.InterviewTemplates.AnyAsync(x => x.Id == t)) throw new NotFoundException("Template");
        if (await db.InterviewFeedback.AnyAsync(f => f.InterviewId == id && f.MarksJson != null)) throw new DomainException("Marks are already given against the current template.");
        interview.TemplateId = req.TemplateId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>The questions and marks. Interviewers see everyone's marks once they have given their own (as with feedback).</summary>
    [HttpGet("interviews/{id:guid}/scorecard")]
    public async Task<ScorecardDto> Scorecard(Guid id)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == id) ?? throw new NotFoundException("Interview");
        var (_, canRun) = await hiring.LoadAsync(interview.ApplicationId);
        var interviewers = HelpdeskService.Ids(interview.InterviewerIds);
        var mineId = me.EmployeeId ?? Guid.Empty;
        if (!canRun && !interviewers.Contains(mineId)) throw new NotFoundException("Interview");
        var questions = interview.TemplateId is { } t
            ? await db.InterviewQuestions.Where(q => q.TemplateId == t).OrderBy(q => q.Order).Select(q => new InterviewQuestionDto(q.Id, q.Text, q.Category, q.MaxMarks)).ToListAsync()
            : [];
        var feedback = await db.InterviewFeedback.Where(f => f.InterviewId == id && f.MarksJson != null).ToListAsync();
        var names = await db.Employees.Where(e => interviewers.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        List<QuestionMark> Parse(string? j) => j is null ? [] : JsonSerializer.Deserialize<List<QuestionMark>>(j) ?? [];
        var mine = Parse(feedback.FirstOrDefault(f => f.InterviewerEmployeeId == mineId)?.MarksJson);
        var canSeeAll = canRun || mine.Count > 0;
        var all = canSeeAll
            ? feedback.Select(f => new ScorecardRow(f.InterviewerEmployeeId, names.GetValueOrDefault(f.InterviewerEmployeeId) ?? "", Parse(f.MarksJson), f.TotalMarks ?? 0, f.MaxMarks ?? 0)).ToList()
            : [];
        return new ScorecardDto(id, interview.TemplateId, questions, mine, all, canSeeAll);
    }

    [HttpPut("interviews/{id:guid}/marks")]
    public async Task<IActionResult> SaveMarks(Guid id, SaveMarksRequest req)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == id) ?? throw new NotFoundException("Interview");
        var mine = me.EmployeeId ?? throw new ForbiddenException("Only the interviewers give marks.");
        if (!HelpdeskService.Ids(interview.InterviewerIds).Contains(mine)) throw new ForbiddenException("Only the interviewers give marks.");
        if (interview.TemplateId is not { } t) throw new DomainException("This interview has no scorecard.");
        var questions = await db.InterviewQuestions.Where(q => q.TemplateId == t).ToListAsync();
        var marks = (req.Marks ?? []).GroupBy(m => m.QuestionId).Select(g => g.First()).ToList();
        foreach (var m in marks)
        {
            var q = questions.FirstOrDefault(x => x.Id == m.QuestionId) ?? throw new NotFoundException("Question");
            if (m.Marks < 0 || m.Marks > q.MaxMarks) throw new DomainException($"\"{q.Text}\" is marked out of {q.MaxMarks}.");
        }
        var feedback = await db.InterviewFeedback.FirstOrDefaultAsync(f => f.InterviewId == id && f.InterviewerEmployeeId == mine);
        if (feedback is null)
        {
            // Marks first: the 1–5 rating follows from the share of marks until the interviewer gives their verdict.
            feedback = new InterviewFeedback { InterviewId = id, InterviewerEmployeeId = mine, Recommendation = HireRecommendation.Yes };
            db.InterviewFeedback.Add(feedback);
        }
        var total = marks.Sum(m => m.Marks);
        var max = questions.Sum(q => q.MaxMarks);
        feedback.MarksJson = JsonSerializer.Serialize(marks);
        feedback.TotalMarks = total;
        feedback.MaxMarks = max;
        if (feedback.Rating == 0)
            feedback.Rating = max == 0 ? 3 : Math.Clamp((int)Math.Round(total * 5m / max), 1, 5);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Shortlisting score ----------

    [HttpGet("posts/{id:guid}/criteria")]
    public async Task<ScreeningCriteriaDto> Criteria(Guid id)
    {
        var p = await db.JobPosts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Job");
        return new ScreeningCriteriaDto(p.ScreenMinExperienceYears, p.ScreenMaxExpectedSalary, p.ScreenCities, p.ScreenKeywords);
    }

    [HttpPut("posts/{id:guid}/criteria")]
    public async Task<IActionResult> SaveCriteria(Guid id, ScreeningCriteriaDto req)
    {
        var p = await db.JobPosts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Job");
        if (!me.IsHr() && p.RecruiterEmployeeId != me.EmployeeId && p.HiringManagerEmployeeId != me.EmployeeId)
            throw new ForbiddenException("Only HR, the recruiter or the hiring manager.");
        p.ScreenMinExperienceYears = req.MinExperienceYears;
        p.ScreenMaxExpectedSalary = req.MaxExpectedSalary;
        p.ScreenCities = string.IsNullOrWhiteSpace(req.Cities) ? null : req.Cities.Trim();
        p.ScreenKeywords = string.IsNullOrWhiteSpace(req.Keywords) ? null : req.Keywords.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// 0–100 per application: experience 30, expected salary 20, city 10, keywords 40 (by share found). Criteria not set
    /// give their points to everyone, so only what matters to this job separates people.
    /// </summary>
    [HttpGet("posts/{id:guid}/scores")]
    public async Task<List<ScreeningScoreDto>> Scores(Guid id)
    {
        var p = await db.JobPosts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Job");
        if (!me.IsHr() && p.RecruiterEmployeeId != me.EmployeeId && p.HiringManagerEmployeeId != me.EmployeeId)
            throw new ForbiddenException("Only HR, the recruiter or the hiring manager.");
        var apps = await db.JobApplications.Include(a => a.Candidate).Where(a => a.JobPostId == id).ToListAsync();
        var cities = Split(p.ScreenCities);
        var keywords = Split(p.ScreenKeywords);
        return apps.Select(a =>
        {
            var c = a.Candidate!;
            var reasons = new List<string>();
            var score = 0;
            if (p.ScreenMinExperienceYears is { } min)
            {
                if (c.ExperienceYears is { } y && y >= min) { score += 30; reasons.Add($"{y:0.#} years' experience"); }
                else reasons.Add(c.ExperienceYears is null ? "Experience unknown" : $"Only {c.ExperienceYears:0.#} years");
            }
            else score += 30;
            if (p.ScreenMaxExpectedSalary is { } max)
            {
                if (c.ExpectedSalary is { } s && s <= max) { score += 20; reasons.Add("Salary within budget"); }
                else reasons.Add(c.ExpectedSalary is null ? "Expected salary unknown" : $"Expects PKR {c.ExpectedSalary:N0}");
            }
            else score += 20;
            if (cities.Count > 0)
            {
                if (c.City is { } city && cities.Any(x => x.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase))) { score += 10; reasons.Add($"Based in {city}"); }
            }
            else score += 10;
            if (keywords.Count > 0)
            {
                var text = $"{c.Skills} {c.CurrentTitle} {c.CurrentCompany} {a.CoverLetter}".ToLowerInvariant();
                var found = keywords.Where(k => text.Contains(k.ToLowerInvariant())).ToList();
                score += (int)Math.Round(40m * found.Count / keywords.Count);
                if (found.Count > 0) reasons.Add($"Mentions {string.Join(", ", found)}");
            }
            else score += 40;
            return new ScreeningScoreDto(a.Id, c.Id, c.FullName, a.Stage, score, reasons);
        }).OrderByDescending(x => x.Score).ToList();
    }

    private static List<string> Split(string? csv) =>
        (csv ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

/// <summary>The candidate's own status page: their applications, interviews and offer. Reached by the secret link in their emails.</summary>
[ApiController]
[Route("api/careers/portal")]
[AllowAnonymous]
[EnableRateLimiting("careers")]
public class CandidatePortalController(HrmsDbContext db, RecruitmentService hiring) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<PortalDto> Get(string token)
    {
        var c = await CandidateAsync(token);
        var company = await db.Tenants.Where(t => t.Id == c.TenantId).Select(t => t.Name).FirstAsync();
        var apps = await db.JobApplications.Include(a => a.JobPost).Where(a => a.CandidateId == c.Id).OrderByDescending(a => a.CreatedAtUtc).ToListAsync();
        var ids = apps.Select(a => a.Id).ToList();
        var interviews = (await db.Interviews.Where(i => ids.Contains(i.ApplicationId) && i.Status != InterviewStatus.Cancelled).OrderBy(i => i.StartsAtUtc).ToListAsync())
            .ToLookup(i => i.ApplicationId);
        var letters = await db.CandidateLetters.Where(l => ids.Contains(l.ApplicationId)).Select(l => l.ApplicationId).ToListAsync();
        return new PortalDto(company, c.FullName, c.Email, apps.Select(a => new PortalApplicationDto(a.Id, a.JobPost!.Title, PublicStage(a.Stage), a.CreatedAtUtc,
            interviews[a.Id].Select(i => new PortalInterviewDto(i.Title, i.StartsAtUtc, i.DurationMinutes, i.Mode, i.Where, i.Status)).ToList(),
            a.OfferStatus is null ? null : a.OfferedSalary, a.OfferStatus is null ? null : a.OfferedJoiningDate, a.OfferStatus, letters.Contains(a.Id),
            a.Stage is not (ApplicationStage.Hired or ApplicationStage.Rejected or ApplicationStage.Withdrawn))).ToList());
    }

    [HttpGet("{token}/offer-letter/{applicationId:guid}")]
    public async Task<CandidateLetterDto> OfferLetter(string token, Guid applicationId)
    {
        var c = await CandidateAsync(token);
        if (!await db.JobApplications.AnyAsync(a => a.Id == applicationId && a.CandidateId == c.Id)) throw new NotFoundException("Letter");
        var l = await db.CandidateLetters.FirstOrDefaultAsync(x => x.ApplicationId == applicationId) ?? throw new NotFoundException("Letter");
        var company = await db.Tenants.Where(t => t.Id == c.TenantId).Select(t => new { t.Name, t.Address, t.City, t.Phone }).FirstAsync();
        return new CandidateLetterDto(l.Id, l.ApplicationId, l.CandidateName, l.ReferenceNo, l.Subject, l.Body, l.IssuedOn, l.IssuedByName, l.MarginTopMm, l.MarginSideMm,
            l.Footer, company.Name, LetterService.FullAddress(company.Address, company.City), company.Phone);
    }

    [HttpPost("{token}/offer/{applicationId:guid}")]
    public async Task<IActionResult> RespondToOffer(string token, Guid applicationId, PortalOfferRequest req)
    {
        var c = await CandidateAsync(token);
        var app = await db.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId && a.CandidateId == c.Id) ?? throw new NotFoundException("Application");
        if (app.OfferStatus != OfferStatus.Sent) throw new DomainException("There is no open offer to answer.");
        await hiring.OfferResponseFromCandidateAsync(app.Id, req.Accepted, req.Note);
        return NoContent();
    }

    [HttpPost("{token}/withdraw/{applicationId:guid}")]
    public async Task<IActionResult> Withdraw(string token, Guid applicationId)
    {
        var c = await CandidateAsync(token);
        var app = await db.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId && a.CandidateId == c.Id) ?? throw new NotFoundException("Application");
        if (app.Stage is ApplicationStage.Hired or ApplicationStage.Rejected or ApplicationStage.Withdrawn) throw new DomainException("This application is closed.");
        await hiring.WithdrawByCandidateAsync(app.Id);
        return NoContent();
    }

    private async Task<Candidate> CandidateAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20) throw new NotFoundException("Page");
        var c = await db.Candidates.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.PortalToken == token) ?? throw new NotFoundException("Page");
        db.UseTenant(c.TenantId);
        return c;
    }

    /// <summary>What the candidate sees: no internal stages like "manager shortlist".</summary>
    private static string PublicStage(ApplicationStage s) => s switch
    {
        ApplicationStage.Applied => "Received",
        ApplicationStage.Interview => "Interviewing",
        ApplicationStage.Offer => "Offer made",
        ApplicationStage.Hired => "Hired",
        ApplicationStage.Rejected => "Not taken forward",
        ApplicationStage.Withdrawn => "Withdrawn",
        _ => "In review",
    };
}
