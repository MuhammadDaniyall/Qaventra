using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record AiPolicySource(string Title, string Kind);
public record AiPolicyAnswer(string Answer, bool Found, List<AiPolicySource> Sources, bool UsedAi);

public record AiReportInfo(string Key, string Title, string About);
public record AiReportParams(string? Department, string? Location, DateOnly From, DateOnly To, string? Status);
public record AiReportResult(string Report, string Title, string? Explanation, AiReportParams Params, List<string> Columns,
    List<List<string>> Rows, bool UsedAi, bool Truncated);

file sealed class AiPolicyJson
{
    public bool Found { get; set; }
    public string? Answer { get; set; }
    public List<string>? Sources { get; set; }
}

file sealed class AiReportPickJson
{
    public string? Report { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Status { get; set; }
    public string? Explanation { get; set; }
}

public partial class AiAssistantService
{
    const string NotInPolicyEn = "This is not in the company policy. Please ask HR.";
    const string NotInPolicyUr = "یہ بات کمپنی کی پالیسی میں نہیں ملی۔ براہِ کرم HR سے پوچھیں۔";

    /// <summary>
    /// Answers a question only from the company's own policy text (Policy announcements the person can see), leave type
    /// settings and holidays, and names where the answer came from. Without the AI it falls back to a keyword search
    /// over the same text; when nothing matches it says so instead of guessing.
    /// </summary>
    public async Task<AiPolicyAnswer> AskPolicyAsync(string question, string? language)
    {
        question = question.Trim();
        if (question.Length < 3) throw new DomainException("Write your question.");
        var ur = language == "ur";
        var today = await clock.TodayAsync();

        var who = me.EmployeeId is { } eid
            ? await db.Employees.Where(e => e.Id == eid).Select(e => new { e.DepartmentId, e.DesignationId }).FirstOrDefaultAsync()
            : null;
        var policies = (await db.Announcements.AsNoTracking()
                .Where(a => a.Kind == AnnouncementKind.Policy && a.PublishFrom <= today && (a.PublishTo == null || a.PublishTo >= today))
                .OrderByDescending(a => a.PublishFrom)
                .Select(a => new { a.Title, a.Body, a.DepartmentIds, a.DesignationIds })
                .Take(40).ToListAsync())
            .Where(a => who is null || AnnouncementService.InAudience(a.DepartmentIds, a.DesignationIds, who.DepartmentId, who.DesignationId))
            .ToList();
        var leaveTypes = await db.LeaveTypes.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync();
        var holidays = await db.Holidays.AsNoTracking().Where(h => h.Date.Year == today.Year).OrderBy(h => h.Date)
            .Select(h => new { h.Date, h.Name }).ToListAsync();

        static string LeaveFact(LeaveType l) =>
            $"{l.Name} ({l.Code}): {l.AnnualQuota:0.#} days a year, {(l.IsPaid ? "paid" : "unpaid")}, credited {l.Accrual}" +
            (l.CarryForwardMax > 0 ? $", up to {l.CarryForwardMax:0.#} days carry forward" : ", no carry forward") +
            (l.MaxConsecutiveDays is { } m ? $", at most {m} days in one request" : "") +
            (l.MinNoticeDays > 0 ? $", apply {l.MinNoticeDays} days in advance" : "") +
            (l.ApplicableGender is { } g ? $", only for {g}" : "") +
            (l.AllowedOnProbation ? "" : ", not allowed on probation") +
            (l.DocumentAfterDays is { } d ? $", document needed after {d:0.#} days" : "") +
            (l.Sandwich ? ", weekends/holidays in between count as leave" : "") + ".";

        var sourcesText = new List<(string Title, string Kind, string Text)>();
        foreach (var p in policies)
            sourcesText.Add((p.Title, "Policy", p.Body.Length > 4000 ? p.Body[..4000] : p.Body));
        if (leaveTypes.Count > 0)
            sourcesText.Add(("Leave types", "Leave settings", string.Join("\n", leaveTypes.Select(LeaveFact))));
        if (holidays.Count > 0)
            sourcesText.Add(($"Holidays {today.Year}", "Holiday calendar", string.Join("\n", holidays.Select(h => $"{h.Date:dd MMM yyyy}: {h.Name}"))));

        AiPolicyAnswer NotFound(bool usedAi) => new(ur ? NotInPolicyUr : NotInPolicyEn, false, [], usedAi);
        if (sourcesText.Count == 0) return NotFound(false);

        var data = JsonSerializer.Serialize(new
        {
            question,
            sources = sourcesText.Select(s => new { title = s.Title, kind = s.Kind, text = s.Text }),
        });
        if (data.Length > 30_000) data = data[..30_000];
        var lang = ur ? "Urdu (in Urdu script)" : "simple English (if the question is in Roman Urdu, answer in simple Roman Urdu)";
        var ai = await FreeJsonAsync<AiPolicyJson>(
            "You answer an employee's question using ONLY the company sources given (policies, leave settings, holidays). " +
            "Never use outside knowledge, Pakistani law or common practice, and never invent a rule, number or date. " +
            "If the sources do not clearly answer the question, set found to false. Answer in " + lang + ", in 1 to 4 short sentences. " +
            "Reply as JSON: {\"found\": true or false, \"answer\": string, \"sources\": [the exact titles of the sources you used]}.",
            data);
        if (ai is not null)
        {
            if (!ai.Found || string.IsNullOrWhiteSpace(ai.Answer)) return NotFound(true);
            var used = (ai.Sources ?? []).Select(t => sourcesText.FirstOrDefault(s => string.Equals(s.Title, t?.Trim(), StringComparison.OrdinalIgnoreCase)))
                .Where(s => s.Title is not null).DistinctBy(s => s.Title).Select(s => new AiPolicySource(s.Title, s.Kind)).ToList();
            // An answer that cites nothing from the company is not trusted.
            if (used.Count == 0) return NotFound(true);
            return new AiPolicyAnswer(ai.Answer.Trim(), true, used, true);
        }

        // No AI: plain keyword search over the same text, quoting what matched.
        var words = Regex.Split(question.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(w => w.Length >= 4 && !StopWords.Contains(w)).Distinct().ToList();
        if (words.Count == 0) return NotFound(false);
        var lines = new List<string>();
        var hits = new List<AiPolicySource>();
        foreach (var l in leaveTypes.Where(l => words.Any(w => l.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || l.Code.Equals(w, StringComparison.OrdinalIgnoreCase))))
        {
            lines.Add(LeaveFact(l));
            if (hits.All(h => h.Title != "Leave types")) hits.Add(new("Leave types", "Leave settings"));
        }
        foreach (var p in policies)
        {
            var score = words.Count(w => p.Title.Contains(w, StringComparison.OrdinalIgnoreCase) || p.Body.Contains(w, StringComparison.OrdinalIgnoreCase));
            if (score == 0 || score * 2 < Math.Min(words.Count, 3)) continue;
            var at = words.Select(w => p.Body.IndexOf(w, StringComparison.OrdinalIgnoreCase)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
            var start = Math.Max(0, at - 120);
            var snip = p.Body.Substring(start, Math.Min(360, p.Body.Length - start)).Trim();
            lines.Add($"{p.Title}: {(start > 0 ? "…" : "")}{snip}{(start + 360 < p.Body.Length ? "…" : "")}");
            hits.Add(new(p.Title, "Policy"));
            if (hits.Count >= 4) break;
        }
        if (lines.Count == 0) return NotFound(false);
        return new AiPolicyAnswer(string.Join("\n", lines), true, hits, false);
    }

    static readonly HashSet<string> StopWords = ["kitni", "kitne", "kitna", "kya", "kaise", "kahan", "mein", "mujhe", "hamari", "hamara", "what", "how", "many",
        "much", "does", "have", "with", "from", "about", "there", "this", "that", "company", "policy", "policies", "please", "hain", "hota", "milti", "milta",
        "sakta", "sakti", "leave"];

    // ---------------------------------------------------------------- Natural-language reports

    /// <summary>The only reports the AI may choose. It picks one and fills its filters; the query itself is fixed code here.</summary>
    public static readonly List<AiReportInfo> ReportCatalogue =
    [
        new("headcount", "Headcount", "Current staff (probation + active) per department, optionally for one department or location."),
        new("late_arrivals", "Late arrivals", "Who came late between two dates: number of late days and total late minutes per person."),
        new("absences", "Absences", "Who was absent between two dates: number of absent days per person."),
        new("leave_taken", "Leave taken", "Approved leave between two dates per person and leave type, in days."),
        new("payroll_totals", "Payroll totals", "Payroll for the months between two dates per department: people, gross, tax, deductions and net pay."),
        new("joiners", "New joiners", "People who joined between two dates."),
        new("leavers", "Leavers", "People who left (resigned or terminated) between two dates."),
        new("pending_approvals", "Pending approvals", "Requests still waiting for approval, oldest first."),
        new("expiring_documents", "Expiring documents", "CNIC, passport, visa, licence, insurance or contract ending between two dates (default the next 60 days)."),
    ];

    const int MaxReportRows = 500;

    /// <summary>
    /// Turns an HR question into one whitelisted report. The free model only picks the report key and its filters as
    /// JSON; the server runs a fixed EF query scoped to the company. Without the AI, a keyword match picks the report.
    /// </summary>
    public async Task<AiReportResult> RunReportAsync(string? question, string? report, string? department, string? location, DateOnly? from, DateOnly? to, string? status)
    {
        var today = await clock.TodayAsync();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        string? explanation = null;
        var usedAi = false;

        if (string.IsNullOrWhiteSpace(report))
        {
            if (string.IsNullOrWhiteSpace(question)) throw new DomainException("Type what you want to see.");
            var ai = await FreeJsonAsync<AiReportPickJson>(
                $"You turn an HR manager's question into one report from a fixed list. Today is {today:yyyy-MM-dd}. " +
                "Pick exactly one report key from the list and fill its filters. Do not write SQL or anything else. " +
                "department and location are names as the person wrote them (or null). Dates are yyyy-MM-dd; 'this month' means the 1st of this month to today, " +
                "'last month' the whole previous month. status is optional (e.g. probation, active). explanation is one short sentence saying what the table shows. " +
                "Reply as JSON: {\"report\": key, \"department\": string or null, \"location\": string or null, \"from\": date or null, \"to\": date or null, \"status\": string or null, \"explanation\": string}.",
                JsonSerializer.Serialize(new { question, reports = ReportCatalogue.Select(r => new { r.Key, r.About }) }));
            if (ai is not null && ReportCatalogue.Any(r => r.Key == ai.Report))
            {
                usedAi = true;
                report = ai.Report;
                department ??= Clean(ai.Department);
                location ??= Clean(ai.Location);
                status ??= Clean(ai.Status);
                from ??= ParseDate(ai.From);
                to ??= ParseDate(ai.To);
                explanation = Clean(ai.Explanation);
            }
            else
            {
                report = GuessReport(question);
                if (report is null)
                    throw new DomainException("Could not match that to a report. Try: headcount, late, absent, leave, payroll, joiners, leavers, approvals or expiring documents.");
                var (gf, gt) = GuessRange(question, today);
                from ??= gf;
                to ??= gt;
            }
        }
        var info = ReportCatalogue.FirstOrDefault(r => r.Key == report) ?? throw new DomainException("Unknown report.");

        var f = from ?? (report == "expiring_documents" ? today : monthStart);
        var t = to ?? (report == "expiring_documents" ? today.AddDays(60) : today);
        if (t < f) (f, t) = (t, f);
        if (t.DayNumber - f.DayNumber > 800) throw new DomainException("Pick a period of two years or less.");
        var p = new AiReportParams(Clean(department), Clean(location), f, t, Clean(status));

        // Employees in scope (the company filter is applied by the database context).
        var people = db.Employees.AsNoTracking().AsQueryable();
        if (p.Department is { } dn) people = people.Where(e => e.Department != null && e.Department.Name.Contains(dn));
        List<Guid> offices = [];
        if (p.Location is { } ln)
        {
            var regions = await db.OrgLookups.Where(o => o.Kind == LookupKind.Region && o.Name.Contains(ln)).Select(o => o.Id).ToListAsync();
            offices = await db.OfficeLocations.Where(o => o.Name.Contains(ln)).Select(o => o.Id).ToListAsync();
            people = people.Where(e => (e.RegionId != null && regions.Contains(e.RegionId.Value)) || (e.Address != null && e.Address.Contains(ln))
                || db.AttendanceRecords.Any(a => a.EmployeeId == e.Id && a.OfficeLocationId != null && offices.Contains(a.OfficeLocationId.Value)));
        }
        if (p.Status is { } st && Enum.TryParse<EmploymentStatus>(st, true, out var es)) people = people.Where(e => e.Status == es);
        var ids = people.Select(e => e.Id);

        static string D(DateOnly d) => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        static string N(decimal v) => v.ToString("N0", CultureInfo.InvariantCulture);
        List<string> cols;
        List<List<string>> rows;
        switch (report)
        {
            case "headcount":
            {
                var q = people;
                if (p.Status is null) q = q.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
                var g = await q.GroupBy(e => e.Department != null ? e.Department.Name : "(no department)")
                    .Select(x => new { Dept = x.Key, Total = x.Count(), Probation = x.Count(e => e.Status == EmploymentStatus.Probation),
                        Male = x.Count(e => e.Gender == Gender.Male), Female = x.Count(e => e.Gender == Gender.Female) })
                    .ToListAsync();
                cols = ["Department", "People", "On probation", "Male", "Female"];
                rows = g.OrderByDescending(x => x.Total).Select(x => new List<string> { x.Dept, $"{x.Total}", $"{x.Probation}", $"{x.Male}", $"{x.Female}" }).ToList();
                if (rows.Count > 1) rows.Add(["Total", $"{g.Sum(x => x.Total)}", $"{g.Sum(x => x.Probation)}", $"{g.Sum(x => x.Male)}", $"{g.Sum(x => x.Female)}"]);
                break;
            }
            case "late_arrivals":
            case "absences":
            {
                var late = report == "late_arrivals";
                var recs = db.AttendanceRecords.AsNoTracking().Where(a => a.Date >= p.From && a.Date <= p.To && ids.Contains(a.EmployeeId));
                recs = late ? recs.Where(a => a.LateMinutes > 0 || a.Status == AttendanceStatus.Late) : recs.Where(a => a.Status == AttendanceStatus.Absent);
                var g = await recs.GroupBy(a => a.EmployeeId)
                    .Select(x => new { Id = x.Key, Days = x.Count(), Minutes = x.Sum(a => a.LateMinutes), Last = x.Max(a => a.Date) })
                    .OrderByDescending(x => x.Days).Take(MaxReportRows + 1).ToListAsync();
                var names = await NamesAsync(g.Select(x => x.Id));
                cols = late ? ["Code", "Employee", "Department", "Late days", "Late minutes", "Last late on"] : ["Code", "Employee", "Department", "Absent days", "Last absent on"];
                rows = g.Select(x => late
                    ? new List<string> { names[x.Id].Code, names[x.Id].Name, names[x.Id].Dept, $"{x.Days}", $"{x.Minutes}", D(x.Last) }
                    : [names[x.Id].Code, names[x.Id].Name, names[x.Id].Dept, $"{x.Days}", D(x.Last)]).ToList();
                break;
            }
            case "leave_taken":
            {
                var g = await db.LeaveRequests.AsNoTracking()
                    .Where(l => l.Status == LeaveStatus.Approved && l.FromDate <= p.To && l.ToDate >= p.From && ids.Contains(l.EmployeeId))
                    .GroupBy(l => new { l.EmployeeId, Type = l.LeaveType != null ? l.LeaveType.Name : "" })
                    .Select(x => new { x.Key.EmployeeId, x.Key.Type, Days = x.Sum(l => l.Days), Requests = x.Count() })
                    .OrderByDescending(x => x.Days).Take(MaxReportRows + 1).ToListAsync();
                var names = await NamesAsync(g.Select(x => x.EmployeeId));
                cols = ["Code", "Employee", "Department", "Leave type", "Days", "Requests"];
                rows = g.Select(x => new List<string> { names[x.EmployeeId].Code, names[x.EmployeeId].Name, names[x.EmployeeId].Dept, x.Type, $"{x.Days:0.#}", $"{x.Requests}" }).ToList();
                break;
            }
            case "payroll_totals":
            {
                var g = await db.Payslips.AsNoTracking()
                    .Where(s => s.PayrollRun != null && s.PayrollRun.PeriodStart <= p.To && s.PayrollRun.PeriodEnd >= p.From && ids.Contains(s.EmployeeId))
                    .GroupBy(s => new { s.PayrollRun!.Year, s.PayrollRun.Month, Dept = s.Employee != null && s.Employee.Department != null ? s.Employee.Department.Name : "(no department)" })
                    .Select(x => new { x.Key.Year, x.Key.Month, x.Key.Dept, People = x.Count(), Gross = x.Sum(s => s.GrossEarnings), Tax = x.Sum(s => s.IncomeTax),
                        Other = x.Sum(s => s.OtherDeductions + s.EobiEmployee + s.PfEmployee + s.VpsEmployee), Net = x.Sum(s => s.NetPay) })
                    .ToListAsync();
                cols = ["Month", "Department", "People", "Gross", "Income tax", "Other deductions", "Net pay"];
                rows = g.OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Dept)
                    .Select(x => new List<string> { $"{CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(x.Month)} {x.Year}", x.Dept, $"{x.People}",
                        N(x.Gross), N(x.Tax), N(x.Other), N(x.Net) }).ToList();
                if (rows.Count > 1) rows.Add(["Total", "", $"{g.Sum(x => x.People)}", N(g.Sum(x => x.Gross)), N(g.Sum(x => x.Tax)), N(g.Sum(x => x.Other)), N(g.Sum(x => x.Net))]);
                break;
            }
            case "joiners":
            case "leavers":
            {
                var join = report == "joiners";
                var q = join ? people.Where(e => e.JoiningDate >= p.From && e.JoiningDate <= p.To)
                    : people.Where(e => e.LeavingDate != null && e.LeavingDate >= p.From && e.LeavingDate <= p.To);
                var list = await q.OrderBy(e => join ? e.JoiningDate : e.LeavingDate).Take(MaxReportRows + 1)
                    .Select(e => new { e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Dept = e.Department != null ? e.Department.Name : "",
                        Desig = e.Designation != null ? e.Designation.Title : "", e.JoiningDate, e.LeavingDate, e.Status, e.ExitReason })
                    .ToListAsync();
                cols = join ? ["Code", "Employee", "Department", "Designation", "Joined on", "Status"] : ["Code", "Employee", "Department", "Designation", "Left on", "Status", "Reason"];
                rows = list.Select(e => join
                    ? new List<string> { e.EmployeeCode, e.Name, e.Dept, e.Desig, D(e.JoiningDate), $"{e.Status}" }
                    : [e.EmployeeCode, e.Name, e.Dept, e.Desig, e.LeavingDate is { } l ? D(l) : "", $"{e.Status}", e.ExitReason ?? ""]).ToList();
                break;
            }
            case "pending_approvals":
            {
                var list = await db.ApprovalRequests.AsNoTracking().Where(a => a.Status == ApprovalStatus.Pending && ids.Contains(a.EmployeeId))
                    .OrderBy(a => a.CreatedAtUtc).Take(MaxReportRows + 1)
                    .Select(a => new { a.RequestType, a.Summary, a.EmployeeId, a.CreatedAtUtc, a.CurrentStep }).ToListAsync();
                var names = await NamesAsync(list.Select(x => x.EmployeeId));
                var now = clock.UtcNow;
                cols = ["Type", "Employee", "Department", "Summary", "Waiting (days)", "Step"];
                rows = list.Select(x => new List<string> { $"{x.RequestType}", names[x.EmployeeId].Name, names[x.EmployeeId].Dept, x.Summary,
                    $"{(int)(now - x.CreatedAtUtc).TotalDays}", $"{x.CurrentStep + 1}" }).ToList();
                break;
            }
            case "expiring_documents":
            {
                var list = await people.Where(e => e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated)
                    .Where(e => (e.CnicExpiresOn >= p.From && e.CnicExpiresOn <= p.To) || (e.PassportExpiresOn >= p.From && e.PassportExpiresOn <= p.To)
                        || (e.VisaExpiresOn >= p.From && e.VisaExpiresOn <= p.To) || (e.DrivingLicenseExpiresOn >= p.From && e.DrivingLicenseExpiresOn <= p.To)
                        || (e.InsuranceExpiresOn >= p.From && e.InsuranceExpiresOn <= p.To) || (e.ContractEndDate >= p.From && e.ContractEndDate <= p.To))
                    .Take(MaxReportRows + 1)
                    .Select(e => new { e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Dept = e.Department != null ? e.Department.Name : "",
                        e.CnicExpiresOn, e.PassportExpiresOn, e.VisaExpiresOn, e.DrivingLicenseExpiresOn, e.InsuranceExpiresOn, e.ContractEndDate })
                    .ToListAsync();
                bool In(DateOnly? d) => d is { } x && x >= p.From && x <= p.To;
                cols = ["Code", "Employee", "Department", "Document", "Ends on"];
                rows = list.SelectMany(e => new (string Doc, DateOnly? On)[]
                    {
                        ("CNIC", e.CnicExpiresOn), ("Passport", e.PassportExpiresOn), ("Visa", e.VisaExpiresOn),
                        ("Driving licence", e.DrivingLicenseExpiresOn), ("Insurance", e.InsuranceExpiresOn), ("Contract", e.ContractEndDate),
                    }.Where(d => In(d.On)).Select(d => (e, d.Doc, On: d.On!.Value)))
                    .OrderBy(x => x.On).Select(x => new List<string> { x.e.EmployeeCode, x.e.Name, x.e.Dept, x.Doc, D(x.On) }).ToList();
                break;
            }
            default: throw new DomainException("Unknown report.");
        }

        var truncated = rows.Count > MaxReportRows;
        if (truncated) rows = rows.Take(MaxReportRows).ToList();
        explanation ??= info.About;
        if (p.Location is not null && rows.Count == 0)
            explanation += $" No one matched the location \"{p.Location}\" (locations are matched by region, office or address).";
        return new AiReportResult(info.Key, info.Title, explanation, p, cols, rows, usedAi, truncated);
    }

    async Task<Dictionary<Guid, (string Code, string Name, string Dept)>> NamesAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        var rows = await db.Employees.AsNoTracking().Where(e => list.Contains(e.Id))
            .Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Dept = e.Department != null ? e.Department.Name : "" })
            .ToListAsync();
        var map = rows.ToDictionary(r => r.Id, r => (r.EmployeeCode, r.Name, r.Dept));
        foreach (var id in list) map.TryAdd(id, ("", "(unknown)", ""));
        return map;
    }

    static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) || s.Trim().Equals("null", StringComparison.OrdinalIgnoreCase) ? null
        : s.Trim().Length > 80 ? s.Trim()[..80] : s.Trim();

    static DateOnly? ParseDate(string? s) =>
        DateOnly.TryParseExact(s?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Keyword pick used when the AI is off or gives no usable answer (English and Roman Urdu).</summary>
    public static string? GuessReport(string question)
    {
        var q = question.ToLowerInvariant();
        bool Has(params string[] w) => w.Any(q.Contains);
        if (Has("expir", "khatam", "renew", "cnic", "passport", "visa", "licen")) return "expiring_documents";
        if (Has("late", "dair", "der se", "der say")) return "late_arrivals";
        if (Has("absent", "ghair hazir", "ghair-hazir", "nahi aaye", "nahi aye", "gair hazir")) return "absences";
        if (Has("pending", "approval", "waiting", "manzoori")) return "pending_approvals";
        if (Has("payroll", "salary", "salaries", "tankhwah", "tankhah", "net pay", "gross")) return "payroll_totals";
        if (Has("join", "hire", "new staff", "naye log", "naye")) return "joiners";
        if (Has("left", "leaver", "resign", "terminat", "chor", "chhor", "exit")) return "leavers";
        if (Has("leave", "chutti", "chhutti")) return "leave_taken";
        if (Has("headcount", "kitne log", "how many", "staff", "employees", "strength")) return "headcount";
        return null;
    }

    static (DateOnly? From, DateOnly? To) GuessRange(string question, DateOnly today)
    {
        var q = question.ToLowerInvariant();
        var month = new DateOnly(today.Year, today.Month, 1);
        if (q.Contains("last month") || q.Contains("pichle mahine") || q.Contains("pichhle mahine")) return (month.AddMonths(-1), month.AddDays(-1));
        if (q.Contains("this year") || q.Contains("is saal")) return (new DateOnly(today.Year, 1, 1), today);
        if (q.Contains("today") || q.Contains("aaj")) return (today, today);
        if (q.Contains("this week") || q.Contains("is hafte")) return (today.AddDays(-((int)today.DayOfWeek + 6) % 7), today);
        return (null, null);
    }
}
