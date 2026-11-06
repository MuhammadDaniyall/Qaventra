using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
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

public record QuizQuestionInput([Required, StringLength(1000)] string Text, [Required] List<string> Options, int CorrectIndex);

public record SaveQuizRequest(
    Guid ProgramId, [Required, StringLength(150)] string Title, [StringLength(2000)] string? Instructions,
    [Range(1, 100)] int PassPercent, [Range(1, 600)] int? TimeLimitMinutes, [Range(0, 50)] int MaxAttempts, bool IsActive,
    [Required] List<QuizQuestionInput> Questions);

/// <summary>HR's full view, right answers included.</summary>
public record QuizEditDto(Guid Id, Guid ProgramId, string Title, string? Instructions, int PassPercent, int? TimeLimitMinutes, int MaxAttempts, bool IsActive,
    List<QuizQuestionInput> Questions);

/// <summary>A quiz in the list. Attempt figures are the signed-in person's own; Takers/PassRate are for HR.</summary>
public record QuizListDto(Guid Id, Guid ProgramId, string Program, string Title, int Questions, int PassPercent, int? TimeLimitMinutes, int MaxAttempts,
    bool IsActive, int AttemptsUsed, int? BestScore, bool Passed, Guid? CertificateId, Guid? OpenAttemptId, int Takers, int? PassRate, RecordStampsDto Stamps);

/// <summary>A question as the person taking the quiz sees it: no right answer.</summary>
public record QuizPaperQuestion(Guid Id, string Text, List<string> Options);

public record QuizPaperDto(Guid AttemptId, Guid QuizId, string Title, string? Instructions, string Program, int PassPercent, DateTime StartedAtUtc,
    DateTime? DeadlineUtc, List<QuizPaperQuestion> Questions);

public record SubmitQuizRequest([Required] Dictionary<Guid, int> Answers);

public record QuizReviewItem(Guid QuestionId, string Text, List<string> Options, int? Chosen, int CorrectIndex);

public record QuizResultDto(Guid AttemptId, int Correct, int Total, int ScorePercent, int PassPercent, bool Passed, bool TimedOut, Guid? CertificateId,
    string? CertificateCode, List<QuizReviewItem> Review);

public record QuizAttemptRowDto(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, DateTime StartedAtUtc, DateTime? SubmittedAtUtc,
    int ScorePercent, bool Passed, bool TimedOut);

public record DraftQuizRequest(Guid ProgramId, [Range(3, 20)] int Count = 5, [StringLength(20000)] string? CourseText = null);

public record DraftQuizDto(List<QuizQuestionInput> Questions);

public record CertificateDto(Guid Id, string Code, string EmployeeName, string CourseTitle, DateOnly IssuedOn, int ScorePercent, string CompanyName,
    string? LogoDataUrl, string VerifyPath);

public record SaveLogoRequest([StringLength(300_000)] string? LogoDataUrl);

/// <summary>Course quizzes and the certificates passing them earns. Questions are shown without answers; scoring is done here.</summary>
[ApiController]
[Route("api/training")]
[Authorize(Policy = Policies.CompanyUser)]
public class TrainingQuizController(HrmsDbContext db, TrainingService training, TenantClock clock, AiAssistantService ai, ICurrentUser me) : ControllerBase
{
    /// <summary>Extra seconds after the time limit for the answers to arrive.</summary>
    public const int GraceSeconds = 30;

