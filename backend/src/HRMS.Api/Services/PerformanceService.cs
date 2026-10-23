using System.Text.Json;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record GoalInput(int SectionIndex, string Title, string? Description, string? Target, decimal Weight);

public record ItemScoreInput(Guid ItemId, int? Score, string? Comment);

public record ManagerReviewInput(
    IReadOnlyList<ItemScoreInput> Scores, string? Comment, string? Strengths, string? Improvements, string? DevelopmentPlan, int? Potential);

/// <summary>
/// Review cycles: goals, self review, manager review, HR calibration and the employee's sign-off.
/// Each stage is one person's turn; HR can step in at any stage and send a review back one stage.
/// </summary>
public class PerformanceService(HrmsDbContext db, ICurrentUser me, TenantClock clock, NotificationService notify)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static List<ReviewSection> Sections(string json) => JsonSerializer.Deserialize<List<ReviewSection>>(json, Json) ?? [];

    public bool IsEmployee(PerformanceReview r) => me.EmployeeId is { } m && r.EmployeeId == m;

    /// <summary>The reviewer (their manager at launch); HR stands in, and also when there is no manager.</summary>
    public bool IsReviewer(PerformanceReview r) => me.IsHr() || (me.EmployeeId is { } m && r.ReviewerEmployeeId == m);

    public static void ValidateSections(IReadOnlyList<ReviewSection> sections)
    {
        if (sections.Count == 0)
            throw new DomainException("Add at least one section.");
        if (sections.Any(s => string.IsNullOrWhiteSpace(s.Name)))
            throw new DomainException("Every section needs a name.");
        if (sections.Any(s => s.Weight <= 0) || sections.Sum(s => s.Weight) != 100)
            throw new DomainException("Section weights must add up to 100.");
        if (sections.Any(s => s.Kind == ReviewSectionKind.Competencies && (s.Items is null || s.Items.Count == 0 || s.Items.Any(i => string.IsNullOrWhiteSpace(i.Title)))))
            throw new DomainException("List the items for each competency section.");
    }

    // ---------- Cycle ----------

    /// <summary>Creates a review for everyone in scope, with the template's competencies filled in.</summary>
    public async Task<int> LaunchAsync(Guid cycleId)
    {
        var cycle = await db.ReviewCycles.FirstOrDefaultAsync(c => c.Id == cycleId) ?? throw new NotFoundException("Review cycle");
        if (cycle.Status != ReviewCycleStatus.Draft)
            throw new DomainException("This cycle has already been launched.");
        var template = await db.PerformanceTemplates.FirstOrDefaultAsync(t => t.Id == cycle.TemplateId) ?? throw new NotFoundException("Template");
        var sections = Sections(template.SectionsJson);
        ValidateSections(sections);
        cycle.SectionsJson = template.SectionsJson;
        cycle.SelfReview = template.SelfReview;

        var q = db.Employees.Where(e => (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation) && e.JoiningDate <= cycle.PeriodTo);
        if (!string.IsNullOrEmpty(cycle.DepartmentIds))
        {
            var depts = AnnouncementService.Departments(cycle.DepartmentIds);
            q = q.Where(e => e.DepartmentId != null && depts.Contains(e.DepartmentId.Value));
        }
        var people = await q.Select(e => new { e.Id, e.ManagerId }).ToListAsync();
        if (people.Count == 0)
            throw new DomainException("Nobody is in scope for this cycle.");
        var hasGoals = sections.Any(s => s.Kind == ReviewSectionKind.Goals);
        var first = hasGoals ? ReviewStage.Goals : cycle.SelfReview ? ReviewStage.SelfReview : ReviewStage.ManagerReview;
        foreach (var p in people)
        {
            var review = new PerformanceReview { CycleId = cycle.Id, EmployeeId = p.Id, ReviewerEmployeeId = p.ManagerId, Stage = first };
            for (var i = 0; i < sections.Count; i++)
            {
                if (sections[i].Kind != ReviewSectionKind.Competencies)
                    continue;
                var items = sections[i].Items;
                for (var j = 0; j < items.Count; j++)
                    review.Items.Add(new ReviewItem { SectionIndex = i, Title = items[j].Title.Trim(), Description = items[j].Description, Weight = Math.Round(100m / items.Count, 2), Order = j + 1 });
            }
            db.PerformanceReviews.Add(review);
        }
        cycle.Status = ReviewCycleStatus.Active;
        cycle.LaunchedAtUtc = clock.UtcNow;
        var what = first switch { ReviewStage.Goals => "write your goals", ReviewStage.SelfReview => "rate yourself", _ => "wait for your manager's review" };
        await notify.ToEmployeesAsync(people.Select(p => p.Id), NotificationKind.Performance, $"{cycle.Name} has started",
            $"Please {what}" + (first == ReviewStage.Goals && cycle.GoalsDue is { } g ? $" by {g:d MMM}." : first == ReviewStage.SelfReview && cycle.SelfReviewDue is { } s ? $" by {s:d MMM}." : "."), "/performance");
        await db.SaveChangesAsync();
        return people.Count;
    }

    // ---------- Goals ----------

    /// <summary>Replaces the review's goals. The employee writes them; the reviewer or HR can edit them before approving.</summary>
    public async Task SaveGoalsAsync(Guid reviewId, IReadOnlyList<GoalInput> goals)
    {
        var (r, sections) = await LoadAsync(reviewId);
        var employee = IsEmployee(r) && r.Stage == ReviewStage.Goals;
        var reviewer = IsReviewer(r) && r.Stage is ReviewStage.Goals or ReviewStage.GoalsSubmitted;
        if (!employee && !reviewer)
            throw new ForbiddenException(r.Stage is ReviewStage.Goals or ReviewStage.GoalsSubmitted ? "Waiting for your manager to approve your goals." : "Goals can no longer be changed.");
        var goalSections = sections.Select((s, i) => (s, i)).Where(x => x.s.Kind == ReviewSectionKind.Goals).Select(x => x.i).ToHashSet();
        if (goals.Any(g => !goalSections.Contains(g.SectionIndex)))
            throw new DomainException("Goals go in a goals section.");
        if (goals.Count > 20 || goals.Any(g => string.IsNullOrWhiteSpace(g.Title) || g.Weight <= 0))
            throw new DomainException("Give each goal a title and a weight (up to 20 goals).");
        db.ReviewItems.RemoveRange(r.Items.Where(i => goalSections.Contains(i.SectionIndex)));
        r.Items.RemoveAll(i => goalSections.Contains(i.SectionIndex));
        var order = 0;
        foreach (var g in goals)
        {
            var item = new ReviewItem
            {
                ReviewId = r.Id, SectionIndex = g.SectionIndex, Title = g.Title.Trim(), Description = Clean(g.Description, 2000),
                Target = Clean(g.Target, 1000), Weight = g.Weight, Order = ++order,
            };
            r.Items.Add(item);
            db.ReviewItems.Add(item);
        }
        await db.SaveChangesAsync();
    }

    public async Task SubmitGoalsAsync(Guid reviewId)
    {
        var (r, sections) = await LoadAsync(reviewId);
        if (!IsEmployee(r) || r.Stage != ReviewStage.Goals)
            throw new ForbiddenException("Only you can send your goals, while they are with you.");
        ValidateGoalWeights(r, sections);
        r.Stage = ReviewStage.GoalsSubmitted;
        r.GoalsSubmittedAtUtc = clock.UtcNow;
        await NotifyReviewerAsync(r, $"{await NameAsync(r.EmployeeId)} sent their goals for approval");
        await db.SaveChangesAsync();
    }

    /// <summary>The reviewer approves the goals (or sends them back to the employee with a note).</summary>
    public async Task DecideGoalsAsync(Guid reviewId, bool approve, string? note)
    {
        var (r, sections) = await LoadAsync(reviewId);
        if (!IsReviewer(r) || IsEmployee(r) || r.Stage is not (ReviewStage.Goals or ReviewStage.GoalsSubmitted))
            throw new ForbiddenException("Only the reviewer can approve these goals now.");
        if (!approve)
        {
            r.Stage = ReviewStage.Goals;
            await notify.ToEmployeesAsync([r.EmployeeId], NotificationKind.Performance, "Your goals need changes", note, "/performance");
            await db.SaveChangesAsync();
            return;
        }
        ValidateGoalWeights(r, sections);
        r.Stage = r.Cycle!.SelfReview ? ReviewStage.SelfReview : ReviewStage.ManagerReview;
        await notify.ToEmployeesAsync([r.EmployeeId], NotificationKind.Performance, "Your goals were approved",
            r.Stage == ReviewStage.SelfReview ? "Rate yourself against them when the review opens." : note, "/performance");
        await db.SaveChangesAsync();
    }

    private static void ValidateGoalWeights(PerformanceReview r, List<ReviewSection> sections)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i].Kind != ReviewSectionKind.Goals)
                continue;
            var goals = r.Items.Where(x => x.SectionIndex == i).ToList();
            if (goals.Count == 0)
                throw new DomainException($"Add at least one goal under {sections[i].Name}.");
            if (goals.Sum(x => x.Weight) != 100)
                throw new DomainException($"Goal weights under {sections[i].Name} must add up to 100 (now {goals.Sum(x => x.Weight):0.##}).");
        }
    }

    // ---------- Self and manager review ----------

    public async Task SaveSelfAsync(Guid reviewId, IReadOnlyList<ItemScoreInput> scores, string? comment, bool submit)
    {
        var (r, sections) = await LoadAsync(reviewId);
        if (!IsEmployee(r) || r.Stage != ReviewStage.SelfReview)
            throw new ForbiddenException("Your self review is not open.");
        Apply(r, scores, self: true);
        r.SelfComment = Clean(comment, 4000);
        r.SelfScore = PerformanceMath.Score(sections, r.Items.Select(i => new ScoredItem(i.SectionIndex, i.Weight, i.SelfScore)));
        if (submit)
        {
            if (r.Items.Any(i => i.SelfScore is null))
                throw new DomainException("Rate every item before sending.");
            r.Stage = ReviewStage.ManagerReview;
            r.SelfSubmittedAtUtc = clock.UtcNow;
            await NotifyReviewerAsync(r, $"{await NameAsync(r.EmployeeId)} finished their self review");
        }
        await db.SaveChangesAsync();
    }

    public async Task SaveManagerAsync(Guid reviewId, ManagerReviewInput input, bool submit)
    {
        var (r, sections) = await LoadAsync(reviewId);
        if (!IsReviewer(r) || IsEmployee(r) || r.Stage != ReviewStage.ManagerReview)
            throw new ForbiddenException("The manager review is not open for you.");
        if (input.Potential is { } p && p is < 1 or > 3)
            throw new DomainException("Potential is low, medium or high.");
        Apply(r, input.Scores, self: false);
        r.ManagerComment = Clean(input.Comment, 4000);
        r.Strengths = Clean(input.Strengths, 4000);
        r.Improvements = Clean(input.Improvements, 4000);
        r.DevelopmentPlan = Clean(input.DevelopmentPlan, 4000);
        r.Potential = input.Potential;
        r.ManagerScore = PerformanceMath.Score(sections, r.Items.Select(i => new ScoredItem(i.SectionIndex, i.Weight, i.ManagerScore)));
        if (submit)
        {
            if (r.Items.Any(i => i.ManagerScore is null))
                throw new DomainException("Rate every item before sending.");
            if (r.Potential is null)
                throw new DomainException("Set their potential (for the 9-box).");
            r.Stage = ReviewStage.Calibration;
            r.ManagerSubmittedAtUtc = clock.UtcNow;
            r.FinalRating = PerformanceMath.Rating(r.ManagerScore!.Value);
            await notify.ToHrAsync(NotificationKind.Performance, $"Manager review done for {await NameAsync(r.EmployeeId)}", $"Score {r.ManagerScore:0.00}", "/performance?tab=cycles");
        }
        await db.SaveChangesAsync();
    }

    private static void Apply(PerformanceReview r, IReadOnlyList<ItemScoreInput> scores, bool self)
    {
        foreach (var s in scores)
        {
            var item = r.Items.FirstOrDefault(i => i.Id == s.ItemId) ?? throw new NotFoundException("Review item");
            if (s.Score is { } v && v is < 1 or > 5)
                throw new DomainException("Scores go from 1 to 5.");
            if (self)
            {
                item.SelfScore = s.Score;
                item.SelfComment = Clean(s.Comment, 2000);
            }
            else
            {
                item.ManagerScore = s.Score;
                item.ManagerComment = Clean(s.Comment, 2000);
            }
        }
    }

    // ---------- Calibration, release and sign-off ----------

    public async Task CalibrateAsync(Guid reviewId, int finalRating, string? note)
    {
        if (!me.IsHr())
            throw new ForbiddenException("Only HR calibrates ratings.");
        var (r, _) = await LoadAsync(reviewId);
        if (r.Stage != ReviewStage.Calibration)
            throw new DomainException("This review is not waiting for calibration.");
        if (finalRating is < 1 or > 5)
            throw new DomainException("The final rating goes from 1 to 5.");
        r.FinalRating = finalRating;
        r.CalibrationNote = Clean(note, 4000);
        await db.SaveChangesAsync();
    }

    /// <summary>HR releases calibrated reviews (all in the cycle, or the ones given) to the employees to sign off.</summary>
    public async Task<int> ReleaseAsync(Guid cycleId, IReadOnlyCollection<Guid>? reviewIds)
    {
        if (!me.IsHr())
            throw new ForbiddenException("Only HR releases reviews.");
        var q = db.PerformanceReviews.Where(r => r.CycleId == cycleId && r.Stage == ReviewStage.Calibration);
        if (reviewIds is { Count: > 0 })
            q = q.Where(r => reviewIds.Contains(r.Id));
        var list = await q.Include(r => r.Cycle).ToListAsync();
        foreach (var r in list)
        {
            r.Stage = ReviewStage.SignOff;
            r.ReleasedAtUtc = clock.UtcNow;
        }
        if (list.Count > 0)
            await notify.ToEmployeesAsync(list.Select(r => r.EmployeeId), NotificationKind.Performance, $"Your {list[0].Cycle!.Name} review is ready",
                "Read it and sign off.", "/performance");
        await db.SaveChangesAsync();
        return list.Count;
    }

    public async Task SignOffAsync(Guid reviewId, bool agree, string? comment)
    {
        var (r, _) = await LoadAsync(reviewId);
        if (!IsEmployee(r) || r.Stage != ReviewStage.SignOff)
            throw new ForbiddenException("Your review is not waiting for your sign-off.");
        if (!agree && string.IsNullOrWhiteSpace(comment))
            throw new DomainException("Say what you disagree with.");
        r.EmployeeAgrees = agree;
        r.EmployeeSignOffComment = Clean(comment, 4000);
        r.SignedOffAtUtc = clock.UtcNow;
        r.Stage = ReviewStage.Done;
        var name = await NameAsync(r.EmployeeId);
        if (!agree)
        {
            await notify.ToHrAsync(NotificationKind.Performance, $"{name} disagrees with their review", r.EmployeeSignOffComment, "/performance?tab=cycles");
            if (r.ReviewerEmployeeId is { } rev)
                await notify.ToEmployeesAsync([rev], NotificationKind.Performance, $"{name} disagrees with their review", r.EmployeeSignOffComment, $"/performance/reviews/{r.Id}");
        }
        await db.SaveChangesAsync();
    }

    /// <summary>HR sends a review back one stage, e.g. to let a manager change scores.</summary>
    public async Task RevertAsync(Guid reviewId)
    {
        if (!me.IsHr())
            throw new ForbiddenException("Only HR can send a review back.");
        var (r, sections) = await LoadAsync(reviewId);
        var hasGoals = sections.Any(s => s.Kind == ReviewSectionKind.Goals);
        r.Stage = r.Stage switch
        {
            ReviewStage.Done or ReviewStage.SignOff => ReviewStage.Calibration,
            ReviewStage.Calibration => ReviewStage.ManagerReview,
            ReviewStage.ManagerReview when r.Cycle!.SelfReview => ReviewStage.SelfReview,
            ReviewStage.ManagerReview or ReviewStage.SelfReview when hasGoals => ReviewStage.Goals,
            ReviewStage.GoalsSubmitted => ReviewStage.Goals,
            _ => throw new DomainException("This review cannot go back further."),
        };
        if (r.Stage == ReviewStage.Calibration)
        {
            r.EmployeeAgrees = null;
            r.SignedOffAtUtc = null;
            r.ReleasedAtUtc = null;
        }
        await db.SaveChangesAsync();
    }

    public async Task<(PerformanceReview Review, List<ReviewSection> Sections)> LoadAsync(Guid id)
    {
        var r = await db.PerformanceReviews.Include(x => x.Cycle).Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Review");
        if (!IsEmployee(r) && !IsReviewer(r))
            throw new NotFoundException("Review");
        if (r.Cycle!.Status == ReviewCycleStatus.Closed && !me.IsHr())
            throw new DomainException("This review cycle is closed.");
        return (r, Sections(r.Cycle.SectionsJson));
    }

    private async Task NotifyReviewerAsync(PerformanceReview r, string title)
    {
        if (r.ReviewerEmployeeId is { } rev)
            await notify.ToEmployeesAsync([rev], NotificationKind.Performance, title, r.Cycle?.Name, $"/performance/reviews/{r.Id}");
        else
            await notify.ToHrAsync(NotificationKind.Performance, title, r.Cycle?.Name, $"/performance/reviews/{r.Id}");
    }

    private Task<string> NameAsync(Guid employeeId) => db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();

    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();
}
