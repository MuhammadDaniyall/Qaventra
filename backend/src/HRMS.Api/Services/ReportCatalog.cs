using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

/// <summary>Filters a report understands: "period" (from–to), "date", "month", "year", "department", "employee", "includeLeft".</summary>
public record ReportDefinition(string Key, string Category, string Name, string Description, string[] Filters, bool HrOnly);

public record ReportRequest(DateOnly? From, DateOnly? To, DateOnly? Date, int? Year, int? Month, List<Guid>? DepartmentIds, Guid? EmployeeId, bool IncludeLeft, List<Guid>? GroupIds = null);

/// <summary>A report's result. Numeric columns are right-aligned and totalled when <see cref="Totals"/> is set.</summary>
public record ReportResult(string Title, string? Subtitle, List<string> Headers, List<bool> Numeric, List<List<string>> Rows, List<string>? Totals);

/// <summary>
/// The catalog of ready-made reports: one place to pick a report, filter it and see, export or print it.
/// HR sees every report over everyone; managers see team reports (no pay) over their direct reports.
/// </summary>
public class ReportCatalog(HrmsDbContext db, ICurrentUser me, TenantClock clock, ScheduleService schedules, HRMS.Api.Controllers.PenaltyService penalties,
    HRMS.Api.Controllers.FundService funds)
{
    public static readonly List<ReportDefinition> All =
    [
        new("employees.list", "Employees", "Employee list", "Everyone with department, designation, manager, joining date and contact", ["department", "includeLeft"], false),
        new("employees.headcount", "Employees", "Headcount", "Employees by department, men and women, on probation", ["department"], false),
        new("employees.movement", "Employees", "Joiners and leavers", "Who joined and who left in a period", ["period", "department"], false),
        new("employees.birthdays", "Employees", "Birthdays and anniversaries", "Birthdays and work anniversaries in a month", ["month", "department"], false),
        new("employees.documents", "Employees", "Documents expiring", "CNICs, passports, visas, licences, insurance, contracts and uploaded documents expired or expiring by a date", ["date", "department"], true),
        new("employees.file", "Employees", "Employee file", "Division, sub-department, grade, cost center, region, group, confirmation and contract end", ["department", "includeLeft"], true),
        new("employees.dependants", "Employees", "Dependants and nominees", "Family members on file, nominees and insurance cover", ["department"], true),
        new("employees.qualifications", "Employees", "Qualifications", "Degrees and certificates on file, highest first", ["department"], false),
        new("employees.experience", "Employees", "Previous experience", "Past jobs on file with years of experience", ["department"], true),
        new("attendance.daily", "Attendance", "Daily attendance", "In, out, late, absent and on leave for one day", ["date", "department"], false),
        new("attendance.register", "Attendance", "Attendance register", "Days present, late, half, absent and on leave per person for a month", ["month", "department", "employee"], false),
        new("attendance.late", "Attendance", "Late arrivals", "Every late check-in in a period", ["period", "department", "employee"], false),
        new("attendance.early", "Attendance", "Early leavers", "Every check-out before the shift ended, beyond the grace", ["period", "department", "employee"], false),
        new("attendance.penalties", "Attendance", "Attendance penalties", "Penalty rules applied for a month: days of pay and leave per person", ["month", "department", "employee"], true),
        new("projects.hours", "Projects", "Project hours", "Hours logged on submitted and approved timesheets, by person and project", ["period", "department", "employee"], false),
        new("leave.balances", "Leave", "Leave balances", "Entitled, carried, used and remaining by leave type", ["year", "department", "employee"], false),
        new("leave.taken", "Leave", "Leave taken", "Approved leave in a period", ["period", "department", "employee"], false),
        new("payroll.sheet", "Payroll", "Salary sheet", "Gross, deductions and net pay for a month", ["month", "department"], true),
        new("payroll.bank", "Payroll", "Bank advice", "Net pay to transfer, with bank and account", ["month", "department"], true),
        new("payroll.tax", "Payroll", "Income tax deducted", "Taxable income and tax withheld for a month", ["month", "department"], true),
        new("payroll.eobi", "Payroll", "EOBI contributions", "Employee and employer EOBI for a month", ["month", "department"], true),
        new("payroll.socialsecurity", "Payroll", "Social security (PESSI/SESSI)", "Employer social security contribution for a month", ["month", "department"], true),
        new("payroll.cash", "Payroll", "Cash and cheque payments", "Net pay for people paid in cash or by cheque, with a signature column", ["month", "department"], true),
        new("payroll.costcenters", "Payroll", "Salary cost by cost center", "Gross pay and employer costs split across cost centers", ["month"], true),
        new("payroll.taxcertificate", "Payroll", "Tax certificate", "Taxable income and tax deducted month by month in a tax year (July to June); choose the year it ends", ["year", "employee"], true),
        new("payroll.vps", "Payroll", "Voluntary pension (VPS)", "Member and company contributions for a month, with the tax credit, to send to the pension funds", ["month", "department"], true),
        new("payroll.pf", "Payroll", "Provident fund balances", "Employee and employer shares, profit, withdrawals and vested balance", ["department", "includeLeft"], true),
        new("payroll.gratuity", "Payroll", "Gratuity liability", "Years of service and gratuity due today", ["department"], true),
        new("payroll.commission", "Payroll", "Commission and bonuses", "Approved commission, bonus and allowance requests in a period", ["period", "department", "employee"], true),
        new("expenses.claims", "Expenses", "Expense claims", "Claims submitted in a period with status and payment", ["period", "department", "employee"], true),
        new("approvals.pending", "Approvals", "Pending approvals", "Everything waiting, who it waits for and how long", ["department"], false),
        new("training.hours", "Training", "Training by person", "Sessions attended, hours and scores in a period", ["period", "department", "employee"], false),
        new("travel.trips", "Travel", "Trips", "Business trips in a period with cost and settlement", ["period", "department", "employee"], true),
    ];

    public IEnumerable<ReportDefinition> Available() => All.Where(r => me.IsHr() || !r.HrOnly);

    public async Task<ReportResult> RunAsync(string key, ReportRequest f)
    {
        var def = All.FirstOrDefault(r => r.Key == key) ?? throw new NotFoundException("Report");
        if (def.HrOnly && !me.IsHr())
            throw new ForbiddenException("Only HR can run this report.");
        if (f.GroupIds is { Count: > 0 } gids)
            groups = (await db.OrgLookups.Where(l => gids.Contains(l.Id)).Select(l => new { l.Id, l.Kind }).ToListAsync()).Select(x => (x.Id, x.Kind)).ToList();
        var today = await clock.TodayAsync();
        var from = f.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = f.To ?? today;
        if (to < from)
            throw new DomainException("The period ends before it starts.");
        if (to.DayNumber - from.DayNumber > 400)
            throw new DomainException("Choose a period of at most a year.");
        var month = new DateOnly(f.Year ?? today.Year, f.Month ?? today.Month, 1);
        var monthEnd = month.AddMonths(1).AddDays(-1);
        var people = People(f);
        var range = $"{from:d MMM yyyy} – {to:d MMM yyyy}";

        switch (key)
        {
            case "employees.list":
            {
                var rows = await people.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Select(e => new
                {
                    e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "", Designation = e.Designation != null ? e.Designation.Title : "",
                    Manager = e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : "", e.JoiningDate, e.LeavingDate, e.Status, e.Phone, e.Email, e.Cnic,
                }).ToListAsync();
                return T(def, $"{rows.Count} people", ["Code", "Name", "Department", "Designation", "Manager", "Joined", "Status", "Phone", "Email", "CNIC"], [],
                    rows.Select(r => L(r.EmployeeCode, r.Name, r.Department, r.Designation, r.Manager, D(r.JoiningDate), Status(r.Status, r.LeavingDate), r.Phone, r.Email, r.Cnic)));
            }
            case "employees.headcount":
            {
                var rows = await people.GroupBy(e => e.Department != null ? e.Department.Name : "No department")
                    .Select(g => new
                    {
                        g.Key, Total = g.Count(), Men = g.Count(e => e.Gender == Gender.Male), Women = g.Count(e => e.Gender == Gender.Female),
                        Probation = g.Count(e => e.Status == EmploymentStatus.Probation),
                    }).OrderBy(x => x.Key).ToListAsync();
                return T(def, $"As of {today:d MMM yyyy}", ["Department", "Employees", "Men", "Women", "On probation"], [1, 2, 3, 4],
                    rows.Select(r => L(r.Key, N(r.Total), N(r.Men), N(r.Women), N(r.Probation))), totals: true);
            }
            case "employees.movement":
            {
                var all = People(f with { IncludeLeft = true });
                var joined = await all.Where(e => e.JoiningDate >= from && e.JoiningDate <= to).Select(e => new { E = "Joined", e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "", Designation = e.Designation != null ? e.Designation.Title : "", Date = e.JoiningDate }).ToListAsync();
                var left = await all.Where(e => e.LeavingDate >= from && e.LeavingDate <= to).Select(e => new { E = "Left", e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "", Designation = e.Designation != null ? e.Designation.Title : "", Date = e.LeavingDate!.Value }).ToListAsync();
                return T(def, $"{range}: {joined.Count} joined, {left.Count} left", ["", "Code", "Name", "Department", "Designation", "Date"], [],
                    joined.Concat(left).OrderBy(x => x.Date).Select(r => L(r.E, r.EmployeeCode, r.Name, r.Department, r.Designation, D(r.Date))));
            }
            case "employees.birthdays":
            {
                var m = month.Month;
                var rows = await people.Where(e => (e.DateOfBirth != null && e.DateOfBirth.Value.Month == m) || (e.JoiningDate.Month == m && e.JoiningDate.Year < month.Year))
                    .Select(e => new { Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "", e.DateOfBirth, e.JoiningDate }).ToListAsync();
                var list = new List<(int Day, List<string> Row)>();
                foreach (var r in rows)
                {
                    if (r.DateOfBirth is { } b && b.Month == m)
                        list.Add((b.Day, L($"{b.Day} {month:MMM}", r.Name, r.Department, "Birthday", "")));
                    if (r.JoiningDate.Month == m && r.JoiningDate.Year < month.Year)
                        list.Add((r.JoiningDate.Day, L($"{r.JoiningDate.Day} {month:MMM}", r.Name, r.Department, "Work anniversary", $"{month.Year - r.JoiningDate.Year} years")));
                }
                return T(def, month.ToString("MMMM yyyy"), ["Day", "Name", "Department", "Occasion", ""], [], list.OrderBy(x => x.Day).Select(x => x.Row));
            }
            case "employees.documents":
            {
                var by = f.Date ?? today.AddDays(30);
                var staff = await people.Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName }).ToListAsync();
                var docs = await ExpiriesAsync(db, staff.Select(s => s.Id).ToList(), null, by);
                return T(def, $"Expired or expiring by {by:d MMM yyyy}", ["Code", "Name", "Document", "Expires", ""], [],
                    docs.Select(d =>
                    {
                        var s = staff.First(x => x.Id == d.EmployeeId);
                        return L(s.EmployeeCode, s.Name, d.Document, D(d.ExpiresOn), d.ExpiresOn < today ? "Expired" : "Expiring");
                    }));
            }
            case "attendance.early":
            {
                var staff = await people.Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "" }).ToListAsync();
                var ids = staff.Select(s => s.Id).ToList();
                var recs = await db.AttendanceRecords.Where(a => a.EarlyLeaveMinutes > 0 && a.Date >= from && a.Date <= to && ids.Contains(a.EmployeeId))
                    .OrderBy(a => a.Date).ToListAsync();
                var rows = new List<List<string>>();
                foreach (var a in recs)
                {
                    var s = staff.First(x => x.Id == a.EmployeeId);
                    rows.Add(L(D(a.Date), s.EmployeeCode, s.Name, s.Department, a.CheckOutUtc is { } o ? (await clock.ToLocalAsync(o)).ToString("HH:mm") : "", N(a.EarlyLeaveMinutes)));
                }
                return T(def, $"{range}: {recs.Count} early leaves", ["Date", "Code", "Name", "Department", "Out", "Minutes early"], [5], rows, totals: true);
            }
            case "attendance.penalties":
            {
                var staff = await people.Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName }).ToListAsync();
                var lines = await penalties.ComputeAsync(staff.Select(s => s.Id).ToList(), month.Year, month.Month);
                var closed = await db.AttendancePeriods.AnyAsync(p => p.Year == month.Year && p.Month == month.Month);
                var charges = closed ? (await db.PenaltyCharges.Where(c => c.Year == month.Year && c.Month == month.Month).ToListAsync()).ToLookup(c => c.EmployeeId) : null;
                var rows = new List<List<string>>();
                foreach (var s in staff.OrderBy(s => s.Name))
                {
                    if (charges is not null)
                        foreach (var c in charges[s.Id])
                            rows.Add(L(s.EmployeeCode, s.Name, c.RuleName, N(c.Occurrences), c.PayDays.ToString("0.##"), c.LeaveDays.ToString("0.##")));
                    else
                        foreach (var l in lines[s.Id])
                            rows.Add(L(s.EmployeeCode, s.Name, l.RuleName, N(l.Occurrences), l.Action == PenaltyAction.DeductPay ? l.Days.ToString("0.##") : "0",
                                l.Action == PenaltyAction.DeductLeave ? l.Days.ToString("0.##") : "0"));
                }
                return T(def, month.ToString("MMMM yyyy") + (closed ? " (closed)" : " (so far)"), ["Code", "Name", "Rule", "Times", "Pay days", "Leave days"], [3, 4, 5], rows, totals: true);
            }
            case "projects.hours":
            {
                var staff = await people.Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName }).ToListAsync();
                var ids = staff.Select(s => s.Id).ToList();
                var logged = await db.TimesheetLines.Where(l => l.Date >= from && l.Date <= to)
                    .Join(db.Timesheets.Where(t => t.Status != TimesheetStatus.Draft && ids.Contains(t.EmployeeId)), l => l.TimesheetId, t => t.Id, (l, t) => new { t.EmployeeId, l.ProjectId, l.Minutes })
                    .ToListAsync();
                var projectNames = await db.Projects.ToDictionaryAsync(p => p.Id, p => p.Name);
                var rates = await db.ProjectMembers.ToDictionaryAsync(m => (m.ProjectId, m.EmployeeId), m => m.HourlyRate ?? 0);
                var hr = me.IsHr();
                var rows = logged.GroupBy(x => (x.EmployeeId, x.ProjectId)).Select(g =>
                {
                    var s = staff.First(x => x.Id == g.Key.EmployeeId);
                    var hours = g.Sum(x => x.Minutes) / 60m;
                    var row = L(s.EmployeeCode, s.Name, projectNames.GetValueOrDefault(g.Key.ProjectId) ?? "", hours.ToString("0.##"));
                    if (hr) row.Add((hours * rates.GetValueOrDefault((g.Key.ProjectId, g.Key.EmployeeId))).ToString("0"));
                    return row;
                }).OrderBy(r => r[1]).ThenBy(r => r[2]).ToList();
                return T(def, range, hr ? ["Code", "Name", "Project", "Hours", "Cost"] : ["Code", "Name", "Project", "Hours"], hr ? [3, 4] : [3], rows, totals: true);
            }
            case "employees.file":
            {
                var names = await db.OrgLookups.ToDictionaryAsync(l => l.Id, l => l.Name);
                string Nm(Guid? id) => id is { } x ? names.GetValueOrDefault(x) ?? "" : "";
                var rows = await people.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Select(e => new
                {
                    e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "",
                    e.DivisionId, e.SubDepartmentId, e.GradeId, e.CostCenterId, e.RegionId, e.EmployeeGroupId, e.ConfirmationDate, e.ContractEndDate, e.Status, e.LeavingDate,
                }).ToListAsync();
                return T(def, $"{rows.Count} people", ["Code", "Name", "Department", "Sub-department", "Division", "Grade", "Cost center", "Region", "Group", "Confirmed", "Contract ends", "Status"], [],
                    rows.Select(r => L(r.EmployeeCode, r.Name, r.Department, Nm(r.SubDepartmentId), Nm(r.DivisionId), Nm(r.GradeId), Nm(r.CostCenterId), Nm(r.RegionId),
                        Nm(r.EmployeeGroupId), r.ConfirmationDate is { } c ? D(c) : "", r.ContractEndDate is { } ce ? D(ce) : "", Status(r.Status, r.LeavingDate))));
            }
            case "employees.dependants":
            {
                var staff = await people.Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName }).ToListAsync();
                var ids = staff.Select(s => s.Id).ToList();
                var deps = await db.Dependants.Where(d => ids.Contains(d.EmployeeId)).OrderBy(d => d.Order).ToListAsync();
                return T(def, $"{deps.Count} family members of {deps.Select(d => d.EmployeeId).Distinct().Count()} people", ["Code", "Employee", "Name", "Relation", "CNIC", "Born", "Nominee", "Insured"], [],
                    deps.OrderBy(d => staff.First(s => s.Id == d.EmployeeId).Name).Select(d =>
                    {
                        var s = staff.First(x => x.Id == d.EmployeeId);
                        return L(s.EmployeeCode, s.Name, d.Name, d.Relation, d.Cnic, d.DateOfBirth is { } b ? D(b) : "",
                            d.IsNominee ? (d.NomineeShare is { } sh ? $"{sh:0.##}%" : "Yes") : "", d.CoveredByInsurance ? "Yes" : "");
                    }));
            }
            case "employees.qualifications":
            {
                var staff = await people.Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName }).ToListAsync();
                var ids = staff.Select(s => s.Id).ToList();
                var quals = await db.Qualifications.Where(q => ids.Contains(q.EmployeeId)).ToListAsync();
                return T(def, $"{quals.Count} qualifications", ["Code", "Employee", "Degree", "Field", "Institute", "Grade", "Year", "Highest"], [],
                    quals.OrderBy(q => staff.First(s => s.Id == q.EmployeeId).Name).ThenByDescending(q => q.IsHighest).ThenByDescending(q => q.ToYear).Select(q =>
                    {
                        var s = staff.First(x => x.Id == q.EmployeeId);
                        return L(s.EmployeeCode, s.Name, q.Degree, q.Field, q.Institute, q.Grade, q.ToYear?.ToString(), q.IsHighest ? "Yes" : "");
                    }));
            }
            case "employees.experience":
            {
                var staff = await people.Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName }).ToListAsync();
                var ids = staff.Select(s => s.Id).ToList();
                var jobs = await db.WorkExperiences.Where(j => ids.Contains(j.EmployeeId)).ToListAsync();
                static decimal Years(DateOnly? a, DateOnly? b) => a is { } x && b is { } y && y >= x ? Math.Round((y.DayNumber - x.DayNumber) / 365.25m, 1) : 0;
                return T(def, $"{jobs.Count} past jobs", ["Code", "Employee", "Organization", "Designation", "From", "To", "Years", "Last salary", "Reason for leaving"], [6, 7],
                    jobs.OrderBy(j => staff.First(s => s.Id == j.EmployeeId).Name).ThenByDescending(j => j.FromDate).Select(j =>
                    {
                        var s = staff.First(x => x.Id == j.EmployeeId);
                        return L(s.EmployeeCode, s.Name, j.Organization, j.Designation, j.FromDate is { } a ? D(a) : "", j.ToDate is { } b ? D(b) : "",
                            Years(j.FromDate, j.ToDate).ToString("0.#"), j.LastSalary is { } ls ? ls.ToString("N0") : "", j.ReasonForLeaving);
                    }));
            }
            case "attendance.daily":
            {
                var day = f.Date ?? today;
                var staff = await people.OrderBy(e => e.FirstName).Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department != null ? e.Department.Name : "" }).ToListAsync();
                var ids = staff.Select(s => s.Id).ToList();
                var plan = await schedules.ForEmployeesAsync(ids, day, day);
                var recs = await db.AttendanceRecords.Where(a => a.Date == day && ids.Contains(a.EmployeeId)).ToListAsync();
                var leave = await db.LeaveRequests.Where(l => l.Status == LeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day && ids.Contains(l.EmployeeId))
                    .Select(l => new { l.EmployeeId, Type = l.LeaveType!.Name }).ToListAsync();
                var rows = new List<List<string>>();
                foreach (var s in staff)
                {
                    var rec = recs.FirstOrDefault(x => x.EmployeeId == s.Id);
                    var lv = leave.FirstOrDefault(x => x.EmployeeId == s.Id);
                    var working = plan.TryGetValue(s.Id, out var sc) && sc.IsWorkingDay(day);
                    var status = rec is not null ? ScheduledService.Humanize(rec.Status.ToString()) : lv is not null ? $"On leave ({lv.Type})" : working ? "Absent" : "Off";
                    rows.Add(L(s.EmployeeCode, s.Name, s.Department, status, rec is null ? "" : Time(await clock.ToLocalAsync(rec.CheckInUtc)),
                        rec?.CheckOutUtc is { } co ? Time(await clock.ToLocalAsync(co)) : "", rec is null ? "" : Hours(rec.WorkedMinutes)));
                }
                return T(def, $"{day:dddd d MMM yyyy}: {recs.Count} in, {rows.Count(r => r[3] == "Absent")} absent, {leave.Count} on leave",
                    ["Code", "Name", "Department", "Status", "In", "Out", "Hours"], [6], rows);
            }
            case "attendance.register":
            {
                var end = monthEnd > today ? today : monthEnd;
                var staff = await people.OrderBy(e => e.FirstName).Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, e.JoiningDate, e.LeavingDate }).ToListAsync();
                var ids = staff.Select(s => s.Id).ToList();
                var plan = await schedules.ForEmployeesAsync(ids, month, monthEnd);
                var recs = await db.AttendanceRecords.Where(a => a.Date >= month && a.Date <= monthEnd && ids.Contains(a.EmployeeId))
                    .Select(a => new { a.EmployeeId, a.Date, a.Status, a.WorkedMinutes }).ToListAsync();
                var leaves = await db.LeaveRequests.Where(l => l.Status == LeaveStatus.Approved && l.FromDate <= monthEnd && l.ToDate >= month && ids.Contains(l.EmployeeId))
                    .Select(l => new { l.EmployeeId, l.FromDate, l.ToDate }).ToListAsync();
                var rows = staff.Select(s =>
                {
                    var mine = recs.Where(r => r.EmployeeId == s.Id).ToList();
                    var sc = plan.GetValueOrDefault(s.Id);
                    int working = 0, absent = 0, onLeave = 0;
                    for (var d = month; d <= end; d = d.AddDays(1))
                    {
                        if (d < s.JoiningDate || (s.LeavingDate is { } lv && d > lv) || sc is null || !sc.IsWorkingDay(d))
                            continue;
                        working++;
                        if (leaves.Any(l => l.EmployeeId == s.Id && l.FromDate <= d && l.ToDate >= d))
                            onLeave++;
                        else if (mine.All(r => r.Date != d))
                            absent++;
                    }
                    return L(s.EmployeeCode, s.Name, N(working), N(mine.Count(r => r.Status == AttendanceStatus.Present)), N(mine.Count(r => r.Status == AttendanceStatus.Late)),
                        N(mine.Count(r => r.Status == AttendanceStatus.HalfDay)), N(absent), N(onLeave), Hours(mine.Sum(r => r.WorkedMinutes)));
                }).ToList();
                return T(def, $"{month:MMMM yyyy}{(end < monthEnd ? $" (to {end:d MMM})" : "")}", ["Code", "Name", "Working days", "Present", "Late", "Half day", "Absent", "On leave", "Hours"],
                    [2, 3, 4, 5, 6, 7, 8], rows, totals: true);
            }
            case "attendance.late":
            {
                var ids = people.Select(e => e.Id);
                var recs = await db.AttendanceRecords.Where(a => a.Status == AttendanceStatus.Late && a.Date >= from && a.Date <= to && ids.Contains(a.EmployeeId))
                    .OrderBy(a => a.Date).Select(a => new { a.Date, a.CheckInUtc, a.Employee!.EmployeeCode, Name = a.Employee.FirstName + " " + a.Employee.LastName, Department = a.Employee.Department != null ? a.Employee.Department.Name : "", Shift = a.Employee.Shift != null ? a.Employee.Shift.StartTime : (TimeOnly?)null })
                    .ToListAsync();
                var rows = new List<List<string>>();
                foreach (var r in recs)
                {
                    var inAt = await clock.ToLocalAsync(r.CheckInUtc);
                    var late = r.Shift is { } st ? (int)(TimeOnly.FromDateTime(inAt) - st).TotalMinutes : (int?)null;
                    rows.Add(L(D(r.Date), r.EmployeeCode, r.Name, r.Department, Time(inAt), late is > 0 ? $"{late} min" : ""));
                }
                return T(def, $"{range}: {rows.Count} late arrivals", ["Date", "Code", "Name", "Department", "In", "Late by"], [], rows);
            }
            case "leave.balances":
            {
                var year = f.Year ?? today.Year;
                var ids = people.Select(e => e.Id);
                var rows = await db.LeaveBalances.Where(b => b.Year == year && ids.Contains(b.EmployeeId))
                    .Select(b => new
                    {
                        Code = db.Employees.Where(e => e.Id == b.EmployeeId).Select(e => e.EmployeeCode).First(),
                        Name = db.Employees.Where(e => e.Id == b.EmployeeId).Select(e => e.FirstName + " " + e.LastName).First(),
                        Type = b.LeaveType!.Name, b.Entitled, b.CarriedForward, b.Adjustment, b.Credited, b.Used,
                    }).ToListAsync();
                return T(def, $"Leave year {year}", ["Code", "Name", "Leave", "Entitled", "Carried", "Adjusted", "Comp-off", "Used", "Remaining"], [3, 4, 5, 6, 7, 8],
                    rows.OrderBy(r => r.Name).ThenBy(r => r.Type).Select(r => L(r.Code, r.Name, r.Type, Num(r.Entitled), Num(r.CarriedForward), Num(r.Adjustment), Num(r.Credited), Num(r.Used),
                        Num(r.Entitled + r.CarriedForward + r.Adjustment + r.Credited - r.Used))));
            }
            case "leave.taken":
            {
                var ids = people.Select(e => e.Id);
                var rows = await db.LeaveRequests.Where(l => l.Status == LeaveStatus.Approved && l.FromDate <= to && l.ToDate >= from && ids.Contains(l.EmployeeId))
                    .OrderBy(l => l.FromDate).Select(l => new { l.Employee!.EmployeeCode, Name = l.Employee.FirstName + " " + l.Employee.LastName, Type = l.LeaveType!.Name, l.FromDate, l.ToDate, l.Days, l.LeaveType.IsPaid })
                    .ToListAsync();
                return T(def, range, ["Code", "Name", "Leave", "From", "To", "Days", "Paid"], [5],
                    rows.Select(r => L(r.EmployeeCode, r.Name, r.Type, D(r.FromDate), D(r.ToDate), Num(r.Days), r.IsPaid ? "Yes" : "No")), totals: true);
            }
            case "payroll.costcenters":
            {
                var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Year == month.Year && r.Month == month.Month);
                if (run is null)
                    return T(def, $"{month:MMMM yyyy}: payroll not processed", ["Note"], [], [L("Process this month's payroll first.")]);
                var slips = await db.Payslips.Where(p => p.PayrollRunId == run.Id)
                    .Select(p => new { p.EmployeeId, p.Employee!.CostCenterId, p.GrossEarnings, Employer = p.EobiEmployer + p.PfEmployer + p.SocialSecurityEmployer, p.NetPay }).ToListAsync();
                var splits = (await db.SalarySplits.ToListAsync()).ToLookup(s => s.EmployeeId);
                var names = await db.OrgLookups.Where(l => l.Kind == LookupKind.CostCenter).ToDictionaryAsync(l => l.Id, l => l.Name);
                var totals = new Dictionary<string, (decimal Gross, decimal Employer, decimal Net, decimal People)>();
                foreach (var s in slips)
                {
                    var parts = splits[s.EmployeeId].Any()
                        ? splits[s.EmployeeId].Select(x => (Name: names.GetValueOrDefault(x.CostCenterId) ?? "?", Share: x.Percent / 100m)).ToList()
                        : [(Name: s.CostCenterId is { } c ? names.GetValueOrDefault(c) ?? "?" : "No cost center", Share: 1m)];
                    foreach (var (name, share) in parts)
                    {
                        var t = totals.GetValueOrDefault(name);
                        totals[name] = (t.Gross + s.GrossEarnings * share, t.Employer + s.Employer * share, t.Net + s.NetPay * share, t.People + share);
                    }
                }
                return T(def, month.ToString("MMMM yyyy"), ["Cost center", "People", "Gross", "Employer costs", "Total cost", "Net pay"], [1, 2, 3, 4, 5],
                    totals.OrderBy(x => x.Key).Select(x => L(x.Key, x.Value.People.ToString("0.##"), M(x.Value.Gross), M(x.Value.Employer), M(x.Value.Gross + x.Value.Employer), M(x.Value.Net))), totals: true);
            }
            case "payroll.taxcertificate":
            {
                // Tax year ending in June of the chosen year.
                var endYear = f.Year ?? (today.Month >= 7 ? today.Year + 1 : today.Year);
                var start = (endYear - 1) * 12 + 7;
                var finish = endYear * 12 + 6;
                var ids = People(f with { IncludeLeft = true }).Select(e => e.Id);
                var rows = await db.Payslips.Where(p => ids.Contains(p.EmployeeId) && p.PayrollRun!.Status == PayrollRunStatus.Finalized
                                                        && p.PayrollRun.Year * 12 + p.PayrollRun.Month >= start && p.PayrollRun.Year * 12 + p.PayrollRun.Month <= finish)
                    .Select(p => new { p.Employee!.EmployeeCode, Name = p.Employee.FirstName + " " + p.Employee.LastName, p.Employee.Cnic, p.Employee.Ntn, p.PayrollRun!.Year, p.PayrollRun.Month, p.GrossEarnings, p.TaxableIncome, p.IncomeTax })
                    .ToListAsync();
                return T(def, $"Tax year {endYear - 1}-{endYear % 100:00} (July {endYear - 1} to June {endYear})", ["Code", "Name", "CNIC", "NTN", "Month", "Gross", "Taxable", "Tax deducted"], [5, 6, 7],
                    rows.OrderBy(r => r.Name).ThenBy(r => r.Year).ThenBy(r => r.Month)
                        .Select(r => L(r.EmployeeCode, r.Name, r.Cnic ?? "", r.Ntn ?? "", new DateOnly(r.Year, r.Month, 1).ToString("MMM yyyy"), M(r.GrossEarnings), M(r.TaxableIncome), M(r.IncomeTax))), totals: true);
            }
            case "payroll.pf":
            case "payroll.gratuity":
            {
                var ids = await people.Select(e => e.Id).ToListAsync();
                var list = await funds.SummariesAsync(ids, today);
                return key == "payroll.pf"
                    ? T(def, $"As of {today:d MMM yyyy}", ["Code", "Name", "Employee share", "Employer share", "Profit", "Withdrawn", "Balance", "Vested"], [2, 3, 4, 5, 6, 7],
                        list.Where(x => x.PfMember || x.PfBalance != 0).Select(x => L(x.Code, x.Name, M(x.PfEmployeeShare), M(x.PfEmployerShare), M(x.PfProfit), M(x.PfWithdrawn), M(x.PfBalance), M(x.PfVested))), totals: true)
                    : T(def, $"As of {today:d MMM yyyy}", ["Code", "Name", "Years of service", "Accrued", "Withdrawn", "Due"], [2, 3, 4, 5],
                        list.Select(x => L(x.Code, x.Name, x.ServiceYears.ToString("0.##"), M(x.GratuityAccrued), M(x.GratuityWithdrawn), M(x.GratuityBalance))), totals: true);
            }
            case "payroll.commission":
            {
                var ids = People(f with { IncludeLeft = true }).Select(e => e.Id);
                var rows = await db.PayRequests.Where(r => ids.Contains(r.EmployeeId) && r.Status == RequestStatus.Approved
                                                         && (r.Kind == PayRequestKind.Commission || r.Kind == PayRequestKind.Bonus || r.Kind == PayRequestKind.Allowance)
                                                         && r.Year * 12 + r.Month >= from.Year * 12 + from.Month && r.Year * 12 + r.Month <= to.Year * 12 + to.Month)
                    .OrderBy(r => r.Year).ThenBy(r => r.Month)
                    .Select(r => new { r.Employee!.EmployeeCode, Name = r.Employee.FirstName + " " + r.Employee.LastName, r.Kind, r.Title, r.Year, r.Month, r.Amount, r.IsTaxable, Paid = r.PaidAtUtc != null })
                    .ToListAsync();
                return T(def, range, ["Code", "Name", "Kind", "Title", "Month", "Amount", "Taxable", "Paid"], [5],
                    rows.Select(r => L(r.EmployeeCode, r.Name, r.Kind.ToString(), r.Title, new DateOnly(r.Year, r.Month, 1).ToString("MMM yyyy"), M(r.Amount), r.IsTaxable ? "Yes" : "No", r.Paid ? "Yes" : "")), totals: true);
            }
            case "payroll.sheet":
            case "payroll.bank":
            case "payroll.tax":
            case "payroll.eobi":
            case "payroll.cash":
            case "payroll.socialsecurity":
            case "payroll.vps":
            {
                var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Year == month.Year && r.Month == month.Month);
                if (run is null)
                    return T(def, $"{month:MMMM yyyy}: payroll not processed", ["Note"], [], [L("Process this month's payroll first.")]);
                var ids = People(f with { IncludeLeft = true }).Select(e => e.Id);
                var slips = await db.Payslips.Where(p => p.PayrollRunId == run.Id && ids.Contains(p.EmployeeId)).OrderBy(p => p.Employee!.FirstName)
                    .Select(p => new
                    {
                        p.EmployeeId, p.Employee!.EmployeeCode, Name = p.Employee.FirstName + " " + p.Employee.LastName, Department = p.Employee.Department != null ? p.Employee.Department.Name : "",
                        p.Employee.Cnic, p.Employee.BankName, p.Employee.BankAccount, p.PaidDays, p.GrossEarnings, p.TaxableIncome, p.IncomeTax, p.EobiEmployee, p.EobiEmployer, p.OtherDeductions, p.NetPay,
                        p.Employee.PaymentMethod, p.Employee.SocialSecurityNo, p.SocialSecurityEmployer,
                        p.Employee.VpsProvider, p.Employee.VpsAccountNo, p.VpsEmployee, p.VpsEmployer, p.VpsTaxCredit,
                    }).ToListAsync();
                var sub = $"{month:MMMM yyyy} · {(run.Status == PayrollRunStatus.Finalized ? "finalized" : "draft")}";
                // Held salaries are not transferred until released.
                var held = await db.SalaryHolds.Where(h => h.Year == month.Year && h.Month == month.Month && h.ReleasedAtUtc == null).Select(h => h.EmployeeId).ToListAsync();
                return key switch
                {
                    "payroll.sheet" => T(def, sub, ["Code", "Name", "Department", "Paid days", "Gross", "Income tax", "EOBI", "Other deductions", "Net pay"], [3, 4, 5, 6, 7, 8],
                        slips.Select(s => L(s.EmployeeCode, s.Name, s.Department, Num(s.PaidDays), M(s.GrossEarnings), M(s.IncomeTax), M(s.EobiEmployee), M(s.OtherDeductions), M(s.NetPay))), totals: true),
                    "payroll.bank" => T(def, held.Count > 0 ? $"{sub} · {held.Count} on hold, not included" : sub, ["Code", "Name", "Bank", "Account", "Amount"], [4],
                        slips.Where(s => !held.Contains(s.EmployeeId) && s.PaymentMethod == PaymentMethod.BankTransfer)
                            .Select(s => L(s.EmployeeCode, s.Name, s.BankName ?? "", s.BankAccount ?? "", M(s.NetPay))), totals: true),
                    "payroll.cash" => T(def, sub, ["Code", "Name", "Department", "Paid by", "Net pay", "Signature"], [4],
                        slips.Where(s => !held.Contains(s.EmployeeId) && s.PaymentMethod != PaymentMethod.BankTransfer)
                            .Select(s => L(s.EmployeeCode, s.Name, s.Department, s.PaymentMethod == PaymentMethod.Cash ? "Cash" : "Cheque", M(s.NetPay), "")), totals: true),
                    "payroll.vps" => T(def, sub, ["Code", "Name", "CNIC", "Pension fund", "Account", "Member share", "Company share", "Total", "Tax credit"], [5, 6, 7, 8],
                        slips.Where(s => s.VpsEmployee + s.VpsEmployer > 0).OrderBy(s => s.VpsProvider).Select(s => L(s.EmployeeCode, s.Name, s.Cnic ?? "", s.VpsProvider ?? "",
                            s.VpsAccountNo ?? "", M(s.VpsEmployee), M(s.VpsEmployer), M(s.VpsEmployee + s.VpsEmployer), M(s.VpsTaxCredit))), totals: true),
                    "payroll.socialsecurity" => T(def, sub, ["Code", "Name", "CNIC", "Social security no.", "Wages", "Employer contribution"], [4, 5],
                        slips.Where(s => s.SocialSecurityEmployer > 0).Select(s => L(s.EmployeeCode, s.Name, s.Cnic ?? "", s.SocialSecurityNo ?? "", M(s.GrossEarnings), M(s.SocialSecurityEmployer))), totals: true),
                    "payroll.tax" => T(def, sub, ["Code", "Name", "CNIC", "Taxable income", "Tax deducted"], [3, 4],
                        slips.Where(s => s.IncomeTax > 0 || s.TaxableIncome > 0).Select(s => L(s.EmployeeCode, s.Name, s.Cnic ?? "", M(s.TaxableIncome), M(s.IncomeTax))), totals: true),
                    _ => T(def, sub, ["Code", "Name", "CNIC", "Employee share", "Employer share", "Total"], [3, 4, 5],
                        slips.Where(s => s.EobiEmployee > 0 || s.EobiEmployer > 0).Select(s => L(s.EmployeeCode, s.Name, s.Cnic ?? "", M(s.EobiEmployee), M(s.EobiEmployer), M(s.EobiEmployee + s.EobiEmployer))), totals: true),
                };
            }
            case "expenses.claims":
            {
                var ids = People(f with { IncludeLeft = true }).Select(e => e.Id);
                var fromUtc = await clock.ToUtcAsync(from.ToDateTime(TimeOnly.MinValue));
                var toUtc = await clock.ToUtcAsync(to.ToDateTime(TimeOnly.MaxValue));
                var rows = await db.ExpenseClaims.Where(c => c.CreatedAtUtc >= fromUtc && c.CreatedAtUtc <= toUtc && ids.Contains(c.EmployeeId))
                    .OrderBy(c => c.CreatedAtUtc).Select(c => new { c.Employee!.EmployeeCode, Name = c.Employee.FirstName + " " + c.Employee.LastName, c.Title, c.CreatedAtUtc, c.Status, c.Total, c.PaidVia })
                    .ToListAsync();
                var list = new List<List<string>>();
                foreach (var r in rows)
                    list.Add(L(D(DateOnly.FromDateTime(await clock.ToLocalAsync(r.CreatedAtUtc))), r.EmployeeCode, r.Name, r.Title, r.Status.ToString(), r.PaidVia ?? "", M(r.Total)));
                return T(def, range, ["Submitted", "Code", "Name", "Claim", "Status", "Paid", "Amount"], [6], list, totals: true);
            }
            case "approvals.pending":
            {
                var ids = people.Select(e => e.Id);
                var rows = await db.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending && ids.Contains(a.EmployeeId)).OrderBy(a => a.CreatedAtUtc)
                    .Select(a => new
                    {
                        a.RequestType, Name = a.Employee!.FirstName + " " + a.Employee.LastName, a.Summary, a.CreatedAtUtc,
                        Step = a.Steps.Where(s => s.Status == ApprovalStepStatus.Current).Select(s => new { s.Kind, s.ApproverEmployeeId }).FirstOrDefault(),
                    }).ToListAsync();
                var approverIds = rows.Where(r => r.Step?.ApproverEmployeeId != null).Select(r => r.Step!.ApproverEmployeeId!.Value).Distinct().ToList();
                var names = await db.Employees.Where(e => approverIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
                return T(def, $"{rows.Count} waiting", ["Request", "Employee", "Details", "Waiting for", "Days"], [4],
                    rows.Select(r => L(ScheduledService.Humanize(r.RequestType.ToString()), r.Name, r.Summary,
                        r.Step?.ApproverEmployeeId is { } a ? names.GetValueOrDefault(a) ?? "" : "HR", N((int)(clock.UtcNow - r.CreatedAtUtc).TotalDays))));
            }
            case "training.hours":
            {
                var ids = people.Select(e => e.Id);
                var fromUtc = await clock.ToUtcAsync(from.ToDateTime(TimeOnly.MinValue));
                var toUtc = await clock.ToUtcAsync(to.ToDateTime(TimeOnly.MaxValue));
                var rows = await db.TrainingEnrollments.Where(x => ids.Contains(x.EmployeeId) && x.Session!.StartsAtUtc >= fromUtc && x.Session.StartsAtUtc <= toUtc
                                                                   && (x.Status == EnrollmentStatus.Attended || x.Status == EnrollmentStatus.Completed || x.Status == EnrollmentStatus.Failed))
                    .Select(x => new { x.Employee!.EmployeeCode, Name = x.Employee.FirstName + " " + x.Employee.LastName, Course = x.Session!.Program!.Title, x.Session.Program.DurationHours, x.Session.StartsAtUtc, x.Session.EndsAtUtc, x.Status, x.Score })
                    .ToListAsync();
                return T(def, range, ["Code", "Name", "Course", "Date", "Hours", "Result", "Score"], [4],
                    rows.OrderBy(r => r.Name).ThenBy(r => r.StartsAtUtc).Select(r => L(r.EmployeeCode, r.Name, r.Course, D(DateOnly.FromDateTime(r.StartsAtUtc)),
                        Num(r.DurationHours ?? (decimal)Math.Round((r.EndsAtUtc - r.StartsAtUtc).TotalHours, 1)), r.Status.ToString(), r.Score is { } s ? $"{s}%" : "")), totals: true);
            }
            case "travel.trips":
            {
                var ids = People(f with { IncludeLeft = true }).Select(e => e.Id);
                var trips = await db.TravelRequests.Include(t => t.Legs).Include(t => t.Expenses).Include(t => t.Employee)
                    .Where(t => t.DepartOn <= to && t.ReturnOn >= from && ids.Contains(t.EmployeeId) && t.Status != TravelStatus.Rejected && t.Status != TravelStatus.Cancelled)
                    .OrderBy(t => t.DepartOn).ToListAsync();
                return T(def, range, ["Name", "Route", "From", "To", "Status", "Booked", "Claimed", "Advance"], [5, 6, 7],
                    trips.Select(t => L(t.Employee!.FullName, TravelService.Route(t), D(t.DepartOn), D(t.ReturnOn),
                        t.Settlement == SettlementStatus.None ? t.Status.ToString() : ScheduledService.Humanize(t.Settlement.ToString()),
                        M(TravelService.BookedCost(t)), M(TravelService.Claimed(t)), M(t.AdvancePaid ?? 0))), totals: true);
            }
            default:
                throw new NotFoundException("Report");
        }
    }

    /// <summary>The people a report covers: everyone (HR) or direct reports (managers), filtered by department and person.</summary>
    private IQueryable<Employee> People(ReportRequest f)
    {
        var q = db.Employees.AsQueryable();
        if (!me.IsHr())
        {
            var mine = me.EmployeeId ?? Guid.Empty;
            q = q.Where(e => e.ManagerId == mine);
        }
        if (!f.IncludeLeft)
            q = q.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
        if (f.DepartmentIds is { Count: > 0 } d)
            q = q.Where(e => e.DepartmentId != null && d.Contains(e.DepartmentId.Value));
        if (f.EmployeeId is { } id)
            q = q.Where(e => e.Id == id);
        foreach (var g in groups.GroupBy(x => x.Kind))
        {
            var ids = g.Select(x => (Guid?)x.Id).ToList();
            q = g.Key switch
            {
                LookupKind.Division => q.Where(e => ids.Contains(e.DivisionId)),
                LookupKind.SubDepartment => q.Where(e => ids.Contains(e.SubDepartmentId)),
                LookupKind.Grade => q.Where(e => ids.Contains(e.GradeId)),
                LookupKind.CostCenter => q.Where(e => ids.Contains(e.CostCenterId)),
                LookupKind.Region => q.Where(e => ids.Contains(e.RegionId)),
                LookupKind.EmployeeGroup => q.Where(e => ids.Contains(e.EmployeeGroupId)),
                _ => q,
            };
        }
        return q;
    }

    private List<(Guid Id, LookupKind Kind)> groups = [];

    /// <summary>
    /// Expiry dates on employees' files and uploaded documents for the given people, from (inclusive, optional) to (inclusive).
    /// Used by the documents report, the scheduled report and the expiry alert.
    /// </summary>
    public static async Task<List<(Guid EmployeeId, string Document, DateOnly ExpiresOn)>> ExpiriesAsync(HrmsDbContext db, List<Guid> ids, DateOnly? from, DateOnly to)
    {
        var lo = from ?? DateOnly.MinValue;
        var list = new List<(Guid, string, DateOnly)>();
        var docs = await db.Attachments.Where(a => a.OwnerType == AttachmentOwner.EmployeeDocument && a.ExpiresOn != null && a.ExpiresOn >= lo && a.ExpiresOn <= to && ids.Contains(a.OwnerId))
            .Select(a => new { a.OwnerId, a.Category, a.FileName, a.ExpiresOn }).ToListAsync();
        list.AddRange(docs.Select(d => (d.OwnerId, d.Category is { } c ? ScheduledService.Humanize(c.ToString()) : d.FileName, d.ExpiresOn!.Value)));
        var files = await db.Employees.Where(e => ids.Contains(e.Id)).Select(e => new
        {
            e.Id, e.CnicExpiresOn, e.PassportExpiresOn, e.VisaExpiresOn, e.DrivingLicenseExpiresOn, e.InsuranceExpiresOn, e.ContractEndDate,
        }).ToListAsync();
        foreach (var e in files)
            foreach (var (name, date) in new[]
                     {
                         ("CNIC", e.CnicExpiresOn), ("Passport", e.PassportExpiresOn), ("Visa", e.VisaExpiresOn), ("Driving licence", e.DrivingLicenseExpiresOn),
                         ("Health insurance", e.InsuranceExpiresOn), ("Contract", e.ContractEndDate),
                     })
                if (date is { } d && d >= lo && d <= to)
                    list.Add((e.Id, name, d));
        return list.OrderBy(x => x.Item3).ToList();
    }

    private static ReportResult T(ReportDefinition def, string? subtitle, List<string> headers, int[] numeric, IEnumerable<List<string>> rows, bool totals = false)
    {
        var list = rows.ToList();
        var isNum = headers.Select((_, i) => numeric.Contains(i)).ToList();
        List<string>? sum = null;
        if (totals && list.Count > 0)
        {
            sum = headers.Select((_, i) => i == 0 ? "Total" : numeric.Contains(i) && i != 0
                ? Format(list.Sum(r => decimal.TryParse(r[i].Replace(",", "").Replace(" h", ""), out var v) ? v : 0), list[0][i])
                : "").ToList();
        }
        return new ReportResult(def.Name, subtitle, headers, isNum, list, sum);
    }

    /// <summary>Formats a total like the column's values (hours keep their " h").</summary>
    private static string Format(decimal v, string sample) => sample.EndsWith(" h") ? $"{v:0.#} h" : sample.Contains(',') || v >= 1000 ? v.ToString("N0") : v.ToString("0.##");

    private static List<string> L(params string?[] cells) => cells.Select(c => c ?? "").ToList();
    private static string D(DateOnly d) => d.ToString("d MMM yyyy");
    private static string N(int n) => n.ToString();
    private static string Num(decimal n) => n.ToString("0.##");
    private static string M(decimal n) => n.ToString("N0");
    private static string Time(DateTime t) => t.ToString("HH:mm");
    private static string Hours(int minutes) => $"{minutes / 60m:0.#} h";

    private static string Status(EmploymentStatus s, DateOnly? left) =>
        left is { } l && s is not (EmploymentStatus.Active or EmploymentStatus.Probation) ? $"{s} ({l:d MMM yyyy})" : s.ToString();
}