    [HttpGet("quizzes")]
    public async Task<List<QuizListDto>> Quizzes()
    {
        var q = db.TrainingQuizzes.AsQueryable();
        if (!training.IsHr)
            q = q.Where(x => x.IsActive && x.Program!.IsActive);
        var mine = me.EmployeeId ?? Guid.Empty;
        var rows = await q.OrderBy(x => x.Program!.Title).ThenBy(x => x.Title).Select(x => new
        {
            Quiz = x,
            Program = x.Program!.Title,
            Questions = db.TrainingQuizQuestions.Count(z => z.QuizId == x.Id),
            Used = db.TrainingQuizAttempts.Count(a => a.QuizId == x.Id && a.EmployeeId == mine && a.SubmittedAtUtc != null),
            Best = db.TrainingQuizAttempts.Where(a => a.QuizId == x.Id && a.EmployeeId == mine && a.SubmittedAtUtc != null).Max(a => (int?)a.ScorePercent),
            Passed = db.TrainingQuizAttempts.Any(a => a.QuizId == x.Id && a.EmployeeId == mine && a.Passed),
            Certificate = db.TrainingCertificates.Where(c => c.EmployeeId == mine && db.TrainingQuizAttempts.Any(a => a.Id == c.AttemptId && a.QuizId == x.Id))
                .Select(c => (Guid?)c.Id).FirstOrDefault(),
            Open = db.TrainingQuizAttempts.Where(a => a.QuizId == x.Id && a.EmployeeId == mine && a.SubmittedAtUtc == null).Select(a => (Guid?)a.Id).FirstOrDefault(),
            Takers = db.TrainingQuizAttempts.Where(a => a.QuizId == x.Id && a.SubmittedAtUtc != null).Select(a => a.EmployeeId).Distinct().Count(),
            Passers = db.TrainingQuizAttempts.Where(a => a.QuizId == x.Id && a.Passed).Select(a => a.EmployeeId).Distinct().Count(),
        }).ToListAsync();
        var hr = training.IsHr;
        return rows.Select(r => new QuizListDto(r.Quiz.Id, r.Quiz.ProgramId, r.Program, r.Quiz.Title, r.Questions, r.Quiz.PassPercent, r.Quiz.TimeLimitMinutes,
            r.Quiz.MaxAttempts, r.Quiz.IsActive, r.Used, r.Best, r.Passed, r.Certificate, r.Open, hr ? r.Takers : 0,
            hr && r.Takers > 0 ? (int)Math.Round(100.0 * r.Passers / r.Takers) : null, RecordStampsDto.Of(r.Quiz))).ToList();
    }

