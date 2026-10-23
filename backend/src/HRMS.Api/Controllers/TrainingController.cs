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

public record TrainingMeDto(bool IsHr, bool HasTeam, bool IsTrainer);

public record TrainingCategoryDto(Guid Id, string Name, int Programs);

public record SaveTrainingCategoryRequest([Required, StringLength(100)] string Name);

public record TrainingProgramDto(
    Guid Id, string Title, Guid? CategoryId, string? Category, string? Description, TrainingMode Mode, decimal? DurationHours, string? Link, bool IsMandatory,
    List<Guid> DepartmentIds, int? CompleteWithinDays, int? RepeatEveryMonths, bool IsActive, int Sessions, int Completed);

public record SaveTrainingProgramRequest(
    [Required, StringLength(150)] string Title, Guid? CategoryId, [StringLength(4000)] string? Description, TrainingMode Mode, decimal? DurationHours,
    [StringLength(500)] string? Link, bool IsMandatory, List<Guid>? DepartmentIds, int? CompleteWithinDays, int? RepeatEveryMonths, bool IsActive = true);

public record TrainingSessionDto(
    Guid Id, Guid ProgramId, string Program, TrainingMode Mode, DateTime StartsAtUtc, DateTime EndsAtUtc, string? Venue, Guid? TrainerEmployeeId, string? Trainer,
    string? ExternalTrainer, int? Capacity, decimal? Cost, TrainingSessionStatus Status, string? Notes, int Enrolled, int Completed, double? AverageRating,
    bool CanRun);

public record SaveTrainingSessionRequest(
    Guid ProgramId, DateTime StartsAtUtc, DateTime EndsAtUtc, [StringLength(500)] string? Venue, Guid? TrainerEmployeeId, [StringLength(200)] string? ExternalTrainer,
    int? Capacity, decimal? Cost, [StringLength(2000)] string? Notes);

public record EnrollmentDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Department, EnrollmentStatus Status, string? NominatedByName, int? Score,
    DateOnly? CompletedOn, int? FeedbackRating, int? TrainerRating, bool? WouldRecommend, string? FeedbackComment);

public record SessionDetailDto(TrainingSessionDto Session, List<EnrollmentDto> Enrollments, bool CanNominate);

public record EnrollRequest([Required] List<Guid> EmployeeIds);

public record TrainingAttendanceRequest([Required] List<AttendanceMark> Marks);

public record SessionStatusRequest(TrainingSessionStatus Status);

public record FeedbackRequestBody(int Rating, int? TrainerRating, bool? WouldRecommend, [StringLength(2000)] string? Comment);

public record MyTrainingDto(
    Guid EnrollmentId, Guid SessionId, string Program, TrainingMode Mode, DateTime StartsAtUtc, DateTime EndsAtUtc, string? Venue, string? Trainer,
    EnrollmentStatus Status, int? Score, DateOnly? CompletedOn, bool FeedbackGiven, string? Link);

public record SaveTrainingRequestBody(Guid? ProgramId, [StringLength(200)] string? CourseName, [StringLength(200)] string? Provider, DateOnly? PreferredDate, decimal? Cost,
    [Required, StringLength(2000)] string Reason);

public record TrainingRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, Guid? ProgramId, string Course, string? Provider, DateOnly? PreferredDate, decimal? Cost, string Reason,
    RequestStatus Status, string? DecisionNote, DateOnly? CompletedOn, DateTime CreatedAtUtc);

public record CompleteRequestBody(DateOnly CompletedOn);

public record ComplianceProgramDto(Guid Id, string Title, int? RepeatEveryMonths);

public record ComplianceRowDto(Guid EmployeeId, string Name, string Code, string? Department, List<ComplianceCell> Cells);

public record ComplianceDto(List<ComplianceProgramDto> Programs, List<ComplianceRowDto> Rows, int Done, int DueSoon, int Overdue, int NotDone);

public record TrainingReportRowDto(string Name, int Sessions, int People, decimal Hours, decimal Cost, double? Rating);

public record TrainingReportDto(int Sessions, int PeopleTrained, decimal Hours, decimal Cost, double? AverageRating, int? RecommendPercent,
    List<TrainingReportRowDto> ByProgram, List<TrainingReportRowDto> ByDepartment);

