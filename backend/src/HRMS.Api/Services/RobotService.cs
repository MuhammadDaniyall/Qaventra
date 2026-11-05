using System.Text.Json;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record RobotFinding(string Title, string Detail, string Level, string? Link);
public record RobotInfo(RobotKind Kind, string Name, string About, string[] Does, bool CanPropose);

/// <summary>
/// The company's AI robot employees. Each robot gathers facts from the company's own records with fixed, tested queries
/// (so its numbers are never made up), asks a free AI model to explain them and to draft any message, and proposes
/// changes for a person to approve. Every step is written to the run's log. Robots never approve requests, change pay or
/// release payroll; at the Routine level they may only send reminders on their own.
/// </summary>
public class RobotService(HrmsDbContext db, ICurrentUser me, TenantClock clock, AiAssistantService ai, NotificationService notify,
    IServiceProvider services, ILogger<RobotService> log)
{
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static readonly Dictionary<RobotKind, RobotInfo> Catalog = new()
    {
        [RobotKind.PayrollChecker] = new(RobotKind.PayrollChecker, "Paro · Payroll checker",
            "Checks the latest payroll run before it is released: missing payslips, zero or negative pay, big jumps from last month, missing bank accounts.",
            ["Compares every payslip with last month", "Finds active people with no payslip", "Asks people with no bank account to add one"], true),
        [RobotKind.AttendanceWatcher] = new(RobotKind.AttendanceWatcher, "Atif · Attendance watcher",
            "Looks at yesterday's and this week's attendance: missed check-outs and people late again and again.",
            ["Finds missed check-outs", "Spots repeated lateness", "Reminds people to send a missed-punch request"], true),
        [RobotKind.CvScreener] = new(RobotKind.CvScreener, "Sana · CV screener",
            "Reads new applicants' CVs against the job and scores the fit, so recruiters start with the best.",
            ["Scores new applications 0-100", "Lists strengths and gaps", "Proposes shortlisting strong candidates"], true),
        [RobotKind.HelpdeskResponder] = new(RobotKind.HelpdeskResponder, "Hadi · Helpdesk responder",
            "Drafts a first reply to helpdesk tickets nobody has answered yet, and flags tickets past their due time.",
            ["Drafts first replies", "Flags overdue tickets"], true),
        [RobotKind.PeopleRisk] = new(RobotKind.PeopleRisk, "Rabia · People risk",
            "Points HR to people who may be unhappy or about to leave: repeated lateness and absence, a lot of leave, no raise for a long time.",
            ["Scores leaving risk from attendance, leave and pay history", "Lists pending resignations"], false),
        [RobotKind.OnboardingCoach] = new(RobotKind.OnboardingCoach, "Omar · Onboarding coach",
            "Keeps new joiners' onboarding on time: overdue tasks and joiners arriving soon with work still open.",
            ["Finds overdue onboarding tasks", "Reminds whoever owns each task"], true),
        [RobotKind.LeavePlanner] = new(RobotKind.LeavePlanner, "Laila · Leave planner",
            "Looks ahead at leave: days when too much of a team is away, leave waiting too long for approval, and unused leave before the year ends.",
            ["Finds team clashes in the next two weeks", "Flags leave waiting for approval", "Reminds people of unused leave late in the year"], true),
        [RobotKind.DocumentWatcher] = new(RobotKind.DocumentWatcher, "Danish · Document watcher",
            "Watches ID card, passport, visa, licence and insurance expiry dates and reminds people before they run out.",
            ["Finds documents expired or expiring within 30 days", "Reminds the employee to renew"], true),
    };

    // ---------- Settings ----------

    /// <summary>Gives the company one robot of each kind (switched off) the first time AI Team is opened.</summary>
    public async Task<List<AiRobot>> EnsureAsync()
    {
        var robots = await db.AiRobots.ToListAsync();
        var missing = Catalog.Keys.Where(k => robots.All(r => r.Kind != k)).ToList();
        if (missing.Count > 0)
        {
            var hour = 8;
            foreach (var k in missing)
            {
                var r = new AiRobot { Kind = k, Name = Catalog[k].Name, OwnerUserId = me.UserId, RunHour = hour++ % 24 };
                db.AiRobots.Add(r);
                robots.Add(r);
            }
            await db.SaveChangesAsync();
        }
        return robots.OrderBy(r => r.Kind).ToList();
    }

    public async Task<int> RunsThisMonthAsync(Guid robotId)
    {
        var start = await MonthStartUtcAsync();
        return await db.AiRobotRuns.CountAsync(r => r.RobotId == robotId && r.StartedAtUtc >= start);
    }

    async Task<DateTime> MonthStartUtcAsync()
    {
        var today = await clock.TodayAsync();
        return await clock.ToUtcAsync(new DateTime(today.Year, today.Month, 1));
    }

    public async Task UpdateAsync(Guid id, bool enabled, RobotAutonomy autonomy, Guid? ownerUserId, int runHour, int runsPerMonth)
    {
        var r = await db.AiRobots.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Robot");
        if (!Enum.IsDefined(autonomy)) throw new DomainException("Choose how far the robot may go.");
        if (!Catalog[r.Kind].CanPropose && autonomy != RobotAutonomy.Advisory)
            throw new DomainException($"{r.Name} only gives advice; it cannot propose changes.");
        if (runHour is < 0 or > 23) throw new DomainException("The run hour is 0 to 23.");
        if (runsPerMonth is < 1 or > 500) throw new DomainException("Runs a month is 1 to 500.");
        if (ownerUserId is { } o && !await db.Users.AnyAsync(u => u.Id == o && u.IsActive
                && (u.Role == UserRole.CompanyAdmin || u.Role == UserRole.HrManager)))
            throw new DomainException("The owner must be someone in HR or a company admin.");
        r.Enabled = enabled;
        r.Autonomy = autonomy;
        r.OwnerUserId = ownerUserId;
        r.RunHour = runHour;
        r.RunsPerMonth = runsPerMonth;
        await db.SaveChangesAsync();
    }

    public async Task SetStoppedAsync(bool stopped)
    {
        var t = await db.Tenants.FirstAsync(x => x.Id == db.CurrentTenantId);
        t.RobotsStopped = stopped;
        t.RobotsStoppedBy = stopped ? await MyNameAsync() : null;
        await db.SaveChangesAsync();
    }

    async Task<string> MyNameAsync() =>
        me.UserId is { } u ? await db.Users.Where(x => x.Id == u).Select(x => x.FullName).FirstOrDefaultAsync() ?? "HR" : "AI Team";

    // ---------- A run ----------

    sealed class Work(AiRobot robot, DateOnly today, DateTime now)
    {
        public AiRobot Robot { get; } = robot;
        public DateOnly Today { get; } = today;
        public DateTime Now { get; } = now;
        public List<string> Steps { get; } = [];
        public List<RobotFinding> Findings { get; } = [];
        public List<Proposal> Proposals { get; } = [];
        public void Step(string s) => Steps.Add(s);
        public void Find(string title, string detail, string level, string? link = null) => Findings.Add(new(title, detail, level, link));
    }

    /// <param name="LowRisk">A reminder the robot may send on its own at the Routine level.</param>
    sealed record Proposal(string Action, string Title, string Detail, object Payload, string SubjectKey, string? Link, bool LowRisk);

    record SummaryAnswer(string Summary);
    record ReplyAnswer(string Reply);

    public async Task<AiRobotRun> RunAsync(Guid robotId, bool manual)
    {
        var robot = await db.AiRobots.FirstOrDefaultAsync(x => x.Id == robotId) ?? throw new NotFoundException("Robot");
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == db.CurrentTenantId);
        if (tenant.RobotsStopped)
            throw new DomainException($"The AI Team is stopped{(tenant.RobotsStoppedBy is { } by ? $" by {by}" : "")}. Start it again first.");
        if (await RunsThisMonthAsync(robot.Id) >= robot.RunsPerMonth)
            throw new DomainException($"{robot.Name} has used this month's {robot.RunsPerMonth} runs. Raise its budget to run it again.");

        var now = clock.UtcNow;
        var run = new AiRobotRun
        {
            RobotId = robot.Id, Manual = manual, StartedAtUtc = now,
            StartedByName = manual ? await MyNameAsync() : "Schedule",
        };
        db.AiRobotRuns.Add(run);
        robot.LastRunAtUtc = now;
        await db.SaveChangesAsync();

        var work = new Work(robot, await clock.TodayAsync(), now);
        work.Step($"Started {(manual ? $"by {run.StartedByName}" : "on its daily schedule")} at the {robot.Autonomy} level.");
        try
        {
            switch (robot.Kind)
            {
                case RobotKind.PayrollChecker: await PayrollAsync(work); break;
                case RobotKind.AttendanceWatcher: await AttendanceAsync(work); break;
                case RobotKind.CvScreener: await CvAsync(work); break;
                case RobotKind.HelpdeskResponder: await HelpdeskAsync(work); break;
                case RobotKind.PeopleRisk: await PeopleRiskAsync(work); break;
                case RobotKind.OnboardingCoach: await OnboardingAsync(work); break;
                case RobotKind.LeavePlanner: await LeaveAsync(work); break;
                case RobotKind.DocumentWatcher: await DocumentsAsync(work); break;
            }
            run.Proposals = await SaveProposalsAsync(work, run);

            var issues = work.Findings.Count(f => f.Level != "good");
            var acts = work.Findings.Count(f => f.Level == "act");
            var plain = issues == 0
                ? "All clear: " + (work.Findings.FirstOrDefault()?.Detail ?? "nothing needs attention today.")
                : $"{issues} thing{(issues == 1 ? "" : "s")} to look at"
                  + (acts > 0 ? $" ({acts} need{(acts == 1 ? "s" : "")} action)" : "")
                  + (run.Proposals > 0 ? $"; {run.Proposals} proposal{(run.Proposals == 1 ? "" : "s")} prepared." : ".");
            run.Summary = plain;
            if (issues > 0)
            {
                work.Step("Asked the AI to explain the findings.");
                var said = await ai.FreeJsonAsync<SummaryAnswer>($"""
                    You are {robot.Name}, an AI robot in the HR team of {tenant.Name}. Today is {work.Today:d MMMM yyyy}.
                    {Catalog[robot.Kind].About}
                    Write a summary of today's findings for the HR team in two or three plain sentences: what matters most and what to do first.
                    Use only the findings given; keep every number exactly as given; do not invent anything. JSON: {"{"}"summary": text{"}"}.
                    """, JsonSerializer.Serialize(work.Findings.Take(40), Web));
                if (!string.IsNullOrWhiteSpace(said?.Summary))
                {
                    run.Summary = Cut(said.Summary.Trim(), 1900) + (run.Proposals > 0 ? $" ({run.Proposals} proposal{(run.Proposals == 1 ? "" : "s")} prepared.)" : "");
                    run.UsedAi = true;
                }
                else work.Step("The AI was busy; kept the robot's own summary.");
            }
            run.Status = RobotRunStatus.Done;
            work.Step("Finished.");
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or DbUpdateException or HttpRequestException)
        {
            log.LogWarning(ex, "Robot {Robot} failed", robot.Kind);
            run.Status = RobotRunStatus.Failed;
            run.Error = Cut(ex.Message, 900);
            run.Summary = "The run stopped with an error.";
            work.Step($"Stopped: {run.Error}");
        }
        run.FinishedAtUtc = clock.UtcNow;
        run.StepsJson = JsonSerializer.Serialize(work.Steps, Web);
        run.FindingsJson = JsonSerializer.Serialize(work.Findings, Web);
        robot.LastSummary = Cut(run.Summary ?? "", 1900);
        await db.SaveChangesAsync();

        // The owner hears about anything that needs a person.
        if (robot.OwnerUserId is { } owner && (work.Findings.Any(f => f.Level == "act") || run.Proposals > 0) && !(manual && owner == me.UserId))
        {
            await notify.ToUsersAsync(db.Users.Where(u => u.Id == owner), NotificationKind.Alert, $"AI Team: {robot.Name}",
                run.Summary, "/ai-team", includeMe: true, email: false);
            await db.SaveChangesAsync();
        }
        return run;
    }

    /// <summary>Saves new proposals (never the same thing twice while one is open or was just decided); Routine robots send reminders themselves.</summary>
    async Task<int> SaveProposalsAsync(Work work, AiRobotRun run)
    {
        if (work.Robot.Autonomy == RobotAutonomy.Advisory || !Catalog[work.Robot.Kind].CanPropose)
        {
            if (work.Proposals.Count > 0)
                work.Step($"Would propose {work.Proposals.Count} change(s), but it is set to advise only.");
            return 0;
        }
        var recent = work.Now.AddDays(-7);
        var keys = work.Proposals.Select(p => p.SubjectKey).Distinct().ToList();
        var taken = (await db.AiRobotProposals
                .Where(p => p.RobotId == work.Robot.Id && keys.Contains(p.SubjectKey)
                            && (p.Status == RobotProposalStatus.Pending || p.CreatedAtUtc >= recent))
                .Select(p => p.SubjectKey).ToListAsync())
            .ToHashSet();
        var count = 0;
        foreach (var p in work.Proposals.Take(50))
        {
            if (!taken.Add(p.SubjectKey)) continue;
            var row = new AiRobotProposal
            {
                RobotId = work.Robot.Id, RunId = run.Id, Action = p.Action, Title = Cut(p.Title, 290), Detail = Cut(p.Detail, 3900),
                PayloadJson = JsonSerializer.Serialize(p.Payload, Web), SubjectKey = Cut(p.SubjectKey, 100), Link = p.Link,
            };
            db.AiRobotProposals.Add(row);
            count++;
            if (work.Robot.Autonomy == RobotAutonomy.Routine && p.LowRisk)
            {
                await ExecuteAsync(row, null);
                row.Status = RobotProposalStatus.AutoDone;
                row.DecidedByName = work.Robot.Name;
                row.DecidedAtUtc = clock.UtcNow;
                work.Step($"Sent on its own (low-risk reminder): {row.Title}");
            }
        }
        if (count > 0) work.Step($"Prepared {count} proposal(s).");
        await db.SaveChangesAsync();
        return count;
    }

    // ---------- Proposals ----------

    public async Task DecideAsync(Guid id, bool approve, string? note, string? body)
    {
        var p = await db.AiRobotProposals.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Proposal");
        if (p.Status != RobotProposalStatus.Pending) throw new DomainException("This proposal was already decided.");
        p.DecidedByName = await MyNameAsync();
        p.DecidedAtUtc = clock.UtcNow;
        p.Note = Cut(note?.Trim(), 990);
        if (!approve)
        {
            p.Status = RobotProposalStatus.Rejected;
            await db.SaveChangesAsync();
            return;
        }
        try
        {
            await ExecuteAsync(p, string.IsNullOrWhiteSpace(body) ? null : body.Trim());
            p.Status = RobotProposalStatus.Approved;
        }
        catch (DomainException ex)
        {
            p.Status = RobotProposalStatus.Failed;
            p.Note = Cut(ex.Message, 990);
        }
        await db.SaveChangesAsync();
    }

    async Task ExecuteAsync(AiRobotProposal p, string? editedBody)
    {
        using var doc = JsonDocument.Parse(p.PayloadJson);
        var x = doc.RootElement;
        string Str(string name) => x.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        Guid Id(string name) => Guid.TryParse(Str(name), out var g) ? g : throw new DomainException("The proposal is missing its record.");
        switch (p.Action)
        {
            case "notify_employee":
            {
                var ids = x.TryGetProperty("employeeIds", out var arr) && arr.ValueKind == JsonValueKind.Array
                    ? arr.EnumerateArray().Select(e => Guid.TryParse(e.GetString(), out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList()
                    : [];
                if (ids.Count == 0) throw new DomainException("Nobody to remind.");
                await notify.ToEmployeesAsync(ids, NotificationKind.Alert, Str("title"), editedBody ?? Str("body"), Str("link") is { Length: > 0 } l ? l : null,
                    includeMe: true);
                break;
            }
            case "helpdesk_reply":
            {
                var helpdesk = services.GetRequiredService<HelpdeskService>();
                await helpdesk.CommentAsync(Id("ticketId"), editedBody ?? Str("body"), isInternal: false);
                break;
            }
            case "shortlist":
            {
                var recruitment = services.GetRequiredService<RecruitmentService>();
                var appId = Id("applicationId");
                var app = await db.JobApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appId) ?? throw new DomainException("The application is gone.");
                if (app.Stage != ApplicationStage.Applied) throw new DomainException("The candidate has already moved on from Applied.");
                await recruitment.MoveAsync(appId, ApplicationStage.Shortlisted, null, emailCandidate: false);
                await recruitment.NoteAsync(appId, $"Shortlisted on the advice of {Catalog[RobotKind.CvScreener].Name}: {Str("why")}");
                break;
            }
            default:
                throw new DomainException($"Unknown action {p.Action}.");
        }
    }

    // ---------- The robots ----------

    static string Names(IEnumerable<string> names, int max = 8)
    {
        var list = names.Distinct().ToList();
        return list.Count <= max ? string.Join(", ", list) : $"{string.Join(", ", list.Take(max))} and {list.Count - max} more";
    }

    static string Money(decimal v) => $"PKR {v:N0}";

    async Task PayrollAsync(Work w)
    {
        // The latest run of each pay frequency (monthly, weekly…), since each has its own people.
        var latest = (await db.PayrollRuns.AsNoTracking().ToListAsync())
            .GroupBy(r => r.Frequency)
            .Select(g => g.OrderByDescending(r => r.PeriodStart).ThenByDescending(r => r.ProcessedAtUtc).First())
            .OrderBy(r => r.Frequency).ToList();
        if (latest.Count == 0)
        {
            w.Step("No payroll run yet; nothing to check.");
            w.Find("No payroll run yet", "Run payroll first; the checker reads the latest run.", "good", "/payroll");
            return;
        }
        foreach (var run in latest)
            await PayrollRunAsync(w, run);
    }

    async Task PayrollRunAsync(Work w, PayrollRun run)
    {
        var label = (run.Frequency == PayFrequency.Monthly ? new DateTime(run.Year, run.Month, 1).ToString("MMMM yyyy") : $"{run.PeriodStart:d MMM}–{run.PeriodEnd:d MMM yyyy}")
            + (run.Frequency == PayFrequency.Monthly ? "" : $" ({run.Frequency.ToString().ToLowerInvariant()})");
        var link = $"/payroll/runs/{run.Id}";
        w.Step($"Read the {label} payroll run ({run.Status}).");
        var before = w.Findings.Count;
        var slips = await db.Payslips.AsNoTracking().Where(p => p.PayrollRunId == run.Id)
            .Select(p => new { p.EmployeeId, p.NetPay, p.GrossEarnings, p.UnpaidDays, Name = p.Employee!.FirstName + " " + p.Employee.LastName, p.Employee.BankAccount })
            .ToListAsync();
        w.Step($"Checked {slips.Count} payslips.");

        var prevRun = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.Id != run.Id && r.Frequency == run.Frequency && r.PeriodStart < run.PeriodStart)
            .OrderByDescending(r => r.PeriodStart).FirstOrDefaultAsync();
        var prev = prevRun is null ? [] : await db.Payslips.AsNoTracking().Where(p => p.PayrollRunId == prevRun.Id)
            .ToDictionaryAsync(p => p.EmployeeId, p => p.NetPay);
        if (prevRun is not null) w.Step($"Compared with the run of {prevRun.PeriodStart:d MMM yyyy}.");

        var zero = slips.Where(s => s.NetPay <= 0).ToList();
        if (zero.Count > 0)
            w.Find($"{zero.Count} payslip(s) with no pay", $"Net pay is zero or less for {Names(zero.Select(z => z.Name))} in {label}.", "act", link);

        var jumps = slips.Where(s => prev.TryGetValue(s.EmployeeId, out var before) && before > 0 && Math.Abs(s.NetPay - before) / before > 0.25m)
            .Select(s => (s.Name, Before: prev[s.EmployeeId], s.NetPay)).ToList();
        foreach (var j in jumps.Take(10))
            w.Find($"{j.Name}: net pay {(j.NetPay > j.Before ? "up" : "down")} {Math.Abs(j.NetPay - j.Before) / j.Before:P0}",
                $"{Money(j.Before)} last time, {Money(j.NetPay)} in {label}. Check overtime, unpaid days, arrears or one-off items.", "watch", link);
        if (jumps.Count > 10) w.Find($"{jumps.Count - 10} more big changes", "More payslips moved by over 25% from last month.", "watch", link);

        var unpaid = slips.Where(s => s.UnpaidDays >= 5).ToList();
        if (unpaid.Count > 0)
            w.Find($"{unpaid.Count} people with 5+ unpaid days", $"{Names(unpaid.Select(u => $"{u.Name} ({u.UnpaidDays:0.#})"))}. Make sure their leave and attendance are right before release.", "watch", link);

        var paid = slips.Select(s => s.EmployeeId).ToHashSet();
        var missing = await db.Employees.AsNoTracking()
            .Where(e => (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation) && e.PayFrequency == run.Frequency
                        && e.JoiningDate <= run.PeriodEnd && (e.LeavingDate == null || e.LeavingDate >= run.PeriodStart))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName }).ToListAsync();
        missing = missing.Where(e => !paid.Contains(e.Id)).ToList();
        if (missing.Count > 0)
            w.Find($"{missing.Count} active people have no payslip", $"{Names(missing.Select(m => m.Name))} are not in the {label} run. Add a salary or check their status.", "act", link);

        var noBank = slips.Where(s => string.IsNullOrWhiteSpace(s.BankAccount)).ToList();
        if (noBank.Count > 0)
        {
            w.Find($"{noBank.Count} people have no bank account", $"{Names(noBank.Select(n => n.Name))} cannot be paid by bank transfer.", "watch", "/employees");
            foreach (var n in noBank)
                w.Proposals.Add(new("notify_employee", $"Ask {n.Name} to add a bank account",
                    "Your bank account is missing from your HR profile, so your salary cannot be paid by bank transfer. Please add it under My profile, or send it to HR.",
                    new { employeeIds = new[] { n.EmployeeId.ToString() }, title = "Please add your bank account", body = "Your bank account is missing from your HR profile, so your salary cannot be paid by bank transfer. Please add it under My profile, or send it to HR.", link = "/me/profile" },
                    $"bank:{n.EmployeeId}", "/employees/" + n.EmployeeId, true));
        }

        if (prevRun is not null && prevRun.TotalNet > 0)
        {
            var change = (run.TotalNet - prevRun.TotalNet) / prevRun.TotalNet;
            w.Find($"Total net pay {(change >= 0 ? "up" : "down")} {Math.Abs(change):P1}", $"{Money(prevRun.TotalNet)} → {Money(run.TotalNet)} for {slips.Count} people.",
                Math.Abs(change) > 0.15m ? "watch" : "good", link);
        }
        if (w.Findings.Count == before)
            w.Find($"{label}: no problems found", $"{slips.Count} payslip(s) checked.", "good", link);
    }

    async Task AttendanceAsync(Work w)
    {
        var yesterday = w.Today.AddDays(-1);
        var weekAgo = w.Today.AddDays(-7);
        var open = await db.AttendanceRecords.AsNoTracking().Where(a => a.Date == yesterday && a.CheckOutUtc == null)
            .Select(a => new { a.EmployeeId, Name = a.Employee!.FirstName + " " + a.Employee.LastName }).ToListAsync();
        w.Step($"Read {yesterday:d MMM} attendance: {open.Count} without a check-out.");
        if (open.Count > 0)
        {
            w.Find($"{open.Count} missed check-out(s) on {yesterday:d MMM}", Names(open.Select(o => o.Name)) + ". Their worked hours stay at zero until a missed-punch request is approved.",
                "watch", "/attendance/daily");
            foreach (var o in open)
                w.Proposals.Add(new("notify_employee", $"Remind {o.Name} about the missed check-out",
                    $"You did not check out on {yesterday:dddd d MMM}. Please send a missed-punch request so your hours count.",
                    new { employeeIds = new[] { o.EmployeeId.ToString() }, title = $"Missed check-out on {yesterday:d MMM}", body = $"You did not check out on {yesterday:dddd d MMM}. Please send a missed-punch request so your hours count.", link = "/me/attendance" },
                    $"out:{o.EmployeeId}:{yesterday:yyyyMMdd}", "/attendance/daily", true));
        }

        var late = (await db.AttendanceRecords.AsNoTracking().Where(a => a.Date >= weekAgo && a.Date < w.Today && a.LateMinutes > 0)
                .Select(a => new { a.EmployeeId, a.LateMinutes, Name = a.Employee!.FirstName + " " + a.Employee.LastName }).ToListAsync())
            .GroupBy(a => (a.EmployeeId, a.Name)).Where(g => g.Count() >= 3)
            .Select(g => (g.Key.EmployeeId, g.Key.Name, Days: g.Count(), Minutes: g.Sum(x => x.LateMinutes))).OrderByDescending(g => g.Days).ToList();
        w.Step($"Checked lateness over the last 7 days: {late.Count} people late 3+ times.");
        foreach (var l in late.Take(10))
        {
            w.Find($"{l.Name} late {l.Days} times this week", $"{l.Minutes} minutes late in all since {weekAgo:d MMM}.", l.Days >= 5 ? "act" : "watch", "/attendance/monthly");
            w.Proposals.Add(new("notify_employee", $"Remind {l.Name} about lateness",
                $"You were late {l.Days} times in the last 7 days ({l.Minutes} minutes). Please be on time; repeated lateness may lead to a deduction under company policy.",
                new { employeeIds = new[] { l.EmployeeId.ToString() }, title = "About your lateness this week", body = $"You were late {l.Days} times in the last 7 days ({l.Minutes} minutes). Please be on time; repeated lateness may lead to a deduction under company policy.", link = "/me/attendance" },
                $"late:{l.EmployeeId}:{w.Today:yyyyMMdd}", "/attendance/monthly", true));
        }
        if (open.Count == 0 && late.Count == 0)
            w.Find("Attendance looks clean", $"No missed check-outs on {yesterday:d MMM} and nobody late 3+ times this week.", "good");
    }

    async Task CvAsync(Work w)
    {
        var since = w.Now.AddDays(-30);
        var apps = await db.JobApplications.AsNoTracking().Where(a => a.Stage == ApplicationStage.Applied && a.CreatedAtUtc >= since)
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new { a.Id, Name = a.Candidate!.FirstName + " " + a.Candidate.LastName, Job = a.JobPost!.Title }).ToListAsync();
        w.Step($"Found {apps.Count} new application(s) in the last 30 days.");
        var files = services.GetRequiredService<AttachmentService>();
        var done = 0;
        foreach (var a in apps)
        {
            if (done >= 5) break;
            var key = a.Id.ToString();
            if (await db.AiRobotRuns.AnyAsync(r => r.RobotId == w.Robot.Id && r.Status == RobotRunStatus.Done && r.FindingsJson.Contains(key)))
                continue;
            AiCvScreen s;
            try
            {
                s = await ai.ScreenCvAsync(a.Id, files, false);
            }
            catch (DomainException ex)
            {
                w.Step($"Could not score {a.Name}: {ex.Message}");
                w.Find($"{apps.Count} application(s) not scored", $"The AI could not read CVs right now ({ex.Message}). Try again later.", "watch", "/recruitment");
                break;
            }
            done++;
            var link = $"/recruitment/applications/{a.Id}";
            w.Step($"Scored {a.Name} for {a.Job}: {s.Score}/100{(s.FromCv ? "" : " (no CV, from the form only)")}.");
            w.Find($"{a.Name} · {a.Job}: {s.Score}/100", $"{s.Summary} Strengths: {string.Join("; ", s.Strengths.Take(3))}. Gaps: {string.Join("; ", s.Gaps.Take(3))}.",
                s.Score >= 70 ? "good" : s.Score >= 45 ? "watch" : "act", link);
            if (s.Score >= 70)
                w.Proposals.Add(new("shortlist", $"Shortlist {a.Name} for {a.Job}", $"Scored {s.Score}/100. {s.Summary}",
                    new { applicationId = a.Id.ToString(), why = $"{s.Score}/100. {s.Summary}" }, $"cv:{a.Id}", link, false));
        }
        if (done == 0 && apps.Count == 0) w.Find("No new applicants", "Nobody new is waiting in Applied.", "good");
    }

    async Task HelpdeskAsync(Work w)
    {
        var tenant = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.Name).FirstAsync();
        var cutoff = w.Now.AddMinutes(-30);
        var waiting = await db.Tickets.AsNoTracking()
            .Where(t => t.Status == TicketStatus.Open && t.FirstResponseAtUtc == null && t.CreatedAtUtc <= cutoff)
            .OrderBy(t => t.CreatedAtUtc).Take(8)
            .Select(t => new { t.Id, t.Number, t.Subject, t.Description, Category = t.Category!.Name, Name = t.Employee!.FirstName + " " + t.Employee.LastName, t.CreatedAtUtc })
            .ToListAsync();
        w.Step($"Found {waiting.Count} ticket(s) with no reply yet.");
        foreach (var t in waiting)
        {
            var hours = (int)(w.Now - t.CreatedAtUtc).TotalHours;
            var link = $"/helpdesk/{t.Id}";
            w.Find($"#{t.Number} {t.Subject}", $"{t.Name} ({t.Category}) has waited {hours} hour(s) with no reply.", hours >= 24 ? "act" : "watch", link);
            var draft = await ai.FreeJsonAsync<ReplyAnswer>($"""
                You are the HR helpdesk of {tenant}. Draft a short, polite first reply to this employee's ticket (2 to 5 sentences).
                Answer in the language the ticket is written in. Do not promise money, approvals or dates, and do not invent company policy;
                when HR must check something, say HR will look into it and reply. Ask for any detail HR will need. JSON: {"{"}"reply": text{"}"}.
                """, $"Category: {t.Category}\nSubject: {t.Subject}\nFrom: {t.Name}\n\n{Cut(t.Description, 4000)}");
            if (string.IsNullOrWhiteSpace(draft?.Reply))
            {
                w.Step($"The AI was busy; no draft for #{t.Number}.");
                continue;
            }
            w.Step($"Drafted a reply for #{t.Number}.");
            w.Proposals.Add(new("helpdesk_reply", $"Reply to #{t.Number}: {t.Subject}", Cut(draft.Reply.Trim(), 3000),
                new { ticketId = t.Id.ToString(), body = Cut(draft.Reply.Trim(), 3000) }, $"ticket:{t.Id}", link, false));
        }
        var overdue = await db.Tickets.AsNoTracking()
            .CountAsync(t => t.DueAtUtc < w.Now && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress));
        w.Step($"{overdue} open ticket(s) are past their due time.");
        if (overdue > 0) w.Find($"{overdue} ticket(s) past due", "Open tickets past their target time. Reassign or answer them first.", "act", "/helpdesk/queue");
        if (waiting.Count == 0 && overdue == 0) w.Find("Helpdesk is on time", "Every ticket has a reply and none is past due.", "good");
    }

    async Task PeopleRiskAsync(Work w)
    {
        var from = w.Today.AddDays(-60);
        var people = await db.Employees.AsNoTracking()
            .Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.JoiningDate }).ToListAsync();
        w.Step($"Looked at {people.Count} active people over the last 60 days.");
        var att = (await db.AttendanceRecords.AsNoTracking().Where(a => a.Date >= from)
                .Select(a => new { a.EmployeeId, a.LateMinutes, a.Status }).ToListAsync())
            .GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => (Late: g.Count(x => x.LateMinutes > 0), Absent: g.Count(x => x.Status == AttendanceStatus.Absent)));
        var leave = (await db.LeaveRequests.AsNoTracking().Where(l => l.Status == LeaveStatus.Approved && l.FromDate >= from)
                .Select(l => new { l.EmployeeId, l.Days }).ToListAsync())
            .GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Days));
        var raised = (await db.SalaryRevisions.AsNoTracking().Select(r => new { r.EmployeeId, r.EffectiveFrom }).ToListAsync())
            .GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.Max(x => x.EffectiveFrom));
        var leaving = await db.Separations.AsNoTracking().Where(s => s.Status == SeparationStatus.Requested || s.Status == SeparationStatus.InProgress)
            .Select(s => s.EmployeeId).ToListAsync();
        w.Step("Combined lateness, absence, leave and pay history.");

        var scored = new List<(string Name, Guid Id, int Score, List<string> Why)>();
        foreach (var p in people.Where(p => !leaving.Contains(p.Id)))
        {
            var why = new List<string>();
            var score = 0;
            if (att.TryGetValue(p.Id, out var a))
            {
                if (a.Late >= 8) { score += 2; why.Add($"late {a.Late} times"); }
                else if (a.Late >= 4) { score += 1; why.Add($"late {a.Late} times"); }
                if (a.Absent >= 3) { score += 2; why.Add($"absent {a.Absent} days"); }
            }
            if (leave.TryGetValue(p.Id, out var d) && d >= 8) { score += 1; why.Add($"{d:0.#} days of leave"); }
            var months = (w.Today.Year - p.JoiningDate.Year) * 12 + w.Today.Month - p.JoiningDate.Month;
            var lastRaise = raised.TryGetValue(p.Id, out var r) ? r : (DateOnly?)null;
            var sinceRaise = lastRaise is { } lr ? (w.Today.Year - lr.Year) * 12 + w.Today.Month - lr.Month : months;
            if (months >= 12 && sinceRaise >= 18) { score += 2; why.Add(lastRaise is null ? $"no raise in {months} months with us" : $"no raise for {sinceRaise} months"); }
            if (score >= 3) scored.Add((p.Name, p.Id, score, why));
        }
        foreach (var s in scored.OrderByDescending(s => s.Score).Take(10))
            w.Find($"{s.Name}: {(s.Score >= 5 ? "high" : "some")} leaving risk",
                $"In the last 60 days: {string.Join(", ", s.Why)}. A pointer for a friendly check-in, not a judgement.",
                s.Score >= 5 ? "act" : "watch", $"/employees/{s.Id}");
        if (leaving.Count > 0)
            w.Find($"{leaving.Count} resignation(s) or exit(s) in progress", "Plan handovers and exit interviews.", "watch", "/separations");
        if (scored.Count == 0) w.Find("No one stands out", "Nobody shows several warning signs together.", "good");
    }

    async Task OnboardingAsync(Work w)
    {
        var list = await db.Onboardings.AsNoTracking().Include(o => o.Tasks).Include(o => o.Employee)
            .Where(o => o.Status == OnboardingStatus.InProgress).ToListAsync();
        w.Step($"Checked {list.Count} onboarding plan(s) in progress.");
        var hrIds = await db.Users.Where(u => u.IsActive && u.EmployeeId != null && (u.Role == UserRole.HrManager || u.Role == UserRole.CompanyAdmin))
            .Select(u => u.EmployeeId!.Value).ToListAsync();
        foreach (var o in list)
        {
            var name = $"{o.Employee!.FirstName} {o.Employee.LastName}";
            var link = $"/onboarding/{o.Id}";
            var overdue = o.Tasks.Where(t => t.CompletedAtUtc == null && t.DueOn < w.Today).OrderBy(t => t.DueOn).ToList();
            var open = o.Tasks.Count(t => t.CompletedAtUtc == null);
            if (o.JoiningDate >= w.Today && o.JoiningDate <= w.Today.AddDays(3) && open > 0)
                w.Find($"{name} joins on {o.JoiningDate:d MMM}", $"{open} onboarding task(s) are still open.", "act", link);
            if (overdue.Count == 0) continue;
            w.Find($"{name}: {overdue.Count} overdue task(s)", string.Join("; ", overdue.Take(5).Select(t => $"{t.Title} (due {t.DueOn:d MMM})")), "watch", link);
            foreach (var t in overdue)
            {
                var who = t.AssigneeKind switch
                {
                    OnboardingAssignee.NewJoiner => [o.EmployeeId],
                    OnboardingAssignee.LineManager => o.Employee.ManagerId is { } m ? [m] : hrIds,
                    OnboardingAssignee.Buddy => o.BuddyEmployeeId is { } b ? [b] : hrIds,
                    OnboardingAssignee.SpecificEmployee => t.AssigneeEmployeeId is { } s ? [s] : hrIds,
                    _ => hrIds,
                };
                if (who.Count == 0) continue;
                var text = $"The onboarding task \"{t.Title}\" for {name} was due on {t.DueOn:d MMM}. Please complete it or update its status.";
                w.Proposals.Add(new("notify_employee", $"Remind about \"{t.Title}\" ({name})", text,
                    new { employeeIds = who.Select(i => i.ToString()).ToArray(), title = $"Onboarding task overdue: {t.Title}", body = text, link },
                    $"onb:{t.Id}", link, true));
            }
        }
        if (w.Findings.Count == 0) w.Find("Onboarding is on time", "No overdue onboarding tasks.", "good");
    }

    async Task LeaveAsync(Work w)
    {
        var until = w.Today.AddDays(14);
        var upcoming = await db.LeaveRequests.AsNoTracking()
            .Where(l => (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending) && l.ToDate >= w.Today && l.FromDate <= until)
            .Select(l => new { l.EmployeeId, l.FromDate, l.ToDate, Dept = l.Employee!.DepartmentId, DeptName = l.Employee.Department != null ? l.Employee.Department.Name : "No department" })
            .ToListAsync();
        var sizes = await db.Employees.AsNoTracking().Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .GroupBy(e => e.DepartmentId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key ?? Guid.Empty, g => g.Count);
        w.Step($"Looked at {upcoming.Count} leave request(s) in the next 14 days.");
        foreach (var dept in upcoming.GroupBy(u => (u.Dept ?? Guid.Empty, u.DeptName)))
        {
            var size = sizes.TryGetValue(dept.Key.Item1, out var s) ? s : 0;
            if (size < 3) continue;
            var worst = Enumerable.Range(0, 15).Select(i => w.Today.AddDays(i))
                .Select(d => (Day: d, Away: dept.Where(x => x.FromDate <= d && x.ToDate >= d).Select(x => x.EmployeeId).Distinct().Count()))
                .Where(x => x.Away >= 2 && x.Away * 100 / size >= 30).OrderByDescending(x => x.Away).FirstOrDefault();
            if (worst.Away > 0)
                w.Find($"{dept.Key.DeptName}: {worst.Away} of {size} away on {worst.Day:ddd d MMM}",
                    "A large part of the team is on (or has asked for) leave that day. Check cover before approving more.", "watch", "/leave/approvals");
        }
        var stale = w.Now.AddDays(-3);
        var waiting = await db.LeaveRequests.AsNoTracking().CountAsync(l => l.Status == LeaveStatus.Pending && l.CreatedAtUtc <= stale);
        w.Step($"{waiting} leave request(s) have waited 3+ days for a decision.");
        if (waiting > 0) w.Find($"{waiting} leave request(s) waiting 3+ days", "Approvers have not decided yet; a reminder may help.", "act", "/leave/approvals");

        if (w.Today.Month >= 10)
        {
            var left = await db.LeaveBalances.AsNoTracking()
                .Where(b => b.Year == w.Today.Year && b.LeaveType!.IsActive && b.LeaveType.IsPaid
                            && (b.Entitled + b.CarriedForward + b.Adjustment + b.Credited - b.Used) >= 10)
                .Select(b => new { b.EmployeeId, Type = b.LeaveType!.Name, Left = b.Entitled + b.CarriedForward + b.Adjustment + b.Credited - b.Used,
                    Name = db.Employees.Where(e => e.Id == b.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault() })
                .ToListAsync();
            w.Step($"Year end is near: {left.Count} balance(s) with 10+ days left.");
            if (left.Count > 0)
                w.Find($"{left.Count} people have 10+ days of leave left", Names(left.Select(l => $"{l.Name} ({l.Left:0.#} {l.Type})")) + ". Unused leave may lapse at year end.", "watch", "/leave/opening-balances");
            foreach (var l in left)
            {
                var text = $"You still have {l.Left:0.#} days of {l.Type} for {w.Today.Year}. Please plan them with your manager before the year ends.";
                w.Proposals.Add(new("notify_employee", $"Remind {l.Name} about {l.Left:0.#} days of {l.Type}", text,
                    new { employeeIds = new[] { l.EmployeeId.ToString() }, title = "Plan your remaining leave", body = text, link = "/me/leave" },
                    $"leaveleft:{l.EmployeeId}:{l.Type}:{w.Today:yyyyMM}", "/me/leave", true));
            }
        }
        if (w.Findings.Count == 0) w.Find("Leave looks fine", "No team clashes in the next two weeks and nothing waiting long.", "good");
    }

    async Task DocumentsAsync(Work w)
    {
        var soon = w.Today.AddDays(30);
        var people = await db.Employees.AsNoTracking()
            .Where(e => (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
                        && (e.CnicExpiresOn <= soon || e.PassportExpiresOn <= soon || e.VisaExpiresOn <= soon || e.DrivingLicenseExpiresOn <= soon || e.InsuranceExpiresOn <= soon))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.CnicExpiresOn, e.PassportExpiresOn, e.VisaExpiresOn, e.DrivingLicenseExpiresOn, e.InsuranceExpiresOn })
            .ToListAsync();
        w.Step($"Checked ID, passport, visa, licence and insurance dates: {people.Count} people affected.");
        foreach (var p in people)
        {
            foreach (var (doc, date) in new[] { ("CNIC", p.CnicExpiresOn), ("passport", p.PassportExpiresOn), ("visa", p.VisaExpiresOn),
                         ("driving licence", p.DrivingLicenseExpiresOn), ("insurance", p.InsuranceExpiresOn) })
            {
                if (date is not { } d || d > soon) continue;
                var gone = d < w.Today;
                w.Find($"{p.Name}: {doc} {(gone ? "expired" : "expires")} {d:d MMM yyyy}", gone ? "Already expired. Ask for the renewed copy." : $"{d.DayNumber - w.Today.DayNumber} day(s) left.",
                    gone ? "act" : "watch", $"/employees/{p.Id}");
                var text = gone
                    ? $"Your {doc} expired on {d:d MMM yyyy}. Please renew it and send HR a copy."
                    : $"Your {doc} expires on {d:d MMM yyyy}. Please renew it in time and send HR a copy.";
                w.Proposals.Add(new("notify_employee", $"Remind {p.Name} to renew their {doc}", text,
                    new { employeeIds = new[] { p.Id.ToString() }, title = $"Your {doc} {(gone ? "has expired" : "expires soon")}", body = text, link = "/me/profile" },
                    $"doc:{p.Id}:{doc}:{d:yyyyMMdd}", $"/employees/{p.Id}", true));
            }
        }
        if (people.Count == 0) w.Find("All documents are current", "Nothing expires in the next 30 days.", "good");
    }

    static string Cut(string? s, int max) => s is null ? "" : s.Length <= max ? s : s[..max];
}

