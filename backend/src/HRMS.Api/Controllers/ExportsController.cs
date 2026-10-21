using ClosedXML.Excel;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

/// <summary>Excel downloads of the main lists, for HR. Dates are company-local.</summary>
[ApiController]
[Route("api/exports")]
[Authorize(Policy = Policies.CompanyUser)]
[Authorize(Policy = Policies.Hr)]
public class ExportsController(HrmsDbContext db, TenantClock clock) : ControllerBase
{
    private const int MaxDays = 366;

    [HttpGet("employees")]
    public async Task<IActionResult> Employees()
    {
        var rows = await db.Employees.OrderBy(e => e.EmployeeCode).Select(e => new
        {
            e.EmployeeCode, e.FirstName, e.LastName, e.Status, e.JoiningDate, e.LeavingDate, e.Email, e.Phone, e.Cnic, e.Gender, e.DateOfBirth,
            Department = e.Department != null ? e.Department.Name : null,
            Designation = e.Designation != null ? e.Designation.Title : null,
            ManagerCode = e.Manager != null ? e.Manager.EmployeeCode : null,
            Manager = e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null,
            Shift = e.Shift != null ? e.Shift.Name : null,
            e.BankName, e.BankAccount, e.Address,
            Login = db.Users.Where(u => u.EmployeeId == e.Id).Select(u => (UserRole?)u.Role).FirstOrDefault(),
        }).ToListAsync();
        var today = await clock.TodayAsync();
        return Xlsx($"employees-{today:yyyy-MM-dd}.xlsx", Excel.Write("Employees", rows,
            new("Employee code", r => r.EmployeeCode), new("First name", r => r.FirstName), new("Last name", r => r.LastName),
            new("Department", r => r.Department), new("Designation", r => r.Designation), new("Manager code", r => r.ManagerCode),
            new("Manager", r => r.Manager), new("Status", r => r.Status.ToString()), new("Joining date", r => r.JoiningDate),
            new("Leaving date", r => r.LeavingDate), new("Email", r => r.Email), new("Phone", r => r.Phone), new("CNIC", r => r.Cnic),
            new("Gender", r => r.Gender?.ToString()), new("Date of birth", r => r.DateOfBirth), new("Shift", r => r.Shift),
            new("Bank name", r => r.BankName), new("Bank account", r => r.BankAccount), new("Address", r => r.Address),
            new("Login role", r => r.Login?.ToString())));
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> Attendance([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        CheckRange(from, to);
        var zone = await clock.ZoneAsync();
        DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        var rows = await db.AttendanceRecords.Where(a => a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date).ThenBy(a => a.Employee!.EmployeeCode)
            .Select(a => new
            {
                a.Date, a.Employee!.EmployeeCode, Name = a.Employee.FirstName + " " + a.Employee.LastName,
                Department = a.Employee.Department != null ? a.Employee.Department.Name : null,
                a.CheckInUtc, a.CheckOutUtc, a.Status, a.Source, a.WorkedMinutes,
            }).ToListAsync();
        return Xlsx($"attendance-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.xlsx", Excel.Write("Attendance", rows,
            new("Date", r => r.Date), new("Employee code", r => r.EmployeeCode), new("Name", r => r.Name), new("Department", r => r.Department),
            new("Check in", r => TimeOnly.FromDateTime(Local(r.CheckInUtc))),
            new("Check out", r => r.CheckOutUtc is { } o ? TimeOnly.FromDateTime(Local(o)) : null),
            new("Worked hours", r => r.WorkedMinutes > 0 ? Math.Round(r.WorkedMinutes / 60.0, 2) : null, "0.00"),
            new("Status", r => r.Status.ToString()), new("Source", r => r.Source.ToString())));
    }

    [HttpGet("leave")]
    public async Task<IActionResult> Leave([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        CheckRange(from, to);
        var rows = await db.LeaveRequests.Where(r => r.FromDate <= to && r.ToDate >= from)
            .OrderBy(r => r.FromDate).Select(r => new
            {
                r.Employee!.EmployeeCode, Name = r.Employee.FirstName + " " + r.Employee.LastName,
                Department = r.Employee.Department != null ? r.Employee.Department.Name : null,
                LeaveType = r.LeaveType!.Name, r.FromDate, r.ToDate, r.Days, r.Status, r.Reason, r.DecisionNote, r.CreatedAtUtc,
            }).ToListAsync();
        return Xlsx($"leave-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.xlsx", Excel.Write("Leave", rows,
            new("Employee code", r => r.EmployeeCode), new("Name", r => r.Name), new("Department", r => r.Department),
            new("Leave type", r => r.LeaveType), new("From", r => r.FromDate), new("To", r => r.ToDate), new("Days", r => (double)r.Days, "0.0"),
            new("Status", r => r.Status.ToString()), new("Reason", r => r.Reason), new("Decision note", r => r.DecisionNote),
            new("Requested on", r => DateOnly.FromDateTime(r.CreatedAtUtc))));
    }

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        CheckRange(from, to);
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var rows = await db.ExpenseClaims.Where(c => c.CreatedAtUtc >= start && c.CreatedAtUtc < end)
            .SelectMany(c => c.Items.Select(i => new
            {
                c.Employee!.EmployeeCode, Name = c.Employee.FirstName + " " + c.Employee.LastName, Claim = c.Title, c.Status, c.PaidVia,
                i.Date, i.Category, i.Description, i.Amount, HasReceipt = i.ReceiptId != null,
            }))
            .OrderBy(r => r.Date).ToListAsync();
        return Xlsx($"expenses-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.xlsx", Excel.Write("Expenses", rows,
            new("Employee code", r => r.EmployeeCode), new("Name", r => r.Name), new("Claim", r => r.Claim), new("Date", r => r.Date),
            new("Category", r => r.Category.ToString()), new("Description", r => r.Description), new("Amount (Rs)", r => r.Amount),
            new("Receipt", r => r.HasReceipt), new("Status", r => r.Status.ToString()), new("Paid via", r => r.PaidVia)));
    }

    /// <summary>One row per payslip with every earning and deduction as its own column, plus bank details for transfers.</summary>
    [HttpGet("payroll/{runId:guid}")]
    public async Task<IActionResult> Payroll(Guid runId)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId) ?? throw new NotFoundException("Payroll run");
        var slips = await db.Payslips.Where(p => p.PayrollRunId == runId).Include(p => p.Lines)
            .Include(p => p.Employee!).ThenInclude(e => e.Department)
            .Include(p => p.Employee!).ThenInclude(e => e.Designation)
            .OrderBy(p => p.Employee!.EmployeeCode).ToListAsync();
        var earnings = slips.SelectMany(s => s.Lines).Where(l => l.Kind == SalaryComponentKind.Earning).Select(l => l.Name).Distinct().ToList();
        var deductions = slips.SelectMany(s => s.Lines).Where(l => l.Kind == SalaryComponentKind.Deduction).Select(l => l.Name).Distinct().ToList();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add($"Payroll {run.Year}-{run.Month:D2}");
        var headers = new List<string> { "Employee code", "Name", "Department", "Designation", "CNIC", "Bank", "Account", "Paid days" };
        headers.AddRange(earnings);
        headers.Add("Gross");
        headers.AddRange(deductions);
        headers.AddRange(["Net pay", "Employer EOBI"]);
        for (var c = 0; c < headers.Count; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        var r = 2;
        foreach (var s in slips)
        {
            var e = s.Employee!;
            object?[] fixedCols = [e.EmployeeCode, e.FullName, e.Department?.Name, e.Designation?.Title, e.Cnic, e.BankName, e.BankAccount, (double)s.PaidDays];
            var c = 1;
            foreach (var v in fixedCols)
                ws.Cell(r, c++).Value = XLCellValue.FromObject(v);
            foreach (var name in earnings)
                Money(ws.Cell(r, c++), s.Lines.Where(l => l.Kind == SalaryComponentKind.Earning && l.Name == name).Sum(l => l.Amount));
            Money(ws.Cell(r, c++), s.GrossEarnings);
            foreach (var name in deductions)
                Money(ws.Cell(r, c++), s.Lines.Where(l => l.Kind == SalaryComponentKind.Deduction && l.Name == name).Sum(l => l.Amount));
            Money(ws.Cell(r, c++), s.NetPay);
            Money(ws.Cell(r, c), s.EobiEmployer);
            r++;
        }
        if (slips.Count > 0)
        {
            ws.Cell(r, 1).Value = "Total";
            ws.Row(r).Style.Font.Bold = true;
            for (var c = 9; c <= headers.Count; c++)
            {
                var col = ws.Column(c).ColumnLetter();
                ws.Cell(r, c).FormulaA1 = $"SUM({col}2:{col}{r - 1})";
                ws.Cell(r, c).Style.NumberFormat.Format = "#,##0";
            }
        }
        Excel.StyleTable(ws, headers.Count, r);
        return Xlsx($"payroll-{run.Year}-{run.Month:D2}{(run.Status == PayrollRunStatus.Draft ? "-draft" : "")}.xlsx", Excel.Save(wb));
    }

    private static void Money(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = "#,##0";
    }

    private static void CheckRange(DateOnly from, DateOnly to)
    {
        if (from == default || to == default || to < from)
            throw new DomainException("Choose a valid date range.");
        if (to.DayNumber - from.DayNumber > MaxDays)
            throw new DomainException("Export at most one year at a time.");
    }

    private FileContentResult Xlsx(string name, byte[] bytes) => File(bytes, Excel.ContentType, name);
}
