using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record ProcessPayrollRequest([Range(2000, 2100)] int Year, [Range(1, 12)] int Month);

public record PayrollRunDto(
    Guid Id, int Year, int Month, PayrollRunStatus Status, string TaxYearCode, DateTime ProcessedAtUtc, DateTime? FinalizedAtUtc,
    int Employees, decimal TotalGross, decimal TotalDeductions, decimal TotalNet);

public record PayslipSummaryDto(
    Guid Id, int Year, int Month, PayrollRunStatus RunStatus, Guid EmployeeId, string EmployeeCode, string EmployeeName, string? Department,
    decimal PaidDays, decimal GrossEarnings, decimal IncomeTax, decimal EobiEmployee, decimal OtherDeductions, decimal NetPay);

public record PayrollRunDetailDto(PayrollRunDto Run, List<PayslipSummaryDto> Payslips, IReadOnlyList<string> Warnings);

public record PayslipLineDto(string Name, SalaryComponentKind Kind, decimal Amount);

public record PayslipDto(
    Guid Id, string CompanyName, int Year, int Month, PayrollRunStatus RunStatus,
    string EmployeeCode, string EmployeeName, string? Department, string? Designation, string? Cnic, string? BankName, string? BankAccount,
    int DaysInMonth, decimal UnpaidDays, decimal PaidDays,
    decimal GrossEarnings, decimal TaxableIncome, decimal IncomeTax, decimal EobiEmployee, decimal EobiEmployer,
    decimal OtherDeductions, decimal NetPay, List<PayslipLineDto> Lines);

public record PayrollSettingsDto([Range(0, 1_000_000)] decimal EobiEmployeeContribution, [Range(0, 1_000_000)] decimal EobiEmployerContribution, bool DeductAbsentDays);

[ApiController]
[Route("api/payroll")]
[Authorize(Policy = Policies.CompanyUser)]
public class PayrollController(HrmsDbContext db, PayrollService payroll, ICurrentUser me) : ControllerBase
{
    [HttpGet("runs"), Authorize(Policy = Policies.Hr)]
    public Task<List<PayrollRunDto>> Runs() =>
        db.PayrollRuns.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).Select(ToDto()).ToListAsync();

    [HttpPost("runs"), Authorize(Policy = Policies.Hr)]
    public async Task<PayrollRunDetailDto> Process(ProcessPayrollRequest req)
    {
        var result = await payroll.ProcessAsync(req.Year, req.Month);
        return await Detail(result.Run.Id, result.Warnings);
    }

    [HttpGet("runs/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public Task<PayrollRunDetailDto> GetRun(Guid id) => Detail(id, []);

    [HttpPost("runs/{id:guid}/finalize"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Finalize(Guid id)
    {
        await payroll.FinalizeAsync(id);
        return NoContent();
    }

    /// <summary>Employees see payslips only once the month is finalized.</summary>
    [HttpGet("my-payslips")]
    public Task<List<PayslipSummaryDto>> MyPayslips() =>
        db.Payslips.Where(p => p.EmployeeId == me.EmployeeId && p.PayrollRun!.Status == PayrollRunStatus.Finalized)
            .OrderByDescending(p => p.PayrollRun!.Year).ThenByDescending(p => p.PayrollRun!.Month)
            .Select(SummaryDto()).ToListAsync();

    [HttpGet("payslips/{id:guid}")]
    public async Task<PayslipDto> Payslip(Guid id)
    {
        var isHr = me.IsHr();
        var p = await db.Payslips.AsNoTracking()
            .Include(x => x.Lines).Include(x => x.PayrollRun)
            .Include(x => x.Employee).ThenInclude(e => e!.Department)
            .Include(x => x.Employee).ThenInclude(e => e!.Designation)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Payslip");

        if (!isHr && (p.EmployeeId != me.EmployeeId || p.PayrollRun!.Status != PayrollRunStatus.Finalized))
            throw new ForbiddenException("You can only view your own finalized payslips.");

        var company = await db.Tenants.Where(t => t.Id == p.TenantId).Select(t => t.Name).FirstAsync();
        var e = p.Employee!;
        return new PayslipDto(
            p.Id, company, p.PayrollRun!.Year, p.PayrollRun.Month, p.PayrollRun.Status,
            e.EmployeeCode, e.FullName, e.Department?.Name, e.Designation?.Title, e.Cnic, e.BankName, e.BankAccount,
            p.DaysInMonth, p.UnpaidDays, p.PaidDays, p.GrossEarnings, p.TaxableIncome, p.IncomeTax, p.EobiEmployee, p.EobiEmployer,
            p.OtherDeductions, p.NetPay,
            p.Lines.OrderBy(l => l.Kind).Select(l => new PayslipLineDto(l.Name, l.Kind, l.Amount)).ToList());
    }

    [HttpGet("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<PayrollSettingsDto> GetSettings()
    {
        var s = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();
        return new PayrollSettingsDto(s.EobiEmployeeContribution, s.EobiEmployerContribution, s.DeductAbsentDays);
    }

    [HttpPut("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveSettings(PayrollSettingsDto req)
    {
        var s = await db.PayrollSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new PayrollSettings();
            db.PayrollSettings.Add(s);
        }
        s.EobiEmployeeContribution = req.EobiEmployeeContribution;
        s.EobiEmployerContribution = req.EobiEmployerContribution;
        s.DeductAbsentDays = req.DeductAbsentDays;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<PayrollRunDetailDto> Detail(Guid id, IReadOnlyList<string> warnings)
    {
        var run = await db.PayrollRuns.Where(r => r.Id == id).Select(ToDto()).FirstOrDefaultAsync() ?? throw new NotFoundException("Payroll run");
        var slips = await db.Payslips.Where(p => p.PayrollRunId == id).OrderBy(p => p.Employee!.EmployeeCode).Select(SummaryDto()).ToListAsync();
        return new PayrollRunDetailDto(run, slips, warnings);
    }

    private static System.Linq.Expressions.Expression<Func<PayrollRun, PayrollRunDto>> ToDto() => r => new PayrollRunDto(
        r.Id, r.Year, r.Month, r.Status, r.TaxYearCode, r.ProcessedAtUtc, r.FinalizedAtUtc,
        r.Payslips.Count, r.TotalGross, r.TotalDeductions, r.TotalNet);

    private static System.Linq.Expressions.Expression<Func<Payslip, PayslipSummaryDto>> SummaryDto() => p => new PayslipSummaryDto(
        p.Id, p.PayrollRun!.Year, p.PayrollRun.Month, p.PayrollRun.Status, p.EmployeeId, p.Employee!.EmployeeCode, p.Employee.FirstName + " " + p.Employee.LastName,
        p.Employee.Department != null ? p.Employee.Department.Name : null,
        p.PaidDays, p.GrossEarnings, p.IncomeTax, p.EobiEmployee, p.OtherDeductions, p.NetPay);
}