[ApiController]
[Route("api/training")]
[Authorize(Policy = Policies.CompanyUser)]
public class TrainingController(HrmsDbContext db, TrainingService training, TenantClock clock, ICurrentUser me) : ControllerBase
{
    [HttpGet("me")]
    public async Task<TrainingMeDto> Me() => new(me.IsHr(),
        me.EmployeeId is { } mine && await db.Employees.AnyAsync(e => e.ManagerId == mine && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)),
        me.EmployeeId is { } m2 && await db.TrainingSessions.AnyAsync(s => s.TrainerEmployeeId == m2));

    // ---------- Catalog ----------

    [HttpGet("categories")]
    public async Task<List<TrainingCategoryDto>> Categories() =>
        await db.TrainingCategories.OrderBy(c => c.Name).Select(c => new TrainingCategoryDto(c.Id, c.Name, db.TrainingPrograms.Count(p => p.CategoryId == c.Id))).ToListAsync();

    [HttpPost("categories"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddCategory(SaveTrainingCategoryRequest req)
    {
        var name = req.Name.Trim();
        if (await db.TrainingCategories.AnyAsync(c => c.Name == name))
            throw new DomainException("This category already exists.");
        db.TrainingCategories.Add(new TrainingCategory { Name = name });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("categories/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateCategory(Guid id, SaveTrainingCategoryRequest req)
    {
        var c = await db.TrainingCategories.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Category");
        var name = req.Name.Trim();
        if (await db.TrainingCategories.AnyAsync(x => x.Name == name && x.Id != id))
            throw new DomainException("This category already exists.");
        c.Name = name;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("categories/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var c = await db.TrainingCategories.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Category");
        if (await db.TrainingPrograms.AnyAsync(p => p.CategoryId == id))
            throw new DomainException("Courses use this category.");
        db.TrainingCategories.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("programs")]
    public async Task<List<TrainingProgramDto>> Programs()
    {
        var q = db.TrainingPrograms.AsQueryable();
        if (!me.IsHr())
            q = q.Where(p => p.IsActive);
        var list = await q.OrderBy(p => p.Title).Select(p => new
        {
            P = p,
            Category = db.TrainingCategories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault(),
            Sessions = db.TrainingSessions.Count(s => s.ProgramId == p.Id),
            Completed = db.TrainingEnrollments.Count(x => x.Session!.ProgramId == p.Id && x.Status == EnrollmentStatus.Completed),
        }).ToListAsync();
        return list.Select(x => new TrainingProgramDto(x.P.Id, x.P.Title, x.P.CategoryId, x.Category, x.P.Description, x.P.Mode, x.P.DurationHours, x.P.Link,
            x.P.IsMandatory, HelpdeskService.Ids(x.P.DepartmentIds), x.P.CompleteWithinDays, x.P.RepeatEveryMonths, x.P.IsActive, x.Sessions, x.Completed)).ToList();
    }

    [HttpPost("programs"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddProgram(SaveTrainingProgramRequest req)
    {
        var p = new TrainingProgram();
        await ApplyAsync(p, req);
        db.TrainingPrograms.Add(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("programs/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateProgram(Guid id, SaveTrainingProgramRequest req)
    {
        var p = await db.TrainingPrograms.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Course");
        await ApplyAsync(p, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("programs/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteProgram(Guid id)
    {
        var p = await db.TrainingPrograms.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Course");
        if (await db.TrainingSessions.AnyAsync(s => s.ProgramId == id) || await db.TrainingRequests.AnyAsync(r => r.ProgramId == id))
            throw new DomainException("This course has sessions or requests. Turn it off instead.");
        db.TrainingPrograms.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Sessions ----------

    /// <summary>Upcoming and recent sessions. Everyone sees them (to know what is on); only HR and trainers run them.</summary>
    [HttpGet("sessions")]
    public async Task<List<TrainingSessionDto>> Sessions([FromQuery] bool past = false)
    {
        var now = clock.UtcNow;
        var q = past ? db.TrainingSessions.Where(s => s.EndsAtUtc < now) : db.TrainingSessions.Where(s => s.EndsAtUtc >= now || s.Status == TrainingSessionStatus.Scheduled);
        var list = await (past ? q.OrderByDescending(s => s.StartsAtUtc) : q.OrderBy(s => s.StartsAtUtc)).Include(s => s.Program).Take(300).ToListAsync();
        return await SessionDtosAsync(list);
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<SessionDetailDto> Session(Guid id)
    {
        var s = await db.TrainingSessions.Include(x => x.Program).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Session");
        var team = training.Nominatable().Select(e => e.Id);
        var q = db.TrainingEnrollments.Where(x => x.SessionId == id);
        if (!training.CanRunSession(s))
            q = q.Where(x => x.EmployeeId == me.EmployeeId || team.Contains(x.EmployeeId));
        var canRun = training.CanRunSession(s);
        var rows = await q.OrderBy(x => x.Employee!.FirstName).Select(x => new EnrollmentDto(x.Id, x.EmployeeId, x.Employee!.FirstName + " " + x.Employee.LastName,
            x.Employee.EmployeeCode, x.Employee.Department != null ? x.Employee.Department.Name : null, x.Status, x.NominatedByName, x.Score, x.CompletedOn,
            canRun ? x.FeedbackRating : null, canRun ? x.TrainerRating : null, canRun ? x.WouldRecommend : null, canRun ? x.FeedbackComment : null)).ToListAsync();
        return new SessionDetailDto((await SessionDtosAsync([s]))[0], rows, await training.Nominatable().AnyAsync());
    }

    [HttpPost("sessions"), Authorize(Policy = Policies.Hr)]
    public async Task<TrainingSessionDto> AddSession(SaveTrainingSessionRequest req)
    {
        var s = new TrainingSession();
        await ApplyAsync(s, req);
        db.TrainingSessions.Add(s);
        await db.SaveChangesAsync();
        await db.Entry(s).Reference(x => x.Program).LoadAsync();
        return (await SessionDtosAsync([s]))[0];
    }

    [HttpPut("sessions/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateSession(Guid id, SaveTrainingSessionRequest req)
    {
        var s = await db.TrainingSessions.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Session");
        if (s.Status != TrainingSessionStatus.Scheduled)
            throw new DomainException("Only scheduled sessions can be changed.");
        await ApplyAsync(s, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("sessions/{id:guid}/status"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SessionStatus(Guid id, SessionStatusRequest req, [FromServices] NotificationService notify)
    {
        var s = await db.TrainingSessions.Include(x => x.Program).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Session");
        if (req.Status != TrainingSessionStatus.Cancelled || s.Status != TrainingSessionStatus.Scheduled)
            throw new DomainException("Only scheduled sessions can be cancelled; mark attendance to complete one.");
        s.Status = TrainingSessionStatus.Cancelled;
        var people = await db.TrainingEnrollments.Where(x => x.SessionId == id && x.Status == EnrollmentStatus.Enrolled).Select(x => x.EmployeeId).ToListAsync();
        await notify.ToEmployeesAsync(people, NotificationKind.Training, $"{s.Program!.Title} on {(await clock.ToLocalAsync(s.StartsAtUtc)):d MMM} is cancelled", null, "/training");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("sessions/{id:guid}/enroll"), Authorize(Policy = Policies.Approver)]
    public async Task<object> Enroll(Guid id, EnrollRequest req) => new { added = await training.EnrollAsync(id, req.EmployeeIds) };

    [HttpPost("sessions/{id:guid}/attendance")]
    public async Task<IActionResult> Attendance(Guid id, TrainingAttendanceRequest req)
    {
        await training.MarkAsync(id, req.Marks);
        return NoContent();
    }

    [HttpPost("enrollments/{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        await training.WithdrawAsync(id);
        return NoContent();
    }

    [HttpPost("enrollments/{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, FeedbackRequestBody req)
    {
        await training.FeedbackAsync(id, new TrainingFeedbackInput(req.Rating, req.TrainerRating, req.WouldRecommend, req.Comment));
        return NoContent();
    }

    /// <summary>The people the signed-in user may nominate.</summary>
    [HttpGet("nominees"), Authorize(Policy = Policies.Approver)]
    public async Task<List<LookupDto>> Nominees() =>
        await training.Nominatable().OrderBy(e => e.FirstName).Select(e => new LookupDto(e.Id, e.EmployeeCode + " · " + e.FirstName + " " + e.LastName)).ToListAsync();

    // ---------- Mine ----------

    [HttpGet("my")]
    public async Task<List<MyTrainingDto>> My()
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        return await db.TrainingEnrollments.Where(x => x.EmployeeId == mine && x.Status != EnrollmentStatus.Withdrawn)
            .OrderByDescending(x => x.Session!.StartsAtUtc)
            .Select(x => new MyTrainingDto(x.Id, x.SessionId, x.Session!.Program!.Title, x.Session.Program.Mode, x.Session.StartsAtUtc, x.Session.EndsAtUtc, x.Session.Venue,
                x.Session.ExternalTrainer ?? db.Employees.Where(e => e.Id == x.Session.TrainerEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                x.Session.Status == TrainingSessionStatus.Cancelled ? EnrollmentStatus.Withdrawn : x.Status, x.Score, x.CompletedOn, x.FeedbackAtUtc != null, x.Session.Program.Link))
            .ToListAsync();
    }

    [HttpGet("my/compliance")]
    public async Task<ComplianceDto> MyCompliance() => await ComplianceFor(db.Employees.Where(e => e.Id == me.EmployeeId));

    /// <summary>HR: everyone; managers: their direct reports.</summary>
    [HttpGet("compliance"), Authorize(Policy = Policies.Approver)]
    public async Task<ComplianceDto> Compliance([FromQuery] Guid? departmentId)
    {
        var q = training.Nominatable();
        if (departmentId is { } d)
            q = q.Where(e => e.DepartmentId == d);
        return await ComplianceFor(q);
    }

    // ---------- Requests ----------

    [HttpGet("requests")]
    public async Task<List<TrainingRequestDto>> Requests()
    {
        var q = db.TrainingRequests.AsQueryable();
        if (!me.IsHr())
            q = q.Where(r => r.EmployeeId == me.EmployeeId);
        return await q.OrderByDescending(r => r.CreatedAtUtc).Take(300).Select(r => new TrainingRequestDto(r.Id, r.EmployeeId, r.Employee!.FirstName + " " + r.Employee.LastName,
            r.ProgramId, r.ProgramId != null ? db.TrainingPrograms.Where(p => p.Id == r.ProgramId).Select(p => p.Title).First() : r.CourseName ?? "", r.Provider,
            r.PreferredDate, r.Cost, r.Reason, r.Status, r.DecisionNote, r.CompletedOn, r.CreatedAtUtc)).ToListAsync();
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(SaveTrainingRequestBody req)
    {
        await training.RequestAsync(new TrainingRequestInput(req.ProgramId, req.CourseName, req.Provider, req.PreferredDate, req.Cost, req.Reason));
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, TravelDecisionRequest? req)
    {
        await training.DecideAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, TravelDecisionRequest? req)
    {
        await training.DecideAsync(id, false, req?.Note);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        await training.CancelRequestAsync(id);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/complete"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> CompleteRequest(Guid id, CompleteRequestBody req)
    {
        await training.CompleteRequestAsync(id, req.CompletedOn);
        return NoContent();
    }

    // ---------- Report ----------

    [HttpGet("report"), Authorize(Policy = Policies.Hr)]
    public async Task<TrainingReportDto> Report([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var today = await clock.TodayAsync();
        var start = await clock.ToUtcAsync((from ?? new DateOnly(today.Year, 1, 1)).ToDateTime(TimeOnly.MinValue));
        var end = await clock.ToUtcAsync((to ?? new DateOnly(today.Year, 12, 31)).ToDateTime(TimeOnly.MaxValue));
        var sessions = await db.TrainingSessions.Include(s => s.Program)
            .Where(s => s.Status != TrainingSessionStatus.Cancelled && s.StartsAtUtc >= start && s.StartsAtUtc <= end).ToListAsync();
        var ids = sessions.Select(s => s.Id).ToList();
        var att = await db.TrainingEnrollments.Where(x => ids.Contains(x.SessionId) && (x.Status == EnrollmentStatus.Attended || x.Status == EnrollmentStatus.Completed || x.Status == EnrollmentStatus.Failed))
            .Select(x => new { x.SessionId, x.EmployeeId, x.FeedbackRating, x.WouldRecommend, Department = x.Employee!.Department != null ? x.Employee.Department.Name : "No department" })
            .ToListAsync();
        decimal Hours(TrainingSession s) => s.Program!.DurationHours ?? (decimal)Math.Round((s.EndsAtUtc - s.StartsAtUtc).TotalHours, 1);
        TrainingReportRowDto Row(string name, List<TrainingSession> ss, IEnumerable<dynamic>? people = null)
        {
            var sid = ss.Select(s => s.Id).ToHashSet();
            var a = att.Where(x => sid.Contains(x.SessionId)).ToList();
            var rated = a.Where(x => x.FeedbackRating != null).ToList();
            return new TrainingReportRowDto(name, ss.Count, a.Select(x => x.EmployeeId).Distinct().Count(),
                a.Sum(x => Hours(ss.First(s => s.Id == x.SessionId))), ss.Sum(s => s.Cost ?? 0), rated.Count > 0 ? Math.Round(rated.Average(x => (double)x.FeedbackRating!), 1) : null);
        }
        var rated = att.Where(x => x.FeedbackRating != null).ToList();
        var recommend = att.Where(x => x.WouldRecommend != null).ToList();
        var byDept = att.GroupBy(x => x.Department).Select(g =>
        {
            var ss = sessions.Where(s => g.Any(x => x.SessionId == s.Id)).ToList();
            var r = g.Where(x => x.FeedbackRating != null).ToList();
            return new TrainingReportRowDto(g.Key, ss.Count, g.Select(x => x.EmployeeId).Distinct().Count(), g.Sum(x => Hours(sessions.First(s => s.Id == x.SessionId))), 0,
                r.Count > 0 ? Math.Round(r.Average(x => (double)x.FeedbackRating!), 1) : null);
        }).OrderByDescending(r => r.Hours).ToList();
        return new TrainingReportDto(sessions.Count, att.Select(x => x.EmployeeId).Distinct().Count(), att.Sum(x => Hours(sessions.First(s => s.Id == x.SessionId))),
            sessions.Sum(s => s.Cost ?? 0), rated.Count > 0 ? Math.Round(rated.Average(x => (double)x.FeedbackRating!), 1) : null,
            recommend.Count > 0 ? (int)Math.Round(100.0 * recommend.Count(x => x.WouldRecommend == true) / recommend.Count) : null,
            sessions.GroupBy(s => s.Program!.Title).Select(g => Row(g.Key, g.ToList())).OrderByDescending(r => r.People).ToList(), byDept);
    }

    // ---------- Helpers ----------

    private async Task<ComplianceDto> ComplianceFor(IQueryable<Employee> people)
    {
        var cells = await training.ComplianceAsync(people);
        var programs = await db.TrainingPrograms.Where(p => p.IsMandatory && p.IsActive).OrderBy(p => p.Title)
            .Select(p => new ComplianceProgramDto(p.Id, p.Title, p.RepeatEveryMonths)).ToListAsync();
        var ids = cells.Select(c => c.EmployeeId).Distinct().ToList();
        var staff = await db.Employees.Where(e => ids.Contains(e.Id)).OrderBy(e => e.FirstName)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Department = e.Department != null ? e.Department.Name : null }).ToListAsync();
        return new ComplianceDto(programs, staff.Select(s => new ComplianceRowDto(s.Id, s.Name, s.EmployeeCode, s.Department, cells.Where(c => c.EmployeeId == s.Id).ToList())).ToList(),
            cells.Count(c => c.State == ComplianceState.Done), cells.Count(c => c.State == ComplianceState.DueSoon), cells.Count(c => c.State == ComplianceState.Overdue),
            cells.Count(c => c.State == ComplianceState.NotDone));
    }

    private async Task<List<TrainingSessionDto>> SessionDtosAsync(List<TrainingSession> list)
    {
        var ids = list.Select(s => s.Id).ToList();
        var counts = await db.TrainingEnrollments.Where(x => ids.Contains(x.SessionId) && x.Status != EnrollmentStatus.Withdrawn)
            .GroupBy(x => x.SessionId)
            .Select(g => new { g.Key, Enrolled = g.Count(), Completed = g.Count(x => x.Status == EnrollmentStatus.Completed), Rating = g.Average(x => (double?)x.FeedbackRating) })
            .ToListAsync();
        var trainerIds = list.Where(s => s.TrainerEmployeeId != null).Select(s => s.TrainerEmployeeId!.Value).Distinct().ToList();
        var names = await db.Employees.Where(e => trainerIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        return list.Select(s =>
        {
            var c = counts.FirstOrDefault(x => x.Key == s.Id);
            return new TrainingSessionDto(s.Id, s.ProgramId, s.Program!.Title, s.Program.Mode, s.StartsAtUtc, s.EndsAtUtc, s.Venue, s.TrainerEmployeeId,
                s.TrainerEmployeeId is { } t ? names.GetValueOrDefault(t) : null, s.ExternalTrainer, s.Capacity, me.IsHr() ? s.Cost : null, s.Status, s.Notes,
                c?.Enrolled ?? 0, c?.Completed ?? 0, c?.Rating is { } r ? Math.Round(r, 1) : null, training.CanRunSession(s));
        }).ToList();
    }

    private async Task ApplyAsync(TrainingProgram p, SaveTrainingProgramRequest r)
    {
        var title = r.Title.Trim();
        if (await db.TrainingPrograms.AnyAsync(x => x.Title == title && x.Id != p.Id))
            throw new DomainException("A course with this name exists.");
        if (!Enum.IsDefined(r.Mode))
            throw new DomainException("Choose how the course is given.");
        if (r.CategoryId is { } c && !await db.TrainingCategories.AnyAsync(x => x.Id == c))
            throw new NotFoundException("Category");
        if (r.DurationHours is <= 0 or > 1000 || r.CompleteWithinDays is < 1 or > 730 || r.RepeatEveryMonths is < 1 or > 120)
            throw new DomainException("Check the hours, days and months.");
        var depts = (r.DepartmentIds ?? []).Distinct().ToList();
        if (await db.Departments.CountAsync(d => depts.Contains(d.Id)) != depts.Count)
            throw new NotFoundException("Department");
        p.Title = title;
        p.CategoryId = r.CategoryId;
        p.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        p.Mode = r.Mode;
        p.DurationHours = r.DurationHours;
        p.Link = string.IsNullOrWhiteSpace(r.Link) ? null : r.Link.Trim();
        p.IsMandatory = r.IsMandatory;
        p.DepartmentIds = r.IsMandatory ? string.Join(',', depts) : "";
        p.CompleteWithinDays = r.IsMandatory ? r.CompleteWithinDays : null;
        p.RepeatEveryMonths = r.IsMandatory ? r.RepeatEveryMonths : null;
        p.IsActive = r.IsActive;
    }

    private async Task ApplyAsync(TrainingSession s, SaveTrainingSessionRequest r)
    {
        if (!await db.TrainingPrograms.AnyAsync(p => p.Id == r.ProgramId && p.IsActive))
            throw new NotFoundException("Course");
        var startsAt = DateTime.SpecifyKind(r.StartsAtUtc, DateTimeKind.Utc);
        var endsAt = DateTime.SpecifyKind(r.EndsAtUtc, DateTimeKind.Utc);
        if (endsAt <= startsAt)
            throw new DomainException("The session must end after it starts.");
        if ((endsAt - startsAt).TotalDays > 31)
            throw new DomainException("A session lasts at most a month.");
        if (r.Capacity is < 1 or > 1000 || r.Cost is < 0)
            throw new DomainException("Check the seats and cost.");
        if (r.TrainerEmployeeId is { } t && !await db.Employees.AnyAsync(e => e.Id == t))
            throw new NotFoundException("Trainer");
        if (r.TrainerEmployeeId is null && string.IsNullOrWhiteSpace(r.ExternalTrainer))
            throw new DomainException("Choose the trainer, an employee or an outside trainer.");
        if (r.Capacity is { } cap && s.Id != Guid.Empty && await db.TrainingEnrollments.CountAsync(x => x.SessionId == s.Id && x.Status != EnrollmentStatus.Withdrawn) > cap)
            throw new DomainException("More people are already booked than these seats.");
        s.ProgramId = r.ProgramId;
        s.StartsAtUtc = startsAt;
        s.EndsAtUtc = endsAt;
        s.Venue = string.IsNullOrWhiteSpace(r.Venue) ? null : r.Venue.Trim();
        s.TrainerEmployeeId = r.TrainerEmployeeId;
        s.ExternalTrainer = r.TrainerEmployeeId is null ? r.ExternalTrainer?.Trim() : null;
        s.Capacity = r.Capacity;
        s.Cost = r.Cost;
        s.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
    }
}