    [HttpGet("quizzes/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<QuizEditDto> Get(Guid id)
    {
        var quiz = await db.TrainingQuizzes.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Quiz");
        return new QuizEditDto(quiz.Id, quiz.ProgramId, quiz.Title, quiz.Instructions, quiz.PassPercent, quiz.TimeLimitMinutes, quiz.MaxAttempts, quiz.IsActive,
            quiz.Questions.OrderBy(x => x.Order).Select(x => new QuizQuestionInput(x.Text, Options(x), x.CorrectIndex)).ToList());
    }

    [HttpPost("quizzes"), Authorize(Policy = Policies.Hr)]
    public async Task<QuizEditDto> Create(SaveQuizRequest req)
    {
        var quiz = new TrainingQuiz();
        await ApplyAsync(quiz, req);
        db.TrainingQuizzes.Add(quiz);
        await db.SaveChangesAsync();
        return await Get(quiz.Id);
    }

    [HttpPut("quizzes/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<QuizEditDto> Update(Guid id, SaveQuizRequest req)
    {
        var quiz = await db.TrainingQuizzes.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Quiz");
        await ApplyAsync(quiz, req);
        await db.SaveChangesAsync();
        return await Get(id);
    }

    [HttpDelete("quizzes/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var quiz = await db.TrainingQuizzes.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Quiz");
        if (await db.TrainingQuizAttempts.AnyAsync(a => a.QuizId == id))
            throw new DomainException("People have taken this quiz. Turn it off instead.");
        db.TrainingQuizzes.Remove(quiz);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Everyone's attempts at a quiz, newest first.</summary>
    [HttpGet("quizzes/{id:guid}/attempts"), Authorize(Policy = Policies.Hr)]
    public Task<List<QuizAttemptRowDto>> Attempts(Guid id) =>
        db.TrainingQuizAttempts.Where(a => a.QuizId == id && a.SubmittedAtUtc != null).OrderByDescending(a => a.SubmittedAtUtc).Take(500)
            .Select(a => new QuizAttemptRowDto(a.Id, a.EmployeeId, a.Employee!.FirstName + " " + a.Employee.LastName, a.Employee.EmployeeCode, a.StartedAtUtc,
                a.SubmittedAtUtc, a.ScorePercent, a.Passed, a.TimedOut))
            .ToListAsync();

    /// <summary>
    /// The free AI drafts questions from the course description (or text HR pastes). Nothing is saved: HR checks and edits
    /// the questions in the quiz form first.
    /// </summary>
    [HttpPost("quizzes/draft"), Authorize(Policy = Policies.Hr)]
    public async Task<DraftQuizDto> Draft(DraftQuizRequest req)
    {
        var program = await db.TrainingPrograms.FirstOrDefaultAsync(p => p.Id == req.ProgramId) ?? throw new NotFoundException("Course");
        var text = string.IsNullOrWhiteSpace(req.CourseText) ? program.Description : req.CourseText;
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 40)
            throw new DomainException("Add a course description (or paste the course text) so the AI has something to write questions from.");
        var answer = await ai.FreeJsonAsync<AiQuizJson>(
            $"You write {req.Count} multiple-choice questions that check whether an employee understood a training course. " +
            "Use only facts in the course text. Each question has 4 short options and exactly one right answer. Simple, clear English. " +
            "Reply as JSON: {\"questions\": [{\"text\": string, \"options\": [4 strings], \"correctIndex\": 0-3}]}.",
            $"Course: {program.Title}\n\n{text.Trim()}");
        var questions = (answer?.Questions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Text) && x.Options is { Count: >= 2 and <= 6 } && x.CorrectIndex >= 0 && x.CorrectIndex < x.Options.Count
                        && x.Options.All(o => !string.IsNullOrWhiteSpace(o)))
            .Take(req.Count)
            .Select(x => new QuizQuestionInput(Cut(x.Text!.Trim(), 1000), x.Options!.Select(o => Cut(o.Trim(), 300)).ToList(), x.CorrectIndex))
            .ToList();
        if (questions.Count == 0)
            throw new DomainException("The AI did not answer. Please try again in a minute, or write the questions yourself.");
        return new DraftQuizDto(questions);
    }

    /// <summary>Starts (or resumes) an attempt and returns the questions without the right answers.</summary>
    [HttpPost("quizzes/{id:guid}/start")]
    public async Task<QuizPaperDto> Start(Guid id)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Only employees can take quizzes.");
        var quiz = await db.TrainingQuizzes.Include(x => x.Program).Include(x => x.Questions)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive && x.Program!.IsActive) ?? throw new NotFoundException("Quiz");
        if (quiz.Questions.Count == 0)
            throw new DomainException("This quiz has no questions yet.");
        var now = clock.UtcNow;
        var open = await db.TrainingQuizAttempts.FirstOrDefaultAsync(a => a.QuizId == id && a.EmployeeId == employeeId && a.SubmittedAtUtc == null);
        if (open is not null && open.DeadlineUtc is { } due && due.AddSeconds(GraceSeconds) < now)
        {
            // Time ran out without a submission: it counts as a failed attempt.
            Score(open, quiz, new Dictionary<Guid, int>(), now);
            await db.SaveChangesAsync();
            open = null;
        }
        if (open is null)
        {
            var attempts = await db.TrainingQuizAttempts.CountAsync(a => a.QuizId == id && a.EmployeeId == employeeId);
            if (quiz.MaxAttempts > 0 && attempts >= quiz.MaxAttempts)
                throw new DomainException($"You have used all {quiz.MaxAttempts} attempts for this quiz.");
            var order = quiz.Questions.Select(x => x.Id).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
            open = new TrainingQuizAttempt
            {
                QuizId = id,
                EmployeeId = employeeId,
                StartedAtUtc = now,
                DeadlineUtc = quiz.TimeLimitMinutes is { } m ? now.AddMinutes(m) : null,
                QuestionIdsJson = JsonSerializer.Serialize(order),
                Total = order.Count,
            };
            db.TrainingQuizAttempts.Add(open);
            await db.SaveChangesAsync();
        }
        var byId = quiz.Questions.ToDictionary(x => x.Id);
        var paper = Ids(open).Where(byId.ContainsKey).Select(qid => new QuizPaperQuestion(qid, byId[qid].Text, Options(byId[qid]))).ToList();
        return new QuizPaperDto(open.Id, quiz.Id, quiz.Title, quiz.Instructions, quiz.Program!.Title, quiz.PassPercent, open.StartedAtUtc, open.DeadlineUtc, paper);
    }

    /// <summary>Scores the attempt. Passing issues a certificate. The right answers are shown only now.</summary>
    [HttpPost("quizzes/attempts/{attemptId:guid}/submit")]
    public async Task<QuizResultDto> Submit(Guid attemptId, SubmitQuizRequest req)
    {
        var employeeId = me.EmployeeId ?? throw new DomainException("Only employees can take quizzes.");
        var attempt = await db.TrainingQuizAttempts.FirstOrDefaultAsync(a => a.Id == attemptId && a.EmployeeId == employeeId) ?? throw new NotFoundException("Attempt");
        if (attempt.SubmittedAtUtc is not null)
            throw new DomainException("This attempt has already been submitted.");
        var quiz = await db.TrainingQuizzes.Include(x => x.Program).Include(x => x.Questions).FirstAsync(x => x.Id == attempt.QuizId);
        var now = clock.UtcNow;
        Score(attempt, quiz, req.Answers, now);

        TrainingCertificate? certificate = null;
        if (attempt.Passed)
        {
            certificate = await db.TrainingCertificates
                .Where(c => c.EmployeeId == employeeId && db.TrainingQuizAttempts.Any(a => a.Id == c.AttemptId && a.QuizId == quiz.Id)).FirstOrDefaultAsync();
            if (certificate is null)
            {
                var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
                certificate = new TrainingCertificate
                {
                    AttemptId = attempt.Id,
                    EmployeeId = employeeId,
                    ProgramId = quiz.ProgramId,
                    Code = await NewCodeAsync(),
                    IssuedOn = await clock.TodayAsync(),
                    EmployeeName = name.Trim(),
                    CourseTitle = quiz.Program!.Title,
                    ScorePercent = attempt.ScorePercent,
                };
                db.TrainingCertificates.Add(certificate);
            }
        }
        await db.SaveChangesAsync();

        var chosen = Answers(attempt);
        var byId = quiz.Questions.ToDictionary(x => x.Id);
        var review = Ids(attempt).Where(byId.ContainsKey).Select(qid => new QuizReviewItem(qid, byId[qid].Text, Options(byId[qid]),
            chosen.TryGetValue(qid, out var c) ? c : null, byId[qid].CorrectIndex)).ToList();
        return new QuizResultDto(attempt.Id, attempt.Correct, attempt.Total, attempt.ScorePercent, quiz.PassPercent, attempt.Passed, attempt.TimedOut,
            certificate?.Id, certificate?.Code, review);
    }

    // ---------- Certificates ----------

    [HttpGet("certificates/mine")]
    public async Task<List<CertificateDto>> MyCertificates()
    {
        var mine = me.EmployeeId ?? Guid.Empty;
        var company = await CompanyAsync();
        return (await db.TrainingCertificates.Where(c => c.EmployeeId == mine).OrderByDescending(c => c.IssuedOn).ToListAsync())
            .Select(c => ToDto(c, company.Name, null)).ToList();
    }

    /// <summary>The printable certificate: its owner, or HR.</summary>
    [HttpGet("certificates/{id:guid}")]
    public async Task<CertificateDto> Certificate(Guid id)
    {
        var c = await db.TrainingCertificates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Certificate");
        if (c.EmployeeId != me.EmployeeId && !training.IsHr)
            throw new NotFoundException("Certificate");
        var company = await CompanyAsync();
        return ToDto(c, company.Name, company.LogoDataUrl);
    }

    /// <summary>The company logo printed on certificates. Small images only (stored as a data URL).</summary>
    [HttpPut("certificate-logo"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveLogo(SaveLogoRequest req)
    {
        var logo = string.IsNullOrWhiteSpace(req.LogoDataUrl) ? null : req.LogoDataUrl.Trim();
        if (logo is not null && !(logo.StartsWith("data:image/png;base64,") || logo.StartsWith("data:image/jpeg;base64,") || logo.StartsWith("data:image/svg+xml;base64,")))
            throw new DomainException("The logo must be a PNG, JPEG or SVG image.");
        var tenantId = me.TenantId ?? throw new DomainException("Open a company first.");
        var tenant = await db.Tenants.FirstAsync(t => t.Id == tenantId);
        tenant.LogoDataUrl = logo;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("certificate-logo")]
    public async Task<SaveLogoRequest> Logo() => new((await CompanyAsync()).LogoDataUrl);

    // ---------- Helpers ----------

    /// <summary>Marks an attempt: right answers / questions shown, as a whole percent. A late submission scores nothing.</summary>
    public static void Score(TrainingQuizAttempt attempt, TrainingQuiz quiz, IReadOnlyDictionary<Guid, int> answers, DateTime now)
    {
        var ids = Ids(attempt);
        var late = attempt.DeadlineUtc is { } due && now > due.AddSeconds(GraceSeconds);
        var byId = quiz.Questions.ToDictionary(x => x.Id);
        var kept = ids.Where(answers.ContainsKey).ToDictionary(x => x, x => answers[x]);
        attempt.AnswersJson = JsonSerializer.Serialize(kept);
        attempt.SubmittedAtUtc = now;
        attempt.TimedOut = late;
        attempt.Total = ids.Count;
        attempt.Correct = late ? 0 : ids.Count(x => byId.TryGetValue(x, out var q) && kept.TryGetValue(x, out var a) && a == q.CorrectIndex);
        attempt.ScorePercent = attempt.Total == 0 ? 0 : (int)Math.Floor(100m * attempt.Correct / attempt.Total);
        attempt.Passed = !late && attempt.ScorePercent >= quiz.PassPercent;
    }

    private async Task ApplyAsync(TrainingQuiz quiz, SaveQuizRequest req)
    {
        if (!await db.TrainingPrograms.AnyAsync(p => p.Id == req.ProgramId))
            throw new NotFoundException("Course");
        if (req.Questions.Count is < 1 or > 100)
            throw new DomainException("A quiz needs between 1 and 100 questions.");
        for (var i = 0; i < req.Questions.Count; i++)
        {
            var q = req.Questions[i];
            var options = q.Options.Select(o => (o ?? "").Trim()).ToList();
            if (string.IsNullOrWhiteSpace(q.Text))
                throw new DomainException($"Question {i + 1} has no text.");
            if (options.Count is < 2 or > 6 || options.Any(o => o.Length == 0 || o.Length > 300))
                throw new DomainException($"Question {i + 1} needs 2 to 6 filled-in choices (300 characters at most).");
            if (q.CorrectIndex < 0 || q.CorrectIndex >= options.Count)
                throw new DomainException($"Pick the right answer for question {i + 1}.");
        }
        quiz.ProgramId = req.ProgramId;
        quiz.Title = req.Title.Trim();
        quiz.Instructions = string.IsNullOrWhiteSpace(req.Instructions) ? null : req.Instructions.Trim();
        quiz.PassPercent = req.PassPercent;
        quiz.TimeLimitMinutes = req.TimeLimitMinutes;
        quiz.MaxAttempts = req.MaxAttempts;
        quiz.IsActive = req.IsActive;
        // Questions are replaced as a set; open attempts keep their question ids and lose any that were removed.
        db.TrainingQuizQuestions.RemoveRange(quiz.Questions);
        quiz.Questions = req.Questions.Select((q, i) => new TrainingQuizQuestion
        {
            Order = i,
            Text = q.Text.Trim(),
            OptionsJson = JsonSerializer.Serialize(q.Options.Select(o => o.Trim()).ToList()),
            CorrectIndex = q.CorrectIndex,
        }).ToList();
    }

    private async Task<string> NewCodeAsync()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        while (true)
        {
            var code = string.Concat(Enumerable.Range(0, 10).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]));
            if (!await db.TrainingCertificates.IgnoreQueryFilters().AnyAsync(c => c.Code == code))
                return code;
        }
    }

