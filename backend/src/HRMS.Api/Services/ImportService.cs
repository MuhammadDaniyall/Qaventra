using System.Net.Mail;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record ImportRowResult(int Row, string Label, List<string> Errors, List<string> Notes);

/// <summary>Outcome of checking or importing a sheet. Nothing is saved unless every row is valid.</summary>
public record ImportResult(bool Committed, int Total, int Valid, int Created, int Updated, List<ImportRowResult> Rows)
{
    public int Invalid => Total - Valid;
}

/// <summary>Bulk employee and attendance imports from Excel: download a template, check (dry run), then import all-or-nothing.</summary>
public partial class ImportService(HrmsDbContext db, EmployeeService employees, AttendanceService attendance, TenantClock clock)
{
    public const int MaxRows = 3000;

    // ---------- Employees ----------

    private static readonly string[] EmployeeHeaders =
    [
        "First name*", "Last name*", "Joining date*", "Employee code", "Email", "Phone", "CNIC", "Date of birth", "Gender", "Status",
        "Department", "Designation", "Manager code", "Shift", "Bank name", "Bank account", "Address", "Basic salary",
    ];

    public async Task<byte[]> EmployeeTemplateAsync()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Employees");
        for (var i = 0; i < EmployeeHeaders.Length; i++)
            ws.Cell(1, i + 1).Value = EmployeeHeaders[i];
        Excel.StyleTable(ws, EmployeeHeaders.Length, 1);

        // Reference lists so people type names that match.
        var lists = wb.Worksheets.Add("Lists (read only)");
        lists.Cell(1, 1).Value = "Departments";
        lists.Cell(1, 2).Value = "Designations";
        lists.Cell(1, 3).Value = "Shifts";
        lists.Cell(1, 4).Value = "Managers (code)";
        lists.Cell(1, 5).Value = "Gender";
        lists.Cell(1, 6).Value = "Status";
        void Fill(int col, IEnumerable<string> values)
        {
            var r = 2;
            foreach (var v in values)
                lists.Cell(r++, col).Value = v;
        }
        Fill(1, await db.Departments.OrderBy(d => d.Name).Select(d => d.Name).ToListAsync());
        Fill(2, await db.Designations.OrderBy(d => d.Title).Select(d => d.Title).ToListAsync());
        Fill(3, await db.Shifts.OrderBy(s => s.Name).Select(s => s.Name).ToListAsync());
        Fill(4, await db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .OrderBy(e => e.EmployeeCode).Select(e => e.EmployeeCode + " · " + e.FirstName + " " + e.LastName).ToListAsync());
        Fill(5, ["Male", "Female", "Other"]);
        Fill(6, ["Active", "Probation"]);
        Excel.StyleTable(lists, 6, 1);
        lists.Protect();

