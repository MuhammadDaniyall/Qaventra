using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Domain.Services;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record ProcessPayrollRequest([Range(2000, 2100)] int Year, [Range(1, 12)] int Month);

public record PayrollRunDto(
    Guid Id, int Year, int Month, PayrollRunStatus Status, string TaxYearCode, DateTime ProcessedAtUtc, DateTime? FinalizedAtUtc,
    int Employees, decimal TotalGross, decimal TotalDeductions, decimal TotalNet, DateTime? SubmittedAtUtc, string? ApprovedByName, DateTime? ApprovedAtUtc,
    string? ApprovalNote, DateTime? DisbursedAtUtc, string? DisbursedVia, int Held,
    PayFrequency Frequency = PayFrequency.Monthly, DateOnly PeriodStart = default, DateOnly PeriodEnd = default, string Label = "",
    RecordStampsDto? Stamps = null, string Currency = "PKR", string PayrollCountry = "PK");

/// <summary>A weekly, bi-weekly or semi-monthly period: any day inside it picks it.</summary>
public record ProcessPeriodRequest(PayFrequency Frequency, DateOnly Date);

/// <summary>A pay period, the people paid at its frequency, and its run if one exists.</summary>
public record PayPeriodDto(PayFrequency Frequency, DateOnly Start, DateOnly End, string Label, int People, Guid? RunId, PayrollRunStatus? Status);

public record PayrollDecisionRequest(bool Approve, [StringLength(1000)] string? Note);

public record DisburseRequest([StringLength(100)] string? Via);

public record PayslipSummaryDto(
    Guid Id, int Year, int Month, PayrollRunStatus RunStatus, Guid EmployeeId, string EmployeeCode, string EmployeeName, string? Department,
    decimal PaidDays, decimal GrossEarnings, decimal IncomeTax, decimal EobiEmployee, decimal OtherDeductions, decimal NetPay, string? HeldReason = null, string? Label = null);

public record PayrollRunDetailDto(PayrollRunDto Run, List<PayslipSummaryDto> Payslips, IReadOnlyList<string> Warnings);

public record PayslipLineDto(string Name, SalaryComponentKind Kind, decimal Amount);

public record PayslipDto(
    Guid Id, string CompanyName, int Year, int Month, PayrollRunStatus RunStatus,
    string EmployeeCode, string EmployeeName, string? Department, string? Designation, string? Cnic, string? BankName, string? BankAccount,
    int DaysInMonth, decimal UnpaidDays, decimal PaidDays,
    decimal GrossEarnings, decimal TaxableIncome, decimal IncomeTax, decimal EobiEmployee, decimal EobiEmployer,
    decimal OtherDeductions, decimal NetPay, List<PayslipLineDto> Lines, string? Label = null,
    string Currency = "PKR", string PayrollCountry = "PK", decimal SocialInsuranceEmployee = 0, decimal SocialInsuranceEmployer = 0, string? Iban = null);