    private async Task<(string Name, string? LogoDataUrl)> CompanyAsync()
    {
        var tenantId = me.TenantId ?? Guid.Empty;
        var t = await db.Tenants.Where(x => x.Id == tenantId).Select(x => new { x.Name, x.LogoDataUrl }).FirstOrDefaultAsync();
        return (t?.Name ?? "", t?.LogoDataUrl);
    }

    private static CertificateDto ToDto(TrainingCertificate c, string company, string? logo) =>
        new(c.Id, c.Code, c.EmployeeName, c.CourseTitle, c.IssuedOn, c.ScorePercent, company, logo, $"/verify/{c.Code}");

    private static List<string> Options(TrainingQuizQuestion q) => JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? [];

    private static List<Guid> Ids(TrainingQuizAttempt a) => JsonSerializer.Deserialize<List<Guid>>(a.QuestionIdsJson) ?? [];

    private static Dictionary<Guid, int> Answers(TrainingQuizAttempt a) =>
        a.AnswersJson is null ? new() : JsonSerializer.Deserialize<Dictionary<Guid, int>>(a.AnswersJson) ?? new();

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];

    private record AiQuizJson(List<AiQuizQuestion>? Questions);

    private record AiQuizQuestion(string? Text, List<string>? Options, int CorrectIndex);
}