        var help = wb.Worksheets.Add("How to fill");
        string[] notes =
        [
            "Fill one employee per row on the Employees sheet. An example row is at the bottom of this page.",
            "Columns marked * are required. Dates: 21-Oct-2026, 21/10/2026 or 2026-10-21.",
            "Employee code: leave empty to number automatically (EMP-0001, EMP-0002, ...).",
            "Department, Designation and Shift must match the Lists sheet; tick 'Create missing' on upload to add new departments and designations.",
            "Manager code: an existing employee's code, or the code of another row in this file.",
            "Basic salary (optional) sets a monthly salary from the joining date; add allowances later on the Salary tab.",
            "Upload shows every problem by row before anything is saved. Nothing is imported until all rows are valid.",
        ];
        for (var i = 0; i < notes.Length; i++)
            help.Cell(i + 1, 1).Value = notes[i];
        // The example lives here, not on the data sheet, so a forgotten example can never be imported.
        var exampleRow = notes.Length + 2;
        object[] example = ["Ayesha", "Khan", "01-Nov-2026", "", "ayesha.khan@company.pk", "0300-1234567", "35202-1234567-1",
            "14-May-1996", "Female", "Probation", "Software Engineering", "Software Engineer", "EMP-0002", "", "Meezan Bank", "01011234567", "Gulberg, Lahore", 150000];
        for (var i = 0; i < EmployeeHeaders.Length; i++)
        {
            help.Cell(exampleRow, i + 1).Value = EmployeeHeaders[i];
            help.Cell(exampleRow + 1, i + 1).Value = XLCellValue.FromObject(example[i]);
        }
        help.Row(exampleRow).Style.Font.Bold = true;
        help.Column(1).Width = 120;
        return Excel.Save(wb);
    }

    private sealed class EmployeeDraft
    {
        public required ExcelRow Row;
        public required NewEmployee Input;
        public string? DepartmentName, DesignationName, ManagerCode;
        public decimal? Basic;
        public List<string> Errors = [];
        public List<string> Notes = [];
    }

    [GeneratedRegex(@"^\d{5}-\d{7}-\d$")]
    private static partial Regex CnicPattern();

    public async Task<ImportResult> ImportEmployeesAsync(Stream file, bool commit, bool createMissing)
    {
        var rows = Read(file, ["First name", "Last name", "Joining date"]);
        var departments = (await db.Departments.ToListAsync()).ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        var designations = (await db.Designations.ToListAsync()).ToDictionary(d => d.Title, StringComparer.OrdinalIgnoreCase);
        var shifts = (await db.Shifts.ToListAsync()).ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var existingCodes = (await db.Employees.Select(e => new { e.EmployeeCode, e.Id }).ToListAsync())
            .ToDictionary(e => e.EmployeeCode, e => e.Id, StringComparer.OrdinalIgnoreCase);
        var sheetCodes = rows.Select(r => r.Get("Employee code")).OfType<string>().ToList();

        var drafts = new List<EmployeeDraft>();
        foreach (var row in rows)
        {
            var errors = new List<string>();
            var notes = new List<string>();
            string Required(string header, int max)
            {
                var v = row.Get(header);
                if (v is null) errors.Add($"{header} is required.");
                else if (v.Length > max) errors.Add($"{header} is longer than {max} characters.");
                return v ?? "";
            }
            DateOnly? Date(string header, bool required)
            {
                var v = row.Get(header);
                if (v is null)
                {
                    if (required) errors.Add($"{header} is required.");
                    return null;
                }
                if (Excel.TryDate(v, out var d)) return d;
                errors.Add($"{header} '{v}' is not a date (use 21-Oct-2026).");
                return null;
            }

            var first = Required("First name", 100);
            var last = Required("Last name", 100);
            var joining = Date("Joining date", true);
            var dob = Date("Date of birth", false);

            var code = row.Get("Employee code");
            if (code is not null)
            {
                if (code.Length > 30) errors.Add("Employee code is longer than 30 characters.");
                if (existingCodes.ContainsKey(code)) errors.Add($"Employee code {code} already exists.");
                if (sheetCodes.Count(c => c.Equals(code, StringComparison.OrdinalIgnoreCase)) > 1) errors.Add($"Employee code {code} appears more than once in the file.");
            }

            var email = row.Get("Email");
            if (email is not null && !MailAddress.TryCreate(email, out _)) errors.Add($"Email '{email}' is not valid.");
            var cnic = row.Get("CNIC");
            if (cnic is not null && !CnicPattern().IsMatch(cnic)) errors.Add($"CNIC '{cnic}' must look like 12345-1234567-1.");

            Gender? gender = null;
            if (row.Get("Gender") is { } g)
            {
                if (Enum.TryParse<Gender>(g, true, out var parsed)) gender = parsed;
                else errors.Add($"Gender '{g}' must be Male, Female or Other.");
            }
            var status = EmploymentStatus.Active;
            if (row.Get("Status") is { } s)
            {
                if (Enum.TryParse<EmploymentStatus>(s, true, out var parsed) && parsed is EmploymentStatus.Active or EmploymentStatus.Probation) status = parsed;
                else errors.Add($"Status '{s}' must be Active or Probation.");
            }

            var deptName = row.Get("Department");
            if (deptName is not null && !departments.ContainsKey(deptName))
            {
                if (createMissing) notes.Add($"New department '{deptName}' will be created.");
                else errors.Add($"Department '{deptName}' does not exist. Tick 'Create missing' or fix the name.");
            }
            var titleName = row.Get("Designation");
            if (titleName is not null && !designations.ContainsKey(titleName))
            {
                if (createMissing) notes.Add($"New designation '{titleName}' will be created.");
                else errors.Add($"Designation '{titleName}' does not exist. Tick 'Create missing' or fix the name.");
            }
            Guid? shiftId = null;
            if (row.Get("Shift") is { } shiftName)
            {
                if (shifts.TryGetValue(shiftName, out var shift)) shiftId = shift.Id;
                else errors.Add($"Shift '{shiftName}' does not exist.");
            }

            var managerCode = row.Get("Manager code");
            if (managerCode is not null)
            {
                if (managerCode.Equals(code, StringComparison.OrdinalIgnoreCase)) errors.Add("An employee cannot be their own manager.");
                else if (!existingCodes.ContainsKey(managerCode) && !sheetCodes.Contains(managerCode, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"Manager code {managerCode} is not an existing employee or a row in this file.");
            }

            decimal? basic = null;
            if (row.Get("Basic salary") is { } b)
            {
                if (decimal.TryParse(b.Replace(",", ""), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) && amount is >= 0 and <= 100_000_000) basic = amount;
                else errors.Add($"Basic salary '{b}' is not a valid amount.");
            }

            string? Optional(string header, int max)
            {
                var v = row.Get(header);
                if (v is not null && v.Length > max) errors.Add($"{header} is longer than {max} characters.");
                return v;
            }

            drafts.Add(new EmployeeDraft
            {
                Row = row,
                Input = new NewEmployee(first, last, joining ?? default, code, email, Optional("Phone", 30), cnic, dob, gender, status,
                    Optional("Address", 500), Optional("Bank name", 100), Optional("Bank account", 50), ShiftId: shiftId),
                DepartmentName = deptName,
                DesignationName = titleName,
                ManagerCode = managerCode,
                Basic = basic,
                Errors = errors,
                Notes = notes,
            });
        }

        var limit = await db.Tenants.Where(t => t.Id == db.CurrentTenantId).Select(t => t.MaxEmployees).FirstOrDefaultAsync();
        if (limit is { } max)
        {
            var active = await db.Employees.CountAsync(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation);
            if (active + drafts.Count > max)
                foreach (var d in drafts.Skip(Math.Max(0, max - active)))
                    d.Errors.Add($"Your plan allows {max} active employees; this row would go over the limit.");
        }

        var results = drafts.Select(d => new ImportRowResult(d.Row.Number, $"{d.Input.FirstName} {d.Input.LastName}".Trim(), d.Errors, d.Notes)).ToList();
        var valid = drafts.Count(d => d.Errors.Count == 0);
        if (!commit || valid != drafts.Count || drafts.Count == 0)
            return new ImportResult(false, drafts.Count, valid, 0, 0, results);

        await using var tx = await db.Database.BeginTransactionAsync();
        Guid? DeptId(string? name)
        {
            if (name is null) return null;
            if (!departments.TryGetValue(name, out var d))
            {
                d = new Department { Name = name };
                db.Departments.Add(d);
                departments[name] = d;
            }
            return d.Id;
        }
        Guid? TitleId(string? name)
        {
            if (name is null) return null;
            if (!designations.TryGetValue(name, out var d))
            {
                d = new Designation { Title = name };
                db.Designations.Add(d);
                designations[name] = d;
            }
            return d.Id;
        }

        var created = new List<(EmployeeDraft Draft, Employee Employee)>();
        foreach (var d in drafts)
        {
            var input = d.Input with { DepartmentId = DeptId(d.DepartmentName), DesignationId = TitleId(d.DesignationName) };
            created.Add((d, await employees.CreateAsync(input)));
        }
        var codes = existingCodes.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var (_, e) in created)
            codes[e.EmployeeCode] = e.Id;
        foreach (var (d, e) in created)
        {
            if (d.ManagerCode is not null)
                e.ManagerId = codes[d.ManagerCode];
            if (d.Basic is { } basic)
                db.EmployeeSalaries.Add(new EmployeeSalary { EmployeeId = e.Id, EffectiveFrom = e.JoiningDate, BasicSalary = basic });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new ImportResult(true, drafts.Count, valid, created.Count, 0, results);
    }

    // ---------- Attendance ----------

    private static readonly string[] AttendanceHeaders = ["Employee code*", "Date*", "Check in*", "Check out"];

    public async Task<byte[]> AttendanceTemplateAsync()
    {
        var today = await clock.TodayAsync();
        var sample = await db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)
            .OrderBy(e => e.EmployeeCode).Select(e => e.EmployeeCode).FirstOrDefaultAsync() ?? "EMP-0001";
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Attendance");
        for (var i = 0; i < AttendanceHeaders.Length; i++)
            ws.Cell(1, i + 1).Value = AttendanceHeaders[i];
        Excel.StyleTable(ws, AttendanceHeaders.Length, 1);
        var help = wb.Worksheets.Add("How to fill");
        help.Cell(1, 1).Value = "One row per employee per day. Times in 24-hour (09:05, 18:10) or with AM/PM. Leave Check out empty if they did not punch out.";
        help.Cell(2, 1).Value = "A row for a day that already has attendance replaces it. Late, half day and worked hours are worked out from each employee's shift.";
        help.Cell(4, 1).Value = $"Example:  {sample}  |  {today.AddDays(-1):dd-MMM-yyyy}  |  09:05  |  18:10";
        help.Column(1).Width = 120;
        return Excel.Save(wb);
    }

    public async Task<ImportResult> ImportAttendanceAsync(Stream file, bool commit)
    {
        var rows = Read(file, ["Employee code", "Date", "Check in"]);
        var today = await clock.TodayAsync();
        var codes = (await db.Employees.Select(e => new { e.EmployeeCode, e.Id, e.JoiningDate }).ToListAsync())
            .ToDictionary(e => e.EmployeeCode, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<(Guid, DateOnly)>();
        var parsed = new List<(ExcelRow Row, Guid EmployeeId, DateOnly Date, TimeOnly In, TimeOnly? Out, List<string> Errors, List<string> Notes)>();

        foreach (var row in rows)
        {
            var errors = new List<string>();
            var notes = new List<string>();
            var code = row.Get("Employee code");
            Guid id = default;
            if (code is null) errors.Add("Employee code is required.");
            else if (!codes.TryGetValue(code, out var emp)) errors.Add($"No employee with code {code}.");
            else id = emp.Id;

            DateOnly date = default;
            if (!Excel.TryDate(row.Get("Date"), out date)) errors.Add($"Date '{row.Get("Date")}' is missing or not a date.");
            else if (date >= today) errors.Add("Only past days can be imported; today's attendance comes from check-in.");
            else if (code is not null && codes.TryGetValue(code, out var emp2) && date < emp2.JoiningDate) errors.Add($"{code} joined on {emp2.JoiningDate:dd-MMM-yyyy}.");

            if (!Excel.TryTime(row.Get("Check in"), out var checkIn)) errors.Add($"Check in '{row.Get("Check in")}' is missing or not a time.");
            TimeOnly? checkOut = null;
            if (row.Get("Check out") is { } o)
            {
                if (Excel.TryTime(o, out var t)) checkOut = t;
                else errors.Add($"Check out '{o}' is not a time.");
            }
            if (checkOut is { } co && co <= checkIn) errors.Add("Check out must be after check in.");
            if (id != default && date != default && !seen.Add((id, date))) errors.Add($"{code} on {date:dd-MMM-yyyy} appears more than once.");
            parsed.Add((row, id, date, checkIn, checkOut, errors, notes));
        }

        // Rows that will replace existing attendance are allowed, just flagged.
        var ok = parsed.Where(p => p.Errors.Count == 0).ToList();
        var existing = new HashSet<(Guid, DateOnly)>();
        if (ok.Count > 0)
        {
            var ids = ok.Select(p => p.EmployeeId).Distinct().ToList();
            var first = ok.Min(p => p.Date);
            var last = ok.Max(p => p.Date);
            existing = (await db.AttendanceRecords.Where(a => ids.Contains(a.EmployeeId) && a.Date >= first && a.Date <= last)
                .Select(a => new { a.EmployeeId, a.Date }).ToListAsync())
                .Select(a => (a.EmployeeId, a.Date)).ToHashSet();
        }
        var updates = 0;
        foreach (var p in parsed.Where(p => p.Errors.Count == 0 && existing.Contains((p.EmployeeId, p.Date))))
        {
            p.Notes.Add("Replaces the attendance already recorded for this day.");
            updates++;
        }

        var results = parsed.Select(p => new ImportRowResult(p.Row.Number, $"{p.Row.Get("Employee code")} · {p.Row.Get("Date")}", p.Errors, p.Notes)).ToList();
        var valid = parsed.Count(p => p.Errors.Count == 0);
        if (!commit || valid != parsed.Count || parsed.Count == 0)
            return new ImportResult(false, parsed.Count, valid, 0, updates, results);

        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var p in parsed)
            await attendance.SaveManualAsync(p.EmployeeId, p.Date, p.In, p.Out);
        await tx.CommitAsync();
        return new ImportResult(true, parsed.Count, valid, parsed.Count - updates, updates, results);
    }

    private static List<ExcelRow> Read(Stream file, string[] required)
    {
        var rows = Excel.Read(file, out var headers);
        var missing = required.Where(r => !headers.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
            throw new DomainException($"The sheet has no column {string.Join(", ", missing.Select(m => $"'{m}'"))}. Use the template.");
        if (rows.Count > MaxRows)
            throw new DomainException($"At most {MaxRows} rows per file; split it into smaller files.");
        return rows;
    }
}