public record PayrollSettingsDto(
    [Range(0, 1_000_000)] decimal EobiEmployeeContribution, [Range(0, 1_000_000)] decimal EobiEmployerContribution, bool DeductAbsentDays,
    [Range(0, 5)] decimal OvertimeMultiplier = 1.5m, [Range(0, 31)] int LatesPerPenalty = 0, [Range(0, 5)] decimal LatePenaltyDays = 1m,
    bool DeductHalfDays = false, bool RequireApproval = false, bool EncashOnGross = false,
    bool ProvidentFundEnabled = false, [Range(0, 50)] decimal PfEmployeePercent = 8.33m, [Range(0, 50)] decimal PfEmployerPercent = 8.33m, bool PfOnGross = false,
    [Range(0, 30)] int PfVestingYears = 0, bool GratuityEnabled = false, [Range(0, 90)] decimal GratuityDaysPerYear = 30, [Range(0, 30)] decimal GratuityMinYears = 1,
    bool GratuityOnGross = false, [Range(0, 50)] decimal SocialSecurityPercent = 0, [Range(0, 10_000_000)] decimal? SocialSecurityWageCeiling = null,
    [Range(0, 5)] decimal HolidayOvertimeMultiplier = 2m, [Range(0, 5)] decimal OffDayOvertimeMultiplier = 2m, [Range(0, 480)] int OvertimeMinMinutes = 0,
    [Range(0, 120)] int OvertimeRoundMinutes = 0, bool VpsEnabled = false, [Range(0, 100)] decimal VpsCreditCapPercent = 20m,
    DayOfWeek PayWeekStartsOn = DayOfWeek.Monday, DateOnly? BiWeeklyAnchor = null,
    [RegularExpression("^(PK|AE|SA)$", ErrorMessage = "Payroll country must be PK, AE or SA.")] string PayrollCountry = "PK",
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter code such as PKR, AED or SAR.")] string? Currency = null,
    [Range(0, 30)] decimal GosiAnnuityEmployeePercent = 9m, [Range(0, 30)] decimal GosiAnnuityEmployerPercent = 9m,
    [Range(0, 10)] decimal GosiSanedEmployeePercent = 0.75m, [Range(0, 10)] decimal GosiSanedEmployerPercent = 0.75m,
    [Range(0, 10)] decimal GosiHazardEmployerPercent = 2m, [Range(0, 1_000_000)] decimal GosiWageFloor = 1500m, [Range(0, 1_000_000)] decimal GosiWageCeiling = 45000m,
    [Range(0, 5)] decimal GosiNewEntrantStepPercent = 0.5m, [Range(0, 30)] decimal GosiNewEntrantMaxPercent = 11m,
    [Range(0, 30)] decimal PensionEmployeePercent = 11m, [Range(0, 30)] decimal PensionEmployerPercent = 12.5m,
    [Range(0, 1_000_000)] decimal PensionWageFloor = 3000m, [Range(0, 1_000_000)] decimal PensionWageCeiling = 70000m, bool PensionOnGross = true,
    GratuityWage GulfGratuityWage = GratuityWage.Basic,
    [StringLength(20)] string? WpsEmployerId = null, [StringLength(20)] string? WpsBankRoutingCode = null,
    [StringLength(34)] string? WpsEmployerIban = null, [StringLength(20)] string? WpsAgentId = null);

[ApiController]
[Route("api/payroll")]
[Authorize(Policy = Policies.CompanyUser)]
public class PayrollController(HrmsDbContext db, PayrollService payroll, ICurrentUser me) : ControllerBase
{
    [HttpGet("runs"), Authorize(Policy = Policies.Hr)]
    public async Task<List<PayrollRunDto>> Runs() =>
        (await db.PayrollRuns.OrderByDescending(r => r.PeriodEnd).ThenBy(r => r.Frequency).Select(ToDto()).ToListAsync())
            .Select(r => r with { Label = PayPeriods.Label(r.Frequency, r.PeriodStart, r.PeriodEnd) }).ToList();

    [HttpPost("runs"), Authorize(Policy = Policies.Hr)]
    public async Task<PayrollRunDetailDto> Process(ProcessPayrollRequest req)
    {
        var result = await payroll.ProcessAsync(req.Year, req.Month);
        return await Detail(result.Run.Id, result.Warnings);
    }

    [HttpPost("runs/period"), Authorize(Policy = Policies.Hr)]
    public async Task<PayrollRunDetailDto> ProcessPeriod(ProcessPeriodRequest req)
    {
        var s = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();
        var result = await payroll.ProcessAsync(PayPeriods.Containing(req.Frequency, req.Date, s.PayWeekStartsOn, s.BiWeeklyAnchor));
        return await Detail(result.Run.Id, result.Warnings);
    }

