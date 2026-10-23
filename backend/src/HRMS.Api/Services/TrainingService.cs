using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record AttendanceMark(Guid EnrollmentId, EnrollmentStatus Status, int? Score);

public record TrainingFeedbackInput(int Rating, int? TrainerRating, bool? WouldRecommend, string? Comment);

public record TrainingRequestInput(Guid? ProgramId, string? CourseName, string? Provider, DateOnly? PreferredDate, decimal? Cost, string Reason);

/// <summary>Where one person stands on one mandatory course.</summary>
public enum ComplianceState
{
    Done = 1,
    DueSoon = 2,
    Overdue = 3,
    NotDone = 4,
}

public record ComplianceCell(Guid EmployeeId, Guid ProgramId, ComplianceState State, DateOnly? LastDone, DateOnly? DueOn, bool Scheduled);

/// <summary>
/// Training: a catalog of courses, scheduled sessions people are nominated to, attendance and scores, participant
/// feedback, requests for courses (through the training approval chain) and who is up to date on mandatory courses.
/// HR runs it; managers nominate their team; a session's trainer marks its attendance.
/// </summary>
public class TrainingService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, NotificationService notify)
{
    public const int DueSoonDays = 30;

    /// <summary>People the signed-in user may nominate: everyone for HR, otherwise their direct reports.</summary>
    public IQueryable<Employee> Nominatable()
    {
        var q = db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
        return me.IsHr() ? q : q.Where(e => e.ManagerId == me.EmployeeId && me.EmployeeId != null);
    }

    public bool CanRunSession(TrainingSession s) => me.IsHr() || (me.EmployeeId is { } mine && s.TrainerEmployeeId == mine);

