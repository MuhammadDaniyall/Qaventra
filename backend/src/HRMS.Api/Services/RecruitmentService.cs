using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record JobRequestInput(
    string Title, Guid? DepartmentId, Guid? DesignationId, int Positions, Guid? ReplacesEmployeeId, decimal? SalaryMin, decimal? SalaryMax,
    int? ExperienceMinYears, string? Qualification, string? Skills, DateOnly? NeededBy, string Reason);

public record JobPostInput(
    string Title, Guid? JobRequestId, Guid? DepartmentId, Guid? DesignationId, string? Location, EmploymentType EmploymentType, string Description,
    string? Requirements, decimal? SalaryMin, decimal? SalaryMax, bool ShowSalary, int Positions, DateOnly? ClosesOn, Guid? RecruiterEmployeeId,
    Guid? HiringManagerEmployeeId);

public record CandidateInput(
    string FirstName, string LastName, string Email, string? Phone, string? Cnic, string? City, string? CurrentTitle, string? CurrentCompany,
    decimal? ExperienceYears, decimal? ExpectedSalary, int? NoticeDays, string? LinkedInUrl, string? Skills, string? ReferredBy);

public record CvFile(string FileName, byte[] Data);

public record InterviewInput(string Title, DateTime StartsAtUtc, int DurationMinutes, InterviewMode Mode, string? Where, List<Guid> InterviewerIds, string? Notes, bool EmailCandidate);

public record FeedbackInput(int Rating, HireRecommendation Recommendation, string? Strengths, string? Concerns);

public record OfferInput(decimal Salary, DateOnly JoiningDate, string? Note, bool EmailCandidate);