    /// <summary>
    /// The pay periods ending in a month for every frequency the company uses (monthly only when everyone is paid
    /// monthly), with how many people each pays and its run.
    /// </summary>
    [HttpGet("periods"), Authorize(Policy = Policies.Hr)]
    public async Task<List<PayPeriodDto>> Periods([FromQuery] int year, [FromQuery] int month)
    {
        if (month is < 1 or > 12)
            throw new DomainException("Month must be between 1 and 12.");
        var s = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var counts = await db.Employees.Where(e => e.JoiningDate <= last && (e.LeavingDate == null || e.LeavingDate >= first))
            .GroupBy(e => e.PayFrequency).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var runs = await db.PayrollRuns.Where(r => r.Year == year && r.Month == month)
            .Select(r => new { r.Id, r.Frequency, r.PeriodStart, r.Status }).ToListAsync();
        var list = new List<PayPeriodDto>();
        foreach (var f in Enum.GetValues<PayFrequency>().Where(f => f == PayFrequency.Monthly || counts.ContainsKey(f)))
            foreach (var p in PayPeriods.EndingIn(f, year, month, s.PayWeekStartsOn, s.BiWeeklyAnchor))
            {
                var run = runs.FirstOrDefault(r => r.Frequency == f && r.PeriodStart == p.Start);
                list.Add(new PayPeriodDto(f, p.Start, p.End, p.Label, counts.GetValueOrDefault(f), run?.Id, run?.Status));
            }
        return list;
    }