/// <summary>Runs each switched-on robot once a day at its hour, company by company, unless the company pressed the emergency stop.</summary>
public class RobotWorkService(IServiceProvider services, IHostEnvironment env, ILogger<RobotWorkService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (env.IsEnvironment("Testing"))
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                List<Guid> tenants;
                using (var scope = services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
                    tenants = await db.AiRobots.IgnoreQueryFilters().Where(r => r.Enabled).Select(r => r.TenantId).Distinct().ToListAsync(stoppingToken);
                }
                foreach (var tenant in tenants)
                {
                    List<Guid> due;
                    using (var scope = services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
                        db.UseTenant(tenant);
                        if (!await db.Tenants.AnyAsync(t => t.Id == tenant && t.IsActive && !t.RobotsStopped, stoppingToken))
                            continue;
                        var clock = scope.ServiceProvider.GetRequiredService<TenantClock>();
                        var now = await clock.LocalNowAsync();
                        var zone = await clock.ZoneAsync();
                        var robots = await db.AiRobots.Where(r => r.Enabled && r.RunHour <= now.Hour).ToListAsync(stoppingToken);
                        due = robots.Where(r => r.LastRunAtUtc is not { } last || TimeZoneInfo.ConvertTimeFromUtc(last, zone).Date < now.Date)
                            .Select(r => r.Id).ToList();
                    }
                    foreach (var id in due)
                    {
                        try
                        {
                            using var scope = services.CreateScope();
                            scope.ServiceProvider.GetRequiredService<HrmsDbContext>().UseTenant(tenant);
                            await scope.ServiceProvider.GetRequiredService<RobotService>().RunAsync(id, manual: false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            log.LogWarning(ex, "Robot {Robot} of company {Tenant} could not run.", id, tenant);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "AI Team schedule failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
