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

public record RecruitmentMeDto(bool IsHr, bool RunsJobs, bool CanRequest, int MyInterviews, string? CareersSlug);

public record DecideJobRequest([StringLength(1000)] string? Note);

public record CareersSlugRequest([Required, StringLength(60)] string Slug);

public record SaveJobRequestRequest(
    [Required, StringLength(150)] string Title, Guid? DepartmentId, Guid? DesignationId, int Positions, Guid? ReplacesEmployeeId, decimal? SalaryMin,
    decimal? SalaryMax, int? ExperienceMinYears, [StringLength(300)] string? Qualification, [StringLength(1000)] string? Skills, DateOnly? NeededBy,
    [Required, StringLength(2000)] string Reason);

public record JobRequestDto(
    Guid Id, string Title, Guid? DepartmentId, string? Department, Guid? DesignationId, string? Designation, int Positions, string? Replaces,
    decimal? SalaryMin, decimal? SalaryMax, int? ExperienceMinYears, string? Qualification, string? Skills, DateOnly? NeededBy, string Reason,
    RequestStatus Status, string RequestedBy, Guid RequestedByEmployeeId, DateTime CreatedAtUtc, string? DecisionNote, Guid? JobPostId);

public record SaveJobPostRequest(
    [Required, StringLength(150)] string Title, Guid? JobRequestId, Guid? DepartmentId, Guid? DesignationId, [StringLength(150)] string? Location,
    EmploymentType EmploymentType, [Required, StringLength(8000)] string Description, [StringLength(8000)] string? Requirements, decimal? SalaryMin,
    decimal? SalaryMax, bool ShowSalary, int Positions, DateOnly? ClosesOn, Guid? RecruiterEmployeeId, Guid? HiringManagerEmployeeId);

public record JobPostStatusRequest(JobPostStatus Status);

public record JobPostDto(
    Guid Id, string Title, string Slug, Guid? JobRequestId, Guid? DepartmentId, string? Department, Guid? DesignationId, string? Designation,
    string? Location, EmploymentType EmploymentType, string Description, string? Requirements, decimal? SalaryMin, decimal? SalaryMax, bool ShowSalary,
    int Positions, DateOnly? ClosesOn, JobPostStatus Status, Guid? RecruiterEmployeeId, string? Recruiter, Guid? HiringManagerEmployeeId,
    string? HiringManager, DateTime? PublishedAtUtc, DateTime CreatedAtUtc, Dictionary<string, int> ByStage, int Total, bool CanEdit);

public record PipelineCardDto(
    Guid Id, Guid CandidateId, string Name, string Email, string? CurrentTitle, decimal? ExperienceYears, CandidateSource Source, ApplicationStage Stage,
    int? Rating, bool IsBlacklisted, int Interviews, decimal? AverageFeedback, OfferStatus? OfferStatus, DateTime AppliedAtUtc, DateTime? StageChangedAtUtc);

public record OpeningDto(Guid Id, string Title, string? Department, string? Location, EmploymentType EmploymentType, string Description, string? Requirements, DateOnly? ClosesOn, string Slug);

public class CandidateForm
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [StringLength(100)] public string? LastName { get; set; }
    [Required, StringLength(256)] public string Email { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(15)] public string? Cnic { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(150)] public string? CurrentTitle { get; set; }
    [StringLength(150)] public string? CurrentCompany { get; set; }
    public decimal? ExperienceYears { get; set; }
    public decimal? ExpectedSalary { get; set; }
    public int? NoticeDays { get; set; }
    [StringLength(300)] public string? LinkedInUrl { get; set; }
    [StringLength(1000)] public string? Skills { get; set; }
    [StringLength(4000)] public string? CoverLetter { get; set; }
    public CandidateSource? Source { get; set; }
    [StringLength(150)] public string? ReferredBy { get; set; }
    public IFormFile? Cv { get; set; }

    public CandidateInput ToInput() => new(FirstName, LastName ?? "", Email, Phone, Cnic, City, CurrentTitle, CurrentCompany, ExperienceYears,
        ExpectedSalary, NoticeDays, LinkedInUrl, Skills, ReferredBy);

    public async Task<CvFile?> ReadCvAsync()
    {
        if (Cv is null || Cv.Length == 0)
            return null;
        if (Cv.Length > AttachmentService.MaxBytes)
            throw new DomainException("The CV must be smaller than 5 MB.");
        using var buffer = new MemoryStream();
        await Cv.CopyToAsync(buffer);
        return new CvFile(Cv.FileName, buffer.ToArray());
    }
}

