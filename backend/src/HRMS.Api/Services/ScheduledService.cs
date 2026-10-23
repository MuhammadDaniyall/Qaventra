using System.Net;
using ClosedXML.Excel;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>A report as a plain table, for the email, the Excel copy and the preview.</summary>
public record ReportTable(string Title, string? Subtitle, List<string> Headers, List<List<string>> Rows);

/// <summary>One person an alert is about, with the values its placeholders need.</summary>
public record AlertSubject(Guid EmployeeId, string Name, Guid? ManagerId, string? Department, DateOnly Date, string? Extra, int? Years);

/// <summary>
/// Alerts (birthdays, anniversaries, probation, expiring documents, joiners, leave, missed check-ins) and reports
/// emailed on a schedule. Both run from <see cref="ScheduledWorkService"/>; HR can also run them by hand.
/// Works for the company set with <c>UseTenant</c> or the signed-in user's company.
/// </summary>
public class ScheduledService(HrmsDbContext db, TenantClock clock, NotificationService notify, EmailQueue emails, ScheduleService schedules)
{
    // ---------- Alerts ----------

    /// <summary>People the alert is about on the given day (the event day is that day plus DaysBefore).</summary>
    public async Task<List<AlertSubject>> SubjectsAsync(ScheduledAlert a, DateOnly today)
    {
        var target = today.AddDays(a.Kind == AlertKind.NotCheckedIn ? 0 : a.DaysBefore);
        var depts = HelpdeskService.Ids(a.DepartmentIds);
        var staff = db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
        if (depts.Count > 0)
            staff = staff.Where(e => e.DepartmentId != null && depts.Contains(e.DepartmentId.Value));
        var people = await staff.Select(e => new Person(
            e.Id, e.FirstName + " " + e.LastName, e.ManagerId, e.Department != null ? e.Department.Name : null, e.DateOfBirth, e.JoiningDate, e.Status)).ToListAsync();
        static AlertSubject S(Person p, DateOnly date, string? extra = null, int? years = null) => new(p.Id, p.Name, p.ManagerId, p.Department, date, extra, years);

        switch (a.Kind)
        {
            case AlertKind.Birthday:
                return people.Where(p => p.DateOfBirth is { } d && SameDay(d, target)).Select(p => S(p, target, null, target.Year - p.DateOfBirth!.Value.Year)).ToList();
            case AlertKind.WorkAnniversary:
                return people.Where(p => SameDay(p.JoiningDate, target) && target.Year > p.JoiningDate.Year)
                    .Select(p => S(p, target, null, target.Year - p.JoiningDate.Year)).ToList();
            case AlertKind.ProbationEnd:
                return people.Where(p => p.Status == EmploymentStatus.Probation && p.JoiningDate.AddMonths(a.ProbationMonths) == target).Select(p => S(p, target)).ToList();
            case AlertKind.NewJoiner:
                return people.Where(p => p.JoiningDate == target).Select(p => S(p, target)).ToList();
            case AlertKind.DocumentExpiry:
            {
                var ids = people.Select(p => p.Id).ToList();
                var docs = await ReportCatalog.ExpiriesAsync(db, ids, target, target);
                return docs.Select(d => S(people.First(p => p.Id == d.EmployeeId), target, d.Document)).ToList();
            }
            case AlertKind.LeaveStarting:
            {
                var ids = people.Select(p => p.Id).ToList();
                var leaves = await db.LeaveRequests.Where(l => l.Status == LeaveStatus.Approved && l.FromDate == target && ids.Contains(l.EmployeeId))
                    .Select(l => new { l.EmployeeId, Type = l.LeaveType!.Name, l.Days, l.ToDate }).ToListAsync();
                return leaves.Select(l => S(people.First(p => p.Id == l.EmployeeId), target, $"{l.Type}, {l.Days:0.#} day(s) until {l.ToDate:d MMM}")).ToList();
            }
            case AlertKind.NotCheckedIn:
            {
                var ids = people.Select(p => p.Id).ToList();
                var plan = await schedules.ForEmployeesAsync(ids, today, today);
                var checkedIn = await db.AttendanceRecords.Where(r => r.Date == today && ids.Contains(r.EmployeeId)).Select(r => r.EmployeeId).ToListAsync();
                var onLeave = await db.LeaveRequests.Where(l => l.Status == LeaveStatus.Approved && l.FromDate <= today && l.ToDate >= today && ids.Contains(l.EmployeeId))
                    .Select(l => l.EmployeeId).ToListAsync();
                return people.Where(p => plan.TryGetValue(p.Id, out var s) && s.IsWorkingDay(today) && !checkedIn.Contains(p.Id) && !onLeave.Contains(p.Id))
                    .Select(p => S(p, today)).ToList();
            }
            default:
                return [];
        }
    }

