using System.ComponentModel.DataAnnotations;
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

public record FundEntryDto(Guid Id, FundKind Fund, FundEntryKind Kind, DateOnly Date, decimal EmployeeAmount, decimal EmployerAmount, string? Note);

/// <summary>Provident fund and gratuity position for one person. Gratuity "accrued" is worked out from service and salary.</summary>
public record FundSummaryDto(Guid EmployeeId, string Name, string Code, bool PfMember, decimal PfEmployeeShare, decimal PfEmployerShare, decimal PfProfit,
    decimal PfWithdrawn, decimal PfBalance, decimal PfVested, decimal ServiceYears, decimal GratuityAccrued, decimal GratuityWithdrawn, decimal GratuityBalance);

public record SaveFundEntryRequest(Guid EmployeeId, FundKind Fund, FundEntryKind Kind, DateOnly Date, decimal EmployeeAmount, decimal EmployerAmount,
    [StringLength(300)] string? Note);

public record DistributeProfitRequest(DateOnly Date, [Range(0.01, 100)] decimal RatePercent, [StringLength(300)] string? Note);

public record FundWithdrawalDto(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, FundKind Fund, decimal Amount, string Reason, bool PayWithSalary,
    RequestStatus Status, DateTime CreatedAtUtc, string? DecisionNote);

public record RequestWithdrawalRequest(FundKind Fund, [Range(1, 100_000_000)] decimal Amount, [Required, StringLength(500)] string Reason, bool PayWithSalary = true,
    Guid? EmployeeId = null);

public record TaxAdjustmentDto(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string TaxYearCode, TaxAdjustmentKind Kind, decimal Amount, string? Note);
public record SaveTaxAdjustmentRequest(Guid EmployeeId, [Required, StringLength(20)] string TaxYearCode, TaxAdjustmentKind Kind, [Range(0, 1_000_000_000)] decimal Amount,
    [StringLength(300)] string? Note);

public record SalarySplitDto(Guid CostCenterId, [Range(0.01, 100)] decimal Percent);
public record SaveSalarySplitRequest(List<SalarySplitDto> Splits);