public record CvUploadForm
{
    [Required] public IFormFile File { get; set; } = null!;
}

public record SaveCandidateRequest(
    [Required, StringLength(100)] string FirstName, [StringLength(100)] string? LastName, [Required, StringLength(256)] string Email, [StringLength(30)] string? Phone,
    [StringLength(15)] string? Cnic, [StringLength(100)] string? City, [StringLength(150)] string? CurrentTitle, [StringLength(150)] string? CurrentCompany,
    decimal? ExperienceYears, decimal? ExpectedSalary, int? NoticeDays, [StringLength(300)] string? LinkedInUrl, [StringLength(1000)] string? Skills,
    CandidateSource Source, [StringLength(150)] string? ReferredBy);

public record CandidateListItemDto(
    Guid Id, string Name, string Email, string? Phone, string? City, string? CurrentTitle, decimal? ExperienceYears, CandidateSource Source,
    bool IsBlacklisted, int Applications, string? LatestJob, ApplicationStage? LatestStage, DateTime CreatedAtUtc, bool Hired);

public record CandidateApplicationDto(Guid Id, Guid JobPostId, string Job, ApplicationStage Stage, DateTime AppliedAtUtc);

public record CandidateDto(
    Guid Id, string FirstName, string LastName, string Email, string? Phone, string? Cnic, string? City, string? CurrentTitle, string? CurrentCompany,
    decimal? ExperienceYears, decimal? ExpectedSalary, int? NoticeDays, string? LinkedInUrl, string? Skills, CandidateSource Source, string? ReferredBy,
    bool IsBlacklisted, string? BlacklistReason, Guid? EmployeeId, DateTime CreatedAtUtc, List<CandidateApplicationDto> Applications,
    List<CandidateListItemDto> PossibleDuplicates);

public record BlacklistRequest(bool On, [StringLength(500)] string? Reason);

public record ApplicationEventDto(Guid Id, string Text, string? ByName, bool IsNote, DateTime CreatedAtUtc);

public record FeedbackDto(Guid InterviewerEmployeeId, string Interviewer, int Rating, HireRecommendation Recommendation, string? Strengths, string? Concerns, DateTime CreatedAtUtc);

public record InterviewDto(
    Guid Id, Guid ApplicationId, int Round, string Title, DateTime StartsAtUtc, int DurationMinutes, InterviewMode Mode, string? Where,
    List<Guid> InterviewerIds, List<string> Interviewers, InterviewStatus Status, string? Notes, List<FeedbackDto> Feedback, int FeedbackHidden,
    bool IAmInterviewer, bool IGaveFeedback);

public record ApplicationDto(
    Guid Id, CandidateDto Candidate, Guid JobPostId, string Job, Guid? HiringManagerEmployeeId, ApplicationStage Stage, string? CoverLetter, int? Rating,
    string? RejectionReason, decimal? OfferedSalary, DateOnly? OfferedJoiningDate, string? OfferNote, OfferStatus? OfferStatus, DateTime AppliedAtUtc,
    List<ApplicationEventDto> Events, List<InterviewDto> Interviews, bool CanRun, bool CanOffer, bool CanHire);

public record MoveRequest(ApplicationStage Stage, [StringLength(500)] string? Reason, bool EmailCandidate);

public record NoteRequest([Required, StringLength(2000)] string Text);

public record RateRequest(int? Rating);

public record ScheduleInterviewRequest(
    [Required, StringLength(150)] string Title, DateTime StartsAtUtc, int DurationMinutes, InterviewMode Mode, [StringLength(500)] string? Where,
    [Required] List<Guid> InterviewerIds, [StringLength(2000)] string? Notes, bool EmailCandidate);

public record InterviewStatusRequest(InterviewStatus Status);

public record FeedbackRequest(int Rating, HireRecommendation Recommendation, [StringLength(2000)] string? Strengths, [StringLength(2000)] string? Concerns);

public record OfferRequest(decimal Salary, DateOnly JoiningDate, [StringLength(2000)] string? Note, bool EmailCandidate);

public record OfferResponseRequest(bool Accepted, [StringLength(500)] string? Note);

public record HireRequest([StringLength(20)] string? EmployeeCode);

public record HiredDto(Guid EmployeeId, string EmployeeCode);