    /// <summary>Sends the alert for today. Returns how many people it was about.</summary>
    public async Task<int> RunAlertAsync(ScheduledAlert a, bool manual)
    {
        var today = await clock.TodayAsync();
        var subjects = await SubjectsAsync(a, today);
        var extra = HelpdeskService.Ids(a.ExtraRecipientIds);
        var recipients = new HashSet<Guid>();
        foreach (var s in subjects)
        {
            var to = new List<Guid>(extra);
            if (a.ToEmployee)
                to.Add(s.EmployeeId);
            if (a.ToManager && s.ManagerId is { } m)
                to.Add(m);
            var title = Fill(a.TitleTemplate, s, a.DaysBefore);
            var body = a.BodyTemplate is null ? null : Fill(a.BodyTemplate, s, a.DaysBefore);
            var link = a.Kind == AlertKind.DocumentExpiry ? $"/employees/{s.EmployeeId}" : a.Kind == AlertKind.NotCheckedIn ? "/attendance/daily" : null;
            if (a.ToEveryone)
            {
                var everyone = await db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation).Select(e => e.Id).ToListAsync();
                to.AddRange(everyone.Where(id => id != s.EmployeeId || a.ToEmployee));
            }
            await notify.ToEmployeesAsync(to.Distinct(), NotificationKind.Alert, title, body, link, includeMe: true);
            if (a.ToHr)
                await notify.ToHrAsync(NotificationKind.Alert, title, body, link ?? $"/employees/{s.EmployeeId}", includeMe: true);
            foreach (var id in to)
                recipients.Add(id);
        }
        a.LastRunOn = today;
        db.ScheduledRuns.Add(new ScheduledRun
        {
            AlertId = a.Id, Name = a.Name, Items = subjects.Count, Recipients = recipients.Count + (a.ToHr && subjects.Count > 0 ? 1 : 0), Manual = manual,
            Note = subjects.Count == 0 ? "Nobody today" : string.Join(", ", subjects.Take(5).Select(s => s.Name)) + (subjects.Count > 5 ? $" and {subjects.Count - 5} more" : ""),
        });
        await db.SaveChangesAsync();
        return subjects.Count;
    }

    public static string Fill(string template, AlertSubject s, int daysBefore) => template
        .Replace("{name}", s.Name, StringComparison.OrdinalIgnoreCase)
        .Replace("{firstname}", s.Name.Split(' ')[0], StringComparison.OrdinalIgnoreCase)
        .Replace("{date}", s.Date.ToString("d MMM yyyy"), StringComparison.OrdinalIgnoreCase)
        .Replace("{years}", s.Years?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{document}", s.Extra ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{details}", s.Extra ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{department}", s.Department ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{days}", daysBefore.ToString(), StringComparison.OrdinalIgnoreCase);

    // ---------- Reports ----------

    public async Task<ReportTable> BuildAsync(ScheduledReport r)
    {
        var today = await clock.TodayAsync();
        var depts = HelpdeskService.Ids(r.DepartmentIds);
        var staff = db.Employees.AsQueryable();
        if (depts.Count > 0)
            staff = staff.Where(e => e.DepartmentId != null && depts.Contains(e.DepartmentId.Value));
        var current = staff.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
        var (from, to) = Period(r.Frequency, today);
        switch (r.Kind)
        {
            case ReportKind.Headcount:
            {
                var rows = await current.GroupBy(e => e.Department != null ? e.Department.Name : "No department")
                    .Select(g => new { g.Key, Total = g.Count(), Probation = g.Count(e => e.Status == EmploymentStatus.Probation), Joined = g.Count(e => e.JoiningDate >= from && e.JoiningDate <= to) })
                    .OrderBy(x => x.Key).ToListAsync();
                var left = await staff.Where(e => e.LeavingDate >= from && e.LeavingDate <= to).GroupBy(e => e.Department != null ? e.Department.Name : "No department")
                    .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
                var list = rows.Select(x => new List<string> { x.Key, x.Total.ToString(), x.Probation.ToString(), x.Joined.ToString(), (left.FirstOrDefault(l => l.Key == x.Key)?.Count ?? 0).ToString() }).ToList();
                list.Add(["Total", rows.Sum(x => x.Total).ToString(), rows.Sum(x => x.Probation).ToString(), rows.Sum(x => x.Joined).ToString(), left.Sum(x => x.Count).ToString()]);
                return new("Headcount", $"As of {today:d MMM yyyy}; joined and left {Range(from, to)}", ["Department", "Employees", "On probation", "Joined", "Left"], list);
            }
            case ReportKind.DailyAttendance:
            {
                var day = r.Frequency == ReportFrequency.Daily && (await clock.LocalNowAsync()).Hour < 12 ? today.AddDays(-1) : today;
                var people = await current.OrderBy(e => e.FirstName).Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "" }).ToListAsync();
                var ids = people.Select(p => p.Id).ToList();
                var plan = await schedules.ForEmployeesAsync(ids, day, day);
                var recs = await db.AttendanceRecords.Where(a => a.Date == day && ids.Contains(a.EmployeeId)).ToListAsync();
                var leave = await db.LeaveRequests.Where(l => l.Status == LeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day && ids.Contains(l.EmployeeId))
                    .Select(l => new { l.EmployeeId, Type = l.LeaveType!.Name }).ToListAsync();
                var rows = new List<List<string>>();
                foreach (var p in people)
                {
                    var rec = recs.FirstOrDefault(x => x.EmployeeId == p.Id);
                    var lv = leave.FirstOrDefault(x => x.EmployeeId == p.Id);
                    var working = plan.TryGetValue(p.Id, out var s) && s.IsWorkingDay(day);
                    var status = rec is not null ? Humanize(rec.Status.ToString()) : lv is not null ? $"On leave ({lv.Type})" : working ? "Absent" : "Off";
                    if (status == "Off")
                        continue;
                    rows.Add([p.EmployeeCode, p.Name, p.Department, status,
                        rec is null ? "" : (await clock.ToLocalAsync(rec.CheckInUtc)).ToString("HH:mm"),
                        rec?.CheckOutUtc is { } co ? (await clock.ToLocalAsync(co)).ToString("HH:mm") : ""]);
                }
                var present = rows.Count(x => x[4] != "");
                return new($"Attendance, {day:ddd d MMM yyyy}", $"{present} in, {rows.Count(x => x[3] == "Absent")} absent, {rows.Count(x => x[3].StartsWith("On leave"))} on leave",
                    ["Code", "Name", "Department", "Status", "In", "Out"], rows.OrderBy(x => x[3] == "Absent" ? 0 : 1).ThenBy(x => x[1]).ToList());
            }
            case ReportKind.OnLeave:
            {
                var until = today.AddDays(r.Frequency == ReportFrequency.Monthly ? 30 : 7);
                var ids = current.Select(e => e.Id);
                var rows = await db.LeaveRequests.Where(l => (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending) && l.FromDate <= until && l.ToDate >= today && ids.Contains(l.EmployeeId))
                    .OrderBy(l => l.FromDate)
                    .Select(l => new { l.Employee!.FirstName, l.Employee.LastName, Department = l.Employee.Department != null ? l.Employee.Department.Name : "", Type = l.LeaveType!.Name, l.FromDate, l.ToDate, l.Days, l.Status })
                    .ToListAsync();
                return new("Leave", $"Today to {until:d MMM yyyy}", ["Name", "Department", "Leave", "From", "To", "Days", "Status"],
                    rows.Select(x => new List<string> { $"{x.FirstName} {x.LastName}", x.Department, x.Type, x.FromDate.ToString("d MMM"), x.ToDate.ToString("d MMM"), x.Days.ToString("0.#"), x.Status.ToString() }).ToList());
            }
            case ReportKind.PendingApprovals:
            {
                var ids = current.Select(e => e.Id);
                var rows = await db.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending && ids.Contains(a.EmployeeId)).OrderBy(a => a.CreatedAtUtc)
                    .Select(a => new
                    {
                        a.RequestType, a.Employee!.FirstName, a.Employee.LastName, a.Summary, a.CreatedAtUtc,
                        Step = a.Steps.Where(s => s.Status == ApprovalStepStatus.Current).Select(s => new { s.Kind, s.ApproverEmployeeId }).FirstOrDefault(),
                    }).ToListAsync();
                var approverIds = rows.Where(x => x.Step?.ApproverEmployeeId != null).Select(x => x.Step!.ApproverEmployeeId!.Value).Distinct().ToList();
                var names = await db.Employees.Where(e => approverIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
                var now = DateTime.UtcNow;
                return new("Pending approvals", $"{rows.Count} waiting", ["Request", "Employee", "Details", "Waiting for", "Days waiting"],
                    rows.Select(x => new List<string>
                    {
                        Humanize(x.RequestType.ToString()), $"{x.FirstName} {x.LastName}", x.Summary,
                        x.Step?.ApproverEmployeeId is { } aid ? names.GetValueOrDefault(aid) ?? "" : "HR", ((int)(now - x.CreatedAtUtc).TotalDays).ToString(),
                    }).ToList());
            }
            case ReportKind.ExpiringDocuments:
            {
                var until = today.AddDays(r.Frequency == ReportFrequency.Monthly ? 60 : 30);
                var people = await current.Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode }).ToListAsync();
                var ids = people.Select(p => p.Id).ToList();
                var docs = await ReportCatalog.ExpiriesAsync(db, ids, null, until);
                return new("Documents expiring", $"Expired or expiring by {until:d MMM yyyy}", ["Code", "Name", "Document", "Expires", "State"],
                    docs.Select(d =>
                    {
                        var p = people.First(x => x.Id == d.EmployeeId);
                        return new List<string> { p.EmployeeCode, p.Name, d.Document, d.ExpiresOn.ToString("d MMM yyyy"), d.ExpiresOn < today ? "Expired" : "Expiring" };
                    }).ToList());
            }
            case ReportKind.JoinersAndLeavers:
            {
                var joined = await staff.Where(e => e.JoiningDate >= from && e.JoiningDate <= to)
                    .Select(e => new { e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "", Date = e.JoiningDate, Designation = e.Designation != null ? e.Designation.Title : "" }).ToListAsync();
                var left = await staff.Where(e => e.LeavingDate >= from && e.LeavingDate <= to)
                    .Select(e => new { e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "", Date = e.LeavingDate!.Value, Designation = e.Designation != null ? e.Designation.Title : "" }).ToListAsync();
                var rows = joined.Select(x => new List<string> { "Joined", x.EmployeeCode, x.Name, x.Department, x.Designation, x.Date.ToString("d MMM yyyy") })
                    .Concat(left.Select(x => new List<string> { "Left", x.EmployeeCode, x.Name, x.Department, x.Designation, x.Date.ToString("d MMM yyyy") })).ToList();
                return new("Joiners and leavers", $"{Range(from, to)}: {joined.Count} joined, {left.Count} left", ["", "Code", "Name", "Department", "Designation", "Date"], rows);
            }
            case ReportKind.ProbationDue:
            {
                var until = today.AddDays(30);
                var people = await current.Where(e => e.Status == EmploymentStatus.Probation)
                    .Select(e => new { e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "", e.JoiningDate, Manager = e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : "" })
                    .ToListAsync();
                var rows = people.Select(p => new { p, Ends = p.JoiningDate.AddMonths(3) }).Where(x => x.Ends <= until).OrderBy(x => x.Ends)
                    .Select(x => new List<string> { x.p.EmployeeCode, x.p.Name, x.p.Department, x.p.Manager, x.p.JoiningDate.ToString("d MMM yyyy"), x.Ends.ToString("d MMM yyyy"), x.Ends < today ? "Overdue" : "Due" })
                    .ToList();
                return new("Probation reviews due", "Three months after joining, within the next 30 days or overdue", ["Code", "Name", "Department", "Manager", "Joined", "Probation ends", "State"], rows);
            }
            default:
                return new(r.Name, null, [], []);
        }
    }

    /// <summary>Emails the report to its recipients (or just to <paramref name="onlyTo"/>), with an Excel copy.</summary>
    public async Task<int> SendReportAsync(ScheduledReport r, bool manual, string? onlyTo = null)
    {
        var table = await BuildAsync(r);
        var company = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.Name).FirstAsync();
        var file = new EmailAttachment($"{Safe(r.Name)} {await clock.TodayAsync():yyyy-MM-dd}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ToExcel(table));
        var html = Html(company, r.Name, table);
        List<string> to;
        if (onlyTo is not null)
            to = [onlyTo];
        else
        {
            var ids = HelpdeskService.Ids(r.RecipientIds);
            to = await db.Users.Where(u => u.IsActive && u.EmployeeId != null && ids.Contains(u.EmployeeId.Value)).Select(u => u.Email).ToListAsync();
            to.AddRange(await db.Employees.Where(e => ids.Contains(e.Id) && e.Email != null && !db.Users.Any(u => u.EmployeeId == e.Id)).Select(e => e.Email!).ToListAsync());
            to.AddRange(Emails(r.ExtraEmails));
        }
        foreach (var address in to.Distinct(StringComparer.OrdinalIgnoreCase))
            emails.Enqueue(new EmailMessage(address, $"{r.Name}: {table.Subtitle ?? table.Title}", html, [file]));
        if (onlyTo is null)
        {
            r.LastSentAtUtc = clock.UtcNow;
            r.NextRunAtUtc = await NextRunAsync(r, clock.UtcNow);
        }
        db.ScheduledRuns.Add(new ScheduledRun
        {
            ReportId = r.Id, Name = r.Name, Items = table.Rows.Count, Recipients = to.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Manual = manual,
            Note = onlyTo is null ? table.Subtitle : $"Test sent to {onlyTo}",
        });
        await db.SaveChangesAsync();
        return to.Count;
    }

    /// <summary>The next time after <paramref name="afterUtc"/> the report is due, at its hour in company time.</summary>
    public async Task<DateTime> NextRunAsync(ScheduledReport r, DateTime afterUtc)
    {
        var local = await clock.ToLocalAsync(afterUtc);
        var day = DateOnly.FromDateTime(local);
        for (var i = 0; i < 62; i++)
        {
            var d = day.AddDays(i);
            var due = r.Frequency switch
            {
                ReportFrequency.Daily => true,
                ReportFrequency.Weekly => (int)d.DayOfWeek == r.Day,
                _ => d.Day == r.Day,
            };
            if (!due)
                continue;
            var at = d.ToDateTime(new TimeOnly(r.SendHour, 0));
            if (at <= local)
                continue;
            return await clock.ToUtcAsync(at);
        }
        return afterUtc.AddDays(1);
    }

    /// <summary>Everything due for the current company: today's alerts after their hour, and reports whose time has come.</summary>
    public async Task RunDueAsync()
    {
        var now = await clock.LocalNowAsync();
        var today = DateOnly.FromDateTime(now);
        foreach (var a in await db.ScheduledAlerts.Where(a => a.IsActive && (a.LastRunOn == null || a.LastRunOn < today) && a.SendHour <= now.Hour).ToListAsync())
            await RunAlertAsync(a, manual: false);
        var utc = clock.UtcNow;
        foreach (var r in await db.ScheduledReports.Where(r => r.IsActive && r.NextRunAtUtc != null && r.NextRunAtUtc <= utc).ToListAsync())
            await SendReportAsync(r, manual: false);
    }

    // ---------- Helpers ----------

    private record Person(Guid Id, string Name, Guid? ManagerId, string? Department, DateOnly? DateOfBirth, DateOnly JoiningDate, EmploymentStatus Status);

    public static byte[] ToExcel(ReportTable t)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(t.Title.Length > 31 ? t.Title[..31] : t.Title);
        for (var c = 0; c < t.Headers.Count; c++)
            ws.Cell(1, c + 1).Value = t.Headers[c];
        for (var r = 0; r < t.Rows.Count; r++)
            for (var c = 0; c < t.Rows[r].Count; c++)
                ws.Cell(r + 2, c + 1).Value = t.Rows[r][c];
        Excel.StyleTable(ws, Math.Max(1, t.Headers.Count), t.Rows.Count + 1);
        return Excel.Save(wb);
    }

    private static string Html(string company, string name, ReportTable t)
    {
        static string e(string? s) => WebUtility.HtmlEncode(s ?? "");
        var head = string.Concat(t.Headers.Select(h => $"<th style=\"text-align:left;padding:6px 8px;border-bottom:2px solid #e4e4e7;font-size:12px;color:#52525b\">{e(h)}</th>"));
        var rows = string.Concat(t.Rows.Take(50).Select(r => "<tr>" + string.Concat(r.Select(c => $"<td style=\"padding:6px 8px;border-bottom:1px solid #f4f4f5;font-size:13px\">{e(c)}</td>")) + "</tr>"));
        var more = t.Rows.Count > 50 ? $"<p style=\"color:#71717a;font-size:12px\">Showing 50 of {t.Rows.Count} rows. The attached Excel file has them all.</p>" : "";
        var empty = t.Rows.Count == 0 ? "<p style=\"color:#71717a\">Nothing to report.</p>" : "";
        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:760px;margin:auto;color:#18181b">
              <p style="color:#71717a;font-size:12px;margin:0">{e(company)} · scheduled report</p>
              <h2 style="margin:4px 0 2px">{e(name)}</h2>
              <p style="color:#52525b;margin:0 0 16px">{e(t.Subtitle)}</p>
              {(t.Rows.Count == 0 ? empty : $"<table style=\"border-collapse:collapse;width:100%\"><thead><tr>{head}</tr></thead><tbody>{rows}</tbody></table>{more}")}
              <p style="color:#a1a1aa;font-size:11px;margin-top:24px">Sent by Qaventra because HR scheduled this report for you.</p>
            </div>
            """;
    }

    private static (DateOnly From, DateOnly To) Period(ReportFrequency f, DateOnly today) => f switch
    {
        ReportFrequency.Daily => (today.AddDays(-1), today),
        ReportFrequency.Weekly => (today.AddDays(-7), today),
        _ => (today.AddMonths(-1), today),
    };

    private static string Range(DateOnly from, DateOnly to) => $"{from:d MMM} – {to:d MMM yyyy}";

    private static bool SameDay(DateOnly d, DateOnly target) =>
        d.Month == target.Month && (d.Day == target.Day || (d.Month == 2 && d.Day == 29 && target.Day == 28 && !DateTime.IsLeapYear(target.Year)));

    public static List<string> Emails(string list) =>
        list.Split([',', ';', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Contains('@')).Distinct().ToList();

    private static string Safe(string name) => string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));

    public static string Humanize(string s) => string.Concat(s.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
}

/// <summary>Runs due alerts and reports for every company every 15 minutes. Off in tests.</summary>
public class ScheduledWorkService(IServiceProvider services, IHostEnvironment env, ILogger<ScheduledWorkService> log) : BackgroundService
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
                    tenants = await db.ScheduledAlerts.IgnoreQueryFilters().Where(a => a.IsActive).Select(a => a.TenantId)
                        .Union(db.ScheduledReports.IgnoreQueryFilters().Where(r => r.IsActive).Select(r => r.TenantId))
                        .Distinct().ToListAsync(stoppingToken);
                }
                foreach (var tenant in tenants)
                {
                    try
                    {
                        using var scope = services.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
                        db.UseTenant(tenant);
                        if (!await db.Tenants.AnyAsync(t => t.Id == tenant && t.IsActive, stoppingToken))
                            continue;
                        await scope.ServiceProvider.GetRequiredService<ScheduledService>().RunDueAsync();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        log.LogWarning(ex, "Scheduled alerts and reports failed for company {Tenant}.", tenant);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Scheduled alerts and reports failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