    [HttpGet("runs/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public Task<PayrollRunDetailDto> GetRun(Guid id) => Detail(id, []);

    [HttpPost("runs/{id:guid}/submit"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Submit(Guid id)
    {
        await payroll.SubmitAsync(id);
        return NoContent();
    }

    [HttpPost("runs/{id:guid}/decide"), Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> Decide(Guid id, PayrollDecisionRequest req)
    {
        await payroll.DecideAsync(id, req.Approve, req.Note);
        return NoContent();
    }

    [HttpPost("runs/{id:guid}/undisburse"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Undisburse(Guid id)
    {
        await payroll.UndisburseAsync(id);
        return NoContent();
    }

    [HttpPost("runs/{id:guid}/disburse"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Disburse(Guid id, DisburseRequest req)
    {
        await payroll.DisburseAsync(id, req.Via);
        return NoContent();
    }

    [HttpPost("runs/{id:guid}/finalize"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Finalize(Guid id)
    {
        await payroll.FinalizeAsync(id);
        return NoContent();
    }

    /// <summary>
    /// WPS salary file of a finalized UAE or Saudi run: the UAE SIF (EDR lines + SCR) or the Saudi bank / Mudad CSV.
    /// People paid in cash or by cheque are left out.
    /// </summary>
    [HttpGet("runs/{id:guid}/wps"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> WpsFile(Guid id, [FromServices] TenantClock clock)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Payroll run");
        if (run.Status != PayrollRunStatus.Finalized)
            throw new DomainException("Finalize the payroll before downloading the WPS file.");
        if (run.PayrollCountry is not (PayrollRegime.Uae or PayrollRegime.Saudi))
            throw new DomainException("WPS files are for UAE and Saudi payroll.");
        var s = await db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync() ?? new PayrollSettings();
        if (string.IsNullOrWhiteSpace(s.WpsEmployerId))
            throw new DomainException("Enter the employer establishment ID in payroll settings first.");
        var slips = await db.Payslips.AsNoTracking().Include(p => p.Lines).Include(p => p.Employee)
            .Where(p => p.PayrollRunId == id && p.NetPay > 0 && p.Employee!.PaymentMethod == PaymentMethod.BankTransfer)
            .OrderBy(p => p.Employee!.EmployeeCode).ToListAsync();
        var uae = run.PayrollCountry == PayrollRegime.Uae;
        var missing = slips.Where(p => string.IsNullOrWhiteSpace(p.Employee!.Iban) || string.IsNullOrWhiteSpace(uae ? p.Employee.LabourCardNo : p.Employee.IqamaNo))
            .Select(p => p.Employee!.EmployeeCode).ToList();
        if (missing.Count > 0)
            throw new DomainException($"Add the IBAN and {(uae ? "labour card (MOHRE person ID)" : "Iqama / national ID")} for: {string.Join(", ", missing.Take(20))}.");
        var lines = slips.Select(p =>
        {
            var earn = p.Lines.Where(l => l.Kind == SalaryComponentKind.Earning).ToList();
            var basic = earn.Where(l => l.Name == "Basic Salary").Sum(l => l.Amount);
            var housing = earn.Where(l => PayrollRegime.IsHousing(l.Name)).Sum(l => l.Amount);
            // SIF: fixed = regular salary after deductions; variable = this period's one-off pay (overtime, piece work, expenses).
            var oneOff = Math.Max(0, Math.Min(p.NetPay, earn.Where(l => l.Name.StartsWith("Overtime") || l.Name == "Expense reimbursement" || l.Name.StartsWith("Piece work")).Sum(l => l.Amount)));
            return new WpsLine(p.Employee!.EmployeeCode, p.Employee.FullName, uae ? p.Employee.LabourCardNo : p.Employee.IqamaNo, p.Employee.BankBranchCode,
                p.Employee.Iban, p.NetPay - oneOff, oneOff, p.NetPay, basic, housing, p.GrossEarnings - basic - housing, p.GrossEarnings - p.NetPay, (int)Math.Ceiling(p.UnpaidDays));
        }).ToList();
        var er = new WpsEmployer(s.WpsEmployerId, s.WpsBankRoutingCode, s.WpsEmployerIban, s.WpsAgentId, $"PAYROLL{run.PeriodEnd:yyyyMM}", run.Currency);
        var (name, content) = uae
            ? WpsFiles.UaeSif(er, lines, run.PeriodStart, run.PeriodEnd, await clock.LocalNowAsync())
            : WpsFiles.SaudiCsv(er, lines, run.PeriodEnd);
        return File(System.Text.Encoding.UTF8.GetBytes(content), uae ? "text/plain" : "text/csv", name);
    }

    /// <summary>Employees see payslips only once the month is finalized.</summary>
    [HttpGet("my-payslips")]
    public async Task<List<PayslipSummaryDto>> MyPayslips()
    {
        var mine = db.Payslips.Where(p => p.EmployeeId == me.EmployeeId && p.PayrollRun!.Status == PayrollRunStatus.Finalized);
        var slips = await mine.OrderByDescending(p => p.PayrollRun!.PeriodEnd).Select(SummaryDto()).ToListAsync();
        // Weekly and other non-monthly slips share a month, so each is labelled with its own period.
        var periods = await mine.Select(p => new { p.Id, p.PayrollRun!.Frequency, p.PayrollRun.PeriodStart, p.PayrollRun.PeriodEnd })
            .ToDictionaryAsync(x => x.Id);
        return slips.Select(s => periods.TryGetValue(s.Id, out var x) ? s with { Label = PayPeriods.Label(x.Frequency, x.PeriodStart, x.PeriodEnd) } : s).ToList();
    }

    [HttpGet("payslips/{id:guid}")]
    public async Task<PayslipDto> Payslip(Guid id)
    {
        // The Hr policy, not the role alone, so access templates decide who reads other people's payslips.
        var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var isHr = (await auth.AuthorizeAsync(User, HttpContext, Policies.Hr)).Succeeded;
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
            p.Lines.OrderBy(l => l.Kind).Select(l => new PayslipLineDto(l.Name, l.Kind, l.Amount)).ToList(),
            PayPeriods.Label(p.PayrollRun.Frequency, p.PayrollRun.PeriodStart, p.PayrollRun.PeriodEnd),
            p.PayrollRun.Currency, p.PayrollRun.PayrollCountry, p.SocialInsuranceEmployee, p.SocialInsuranceEmployer, e.Iban);
    }

    /// <summary>
    /// Explains a payslip in plain words with the free AI, compared with the person's previous finalized payslip. Same
    /// access as reading the payslip.
    /// </summary>
    [HttpPost("payslips/{id:guid}/explain")]
    public async Task<AiPayslipExplain> Explain(Guid id, [FromServices] AiAssistantService ai, [FromQuery] string? lang = null)
    {
        var slip = await Payslip(id);
        var cur = await db.Payslips.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.EmployeeId, x.PayrollRun!.PeriodEnd, x.PayrollRun.Frequency }).FirstAsync();
        var prevId = await db.Payslips.AsNoTracking()
            .Where(x => x.EmployeeId == cur.EmployeeId && x.PayrollRun!.Frequency == cur.Frequency && x.PayrollRun.PeriodEnd < cur.PeriodEnd
                        && x.PayrollRun.Status == PayrollRunStatus.Finalized)
            .OrderByDescending(x => x.PayrollRun!.PeriodEnd).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        var prev = prevId is { } pid ? await Payslip(pid) : null;
        return await ai.ExplainPayslipAsync(slip, prev, lang);
    }

    [HttpGet("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<PayrollSettingsDto> GetSettings()
    {
        var s = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();
        return new PayrollSettingsDto(s.EobiEmployeeContribution, s.EobiEmployerContribution, s.DeductAbsentDays, s.OvertimeMultiplier,
            s.LatesPerPenalty, s.LatePenaltyDays, s.DeductHalfDays, s.RequireApproval, s.EncashOnGross,
            s.ProvidentFundEnabled, s.PfEmployeePercent, s.PfEmployerPercent, s.PfOnGross, s.PfVestingYears, s.GratuityEnabled, s.GratuityDaysPerYear,
            s.GratuityMinYears, s.GratuityOnGross, s.SocialSecurityPercent, s.SocialSecurityWageCeiling, s.HolidayOvertimeMultiplier, s.OffDayOvertimeMultiplier,
            s.OvertimeMinMinutes, s.OvertimeRoundMinutes, s.VpsEnabled, s.VpsCreditCapPercent, s.PayWeekStartsOn, s.BiWeeklyAnchor,
            PayrollRegime.Normalize(s.PayrollCountry), s.Currency, s.GosiAnnuityEmployeePercent, s.GosiAnnuityEmployerPercent, s.GosiSanedEmployeePercent,
            s.GosiSanedEmployerPercent, s.GosiHazardEmployerPercent, s.GosiWageFloor, s.GosiWageCeiling, s.GosiNewEntrantStepPercent, s.GosiNewEntrantMaxPercent,
            s.PensionEmployeePercent, s.PensionEmployerPercent, s.PensionWageFloor, s.PensionWageCeiling, s.PensionOnGross, s.GulfGratuityWage,
            s.WpsEmployerId, s.WpsBankRoutingCode, s.WpsEmployerIban, s.WpsAgentId);
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
        s.OvertimeMultiplier = req.OvertimeMultiplier;
        s.LatesPerPenalty = req.LatesPerPenalty;
        s.LatePenaltyDays = req.LatePenaltyDays;
        s.DeductHalfDays = req.DeductHalfDays;
        s.RequireApproval = req.RequireApproval;
        s.EncashOnGross = req.EncashOnGross;
        s.ProvidentFundEnabled = req.ProvidentFundEnabled;
        s.PfEmployeePercent = req.PfEmployeePercent;
        s.PfEmployerPercent = req.PfEmployerPercent;
        s.PfOnGross = req.PfOnGross;
        s.PfVestingYears = req.PfVestingYears;
        s.GratuityEnabled = req.GratuityEnabled;
        s.GratuityDaysPerYear = req.GratuityDaysPerYear;
        s.GratuityMinYears = req.GratuityMinYears;
        s.GratuityOnGross = req.GratuityOnGross;
        s.SocialSecurityPercent = req.SocialSecurityPercent;
        s.SocialSecurityWageCeiling = req.SocialSecurityWageCeiling;
        s.HolidayOvertimeMultiplier = req.HolidayOvertimeMultiplier;
        s.OffDayOvertimeMultiplier = req.OffDayOvertimeMultiplier;
        s.OvertimeMinMinutes = req.OvertimeMinMinutes;
        s.OvertimeRoundMinutes = req.OvertimeRoundMinutes;
        s.VpsEnabled = req.VpsEnabled;
        s.VpsCreditCapPercent = req.VpsCreditCapPercent;
        s.PayWeekStartsOn = req.PayWeekStartsOn;
        s.BiWeeklyAnchor = req.BiWeeklyAnchor;
        // Country: changing it switches the currency to the country's own unless another one is chosen.
        var country = PayrollRegime.Normalize(req.PayrollCountry);
        if (country != PayrollRegime.Normalize(s.PayrollCountry) && (req.Currency is null || req.Currency == s.Currency))
            s.Currency = PayrollRegime.DefaultCurrency(country);
        else if (req.Currency is { } cur)
            s.Currency = cur;
        s.PayrollCountry = country;
        s.GosiAnnuityEmployeePercent = req.GosiAnnuityEmployeePercent;
        s.GosiAnnuityEmployerPercent = req.GosiAnnuityEmployerPercent;
        s.GosiSanedEmployeePercent = req.GosiSanedEmployeePercent;
        s.GosiSanedEmployerPercent = req.GosiSanedEmployerPercent;
        s.GosiHazardEmployerPercent = req.GosiHazardEmployerPercent;
        s.GosiWageFloor = req.GosiWageFloor;
        s.GosiWageCeiling = req.GosiWageCeiling;
        s.GosiNewEntrantStepPercent = req.GosiNewEntrantStepPercent;
        s.GosiNewEntrantMaxPercent = req.GosiNewEntrantMaxPercent;
        s.PensionEmployeePercent = req.PensionEmployeePercent;
        s.PensionEmployerPercent = req.PensionEmployerPercent;
        s.PensionWageFloor = req.PensionWageFloor;
        s.PensionWageCeiling = req.PensionWageCeiling;
        s.PensionOnGross = req.PensionOnGross;
        s.GulfGratuityWage = req.GulfGratuityWage;
        static string? T(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        s.WpsEmployerId = T(req.WpsEmployerId);
        s.WpsBankRoutingCode = T(req.WpsBankRoutingCode);
        s.WpsEmployerIban = T(req.WpsEmployerIban)?.Replace(" ", "").ToUpperInvariant();
        s.WpsAgentId = T(req.WpsAgentId);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<PayrollRunDetailDto> Detail(Guid id, IReadOnlyList<string> warnings)
    {
        var run = await db.PayrollRuns.Where(r => r.Id == id).Select(ToDto()).FirstOrDefaultAsync() ?? throw new NotFoundException("Payroll run");
        run = run with { Label = PayPeriods.Label(run.Frequency, run.PeriodStart, run.PeriodEnd) };
        var slips = await db.Payslips.Where(p => p.PayrollRunId == id).OrderBy(p => p.Employee!.EmployeeCode).Select(SummaryDto()).ToListAsync();
        var holds = await db.SalaryHolds.Where(h => h.Year == run.Year && h.Month == run.Month && h.ReleasedAtUtc == null)
            .ToDictionaryAsync(h => h.EmployeeId, h => h.Reason);
        slips = slips.Select(x => holds.TryGetValue(x.EmployeeId, out var why) ? x with { HeldReason = why } : x).ToList();
        return new PayrollRunDetailDto(run, slips, warnings);
    }

    private System.Linq.Expressions.Expression<Func<PayrollRun, PayrollRunDto>> ToDto() => r => new PayrollRunDto(
        r.Id, r.Year, r.Month, r.Status, r.TaxYearCode, r.ProcessedAtUtc, r.FinalizedAtUtc,
        r.Payslips.Count, r.TotalGross, r.TotalDeductions, r.TotalNet, r.SubmittedAtUtc, r.ApprovedByName, r.ApprovedAtUtc, r.ApprovalNote,
        r.DisbursedAtUtc, r.DisbursedVia, db.SalaryHolds.Count(h => h.Year == r.Year && h.Month == r.Month && h.ReleasedAtUtc == null),
        r.Frequency, r.PeriodStart, r.PeriodEnd, "", new RecordStampsDto(r.CreatedAtUtc, r.CreatedBy, r.ModifiedAtUtc, r.ModifiedBy), r.Currency, r.PayrollCountry);

    private static System.Linq.Expressions.Expression<Func<Payslip, PayslipSummaryDto>> SummaryDto() => p => new PayslipSummaryDto(
        p.Id, p.PayrollRun!.Year, p.PayrollRun.Month, p.PayrollRun.Status, p.EmployeeId, p.Employee!.EmployeeCode, p.Employee.FirstName + " " + p.Employee.LastName,
        p.Employee.Department != null ? p.Employee.Department.Name : null,
        p.PaidDays, p.GrossEarnings, p.IncomeTax, p.EobiEmployee, p.OtherDeductions, p.NetPay, null);
}