public record MyInterviewDto(
    Guid Id, Guid ApplicationId, string Candidate, string Job, string Title, int Round, DateTime StartsAtUtc, int DurationMinutes, InterviewMode Mode,
    string? Where, InterviewStatus Status, bool FeedbackGiven);

public record FunnelRowDto(Guid JobPostId, string Job, JobPostStatus Status, int Positions, Dictionary<string, int> ByStage, int Total, int Hired);

public record RecruitmentReportDto(
    List<FunnelRowDto> Jobs, Dictionary<string, int> BySource, Dictionary<string, int> HiredBySource, double? AverageDaysToHire,
    int OffersMade, int OffersAccepted, int OffersDeclined, int OpenJobs, int OpenPositions, int ActiveCandidates, int InterviewsThisWeek);

[ApiController]
[Route("api/recruitment")]
[Authorize(Policy = Policies.CompanyUser)]
public class RecruitmentController(HrmsDbContext db, RecruitmentService hiring, TenantClock clock, ICurrentUser me) : ControllerBase
{
    private static readonly ApplicationStage[] Closed = [ApplicationStage.Hired, ApplicationStage.Rejected, ApplicationStage.Withdrawn];

    [HttpGet("me")]
    public async Task<RecruitmentMeDto> Me()
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        var text = mine.ToString();
        var canRequest = me.IsHr() || await db.Employees.AnyAsync(e => e.ManagerId == mine && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation));
        var interviews = await db.Interviews.CountAsync(i => i.InterviewerIds.Contains(text) && i.Status == InterviewStatus.Scheduled);
        var slug = me.IsHr() ? await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.CareersSlug).FirstOrDefaultAsync() : null;
        return new RecruitmentMeDto(me.IsHr(), await hiring.MyPosts().AnyAsync(), canRequest, interviews, slug);
    }

    [HttpPut("careers-slug"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SetSlug(CareersSlugRequest req)
    {
        await hiring.SetCareersSlugAsync(req.Slug);
        return NoContent();
    }

    // ---------- Job requests ----------

    /// <summary>HR sees all; others see their own.</summary>
    [HttpGet("requests")]
    public async Task<List<JobRequestDto>> Requests()
    {
        var q = db.JobRequests.AsQueryable();
        if (!me.IsHr())
            q = q.Where(r => r.RequestedByEmployeeId == me.EmployeeId);
        return await ProjectRequests(q.OrderByDescending(r => r.CreatedAtUtc).Take(300)).ToListAsync();
    }

    [HttpGet("requests/{id:guid}")]
    public async Task<JobRequestDto> JobRequest(Guid id, [FromServices] ApprovalEngine approvals)
    {
        var dto = await ProjectRequests(db.JobRequests.Where(r => r.Id == id)).FirstOrDefaultAsync() ?? throw new NotFoundException("Job request");
        if (dto.RequestedByEmployeeId != me.EmployeeId
            && !await approvals.IsApproverAsync(ApprovalRequestType.JobRequest, id, dto.RequestedByEmployeeId))
            throw new NotFoundException("Job request");
        return dto;
    }

    [HttpPost("requests")]
    public async Task<JobRequestDto> CreateRequest(SaveJobRequestRequest req)
    {
        var r = await hiring.RequestJobAsync(new JobRequestInput(req.Title, req.DepartmentId, req.DesignationId, req.Positions, req.ReplacesEmployeeId,
            req.SalaryMin, req.SalaryMax, req.ExperienceMinYears, req.Qualification, req.Skills, req.NeededBy, req.Reason));
        return await ProjectRequests(db.JobRequests.Where(x => x.Id == r.Id)).FirstAsync();
    }

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid id, DecideJobRequest? req)
    {
        await hiring.DecideRequestAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectRequest(Guid id, DecideJobRequest? req)
    {
        await hiring.DecideRequestAsync(id, false, req?.Note);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        await hiring.CancelRequestAsync(id);
        return NoContent();
    }

    // ---------- Job posts ----------

    [HttpGet("jobs")]
    public async Task<List<JobPostDto>> Jobs()
    {
        var posts = await hiring.MyPosts().OrderBy(p => p.Status).ThenByDescending(p => p.CreatedAtUtc).Take(500).ToListAsync();
        return await ToDtosAsync(posts);
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<JobPostDto> Job(Guid id)
    {
        var post = await hiring.MyPosts().FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Job post");
        return (await ToDtosAsync([post]))[0];
    }

    [HttpPost("jobs"), Authorize(Policy = Policies.Hr)]
    public async Task<JobPostDto> CreateJob(SaveJobPostRequest req)
    {
        var post = await hiring.SavePostAsync(null, ToInput(req));
        return (await ToDtosAsync([post]))[0];
    }

    [HttpPut("jobs/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<JobPostDto> UpdateJob(Guid id, SaveJobPostRequest req)
    {
        var post = await hiring.SavePostAsync(id, ToInput(req));
        return (await ToDtosAsync([post]))[0];
    }

    [HttpPost("jobs/{id:guid}/status"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> JobStatus(Guid id, JobPostStatusRequest req)
    {
        await hiring.SetPostStatusAsync(id, req.Status);
        return NoContent();
    }

    [HttpDelete("jobs/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        await hiring.DeletePostAsync(id);
        return NoContent();
    }

    [HttpGet("jobs/{id:guid}/pipeline")]
    public async Task<List<PipelineCardDto>> Pipeline(Guid id)
    {
        if (!await hiring.MyPosts().AnyAsync(p => p.Id == id))
            throw new NotFoundException("Job post");
        return await ProjectCards(db.JobApplications.Where(a => a.JobPostId == id)).ToListAsync();
    }

    /// <summary>Adds a new person (with optional CV) to the job's pipeline.</summary>
    [HttpPost("jobs/{id:guid}/candidates")]
    [RequestSizeLimit(AttachmentService.MaxBytes + 64 * 1024)]
    public async Task<PipelineCardDto> AddCandidate(Guid id, [FromForm] CandidateForm form)
    {
        var post = await hiring.MyPosts().FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Job post");
        var source = form.Source is { } s && Enum.IsDefined(s) && s != CandidateSource.CareersPage ? s : CandidateSource.Manual;
        var app = await hiring.ApplyAsync(post, form.ToInput(), source, form.CoverLetter, await form.ReadCvAsync());
        return await ProjectCards(db.JobApplications.Where(a => a.Id == app.Id)).FirstAsync();
    }

    [HttpPost("jobs/{id:guid}/candidates/{candidateId:guid}")]
    public async Task<PipelineCardDto> AddExisting(Guid id, Guid candidateId)
    {
        var app = await hiring.AddToPipelineAsync(id, candidateId);
        return await ProjectCards(db.JobApplications.Where(a => a.Id == app.Id)).FirstAsync();
    }

    // ---------- Internal job board and referrals ----------

    [HttpGet("openings")]
    public async Task<List<OpeningDto>> Openings()
    {
        var today = await clock.TodayAsync();
        return await db.JobPosts.Where(p => p.Status == JobPostStatus.Open && (p.ClosesOn == null || p.ClosesOn >= today))
            .OrderByDescending(p => p.PublishedAtUtc)
            .Select(p => new OpeningDto(p.Id, p.Title, db.Departments.Where(d => d.Id == p.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                p.Location, p.EmploymentType, p.Description, p.Requirements, p.ClosesOn, p.Slug))
            .ToListAsync();
    }

    [HttpPost("openings/{id:guid}/refer")]
    [RequestSizeLimit(AttachmentService.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Refer(Guid id, [FromForm] CandidateForm form)
    {
        await hiring.ReferAsync(id, form.ToInput(), form.CoverLetter, await form.ReadCvAsync());
        return NoContent();
    }

    /// <summary>People the signed-in user referred and how far they got.</summary>
    [HttpGet("referrals")]
    public async Task<List<CandidateApplicationDto>> MyReferrals()
    {
        var name = await db.Employees.Where(e => e.Id == me.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync();
        if (name is null)
            return [];
        return await db.JobApplications.Where(a => a.Candidate!.ReferredBy == name && a.Candidate.Source == CandidateSource.Referral)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new CandidateApplicationDto(a.Id, a.JobPostId, a.Candidate!.FirstName + " " + a.Candidate.LastName + " · " + a.JobPost!.Title,
                a.Stage, a.CreatedAtUtc))
            .ToListAsync();
    }

    // ---------- Candidates ----------

    [HttpGet("candidates")]
    public async Task<List<CandidateListItemDto>> Candidates([FromQuery] string? search, [FromQuery] bool blacklisted = false)
    {
        await RequireRecruiterAsync();
        var q = db.Candidates.AsQueryable();
        if (!me.IsHr())
        {
            var posts = hiring.MyPosts().Select(p => p.Id);
            q = q.Where(c => db.JobApplications.Any(a => a.CandidateId == c.Id && posts.Contains(a.JobPostId)));
        }
        if (blacklisted)
            q = q.Where(c => c.IsBlacklisted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => (c.FirstName + " " + c.LastName).Contains(s) || c.Email.Contains(s) || (c.Phone != null && c.Phone.Contains(s))
                             || (c.Skills != null && c.Skills.Contains(s)) || (c.CurrentTitle != null && c.CurrentTitle.Contains(s)) || (c.City != null && c.City.Contains(s)));
        }
        return await ProjectCandidates(q.OrderByDescending(c => c.CreatedAtUtc).Take(500)).ToListAsync();
    }

    [HttpGet("candidates/{id:guid}")]
    public async Task<CandidateDto> Candidate(Guid id)
    {
        await RequireRecruiterAsync();
        var c = await db.Candidates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Candidate");
        if (!me.IsHr())
        {
            var posts = hiring.MyPosts().Select(p => p.Id);
            if (!await db.JobApplications.AnyAsync(a => a.CandidateId == id && posts.Contains(a.JobPostId)))
                throw new NotFoundException("Candidate");
        }
        return await CandidateDtoAsync(c, true);
    }

    [HttpPut("candidates/{id:guid}")]
    public async Task<CandidateDto> UpdateCandidate(Guid id, SaveCandidateRequest req)
    {
        await Candidate(id);
        var c = await hiring.UpdateCandidateAsync(id, new CandidateInput(req.FirstName, req.LastName ?? "", req.Email, req.Phone, req.Cnic, req.City,
            req.CurrentTitle, req.CurrentCompany, req.ExperienceYears, req.ExpectedSalary, req.NoticeDays, req.LinkedInUrl, req.Skills, req.ReferredBy), req.Source);
        return await CandidateDtoAsync(c, true);
    }

    [HttpPost("candidates/{id:guid}/blacklist"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Blacklist(Guid id, BlacklistRequest req)
    {
        await hiring.BlacklistAsync(id, req.On, req.Reason);
        return NoContent();
    }

    // ---------- Applications ----------

    [HttpGet("applications/{id:guid}")]
    public async Task<ApplicationDto> Application(Guid id)
    {
        var (app, canRun) = await hiring.LoadAsync(id);
        var events = await db.ApplicationEvents.Where(e => e.ApplicationId == id).OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new ApplicationEventDto(e.Id, e.Text, e.ByName, e.IsNote, e.CreatedAtUtc)).ToListAsync();
        var interviews = await InterviewsAsync(id, canRun);
        var post = app.JobPost!;
        var canOffer = me.IsHr() || post.RecruiterEmployeeId == me.EmployeeId;
        return new ApplicationDto(app.Id, await CandidateDtoAsync(app.Candidate!, canRun), post.Id, post.Title, post.HiringManagerEmployeeId, app.Stage,
            app.CoverLetter, canRun ? app.Rating : null, canRun ? app.RejectionReason : null, canOffer ? app.OfferedSalary : null, app.OfferedJoiningDate,
            canOffer ? app.OfferNote : null, app.OfferStatus, app.CreatedAtUtc, canRun ? events : events.Where(e => e.IsNote).ToList(), interviews,
            canRun, canRun && canOffer, me.IsHr());
    }

    [HttpPost("applications/{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, MoveRequest req)
    {
        await hiring.MoveAsync(id, req.Stage, req.Reason, req.EmailCandidate);
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/notes")]
    public async Task<IActionResult> Note(Guid id, NoteRequest req)
    {
        await hiring.NoteAsync(id, req.Text);
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/rating")]
    public async Task<IActionResult> Rate(Guid id, RateRequest req)
    {
        await hiring.RateAsync(id, req.Rating);
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/interviews")]
    public async Task<IActionResult> Schedule(Guid id, ScheduleInterviewRequest req)
    {
        await hiring.ScheduleAsync(id, new InterviewInput(req.Title, req.StartsAtUtc, req.DurationMinutes, req.Mode, req.Where, req.InterviewerIds, req.Notes, req.EmailCandidate));
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/offer")]
    public async Task<IActionResult> Offer(Guid id, OfferRequest req)
    {
        await hiring.OfferAsync(id, new OfferInput(req.Salary, req.JoiningDate, req.Note, req.EmailCandidate));
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/offer-response")]
    public async Task<IActionResult> OfferResponse(Guid id, OfferResponseRequest req)
    {
        await hiring.OfferResponseAsync(id, req.Accepted, req.Note);
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/hire"), Authorize(Policy = Policies.Hr)]
    public async Task<HiredDto> Hire(Guid id, HireRequest? req)
    {
        var e = await hiring.HireAsync(id, req?.EmployeeCode);
        return new HiredDto(e.Id, e.EmployeeCode);
    }

    // ---------- Interviews ----------

    /// <summary>Interviews the signed-in user sits on: upcoming first, then the ones still needing their feedback.</summary>
    [HttpGet("interviews/mine")]
    public async Task<List<MyInterviewDto>> MyInterviews()
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        var text = mine.ToString();
        var since = clock.UtcNow.AddDays(-60);
        return await db.Interviews.Where(i => i.InterviewerIds.Contains(text) && i.StartsAtUtc >= since && i.Status != InterviewStatus.Cancelled)
            .OrderBy(i => i.StartsAtUtc)
            .Select(i => new MyInterviewDto(i.Id, i.ApplicationId, i.Application!.Candidate!.FirstName + " " + i.Application.Candidate.LastName,
                i.Application.JobPost!.Title, i.Title, i.Round, i.StartsAtUtc, i.DurationMinutes, i.Mode, i.Where, i.Status,
                db.InterviewFeedback.Any(f => f.InterviewId == i.Id && f.InterviewerEmployeeId == mine)))
            .ToListAsync();
    }

    [HttpPost("interviews/{id:guid}/status")]
    public async Task<IActionResult> SetInterviewStatus(Guid id, InterviewStatusRequest req)
    {
        await hiring.SetInterviewStatusAsync(id, req.Status);
        return NoContent();
    }

    [HttpPost("interviews/{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, FeedbackRequest req)
    {
        await hiring.FeedbackAsync(id, new FeedbackInput(req.Rating, req.Recommendation, req.Strengths, req.Concerns));
        return NoContent();
    }

    /// <summary>Current employees, for hiring teams to pick interviewers (they may not be managers).</summary>
    [HttpGet("people")]
    public async Task<List<LookupDto>> People()
    {
        await RequireRecruiterAsync();
        return await db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .OrderBy(e => e.FirstName)
            .Select(e => new LookupDto(e.Id, e.EmployeeCode + " · " + e.FirstName + " " + e.LastName))
            .ToListAsync();
    }

    // ---------- Report ----------

    [HttpGet("report")]
    public async Task<RecruitmentReportDto> Report()
    {
        await RequireRecruiterAsync();
        var posts = await hiring.MyPosts().OrderBy(p => p.Status).ThenByDescending(p => p.CreatedAtUtc).ToListAsync();
        var postIds = posts.Select(p => p.Id).ToList();
        var apps = await db.JobApplications.Where(a => postIds.Contains(a.JobPostId))
            .Select(a => new { a.JobPostId, a.Stage, a.OfferStatus, a.CreatedAtUtc, a.HiredAtUtc, a.Candidate!.Source }).ToListAsync();
        var rows = posts.Select(p =>
        {
            var mine = apps.Where(a => a.JobPostId == p.Id).ToList();
            return new FunnelRowDto(p.Id, p.Title, p.Status, p.Positions,
                mine.GroupBy(a => a.Stage).ToDictionary(g => g.Key.ToString(), g => g.Count()), mine.Count, mine.Count(a => a.Stage == ApplicationStage.Hired));
        }).ToList();
        var hired = apps.Where(a => a.HiredAtUtc is not null).ToList();
        var weekStart = clock.UtcNow.Date.AddDays(-(int)clock.UtcNow.DayOfWeek);
        var interviewsThisWeek = await db.Interviews.CountAsync(i => postIds.Contains(i.Application!.JobPostId) && i.StartsAtUtc >= weekStart
                                                                     && i.StartsAtUtc < weekStart.AddDays(7) && i.Status != InterviewStatus.Cancelled);
        return new RecruitmentReportDto(rows,
            apps.GroupBy(a => a.Source).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            hired.GroupBy(a => a.Source).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            hired.Count > 0 ? Math.Round(hired.Average(a => (a.HiredAtUtc!.Value - a.CreatedAtUtc).TotalDays), 1) : null,
            apps.Count(a => a.OfferStatus is not null), apps.Count(a => a.OfferStatus == OfferStatus.Accepted), apps.Count(a => a.OfferStatus == OfferStatus.Declined),
            posts.Count(p => p.Status == JobPostStatus.Open),
            posts.Where(p => p.Status == JobPostStatus.Open).Sum(p => Math.Max(0, p.Positions - apps.Count(a => a.JobPostId == p.Id && a.Stage == ApplicationStage.Hired))),
            apps.Count(a => !Closed.Contains(a.Stage)), interviewsThisWeek);
    }

    // ---------- Helpers ----------

    private async Task RequireRecruiterAsync()
    {
        if (!await hiring.IsRecruiterAsync())
            throw new ForbiddenException("Only HR and hiring teams can see candidates.");
    }

    private static JobPostInput ToInput(SaveJobPostRequest r) => new(r.Title, r.JobRequestId, r.DepartmentId, r.DesignationId, r.Location, r.EmploymentType,
        r.Description, r.Requirements, r.SalaryMin, r.SalaryMax, r.ShowSalary, r.Positions, r.ClosesOn, r.RecruiterEmployeeId, r.HiringManagerEmployeeId);

    private IQueryable<JobRequestDto> ProjectRequests(IQueryable<JobRequest> q) => q.Select(r => new JobRequestDto(
        r.Id, r.Title, r.DepartmentId, db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => d.Name).FirstOrDefault(),
        r.DesignationId, db.Designations.Where(d => d.Id == r.DesignationId).Select(d => d.Title).FirstOrDefault(), r.Positions,
        db.Employees.Where(e => e.Id == r.ReplacesEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
        r.SalaryMin, r.SalaryMax, r.ExperienceMinYears, r.Qualification, r.Skills, r.NeededBy, r.Reason, r.Status,
        db.Employees.Where(e => e.Id == r.RequestedByEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault() ?? "",
        r.RequestedByEmployeeId, r.CreatedAtUtc, r.DecisionNote,
        db.JobPosts.Where(p => p.JobRequestId == r.Id).Select(p => (Guid?)p.Id).FirstOrDefault()));

    private async Task<List<JobPostDto>> ToDtosAsync(List<JobPost> posts)
    {
        var ids = posts.Select(p => p.Id).ToList();
        var counts = await db.JobApplications.Where(a => ids.Contains(a.JobPostId)).GroupBy(a => new { a.JobPostId, a.Stage })
            .Select(g => new { g.Key.JobPostId, g.Key.Stage, Count = g.Count() }).ToListAsync();
        var people = posts.SelectMany(p => new[] { p.RecruiterEmployeeId, p.HiringManagerEmployeeId }).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        var names = await db.Employees.Where(e => people.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        var deptIds = posts.Select(p => p.DepartmentId).ToList();
        var depts = await db.Departments.Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
        var desIds = posts.Select(p => p.DesignationId).ToList();
        var designations = await db.Designations.Where(d => desIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Title);
        string? N(Guid? id, Dictionary<Guid, string> map) => id is { } x ? map.GetValueOrDefault(x) : null;
        return posts.Select(p =>
        {
            var c = counts.Where(x => x.JobPostId == p.Id).ToList();
            return new JobPostDto(p.Id, p.Title, p.Slug, p.JobRequestId, p.DepartmentId, N(p.DepartmentId, depts), p.DesignationId, N(p.DesignationId, designations),
                p.Location, p.EmploymentType, p.Description, p.Requirements, p.SalaryMin, p.SalaryMax, p.ShowSalary, p.Positions, p.ClosesOn, p.Status,
                p.RecruiterEmployeeId, N(p.RecruiterEmployeeId, names), p.HiringManagerEmployeeId, N(p.HiringManagerEmployeeId, names), p.PublishedAtUtc,
                p.CreatedAtUtc, c.ToDictionary(x => x.Stage.ToString(), x => x.Count), c.Sum(x => x.Count), me.IsHr());
        }).ToList();
    }

    private IQueryable<PipelineCardDto> ProjectCards(IQueryable<JobApplication> q) => q.OrderByDescending(a => a.Rating ?? 0).ThenBy(a => a.CreatedAtUtc).Select(a =>
        new PipelineCardDto(a.Id, a.CandidateId, a.Candidate!.FirstName + " " + a.Candidate.LastName, a.Candidate.Email, a.Candidate.CurrentTitle,
            a.Candidate.ExperienceYears, a.Candidate.Source, a.Stage, a.Rating, a.Candidate.IsBlacklisted,
            db.Interviews.Count(i => i.ApplicationId == a.Id && i.Status != InterviewStatus.Cancelled),
            db.InterviewFeedback.Where(f => db.Interviews.Any(i => i.Id == f.InterviewId && i.ApplicationId == a.Id)).Average(f => (decimal?)f.Rating),
            a.OfferStatus, a.CreatedAtUtc, a.StageChangedAtUtc));

    private IQueryable<CandidateListItemDto> ProjectCandidates(IQueryable<Candidate> q) => q.Select(c => new CandidateListItemDto(
        c.Id, c.FirstName + " " + c.LastName, c.Email, c.Phone, c.City, c.CurrentTitle, c.ExperienceYears, c.Source, c.IsBlacklisted,
        db.JobApplications.Count(a => a.CandidateId == c.Id),
        db.JobApplications.Where(a => a.CandidateId == c.Id).OrderByDescending(a => a.CreatedAtUtc).Select(a => a.JobPost!.Title).FirstOrDefault(),
        db.JobApplications.Where(a => a.CandidateId == c.Id).OrderByDescending(a => a.CreatedAtUtc).Select(a => (ApplicationStage?)a.Stage).FirstOrDefault(),
        c.CreatedAtUtc, c.EmployeeId != null));

    /// <summary>Full details for the hiring team; interviewers get the professional profile without salary or ID.</summary>
    private async Task<CandidateDto> CandidateDtoAsync(Candidate c, bool full)
    {
        var apps = await db.JobApplications.Where(a => a.CandidateId == c.Id).OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new CandidateApplicationDto(a.Id, a.JobPostId, a.JobPost!.Title, a.Stage, a.CreatedAtUtc)).ToListAsync();
        var duplicates = new List<CandidateListItemDto>();
        if (full)
        {
            var phoneTail = c.Phone is { Length: >= 7 } ph ? new string(ph.Where(char.IsDigit).ToArray()) : null;
            phoneTail = phoneTail is { Length: >= 7 } ? phoneTail[^7..] : null;
            var dupes = db.Candidates.Where(x => x.Id != c.Id && ((c.Cnic != null && x.Cnic == c.Cnic)
                                                                  || (phoneTail != null && x.Phone != null && x.Phone.Contains(phoneTail))
                                                                  || (x.FirstName == c.FirstName && x.LastName == c.LastName && x.LastName != "")));
            duplicates = await ProjectCandidates(dupes.Take(5)).ToListAsync();
        }
        return new CandidateDto(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, full ? c.Cnic : null, c.City, c.CurrentTitle, c.CurrentCompany, c.ExperienceYears,
            full ? c.ExpectedSalary : null, c.NoticeDays, c.LinkedInUrl, c.Skills, c.Source, c.ReferredBy, full && c.IsBlacklisted, full ? c.BlacklistReason : null,
            c.EmployeeId, c.CreatedAtUtc, full ? apps : [], duplicates);
    }

    /// <summary>
    /// The hiring team sees all feedback. An interviewer sees the others' feedback only after giving their own, so
    /// nobody is swayed before deciding.
    /// </summary>
    private async Task<List<InterviewDto>> InterviewsAsync(Guid applicationId, bool canRun)
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        var list = await db.Interviews.Where(i => i.ApplicationId == applicationId).OrderBy(i => i.StartsAtUtc).ToListAsync();
        var ids = list.Select(i => i.Id).ToList();
        var feedback = await db.InterviewFeedback.Where(f => ids.Contains(f.InterviewId)).ToListAsync();
        var people = list.SelectMany(i => HelpdeskService.Ids(i.InterviewerIds)).Distinct().ToList();
        var names = await db.Employees.Where(e => people.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        return list.Select(i =>
        {
            var interviewers = HelpdeskService.Ids(i.InterviewerIds);
            var all = feedback.Where(f => f.InterviewId == i.Id).ToList();
            var gave = all.Any(f => f.InterviewerEmployeeId == mine);
            var visible = canRun || gave ? all : [];
            return new InterviewDto(i.Id, i.ApplicationId, i.Round, i.Title, i.StartsAtUtc, i.DurationMinutes, i.Mode, i.Where, interviewers,
                interviewers.Select(x => names.GetValueOrDefault(x) ?? "").ToList(), i.Status, i.Notes,
                visible.Select(f => new FeedbackDto(f.InterviewerEmployeeId, names.GetValueOrDefault(f.InterviewerEmployeeId) ?? "", f.Rating, f.Recommendation,
                    f.Strengths, f.Concerns, f.CreatedAtUtc)).ToList(),
                all.Count - visible.Count, interviewers.Contains(mine), gave);
        }).Where(i => canRun || i.IAmInterviewer).ToList();
    }
}