    public async Task<int> EnrollAsync(Guid sessionId, List<Guid> employeeIds)
    {
        var session = await db.TrainingSessions.Include(s => s.Program).FirstOrDefaultAsync(s => s.Id == sessionId) ?? throw new NotFoundException("Session");
        if (session.Status != TrainingSessionStatus.Scheduled)
            throw new DomainException("This session is not open for nominations.");
        if (session.StartsAtUtc < clock.UtcNow)
            throw new DomainException("This session has already started.");
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0)
            throw new DomainException("Choose who attends.");
        var allowed = await Nominatable().Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync();
        if (allowed.Count != ids.Count)
            throw new ForbiddenException(me.IsHr() ? "Some people do not work here any more." : "You can nominate only your own team.");
        var already = await db.TrainingEnrollments.Where(x => x.SessionId == sessionId && x.Status != EnrollmentStatus.Withdrawn).Select(x => x.EmployeeId).ToListAsync();
        var fresh = ids.Except(already).ToList();
        if (session.Capacity is { } cap && already.Count + fresh.Count > cap)
            throw new DomainException($"Only {Math.Max(0, cap - already.Count)} seats are left.");
        var by = await LetterService.CurrentUserNameAsync(db, me);
        var withdrawn = await db.TrainingEnrollments.Where(x => x.SessionId == sessionId && fresh.Contains(x.EmployeeId)).ToListAsync();
        foreach (var id in fresh)
        {
            var e = withdrawn.FirstOrDefault(x => x.EmployeeId == id);
            if (e is null)
                db.TrainingEnrollments.Add(new TrainingEnrollment { SessionId = sessionId, EmployeeId = id, NominatedByName = by });
            else
            {
                e.Status = EnrollmentStatus.Enrolled;
                e.NominatedByName = by;
            }
        }
        await notify.ToEmployeesAsync(fresh, NotificationKind.Training, $"You are booked on {session.Program!.Title}",
            $"{await WhenAsync(session)}" + (session.Venue is null ? "" : $" · {session.Venue}"), "/training", includeMe: true);
        await db.SaveChangesAsync();
        return fresh.Count;
    }

    public async Task WithdrawAsync(Guid enrollmentId)
    {
        var e = await db.TrainingEnrollments.Include(x => x.Session).FirstOrDefaultAsync(x => x.Id == enrollmentId) ?? throw new NotFoundException("Nomination");
        var mineOrTeam = e.EmployeeId == me.EmployeeId || await Nominatable().AnyAsync(x => x.Id == e.EmployeeId);
        if (!mineOrTeam)
            throw new ForbiddenException("You cannot change this nomination.");
        if (e.Status != EnrollmentStatus.Enrolled || e.Session!.Status != TrainingSessionStatus.Scheduled)
            throw new DomainException("Only upcoming nominations can be withdrawn.");
        e.Status = EnrollmentStatus.Withdrawn;
        await db.SaveChangesAsync();
    }

    /// <summary>HR or the trainer: who came, who passed. Completing sets the completion date to the session's last day.</summary>
    public async Task MarkAsync(Guid sessionId, List<AttendanceMark> marks)
    {
        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId) ?? throw new NotFoundException("Session");
        if (!CanRunSession(session))
            throw new ForbiddenException("Only HR and the trainer mark attendance.");
        if (session.Status == TrainingSessionStatus.Cancelled)
            throw new DomainException("This session was cancelled.");
        if (session.StartsAtUtc > clock.UtcNow)
            throw new DomainException("Mark attendance once the session has started.");
        var ids = marks.Select(m => m.EnrollmentId).ToList();
        var list = await db.TrainingEnrollments.Where(x => x.SessionId == sessionId && ids.Contains(x.Id)).ToListAsync();
        var endDay = DateOnly.FromDateTime(await clock.ToLocalAsync(session.EndsAtUtc));
        foreach (var m in marks)
        {
            var e = list.FirstOrDefault(x => x.Id == m.EnrollmentId) ?? throw new NotFoundException("Nomination");
            if (m.Status is EnrollmentStatus.Enrolled or EnrollmentStatus.Withdrawn || !Enum.IsDefined(m.Status))
                throw new DomainException("Mark each person attended, completed, failed or no-show.");
            if (m.Score is < 0 or > 100)
                throw new DomainException("Scores are 0 to 100.");
            e.Status = m.Status;
            e.Score = m.Score;
            e.CompletedOn = m.Status == EnrollmentStatus.Completed ? endDay : null;
        }
        if (list.Count > 0 && !await db.TrainingEnrollments.AnyAsync(x => x.SessionId == sessionId && x.Status == EnrollmentStatus.Enrolled && !ids.Contains(x.Id)))
            session.Status = TrainingSessionStatus.Completed;
        var done = list.Where(x => x.Status is EnrollmentStatus.Completed or EnrollmentStatus.Attended).Select(x => x.EmployeeId).ToList();
        var program = await db.TrainingPrograms.Where(p => p.Id == session.ProgramId).Select(p => p.Title).FirstAsync();
        await notify.ToEmployeesAsync(done, NotificationKind.Training, $"How was {program}?", "Tell us what you thought of the session.", "/training", includeMe: true);
        await db.SaveChangesAsync();
    }

    public async Task FeedbackAsync(Guid enrollmentId, TrainingFeedbackInput input)
    {
        var e = await db.TrainingEnrollments.FirstOrDefaultAsync(x => x.Id == enrollmentId && x.EmployeeId == me.EmployeeId) ?? throw new NotFoundException("Nomination");
        if (e.Status is not (EnrollmentStatus.Attended or EnrollmentStatus.Completed or EnrollmentStatus.Failed))
            throw new DomainException("Give feedback after attending.");
        if (input.Rating is < 1 or > 5 || input.TrainerRating is < 1 or > 5)
            throw new DomainException("Rate from 1 to 5.");
        e.FeedbackRating = input.Rating;
        e.TrainerRating = input.TrainerRating;
        e.WouldRecommend = input.WouldRecommend;
        e.FeedbackComment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        e.FeedbackAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
    }

    // ---------- Requests ----------

    public async Task<TrainingRequest> RequestAsync(TrainingRequestInput input)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        string title;
        if (input.ProgramId is { } pid)
        {
            title = await db.TrainingPrograms.Where(p => p.Id == pid && p.IsActive).Select(p => p.Title).FirstOrDefaultAsync() ?? throw new NotFoundException("Course");
            if (await db.TrainingRequests.AnyAsync(r => r.EmployeeId == employeeId && r.ProgramId == pid && r.Status == RequestStatus.Pending))
                throw new DomainException("You have already asked for this course.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input.CourseName))
                throw new DomainException("Choose a course or name the outside course.");
            title = input.CourseName.Trim();
        }
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new DomainException("Say how the course will help your work.");
        if (input.Cost is < 0)
            throw new DomainException("The cost cannot be negative.");
        var r = new TrainingRequest
        {
            EmployeeId = employeeId,
            ProgramId = input.ProgramId,
            CourseName = input.ProgramId is null ? Clean(input.CourseName, 200) : null,
            Provider = input.ProgramId is null ? Clean(input.Provider, 200) : null,
            PreferredDate = input.PreferredDate,
            Cost = input.Cost is > 0 ? input.Cost : null,
            Reason = input.Reason.Trim(),
        };
        db.TrainingRequests.Add(r);
        var summary = title + (r.Provider is null ? "" : $" ({r.Provider})") + (r.Cost is { } c ? $" · PKR {c:N0}" : "") + $" · {r.Reason}";
        var approval = await approvals.StartAsync(ApprovalRequestType.Training, r.Id, employeeId, summary);
        if (approval.Status == ApprovalStatus.Approved)
            await ApproveAsync(r, title, "Approved automatically");
        await db.SaveChangesAsync();
        return r;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var r = await db.TrainingRequests.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Training request");
        if (r.Status != RequestStatus.Pending)
            throw new DomainException("This request is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Training, r.Id, r.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
            await ApproveAsync(r, await TitleAsync(r), note);
        else if (outcome == ApprovalStatus.Rejected)
        {
            r.Status = RequestStatus.Rejected;
            r.DecidedAtUtc = clock.UtcNow;
            r.DecisionNote = Clean(note, 1000);
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelRequestAsync(Guid id)
    {
        var r = await db.TrainingRequests.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Training request");
        if (r.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this request.");
        if (r.Status is not (RequestStatus.Pending or RequestStatus.Approved) || r.CompletedOn is not null)
            throw new DomainException("This request can no longer be cancelled.");
        if (r.Status == RequestStatus.Pending)
            await approvals.CancelAsync(ApprovalRequestType.Training, r.Id);
        r.Status = RequestStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    /// <summary>HR: an approved outside course is done (the certificate goes on the request as a file).</summary>
    public async Task CompleteRequestAsync(Guid id, DateOnly completedOn)
    {
        if (!me.IsHr())
            throw new ForbiddenException("Only HR records completed courses.");
        var r = await db.TrainingRequests.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Training request");
        if (r.Status != RequestStatus.Approved)
            throw new DomainException("Only approved requests can be completed.");
        if (completedOn > await clock.TodayAsync())
            throw new DomainException("The completion date is in the future.");
        r.CompletedOn = completedOn;
        await db.SaveChangesAsync();
    }

    private async Task ApproveAsync(TrainingRequest r, string title, string? note)
    {
        r.Status = RequestStatus.Approved;
        r.DecidedAtUtc = clock.UtcNow;
        r.DecisionNote = Clean(note, 1000);
        var name = await db.Employees.Where(e => e.Id == r.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        await notify.ToHrAsync(NotificationKind.Training, $"Training approved for {name}: {title}",
            r.ProgramId is null ? "Arrange the outside course and record it when done." : "Book them on a session.", "/training?tab=requests");
    }

    private async Task<string> TitleAsync(TrainingRequest r) =>
        r.ProgramId is { } pid ? await db.TrainingPrograms.Where(p => p.Id == pid).Select(p => p.Title).FirstAsync() : r.CourseName ?? "Course";

    // ---------- Compliance ----------

    /// <summary>Mandatory courses against the given people: done and valid, due soon, overdue or not done.</summary>
    public async Task<List<ComplianceCell>> ComplianceAsync(IQueryable<Employee> people)
    {
        var today = await clock.TodayAsync();
        var programs = await db.TrainingPrograms.Where(p => p.IsMandatory && p.IsActive).ToListAsync();
        var staff = await people.Select(e => new { e.Id, e.DepartmentId, e.JoiningDate }).ToListAsync();
        var ids = staff.Select(s => s.Id).ToList();
        var pids = programs.Select(p => p.Id).ToList();
        var done = await db.TrainingEnrollments.Where(x => ids.Contains(x.EmployeeId) && x.Status == EnrollmentStatus.Completed && pids.Contains(x.Session!.ProgramId))
            .Select(x => new { x.EmployeeId, x.Session!.ProgramId, Date = x.CompletedOn!.Value }).ToListAsync();
        done.AddRange(await db.TrainingRequests.Where(r => ids.Contains(r.EmployeeId) && r.CompletedOn != null && r.ProgramId != null && pids.Contains(r.ProgramId.Value))
            .Select(r => new { r.EmployeeId, ProgramId = r.ProgramId!.Value, Date = r.CompletedOn!.Value }).ToListAsync());
        var now = clock.UtcNow;
        var booked = await db.TrainingEnrollments.Where(x => ids.Contains(x.EmployeeId) && x.Status == EnrollmentStatus.Enrolled && x.Session!.StartsAtUtc > now
                                                             && x.Session.Status == TrainingSessionStatus.Scheduled && pids.Contains(x.Session.ProgramId))
            .Select(x => new { x.EmployeeId, x.Session!.ProgramId }).ToListAsync();

        var cells = new List<ComplianceCell>();
        foreach (var p in programs)
        {
            var depts = HelpdeskService.Ids(p.DepartmentIds);
            foreach (var s in staff.Where(s => depts.Count == 0 || (s.DepartmentId is { } d && depts.Contains(d))))
            {
                var last = done.Where(x => x.EmployeeId == s.Id && x.ProgramId == p.Id).Select(x => (DateOnly?)x.Date).Max();
                DateOnly? due = last is { } l
                    ? p.RepeatEveryMonths is { } m ? l.AddMonths(m) : null
                    : p.CompleteWithinDays is { } days ? s.JoiningDate.AddDays(days) : null;
                var state = due is { } dd && dd < today ? ComplianceState.Overdue
                    : due is { } ds && ds <= today.AddDays(DueSoonDays) ? ComplianceState.DueSoon
                    : last is not null ? ComplianceState.Done
                    : ComplianceState.NotDone;
                cells.Add(new ComplianceCell(s.Id, p.Id, state, last, due, booked.Any(b => b.EmployeeId == s.Id && b.ProgramId == p.Id)));
            }
        }
        return cells;
    }

    private async Task<string> WhenAsync(TrainingSession s) => (await clock.ToLocalAsync(s.StartsAtUtc)).ToString("ddd d MMM yyyy, h:mm tt");

    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();
}