/// <summary>
/// Hiring: job requests (approved like other requests), job posts with a public careers page, one candidate record
/// per person, each job's pipeline, interviews with feedback, offers and the hire that creates the employee.
/// HR runs everything; a post's recruiter and hiring manager run that post's pipeline; interviewers see the
/// candidates they interview and give feedback.
/// </summary>
public class RecruitmentService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, NotificationService notify, EmployeeService employees,
    IServiceProvider services)
{
    // ---------- Access ----------

    public bool CanRun(JobPost post) =>
        me.IsHr() || (me.EmployeeId is { } mine && (post.RecruiterEmployeeId == mine || post.HiringManagerEmployeeId == mine));

    /// <summary>Posts whose pipeline the signed-in user runs (all for HR).</summary>
    public IQueryable<JobPost> MyPosts()
    {
        if (me.IsHr())
            return db.JobPosts;
        var mine = me.EmployeeId ?? Guid.Empty;
        return db.JobPosts.Where(p => p.RecruiterEmployeeId == mine || p.HiringManagerEmployeeId == mine);
    }

    /// <summary>Applications the signed-in user interviews in (not counting the posts they run).</summary>
    public IQueryable<Guid> InterviewingIds()
    {
        var text = (me.EmployeeId ?? Guid.Empty).ToString();
        return db.Interviews.Where(i => i.InterviewerIds.Contains(text)).Select(i => i.ApplicationId);
    }

    public async Task<bool> IsRecruiterAsync() => me.IsHr() || await MyPosts().AnyAsync();

    /// <summary>The application with its post and candidate, when the signed-in user may see it.</summary>
    public async Task<(JobApplication App, bool CanRun)> LoadAsync(Guid applicationId)
    {
        var app = await db.JobApplications.Include(a => a.JobPost).Include(a => a.Candidate).FirstOrDefaultAsync(a => a.Id == applicationId)
                  ?? throw new NotFoundException("Application");
        if (CanRun(app.JobPost!))
            return (app, true);
        if (await InterviewingIds().AnyAsync(id => id == applicationId))
            return (app, false);
        throw new NotFoundException("Application");
    }

    private async Task<JobApplication> RunAsync(Guid applicationId)
    {
        var (app, canRun) = await LoadAsync(applicationId);
        if (!canRun)
            throw new ForbiddenException("Only HR, the recruiter and the hiring manager can do this.");
        return app;
    }

    // ---------- Job requests ----------

    public async Task<JobRequest> RequestJobAsync(JobRequestInput input)
    {
        var employeeId = MyId();
        if (!me.IsHr() && !await db.Employees.AnyAsync(e => e.ManagerId == employeeId && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new ForbiddenException("Only managers and HR can ask to hire.");
        await ValidateJobAsync(input.Title, input.DepartmentId, input.DesignationId, input.Positions, input.SalaryMin, input.SalaryMax);
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new DomainException("Say why the position is needed.");
        if (input.ReplacesEmployeeId is { } rep && !await db.Employees.AnyAsync(e => e.Id == rep))
            throw new NotFoundException("Employee being replaced");
        if (input.NeededBy is { } nb && nb < await clock.TodayAsync())
            throw new DomainException("The needed-by date has passed.");

        var request = new JobRequest
        {
            RequestedByEmployeeId = employeeId,
            Title = input.Title.Trim(),
            DepartmentId = input.DepartmentId,
            DesignationId = input.DesignationId,
            Positions = input.Positions,
            ReplacesEmployeeId = input.ReplacesEmployeeId,
            SalaryMin = input.SalaryMin,
            SalaryMax = input.SalaryMax,
            ExperienceMinYears = input.ExperienceMinYears,
            Qualification = Clean(input.Qualification, 300),
            Skills = Clean(input.Skills, 1000),
            NeededBy = input.NeededBy,
            Reason = input.Reason.Trim(),
        };
        db.JobRequests.Add(request);
        var summary = $"{request.Positions} × {request.Title}" + (input.ReplacesEmployeeId is null ? " (new position)" : " (replacement)") + $" · {request.Reason}";
        var approval = await approvals.StartAsync(ApprovalRequestType.JobRequest, request.Id, employeeId, summary);
        if (approval.Status == ApprovalStatus.Approved)
            await ApproveRequestAsync(request, "Approved automatically");
        await db.SaveChangesAsync();
        return request;
    }

    public async Task DecideRequestAsync(Guid id, bool approve, string? note)
    {
        var request = await db.JobRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Job request");
        if (request.Status != RequestStatus.Pending)
            throw new DomainException("This request is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.JobRequest, request.Id, request.RequestedByEmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
            await ApproveRequestAsync(request, note);
        else if (outcome == ApprovalStatus.Rejected)
        {
            request.Status = RequestStatus.Rejected;
            request.DecidedAtUtc = clock.UtcNow;
            request.DecisionNote = Clean(note, 1000);
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelRequestAsync(Guid id)
    {
        var request = await db.JobRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Job request");
        if (request.RequestedByEmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this request.");
        if (request.Status is not (RequestStatus.Pending or RequestStatus.Approved))
            throw new DomainException("This request can no longer be cancelled.");
        if (await db.JobPosts.AnyAsync(p => p.JobRequestId == id))
            throw new DomainException("A job post was already made from this request. Close the post instead.");
        if (request.Status == RequestStatus.Pending)
            await approvals.CancelAsync(ApprovalRequestType.JobRequest, request.Id);
        request.Status = RequestStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    private async Task ApproveRequestAsync(JobRequest request, string? note)
    {
        request.Status = RequestStatus.Approved;
        request.DecidedAtUtc = clock.UtcNow;
        request.DecisionNote = Clean(note, 1000);
        var name = await db.Employees.Where(e => e.Id == request.RequestedByEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        await notify.ToHrAsync(NotificationKind.Recruitment, $"Hiring approved: {request.Positions} × {request.Title}",
            $"Requested by {name}. Create a job post to start hiring.", "/recruitment?tab=requests");
    }

    // ---------- Job posts ----------

    public async Task<JobPost> SavePostAsync(Guid? id, JobPostInput input)
    {
        RequireHr();
        await ValidateJobAsync(input.Title, input.DepartmentId, input.DesignationId, input.Positions, input.SalaryMin, input.SalaryMax);
        if (string.IsNullOrWhiteSpace(input.Description))
            throw new DomainException("Describe the job.");
        if (!Enum.IsDefined(input.EmploymentType))
            throw new DomainException("Choose the type of employment.");
        foreach (var person in new[] { input.RecruiterEmployeeId, input.HiringManagerEmployeeId })
            if (person is { } pid && !await db.Employees.AnyAsync(e => e.Id == pid && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
                throw new NotFoundException("Employee");

        JobPost post;
        if (id is { } existing)
        {
            post = await db.JobPosts.FirstOrDefaultAsync(p => p.Id == existing) ?? throw new NotFoundException("Job post");
            if (post.Status == JobPostStatus.Filled)
                throw new DomainException("This job is filled. Reopen it to change it.");
        }
        else
        {
            if (input.JobRequestId is { } rid)
            {
                var request = await db.JobRequests.FirstOrDefaultAsync(r => r.Id == rid) ?? throw new NotFoundException("Job request");
                if (request.Status != RequestStatus.Approved)
                    throw new DomainException("Only approved job requests can be posted.");
            }
            post = new JobPost { JobRequestId = input.JobRequestId, Slug = await NewSlugAsync(input.Title) };
            db.JobPosts.Add(post);
        }

        post.Title = input.Title.Trim();
        post.DepartmentId = input.DepartmentId;
        post.DesignationId = input.DesignationId;
        post.Location = Clean(input.Location, 150);
        post.EmploymentType = input.EmploymentType;
        post.Description = input.Description.Trim();
        post.Requirements = Clean(input.Requirements, 8000);
        post.SalaryMin = input.SalaryMin;
        post.SalaryMax = input.SalaryMax;
        post.ShowSalary = input.ShowSalary && (input.SalaryMin is not null || input.SalaryMax is not null);
        post.Positions = input.Positions;
        post.ClosesOn = input.ClosesOn;
        var newPeople = new[] { input.RecruiterEmployeeId, input.HiringManagerEmployeeId }
            .Where(x => x is not null && x != post.RecruiterEmployeeId && x != post.HiringManagerEmployeeId).Select(x => x!.Value).ToList();
        post.RecruiterEmployeeId = input.RecruiterEmployeeId;
        post.HiringManagerEmployeeId = input.HiringManagerEmployeeId;
        if (newPeople.Count > 0)
            await notify.ToEmployeesAsync(newPeople, NotificationKind.Recruitment, $"You are on the hiring team for {post.Title}",
                "You can see the candidates and move them through the pipeline.", $"/recruitment/jobs/{post.Id}");
        await db.SaveChangesAsync();
        return post;
    }

    /// <summary>Open (publish), close (stop taking applications) or reopen a post.</summary>
    public async Task SetPostStatusAsync(Guid id, JobPostStatus status)
    {
        RequireHr();
        var post = await db.JobPosts.FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Job post");
        if (status is not (JobPostStatus.Open or JobPostStatus.Closed or JobPostStatus.Filled))
            throw new DomainException("A post can be opened, closed or marked filled.");
        if (status == JobPostStatus.Open)
        {
            if (post.ClosesOn is { } c && c < await clock.TodayAsync())
                throw new DomainException("The closing date has passed. Change it first.");
            await EnsureCareersSlugAsync();
            post.PublishedAtUtc ??= clock.UtcNow;
        }
        post.Status = status;
        await db.SaveChangesAsync();
    }

    public async Task DeletePostAsync(Guid id)
    {
        RequireHr();
        var post = await db.JobPosts.FirstOrDefaultAsync(p => p.Id == id) ?? throw new NotFoundException("Job post");
        if (await db.JobApplications.AnyAsync(a => a.JobPostId == id))
            throw new DomainException("People have applied to this job. Close it instead.");
        db.JobPosts.Remove(post);
        await db.SaveChangesAsync();
    }

    /// <summary>The company's careers page address part, made from its name the first time it is needed.</summary>
    public async Task<string> EnsureCareersSlugAsync()
    {
        var tenant = await db.Tenants.FirstAsync(t => t.Id == db.CurrentTenantId);
        if (!string.IsNullOrEmpty(tenant.CareersSlug))
            return tenant.CareersSlug;
        var root = Slugify(tenant.Name, 40);
        if (root.Length < 3)
            root = "company";
        var slug = root;
        for (var n = 2; await db.Tenants.AnyAsync(t => t.CareersSlug == slug); n++)
            slug = $"{root}-{n}";
        tenant.CareersSlug = slug;
        return slug;
    }

    public async Task SetCareersSlugAsync(string slug)
    {
        RequireHr();
        var clean = Slugify(slug, 60);
        if (clean.Length < 3)
            throw new DomainException("Use at least 3 letters or numbers.");
        var tenant = await db.Tenants.FirstAsync(t => t.Id == db.CurrentTenantId);
        if (await db.Tenants.AnyAsync(t => t.CareersSlug == clean && t.Id != tenant.Id))
            throw new DomainException("Another company already uses this address.");
        tenant.CareersSlug = clean;
        await db.SaveChangesAsync();
    }

    // ---------- Candidates and applications ----------

    /// <summary>
    /// Adds a person to a job's pipeline. The same email reuses the existing candidate record and fills in what was
    /// blank. Used by the careers page (no one signed in), referrals and HR adding people by hand.
    /// </summary>
    public async Task<JobApplication> ApplyAsync(JobPost post, CandidateInput input, CandidateSource source, string? coverLetter, CvFile? cv)
    {
        var email = (input.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(input.FirstName))
            throw new DomainException("Enter the first name.");
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new DomainException("Enter a valid email address.");
        if (cv is not null)
            CheckCv(cv);

        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Email == email);
        if (candidate is null)
        {
            candidate = new Candidate { Email = email, Source = source };
            db.Candidates.Add(candidate);
        }
        else if (await db.JobApplications.AnyAsync(a => a.CandidateId == candidate.Id && a.JobPostId == post.Id))
        {
            throw new DomainException(source == CandidateSource.CareersPage
                ? "You have already applied for this job. We will be in touch."
                : $"{candidate.FullName} is already in this job's pipeline.");
        }
        // The latest application's details win, but nothing is wiped by a blank field.
        candidate.FirstName = Clean(input.FirstName, 100)!;
        candidate.LastName = Clean(input.LastName, 100) ?? candidate.LastName;
        candidate.Phone = Clean(input.Phone, 30) ?? candidate.Phone;
        candidate.Cnic = Clean(input.Cnic, 15) ?? candidate.Cnic;
        candidate.City = Clean(input.City, 100) ?? candidate.City;
        candidate.CurrentTitle = Clean(input.CurrentTitle, 150) ?? candidate.CurrentTitle;
        candidate.CurrentCompany = Clean(input.CurrentCompany, 150) ?? candidate.CurrentCompany;
        candidate.ExperienceYears = input.ExperienceYears is >= 0 and < 100 ? input.ExperienceYears : candidate.ExperienceYears;
        candidate.ExpectedSalary = input.ExpectedSalary is > 0 ? input.ExpectedSalary : candidate.ExpectedSalary;
        candidate.NoticeDays = input.NoticeDays is >= 0 and <= 365 ? input.NoticeDays : candidate.NoticeDays;
        candidate.LinkedInUrl = Clean(input.LinkedInUrl, 300) ?? candidate.LinkedInUrl;
        candidate.Skills = Clean(input.Skills, 1000) ?? candidate.Skills;
        if (source == CandidateSource.Referral)
        {
            candidate.Source = source;
            candidate.ReferredBy = Clean(input.ReferredBy, 150);
        }

        var app = new JobApplication
        {
            JobPostId = post.Id,
            CandidateId = candidate.Id,
            CoverLetter = Clean(coverLetter, 4000),
            StageChangedAtUtc = clock.UtcNow,
        };
        db.JobApplications.Add(app);
        var by = await MyNameAsync();
        Log(app.Id, source switch
        {
            CandidateSource.CareersPage => "Applied on the careers page",
            CandidateSource.Referral => $"Referred by {candidate.ReferredBy ?? by}",
            _ => $"Added by {by} ({Label(source).ToLowerInvariant()})",
        }, by);
        if (cv is not null)
            AddCv(candidate.Id, cv, by);

        var team = new[] { post.RecruiterEmployeeId, post.HiringManagerEmployeeId }.Where(x => x is not null).Select(x => x!.Value).ToList();
        var title = $"New candidate for {post.Title}: {candidate.FullName}";
        if (team.Count > 0)
            await notify.ToEmployeesAsync(team, NotificationKind.Recruitment, title, candidate.IsBlacklisted ? "This person is on the do-not-hire list." : null, $"/recruitment/applications/{app.Id}");
        else
            await notify.ToHrAsync(NotificationKind.Recruitment, title, null, $"/recruitment/applications/{app.Id}");
        if (source == CandidateSource.CareersPage)
            notify.EmailOnly(email, $"We received your application for {post.Title}",
                $"Dear {candidate.FirstName},\n\nThank you for applying. Our team will review your application and contact you about the next steps.",
                null, CandidateFooter);
        await db.SaveChangesAsync();
        return app;
    }

    public async Task<JobApplication> ReferAsync(Guid postId, CandidateInput input, string? note, CvFile? cv)
    {
        var employeeId = MyId();
        var post = await db.JobPosts.FirstOrDefaultAsync(p => p.Id == postId && p.Status == JobPostStatus.Open) ?? throw new NotFoundException("Open job");
        var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        return await ApplyAsync(post, input with { ReferredBy = name }, CandidateSource.Referral, note, cv);
    }

    public async Task<JobApplication> AddToPipelineAsync(Guid postId, Guid candidateId)
    {
        var post = await MyPosts().FirstOrDefaultAsync(p => p.Id == postId) ?? throw new NotFoundException("Job post");
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == candidateId) ?? throw new NotFoundException("Candidate");
        if (await db.JobApplications.AnyAsync(a => a.CandidateId == candidateId && a.JobPostId == postId))
            throw new DomainException($"{candidate.FullName} is already in this job's pipeline.");
        var app = new JobApplication { JobPostId = post.Id, CandidateId = candidate.Id, StageChangedAtUtc = clock.UtcNow };
        db.JobApplications.Add(app);
        var by = await MyNameAsync();
        Log(app.Id, $"Added from the candidate pool by {by}", by);
        await db.SaveChangesAsync();
        return app;
    }

    public async Task<Candidate> UpdateCandidateAsync(Guid id, CandidateInput input, CandidateSource source)
    {
        await RequireRecruiterAsync();
        var c = await db.Candidates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Candidate");
        var email = (input.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(input.FirstName))
            throw new DomainException("Enter the first name.");
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new DomainException("Enter a valid email address.");
        if (email != c.Email && await db.Candidates.AnyAsync(x => x.Email == email))
            throw new DomainException("Another candidate has this email.");
        c.Email = email;
        c.FirstName = Clean(input.FirstName, 100)!;
        c.LastName = Clean(input.LastName, 100) ?? "";
        c.Phone = Clean(input.Phone, 30);
        c.Cnic = Clean(input.Cnic, 15);
        c.City = Clean(input.City, 100);
        c.CurrentTitle = Clean(input.CurrentTitle, 150);
        c.CurrentCompany = Clean(input.CurrentCompany, 150);
        c.ExperienceYears = input.ExperienceYears is >= 0 and < 100 ? input.ExperienceYears : null;
        c.ExpectedSalary = input.ExpectedSalary is > 0 ? input.ExpectedSalary : null;
        c.NoticeDays = input.NoticeDays is >= 0 and <= 365 ? input.NoticeDays : null;
        c.LinkedInUrl = Clean(input.LinkedInUrl, 300);
        c.Skills = Clean(input.Skills, 1000);
        c.ReferredBy = Clean(input.ReferredBy, 150);
        if (Enum.IsDefined(source))
            c.Source = source;
        await db.SaveChangesAsync();
        return c;
    }

    public async Task BlacklistAsync(Guid candidateId, bool on, string? reason)
    {
        RequireHr();
        var c = await db.Candidates.FirstOrDefaultAsync(x => x.Id == candidateId) ?? throw new NotFoundException("Candidate");
        if (on && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Say why this person should not be hired.");
        c.IsBlacklisted = on;
        c.BlacklistReason = on ? Clean(reason, 500) : null;
        await db.SaveChangesAsync();
    }

    public async Task AddCvAsync(Guid candidateId, CvFile cv)
    {
        await RequireRecruiterAsync();
        if (!await db.Candidates.AnyAsync(c => c.Id == candidateId))
            throw new NotFoundException("Candidate");
        CheckCv(cv);
        AddCv(candidateId, cv, await MyNameAsync());
        await db.SaveChangesAsync();
    }

    // ---------- Pipeline ----------

    /// <summary>Moves a candidate to another stage. Hiring happens through <see cref="HireAsync"/>.</summary>
    public async Task MoveAsync(Guid applicationId, ApplicationStage stage, string? reason, bool emailCandidate)
    {
        var app = await RunAsync(applicationId);
        if (!Enum.IsDefined(stage))
            throw new DomainException("Choose a stage.");
        if (app.Stage == ApplicationStage.Hired)
            throw new DomainException("This candidate has been hired.");
        if (stage == ApplicationStage.Hired)
            throw new DomainException("Use Hire to make the candidate an employee.");
        if (stage == app.Stage)
            return;
        if (stage == ApplicationStage.Rejected && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Say why the candidate is not going ahead.");
        if (stage == ApplicationStage.Offer && app.OfferStatus is null)
            throw new DomainException("Make an offer to move the candidate to Offer.");

        var from = app.Stage;
        app.Stage = stage;
        app.StageChangedAtUtc = clock.UtcNow;
        if (stage == ApplicationStage.Rejected)
            app.RejectionReason = Clean(reason, 500);
        var by = await MyNameAsync();
        Log(app.Id, $"{Label(from)} → {Label(stage)}" + (string.IsNullOrWhiteSpace(reason) ? "" : $": {reason.Trim()}"), by);

        if (stage == ApplicationStage.Rejected)
        {
            foreach (var i in await db.Interviews.Where(i => i.ApplicationId == app.Id && i.Status == InterviewStatus.Scheduled).ToListAsync())
                i.Status = InterviewStatus.Cancelled;
            if (emailCandidate)
                notify.EmailOnly(app.Candidate!.Email, $"Your application for {app.JobPost!.Title}",
                    $"Dear {app.Candidate.FirstName},\n\nThank you for your interest. After careful review we will not be moving ahead with your application at this time. We wish you the best and hope you will apply again.",
                    null, CandidateFooter);
        }
        await db.SaveChangesAsync();
    }

    public async Task NoteAsync(Guid applicationId, string text)
    {
        var (app, _) = await LoadAsync(applicationId);
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Write the note.");
        db.ApplicationEvents.Add(new ApplicationEvent { ApplicationId = app.Id, Text = Clean(text, 2000)!, ByName = await MyNameAsync(), IsNote = true });
        await db.SaveChangesAsync();
    }

    public async Task RateAsync(Guid applicationId, int? rating)
    {
        var app = await RunAsync(applicationId);
        if (rating is < 1 or > 5)
            throw new DomainException("Rate from 1 to 5.");
        app.Rating = rating;
        await db.SaveChangesAsync();
    }

    // ---------- Interviews ----------

    public async Task<Interview> ScheduleAsync(Guid applicationId, InterviewInput input)
    {
        var app = await RunAsync(applicationId);
        if (app.Stage is ApplicationStage.Hired or ApplicationStage.Rejected or ApplicationStage.Withdrawn)
            throw new DomainException($"The candidate is {Label(app.Stage).ToLowerInvariant()}.");
        if (string.IsNullOrWhiteSpace(input.Title))
            throw new DomainException("Name the interview, e.g. Technical round.");
        if (input.DurationMinutes is < 10 or > 480)
            throw new DomainException("An interview lasts 10 minutes to 8 hours.");
        if (!Enum.IsDefined(input.Mode))
            throw new DomainException("Choose onsite, online or phone.");
        if (input.StartsAtUtc < clock.UtcNow.AddHours(-1))
            throw new DomainException("The interview time has passed.");
        var ids = input.InterviewerIds.Distinct().ToList();
        if (ids.Count is 0 or > 10)
            throw new DomainException("Choose one to ten interviewers.");
        if (await db.Employees.CountAsync(e => ids.Contains(e.Id) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)) != ids.Count)
            throw new NotFoundException("Interviewer");

        var round = await db.Interviews.CountAsync(i => i.ApplicationId == app.Id && i.Status != InterviewStatus.Cancelled) + 1;
        var interview = new Interview
        {
            ApplicationId = app.Id,
            Round = round,
            Title = input.Title.Trim(),
            StartsAtUtc = DateTime.SpecifyKind(input.StartsAtUtc, DateTimeKind.Utc),
            DurationMinutes = input.DurationMinutes,
            Mode = input.Mode,
            Where = Clean(input.Where, 500),
            InterviewerIds = string.Join(',', ids),
            Notes = Clean(input.Notes, 2000),
        };
        db.Interviews.Add(interview);
        if (app.Stage is ApplicationStage.Applied or ApplicationStage.Shortlisted or ApplicationStage.Test or ApplicationStage.OnHold)
        {
            app.Stage = ApplicationStage.Interview;
            app.StageChangedAtUtc = clock.UtcNow;
        }
        var by = await MyNameAsync();
        var when = await LocalTextAsync(interview.StartsAtUtc);
        Log(app.Id, $"Interview scheduled: {interview.Title}, {when}", by);
        await notify.ToEmployeesAsync(ids, NotificationKind.Recruitment, $"Interview: {app.Candidate!.FullName} for {app.JobPost!.Title}",
            $"{interview.Title} · {when} · {Label(interview.Mode)}" + (interview.Where is null ? "" : $" · {interview.Where}"), $"/recruitment/applications/{app.Id}", includeMe: true);
        if (input.EmailCandidate)
            notify.EmailOnly(app.Candidate.Email, $"Interview for {app.JobPost.Title}",
                $"Dear {app.Candidate.FirstName},\n\nYou are invited to an interview ({interview.Title}).\nWhen: {when} ({interview.DurationMinutes} minutes)\n"
                + $"How: {Label(interview.Mode)}" + (interview.Where is null ? "" : $"\nWhere: {interview.Where}") + "\n\nPlease reply to confirm.",
                null, CandidateFooter);
        await db.SaveChangesAsync();
        return interview;
    }

    public async Task SetInterviewStatusAsync(Guid interviewId, InterviewStatus status)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId) ?? throw new NotFoundException("Interview");
        var app = await RunAsync(interview.ApplicationId);
        if (!Enum.IsDefined(status) || status == InterviewStatus.Scheduled && interview.Status == InterviewStatus.Completed)
            throw new DomainException("Choose completed, cancelled or no-show.");
        interview.Status = status;
        Log(app.Id, $"Interview {interview.Title}: {Label(status).ToLowerInvariant()}", await MyNameAsync());
        await db.SaveChangesAsync();
    }

    /// <summary>An interviewer's verdict. The interview is completed once every interviewer has given one.</summary>
    public async Task FeedbackAsync(Guid interviewId, FeedbackInput input)
    {
        var mine = MyId();
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId) ?? throw new NotFoundException("Interview");
        var interviewers = HelpdeskService.Ids(interview.InterviewerIds);
        if (!interviewers.Contains(mine))
            throw new ForbiddenException("Only the interviewers give feedback.");
        if (interview.Status is InterviewStatus.Cancelled or InterviewStatus.NoShow)
            throw new DomainException("This interview did not take place.");
        if (interview.StartsAtUtc > clock.UtcNow)
            throw new DomainException("Give feedback after the interview has started.");
        if (input.Rating is < 1 or > 5)
            throw new DomainException("Rate from 1 to 5.");
        if (!Enum.IsDefined(input.Recommendation))
            throw new DomainException("Say whether you would hire.");

        var feedback = await db.InterviewFeedback.FirstOrDefaultAsync(f => f.InterviewId == interviewId && f.InterviewerEmployeeId == mine);
        var isNew = feedback is null;
        if (feedback is null)
        {
            feedback = new InterviewFeedback { InterviewId = interviewId, InterviewerEmployeeId = mine };
            db.InterviewFeedback.Add(feedback);
        }
        feedback.Rating = input.Rating;
        feedback.Recommendation = input.Recommendation;
        feedback.Strengths = Clean(input.Strengths, 2000);
        feedback.Concerns = Clean(input.Concerns, 2000);

        var given = await db.InterviewFeedback.Where(f => f.InterviewId == interviewId).Select(f => f.InterviewerEmployeeId).ToListAsync();
        if (isNew)
            given.Add(mine);
        if (interviewers.All(given.Contains))
            interview.Status = InterviewStatus.Completed;

        if (isNew)
        {
            var app = await db.JobApplications.Include(a => a.JobPost).Include(a => a.Candidate).FirstAsync(a => a.Id == interview.ApplicationId);
            var by = await MyNameAsync();
            Log(app.Id, $"Feedback from {by} on {interview.Title}: {Label(input.Recommendation)} ({input.Rating}/5)", by);
            var team = new[] { app.JobPost!.RecruiterEmployeeId, app.JobPost.HiringManagerEmployeeId }.Where(x => x is not null).Select(x => x!.Value).ToList();
            var title = $"{by} reviewed {app.Candidate!.FullName}: {Label(input.Recommendation)}";
            if (team.Count > 0)
                await notify.ToEmployeesAsync(team, NotificationKind.Recruitment, title, interview.Title, $"/recruitment/applications/{app.Id}");
            else
                await notify.ToHrAsync(NotificationKind.Recruitment, title, interview.Title, $"/recruitment/applications/{app.Id}");
        }
        await db.SaveChangesAsync();
    }

    // ---------- Offer and hire ----------

    public async Task OfferAsync(Guid applicationId, OfferInput input)
    {
        var app = await RunAsync(applicationId);
        RequireHrOrRecruiter(app.JobPost!);
        if (app.Stage is ApplicationStage.Hired or ApplicationStage.Rejected or ApplicationStage.Withdrawn)
            throw new DomainException($"The candidate is {Label(app.Stage).ToLowerInvariant()}.");
        if (app.Candidate!.IsBlacklisted)
            throw new DomainException("This person is on the do-not-hire list.");
        if (input.Salary <= 0)
            throw new DomainException("Enter the offered salary.");
        if (input.JoiningDate < await clock.TodayAsync())
            throw new DomainException("The joining date has passed.");

        app.OfferedSalary = input.Salary;
        app.OfferedJoiningDate = input.JoiningDate;
        app.OfferNote = Clean(input.Note, 2000);
        app.OfferStatus = OfferStatus.Sent;
        app.Stage = ApplicationStage.Offer;
        app.StageChangedAtUtc = clock.UtcNow;
        var by = await MyNameAsync();
        Log(app.Id, $"Offer made: PKR {input.Salary:N0} a month, joining {input.JoiningDate:d MMM yyyy}", by);
        var offerLetter = await services.GetRequiredService<LetterService>().CandidateLetterAsync(app, app.Candidate, app.JobPost!.Title);
        if (offerLetter is not null)
            Log(app.Id, $"Offer letter {offerLetter.ReferenceNo} prepared", by);
        if (input.EmailCandidate)
            notify.EmailOnly(app.Candidate.Email, $"Job offer: {app.JobPost!.Title}",
                $"Dear {app.Candidate.FirstName},\n\nWe are pleased to offer you the position of {app.JobPost.Title}.\n"
                + $"Salary: PKR {input.Salary:N0} a month\nJoining date: {input.JoiningDate:dddd, d MMMM yyyy}"
                + (app.OfferNote is null ? "" : $"\n\n{app.OfferNote}") + "\n\nPlease reply to accept or decline this offer.",
                null, CandidateFooter);
        await db.SaveChangesAsync();
    }

    /// <summary>Records the candidate's answer to the offer.</summary>
    public async Task OfferResponseAsync(Guid applicationId, bool accepted, string? note)
    {
        var app = await RunAsync(applicationId);
        RequireHrOrRecruiter(app.JobPost!);
        if (app.OfferStatus != OfferStatus.Sent || app.Stage != ApplicationStage.Offer)
            throw new DomainException("There is no open offer.");
        app.OfferStatus = accepted ? OfferStatus.Accepted : OfferStatus.Declined;
        if (!accepted)
        {
            app.Stage = ApplicationStage.Withdrawn;
            app.StageChangedAtUtc = clock.UtcNow;
        }
        var by = await MyNameAsync();
        Log(app.Id, (accepted ? "Offer accepted" : "Offer declined") + (string.IsNullOrWhiteSpace(note) ? "" : $": {note.Trim()}"), by);
        if (accepted)
            await notify.ToHrAsync(NotificationKind.Recruitment, $"{app.Candidate!.FullName} accepted the offer for {app.JobPost!.Title}",
                $"Joining {app.OfferedJoiningDate:d MMM yyyy}. Hire them to create the employee record.", $"/recruitment/applications/{app.Id}");
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Makes the candidate an employee: their details, the post's department and designation, the hiring manager as
    /// manager and the offered joining date. The CV moves to their documents, onboarding starts, and the post is
    /// marked filled once all positions are.
    /// </summary>
    public async Task<Employee> HireAsync(Guid applicationId, string? employeeCode)
    {
        RequireHr();
        var app = await RunAsync(applicationId);
        if (app.Stage == ApplicationStage.Hired)
            throw new DomainException("This candidate has already been hired.");
        if (app.OfferStatus != OfferStatus.Accepted)
            throw new DomainException("The candidate must accept an offer first.");
        var candidate = app.Candidate!;
        if (candidate.EmployeeId is { } existing && await db.Employees.AnyAsync(e => e.Id == existing && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new DomainException($"{candidate.FullName} already works here.");
        var post = app.JobPost!;

        var employee = await employees.CreateAsync(new NewEmployee(
            candidate.FirstName, string.IsNullOrWhiteSpace(candidate.LastName) ? "-" : candidate.LastName, app.OfferedJoiningDate!.Value,
            EmployeeCode: employeeCode, Email: candidate.Email, Phone: candidate.Phone, Cnic: candidate.Cnic,
            DepartmentId: post.DepartmentId, DesignationId: post.DesignationId, ManagerId: post.HiringManagerEmployeeId));
        db.EmployeeSalaries.Add(new EmployeeSalary { EmployeeId = employee.Id, EffectiveFrom = employee.JoiningDate, BasicSalary = app.OfferedSalary!.Value });

        var cvs = await db.Attachments.Where(a => a.OwnerType == AttachmentOwner.Candidate && a.OwnerId == candidate.Id).ToListAsync();
        foreach (var cv in cvs)
        {
            var data = await db.AttachmentContents.Where(c => c.Id == cv.Id).Select(c => c.Data).FirstAsync();
            var copy = new Attachment
            {
                OwnerType = AttachmentOwner.EmployeeDocument, OwnerId = employee.Id, FileName = cv.FileName, ContentType = cv.ContentType,
                SizeBytes = cv.SizeBytes, Category = DocumentCategory.Other, Note = "CV from hiring", UploadedByUserId = cv.UploadedByUserId, UploadedByName = cv.UploadedByName,
            };
            db.Attachments.Add(copy);
            db.AttachmentContents.Add(new AttachmentContent { Id = copy.Id, Data = data });
        }

        candidate.EmployeeId = employee.Id;
        app.Stage = ApplicationStage.Hired;
        app.StageChangedAtUtc = clock.UtcNow;
        app.HiredAtUtc = clock.UtcNow;
        var by = await MyNameAsync();
        Log(app.Id, $"Hired as {employee.EmployeeCode}, joining {employee.JoiningDate:d MMM yyyy}", by);

        // Other jobs the person was being considered for.
        foreach (var other in await db.JobApplications.Where(a => a.CandidateId == candidate.Id && a.Id != app.Id
                     && a.Stage != ApplicationStage.Hired && a.Stage != ApplicationStage.Rejected && a.Stage != ApplicationStage.Withdrawn).ToListAsync())
        {
            other.Stage = ApplicationStage.Withdrawn;
            other.StageChangedAtUtc = clock.UtcNow;
            Log(other.Id, $"Withdrawn: hired for {post.Title}", by);
        }

        var hired = await db.JobApplications.CountAsync(a => a.JobPostId == post.Id && a.Stage == ApplicationStage.Hired) + 1;
        if (hired >= post.Positions)
            post.Status = JobPostStatus.Filled;
        if (post.HiringManagerEmployeeId is { } hm)
            await notify.ToEmployeesAsync([hm], NotificationKind.Recruitment, $"{candidate.FullName} joins your team on {employee.JoiningDate:d MMM}",
                post.Title, $"/employees/{employee.Id}");
        await db.SaveChangesAsync();
        await services.GetRequiredService<LetterService>().AutoIssueAsync(employee.Id, LetterAutoUse.Hire);
        return employee;
    }

    // ---------- Helpers ----------

    private void AddCv(Guid candidateId, CvFile cv, string? by)
    {
        var name = Path.GetFileName(cv.FileName.Trim());
        var attachment = new Attachment
        {
            OwnerType = AttachmentOwner.Candidate,
            OwnerId = candidateId,
            FileName = name.Length > 200 ? name[^200..] : name,
            ContentType = AttachmentService.Types[Path.GetExtension(name)],
            SizeBytes = cv.Data.Length,
            UploadedByUserId = me.UserId,
            UploadedByName = by ?? "Candidate",
        };
        db.Attachments.Add(attachment);
        db.AttachmentContents.Add(new AttachmentContent { Id = attachment.Id, Data = cv.Data });
    }

    /// <summary>CVs are PDF or Word only.</summary>
    public static void CheckCv(CvFile cv)
    {
        var ext = Path.GetExtension(cv.FileName ?? "").ToLowerInvariant();
        if (ext is not (".pdf" or ".doc" or ".docx"))
            throw new DomainException("Upload the CV as a PDF or Word file.");
        if (cv.Data.Length == 0 || cv.Data.Length > AttachmentService.MaxBytes)
            throw new DomainException("The CV must be smaller than 5 MB.");
    }

    private void Log(Guid applicationId, string text, string? by) =>
        db.ApplicationEvents.Add(new ApplicationEvent { ApplicationId = applicationId, Text = text.Length > 2000 ? text[..2000] : text, ByName = by });

    private async Task ValidateJobAsync(string title, Guid? departmentId, Guid? designationId, int positions, decimal? min, decimal? max)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Enter the job title.");
        if (positions is < 1 or > 500)
            throw new DomainException("Positions must be 1 to 500.");
        if (min is < 0 || max is < 0 || min is { } a && max is { } b && a > b)
            throw new DomainException("Check the salary range.");
        if (departmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d))
            throw new NotFoundException("Department");
        if (designationId is { } g && !await db.Designations.AnyAsync(x => x.Id == g))
            throw new NotFoundException("Designation");
    }

    private async Task<string> NewSlugAsync(string title)
    {
        var root = Slugify(title, 60);
        if (root.Length == 0)
            root = "job";
        string slug;
        do
            slug = $"{root}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant()}";
        while (await db.JobPosts.AnyAsync(p => p.Slug == slug));
        return slug;
    }

    public static string Slugify(string text, int max)
    {
        var sb = new StringBuilder();
        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
                sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        var s = sb.ToString().Trim('-');
        return s.Length > max ? s[..max].TrimEnd('-') : s;
    }

    private async Task<string> LocalTextAsync(DateTime utc)
    {
        return (await clock.ToLocalAsync(utc)).ToString("ddd d MMM yyyy, h:mm tt");
    }

    private void RequireHr()
    {
        if (!me.IsHr())
            throw new ForbiddenException("Only HR can do this.");
    }

    private void RequireHrOrRecruiter(JobPost post)
    {
        if (!me.IsHr() && post.RecruiterEmployeeId != me.EmployeeId)
            throw new ForbiddenException("Only HR and the recruiter handle offers.");
    }

    private async Task RequireRecruiterAsync()
    {
        if (!await IsRecruiterAsync())
            throw new ForbiddenException("Only HR and hiring teams can do this.");
    }

    private Task<string?> MyNameAsync() =>
        me.UserId is { } uid ? db.Users.IgnoreQueryFilters().Where(u => u.Id == uid).Select(u => (string?)u.FullName).FirstOrDefaultAsync() : Task.FromResult<string?>(null);

    private Guid MyId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");

    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();

    private const string CandidateFooter = "You get this email because you applied for a job. Reply to it if you have any questions.";

    public static string Label(ApplicationStage s) => s switch
    {
        ApplicationStage.OnHold => "On hold",
        _ => s.ToString(),
    };

    public static string Label(CandidateSource s) => s switch
    {
        CandidateSource.CareersPage => "Careers page",
        CandidateSource.WalkIn => "Walk-in",
        _ => s.ToString(),
    };

    public static string Label(InterviewMode m) => m switch
    {
        InterviewMode.Onsite => "In person",
        _ => m.ToString(),
    };

    public static string Label(InterviewStatus s) => s switch
    {
        InterviewStatus.NoShow => "No-show",
        _ => s.ToString(),
    };

    public static string Label(HireRecommendation r) => r switch
    {
        HireRecommendation.StrongNo => "Strong no",
        HireRecommendation.StrongYes => "Strong yes",
        _ => r.ToString(),
    };
}