/// <summary>Provident fund and gratuity: balances, ledger, withdrawals; also tax adjustments and cost-center splits.</summary>
public class FundService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals)
{
    public async Task<List<FundSummaryDto>> SummariesAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly asOf)
    {
        var settings = await db.PayrollSettings.FirstOrDefaultAsync() ?? new PayrollSettings();
        var people = await db.Employees.Where(e => employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, e.JoiningDate, e.LeavingDate, e.ProvidentFundMember }).ToListAsync();
        var entries = (await db.FundEntries.Where(f => employeeIds.Contains(f.EmployeeId) && f.Date <= asOf).ToListAsync()).ToLookup(f => f.EmployeeId);
        var salaries = new Dictionary<Guid, (decimal Basic, decimal Gross)>();
        foreach (var p in people)
            if (await LetterService.CurrentSalaryAsync(db, p.Id, asOf) is { } s)
                salaries[p.Id] = (s.Basic, s.Gross);
        return people.Select(p =>
        {
            var mine = entries[p.Id].ToList();
            var pf = mine.Where(x => x.Fund == FundKind.ProvidentFund).ToList();
            decimal Pf(FundEntryKind k, bool employer) => pf.Where(x => x.Kind == k).Sum(x => employer ? x.EmployerAmount : x.EmployeeAmount);
            var employeeShare = Pf(FundEntryKind.Opening, false) + Pf(FundEntryKind.Contribution, false);
            var employerShare = Pf(FundEntryKind.Opening, true) + Pf(FundEntryKind.Contribution, true);
            var profit = pf.Where(x => x.Kind == FundEntryKind.Profit).Sum(x => x.EmployeeAmount + x.EmployerAmount);
            var withdrawn = -pf.Where(x => x.Kind is FundEntryKind.Withdrawal or FundEntryKind.Adjustment).Sum(x => x.EmployeeAmount + x.EmployerAmount);
            var end = p.LeavingDate is { } l && l < asOf ? l : asOf;
            var years = Math.Max(0, Math.Round((end.DayNumber - p.JoiningDate.DayNumber + 1) / 365.25m, 2));
            var vested = settings.PfVestingYears == 0 || years >= settings.PfVestingYears;
            var balance = employeeShare + employerShare + profit - withdrawn;
            var vestedBalance = vested ? balance : employeeShare + profit - withdrawn;
            var salary = salaries.GetValueOrDefault(p.Id);
            var gratuityBase = settings.GratuityOnGross ? salary.Gross : salary.Basic;
            var accrued = settings.GratuityEnabled && years >= settings.GratuityMinYears
                ? Math.Round(gratuityBase / 30m * settings.GratuityDaysPerYear * years, 0, MidpointRounding.AwayFromZero) : 0m;
            var gWithdrawn = -mine.Where(x => x.Fund == FundKind.Gratuity && x.Kind is FundEntryKind.Withdrawal or FundEntryKind.Adjustment).Sum(x => x.EmployeeAmount + x.EmployerAmount);
            var gOpening = mine.Where(x => x.Fund == FundKind.Gratuity && x.Kind is FundEntryKind.Opening or FundEntryKind.Contribution).Sum(x => x.EmployeeAmount + x.EmployerAmount);
            return new FundSummaryDto(p.Id, p.Name, p.EmployeeCode, p.ProvidentFundMember, employeeShare, employerShare, profit, withdrawn, balance,
                Math.Max(0, vestedBalance), years, accrued + gOpening, gWithdrawn, Math.Max(0, accrued + gOpening - gWithdrawn));
        }).OrderBy(x => x.Name).ToList();
    }

    public async Task<FundWithdrawal> RequestAsync(Guid employeeId, RequestWithdrawalRequest req)
    {
        var summary = (await SummariesAsync([employeeId], await clock.TodayAsync())).Single();
        var available = req.Fund == FundKind.ProvidentFund ? summary.PfVested : summary.GratuityBalance;
        if (req.Amount > available)
            throw new DomainException($"Only {available:N0} is available.");
        if (await db.FundWithdrawals.AnyAsync(w => w.EmployeeId == employeeId && w.Fund == req.Fund && w.Status == RequestStatus.Pending))
            throw new DomainException("A withdrawal from this fund is already waiting for approval.");
        var w = new FundWithdrawal { EmployeeId = employeeId, Fund = req.Fund, Amount = Math.Round(req.Amount, 0), Reason = req.Reason.Trim(), PayWithSalary = req.PayWithSalary };
        db.FundWithdrawals.Add(w);
        var approval = await approvals.StartAsync(ApprovalRequestType.FundWithdrawal, w.Id, employeeId,
            $"{(req.Fund == FundKind.ProvidentFund ? "Provident fund" : "Gratuity")} withdrawal PKR {w.Amount:N0} · {w.Reason}");
        if (approval.Status == ApprovalStatus.Approved)
            await ApplyAsync(w, "Approved automatically");
        await db.SaveChangesAsync();
        return w;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var w = await db.FundWithdrawals.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Withdrawal");
        if (w.Status != RequestStatus.Pending) throw new DomainException("This request is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.FundWithdrawal, w.Id, w.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
            await ApplyAsync(w, note);
        else if (outcome == ApprovalStatus.Rejected)
        {
            w.Status = RequestStatus.Rejected;
            w.DecidedAtUtc = clock.UtcNow;
            w.DecisionNote = note;
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid id)
    {
        var w = await db.FundWithdrawals.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == me.EmployeeId) ?? throw new NotFoundException("Withdrawal");
        if (w.Status != RequestStatus.Pending) throw new DomainException("Only pending requests can be cancelled.");
        await approvals.CancelAsync(ApprovalRequestType.FundWithdrawal, w.Id);
        w.Status = RequestStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Takes the money out of the ledger (employee share first for the provident fund) and, when asked, pays it with the
    /// current month's salary as a tax-free addition.
    /// </summary>
    private async Task ApplyAsync(FundWithdrawal w, string? note)
    {
        w.Status = RequestStatus.Approved;
        w.DecidedAtUtc = clock.UtcNow;
        w.DecisionNote = note;
        var today = await clock.TodayAsync();
        var label = w.Fund == FundKind.ProvidentFund ? "Provident fund withdrawal" : "Gratuity withdrawal";
        var summary = (await SummariesAsync([w.EmployeeId], today)).Single();
        var fromEmployee = w.Fund == FundKind.ProvidentFund ? Math.Min(w.Amount, Math.Max(0, summary.PfEmployeeShare + summary.PfProfit - summary.PfWithdrawn)) : w.Amount;
        db.FundEntries.Add(new FundEntry
        {
            EmployeeId = w.EmployeeId, Fund = w.Fund, Kind = FundEntryKind.Withdrawal, Date = today,
            EmployeeAmount = -fromEmployee, EmployerAmount = -(w.Amount - fromEmployee), Note = w.Reason,
        });
        if (w.PayWithSalary)
            db.PayRequests.Add(new PayRequest
            {
                EmployeeId = w.EmployeeId, Kind = PayRequestKind.Allowance, Title = label, Amount = w.Amount, IsTaxable = false,
                Year = today.Year, Month = today.Month, Reason = w.Reason, Status = RequestStatus.Approved, DecidedAtUtc = clock.UtcNow,
            });
    }
}

[ApiController]
[Route("api/funds")]
[Authorize(Policy = Policies.CompanyUser)]
public class FundsController(HrmsDbContext db, FundService funds, OnBehalfService onBehalf, ApprovalEngine approvals, TenantClock clock, ICurrentUser me) : ControllerBase
{
    [HttpGet("me")]
    public async Task<FundSummaryDto> Mine() =>
        (await funds.SummariesAsync([me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.")], await clock.TodayAsync())).Single();

    [HttpGet("summary"), Authorize(Policy = Policies.Hr)]
    public async Task<List<FundSummaryDto>> Summary([FromQuery] bool includeLeft = false)
    {
        var ids = await db.Employees.Where(e => includeLeft || e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation).Select(e => e.Id).ToListAsync();
        return await funds.SummariesAsync(ids, await clock.TodayAsync());
    }

    [HttpGet("ledger/{employeeId:guid}")]
    public async Task<List<FundEntryDto>> Ledger(Guid employeeId)
    {
        if (employeeId != me.EmployeeId && !me.IsHr()) throw new ForbiddenException("You can only see your own funds.");
        return await db.FundEntries.Where(f => f.EmployeeId == employeeId).OrderByDescending(f => f.Date).ThenByDescending(f => f.CreatedAtUtc)
            .Select(f => new FundEntryDto(f.Id, f.Fund, f.Kind, f.Date, f.EmployeeAmount, f.EmployerAmount, f.Note)).ToListAsync();
    }

    /// <summary>Opening balances (moving from another system) and corrections.</summary>
    [HttpPost("entries"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddEntry(SaveFundEntryRequest req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == req.EmployeeId)) throw new NotFoundException("Employee");
        if (req.Kind is not (FundEntryKind.Opening or FundEntryKind.Adjustment or FundEntryKind.Profit))
            throw new DomainException("Add an opening balance, profit or correction here; contributions come from payroll.");
        if (req.EmployeeAmount == 0 && req.EmployerAmount == 0) throw new DomainException("Enter an amount.");
        db.FundEntries.Add(new FundEntry
        {
            EmployeeId = req.EmployeeId, Fund = req.Fund, Kind = req.Kind, Date = req.Date, EmployeeAmount = req.EmployeeAmount, EmployerAmount = req.EmployerAmount,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
        });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("entries/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteEntry(Guid id)
    {
        var f = await db.FundEntries.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Entry");
        if (f.Kind is FundEntryKind.Contribution or FundEntryKind.Withdrawal) throw new DomainException("Payroll contributions and approved withdrawals stay on the ledger.");
        db.FundEntries.Remove(f);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Shares the fund's profit for a period: each member gets the rate on their balance, split by share.</summary>
    [HttpPost("profit"), Authorize(Policy = Policies.Hr)]
    public async Task<object> DistributeProfit(DistributeProfitRequest req)
    {
        var ids = await db.FundEntries.Where(f => f.Fund == FundKind.ProvidentFund).Select(f => f.EmployeeId).Distinct().ToListAsync();
        var count = 0;
        decimal total = 0;
        foreach (var s in await funds.SummariesAsync(ids, req.Date))
        {
            if (s.PfBalance <= 0) continue;
            var employeePart = Math.Round((s.PfEmployeeShare + s.PfProfit / 2) * req.RatePercent / 100m, 0);
            var employerPart = Math.Round((s.PfEmployerShare + s.PfProfit / 2) * req.RatePercent / 100m, 0);
            if (employeePart + employerPart <= 0) continue;
            db.FundEntries.Add(new FundEntry
            {
                EmployeeId = s.EmployeeId, Fund = FundKind.ProvidentFund, Kind = FundEntryKind.Profit, Date = req.Date, EmployeeAmount = employeePart,
                EmployerAmount = employerPart, Note = req.Note?.Trim() ?? $"Profit {req.RatePercent:0.##}%",
            });
            count++;
            total += employeePart + employerPart;
        }
        await db.SaveChangesAsync();
        return new { members = count, total };
    }

    // ---------- Withdrawals ----------

    [HttpGet("withdrawals/my")]
    public Task<List<FundWithdrawalDto>> MyWithdrawals() => ProjectAsync(db.FundWithdrawals.Where(w => w.EmployeeId == me.EmployeeId));

    [HttpPost("withdrawals")]
    public async Task<IActionResult> RequestWithdrawal(RequestWithdrawalRequest req)
    {
        var employeeId = await onBehalf.ResolveAsync(req.EmployeeId);
        await funds.RequestAsync(employeeId, req);
        return NoContent();
    }

    [HttpPost("withdrawals/{id:guid}/cancel")]
    public async Task<IActionResult> CancelWithdrawal(Guid id)
    {
        await funds.CancelAsync(id);
        return NoContent();
    }

    [HttpPost("withdrawals/{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, ExemptionDecisionRequest? req)
    {
        await funds.DecideAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("withdrawals/{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, ExemptionDecisionRequest? req)
    {
        await funds.DecideAsync(id, false, req?.Note);
        return NoContent();
    }

    [HttpGet("withdrawals"), Authorize(Policy = Policies.Approver)]
    public async Task<List<FundWithdrawalDto>> Withdrawals([FromQuery] RequestStatus? status)
    {
        var q = db.FundWithdrawals.AsQueryable();
        if (status is { } s) q = q.Where(w => w.Status == s);
        if (!me.IsHr())
        {
            var waiting = await approvals.WaitingForMeAsync(ApprovalRequestType.FundWithdrawal);
            q = q.Where(w => waiting.Contains(w.Id));
        }
        return await ProjectAsync(q);
    }

    private Task<List<FundWithdrawalDto>> ProjectAsync(IQueryable<FundWithdrawal> q) =>
        q.OrderByDescending(w => w.CreatedAtUtc).Take(300)
            .Select(w => new FundWithdrawalDto(w.Id, w.EmployeeId, w.Employee!.FirstName + " " + w.Employee.LastName, w.Employee.EmployeeCode, w.Fund, w.Amount,
                w.Reason, w.PayWithSalary, w.Status, w.CreatedAtUtc, w.DecisionNote)).ToListAsync();

    // ---------- Tax adjustments ----------

    [HttpGet("tax-adjustments"), Authorize(Policy = Policies.Hr)]
    public Task<List<TaxAdjustmentDto>> TaxAdjustments([FromQuery] string? taxYear) =>
        db.TaxAdjustments.Where(t => taxYear == null || t.TaxYearCode == taxYear).OrderBy(t => t.TaxYearCode)
            .Join(db.Employees, t => t.EmployeeId, e => e.Id, (t, e) => new TaxAdjustmentDto(t.Id, t.EmployeeId, e.FirstName + " " + e.LastName, e.EmployeeCode,
                t.TaxYearCode, t.Kind, t.Amount, t.Note))
            .ToListAsync();

    [HttpGet("tax-years")]
    public Task<List<string>> TaxYears() => db.TaxYears.OrderByDescending(t => t.StartDate).Select(t => t.Code).ToListAsync();

    [HttpPost("tax-adjustments"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddTaxAdjustment(SaveTaxAdjustmentRequest req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == req.EmployeeId)) throw new NotFoundException("Employee");
        if (!await db.TaxYears.AnyAsync(t => t.Code == req.TaxYearCode)) throw new DomainException("Choose a tax year from the list.");
        if (!Enum.IsDefined(req.Kind)) throw new DomainException("Choose the kind of adjustment.");
        db.TaxAdjustments.Add(new TaxAdjustment
        {
            EmployeeId = req.EmployeeId, TaxYearCode = req.TaxYearCode, Kind = req.Kind, Amount = Math.Round(req.Amount, 0),
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
        });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("tax-adjustments/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteTaxAdjustment(Guid id)
    {
        db.TaxAdjustments.Remove(await db.TaxAdjustments.FirstOrDefaultAsync(t => t.Id == id) ?? throw new NotFoundException("Adjustment"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Cost center split ----------

    [HttpGet("splits/{employeeId:guid}"), Authorize(Policy = Policies.Hr)]
    public Task<List<SalarySplitDto>> Splits(Guid employeeId) =>
        db.SalarySplits.Where(s => s.EmployeeId == employeeId).Select(s => new SalarySplitDto(s.CostCenterId, s.Percent)).ToListAsync();

    [HttpPut("splits/{employeeId:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveSplits(Guid employeeId, SaveSalarySplitRequest req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId)) throw new NotFoundException("Employee");
        var splits = (req.Splits ?? []).Where(s => s.Percent > 0).ToList();
        if (splits.Count > 0 && Math.Abs(splits.Sum(s => s.Percent) - 100) > 0.01m) throw new DomainException("The shares must add up to 100%.");
        if (splits.Select(s => s.CostCenterId).Distinct().Count() != splits.Count) throw new DomainException("Each cost center once.");
        var ids = splits.Select(s => s.CostCenterId).ToList();
        if (await db.OrgLookups.CountAsync(l => ids.Contains(l.Id) && l.Kind == LookupKind.CostCenter) != ids.Count)
            throw new DomainException("Choose cost centers from the list.");
        db.SalarySplits.RemoveRange(await db.SalarySplits.Where(s => s.EmployeeId == employeeId).ToListAsync());
        foreach (var s in splits)
            db.SalarySplits.Add(new SalarySplit { EmployeeId = employeeId, CostCenterId = s.CostCenterId, Percent = s.Percent });
        await db.SaveChangesAsync();
        return NoContent();
    }
}
