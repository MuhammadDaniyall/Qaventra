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

public record RevisionLine(Guid EmployeeId, decimal? Percent, decimal? Amount);

public record ReviseSalariesRequest(DateOnly EffectiveFrom, SalaryRevisionKind Kind, bool IncludeAllowances, [Required] List<RevisionLine> Lines, [StringLength(500)] string? Reason);

public record RevisionPreviewDto(Guid EmployeeId, string Name, string Code, decimal OldBasic, decimal NewBasic, decimal OldGross, decimal NewGross, decimal ChangePercent, string? Problem);

public record SalaryRevisionDto(Guid Id, Guid EmployeeId, string Name, string Code, DateOnly EffectiveFrom, SalaryRevisionKind Kind, decimal OldBasic, decimal NewBasic,
    decimal OldGross, decimal NewGross, string? Reason, string? ByName, DateTime CreatedAtUtc);

public record RecurringItemDto(Guid Id, Guid EmployeeId, string Name, string Code, string Title, bool IsEarning, decimal Amount, bool IsTaxable, int StartYear, int StartMonth,
    int? EndYear, int? EndMonth, string? Notes, bool Active);

public record SaveRecurringItemRequest(Guid EmployeeId, [Required, StringLength(150)] string Title, bool IsEarning, decimal Amount, bool IsTaxable, int StartYear, int StartMonth,
    int? EndYear, int? EndMonth, [StringLength(500)] string? Notes);

public record SalaryHoldDto(Guid Id, Guid EmployeeId, string Name, string Code, int Year, int Month, string Reason, string? HeldByName, DateTime CreatedAtUtc,
    DateTime? ReleasedAtUtc, string? ReleasedByName);

public record HoldRequest(Guid EmployeeId, int Year, int Month, [Required, StringLength(500)] string Reason);

public record EncashRequest(Guid EmployeeId, Guid LeaveTypeId, decimal Days, [StringLength(500)] string? Note);

public record EncashQuoteDto(decimal Remaining, decimal DayRate, decimal Amount, string Basis);

/// <summary>
/// HR's changes to pay outside the monthly run: salary revisions (increments, one or many people, preview first),
/// allowances and deductions that repeat monthly, holding a month's salary, and paying out unused leave.
/// </summary>
[ApiController]
[Route("api/pay-changes")]
[Authorize(Policy = Policies.Hr)]
public class PayChangesController(HrmsDbContext db, TenantClock clock, ICurrentUser me, NotificationService notify, LeaveService leave) : ControllerBase
{
    // ---------- Salary revisions ----------

    [HttpPost("revisions/preview")]
    public async Task<List<RevisionPreviewDto>> Preview(ReviseSalariesRequest req) => (await PlanAsync(req)).Select(x => x.Preview).ToList();

    /// <summary>Applies the revisions: a new salary from the effective date for each person, and the history line.</summary>
    [HttpPost("revisions")]
    public async Task<object> Revise(ReviseSalariesRequest req)
    {
        var plan = await PlanAsync(req);
        if (plan.Any(p => p.Preview.Problem is not null))
            throw new DomainException(string.Join(" ", plan.Where(p => p.Preview.Problem is not null).Select(p => $"{p.Preview.Name}: {p.Preview.Problem}")));
        var by = await LetterService.CurrentUserNameAsync(db, me);
        foreach (var (preview, current, factor) in plan)
        {
            var existing = await db.EmployeeSalaries.Include(x => x.Components).FirstOrDefaultAsync(x => x.EmployeeId == preview.EmployeeId && x.EffectiveFrom == req.EffectiveFrom);
            var target = existing ?? new EmployeeSalary { EmployeeId = preview.EmployeeId, EffectiveFrom = req.EffectiveFrom, EobiApplicable = current.EobiApplicable };
            if (existing is null)
                db.EmployeeSalaries.Add(target);
            else
                target.Components.Clear();
            target.BasicSalary = preview.NewBasic;
            foreach (var c in current.Components)
                target.Components.Add(new SalaryComponent
                {
                    Name = c.Name, Kind = c.Kind, IsTaxable = c.IsTaxable,
                    Amount = req.IncludeAllowances && c.Kind == SalaryComponentKind.Earning ? Math.Round(c.Amount * factor, 0) : c.Amount,
                });
            db.SalaryRevisions.Add(new SalaryRevision
            {
                EmployeeId = preview.EmployeeId, EffectiveFrom = req.EffectiveFrom, Kind = req.Kind, OldBasic = preview.OldBasic, NewBasic = preview.NewBasic,
                OldGross = preview.OldGross, NewGross = preview.NewGross, Reason = Clean(req.Reason), ByName = by,
            });
            var verb = preview.NewGross >= preview.OldGross ? "increased" : "changed";
            await notify.ToEmployeesAsync([preview.EmployeeId], NotificationKind.Payslip, $"Your salary is {verb} from {req.EffectiveFrom:d MMM yyyy}",
                $"Monthly gross PKR {preview.OldGross:N0} → PKR {preview.NewGross:N0}" + (Clean(req.Reason) is { } r ? $" · {r}" : ""), "/me/payslips");
        }
        await db.SaveChangesAsync();
        return new { revised = plan.Count };
    }

    [HttpGet("revisions")]
    public async Task<List<SalaryRevisionDto>> Revisions([FromQuery] Guid? employeeId) =>
        await db.SalaryRevisions.Where(r => employeeId == null || r.EmployeeId == employeeId).OrderByDescending(r => r.EffectiveFrom).ThenBy(r => r.Employee!.FirstName).Take(500)
            .Select(r => new SalaryRevisionDto(r.Id, r.EmployeeId, r.Employee!.FirstName + " " + r.Employee.LastName, r.Employee.EmployeeCode, r.EffectiveFrom, r.Kind,
                r.OldBasic, r.NewBasic, r.OldGross, r.NewGross, r.Reason, r.ByName, r.CreatedAtUtc)).ToListAsync();

    private async Task<List<(RevisionPreviewDto Preview, EmployeeSalary Current, decimal Factor)>> PlanAsync(ReviseSalariesRequest req)
    {
        if (!Enum.IsDefined(req.Kind))
            throw new DomainException("Choose the kind of change.");
        if (req.Lines.Count is 0 or > 1000)
            throw new DomainException("Choose 1 to 1,000 people.");
        if (req.Lines.Select(l => l.EmployeeId).Distinct().Count() != req.Lines.Count)
            throw new DomainException("Each person appears once.");
        var today = await clock.TodayAsync();
        if (req.EffectiveFrom < today.AddMonths(-6) || req.EffectiveFrom > today.AddMonths(12))
            throw new DomainException("The effective date must be within six months back and a year ahead.");
        var finalized = await db.PayrollRuns.AnyAsync(r => r.Status == PayrollRunStatus.Finalized && r.Year * 12 + r.Month >= req.EffectiveFrom.Year * 12 + req.EffectiveFrom.Month);
        var ids = req.Lines.Select(l => l.EmployeeId).ToList();
        var people = await db.Employees.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        var salaries = (await db.EmployeeSalaries.Include(s => s.Components).Where(s => ids.Contains(s.EmployeeId) && s.EffectiveFrom <= req.EffectiveFrom).ToListAsync())
            .GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First());
        var result = new List<(RevisionPreviewDto, EmployeeSalary, decimal)>();
        foreach (var l in req.Lines)
        {
            if (!people.TryGetValue(l.EmployeeId, out var e))
                throw new NotFoundException("Employee");
            string? problem = null;
            if (!salaries.TryGetValue(l.EmployeeId, out var cur))
            {
                problem = "has no salary before this date";
                cur = new EmployeeSalary { EmployeeId = e.Id };
            }
            var oldGross = Gross(cur);
            if (l.Percent is null == l.Amount is null)
                problem ??= "give either a percentage or an amount";
            decimal newBasic;
            decimal factor;
            if (l.Percent is { } pct)
            {
                if (pct is < -50 or > 200)
                    problem ??= "the percentage looks wrong";
                factor = 1 + pct / 100m;
                newBasic = Math.Round(cur.BasicSalary * factor, 0);
            }
            else
            {
                // A fixed amount goes on the basic; allowances stay as they are.
                newBasic = cur.BasicSalary + (l.Amount ?? 0);
                factor = 1;
            }
            if (newBasic <= 0)
                problem ??= "the new basic must be above zero";
            if (!e.IsCurrent)
                problem ??= "no longer works here";
            if (finalized)
                problem ??= "a finalized payroll already covers this month; choose a later date";
            var newGross = newBasic + cur.Components.Where(c => c.Kind == SalaryComponentKind.Earning)
                .Sum(c => req.IncludeAllowances ? Math.Round(c.Amount * factor, 0) : c.Amount);
            var change = oldGross == 0 ? 0 : Math.Round((newGross - oldGross) / oldGross * 100, 1);
            result.Add((new RevisionPreviewDto(e.Id, e.FullName, e.EmployeeCode, cur.BasicSalary, newBasic, oldGross, newGross, change, problem), cur, factor));
        }
        return result;
    }

    private static decimal Gross(EmployeeSalary s) => s.BasicSalary + s.Components.Where(c => c.Kind == SalaryComponentKind.Earning).Sum(c => c.Amount);

    // ---------- Recurring allowances and deductions ----------

    [HttpGet("recurring")]
    public async Task<List<RecurringItemDto>> Recurring()
    {
        var today = await clock.TodayAsync();
        var period = today.Year * 12 + today.Month;
        return await db.RecurringPayItems.OrderBy(r => r.Employee!.FirstName).ThenBy(r => r.Title)
            .Select(r => new RecurringItemDto(r.Id, r.EmployeeId, r.Employee!.FirstName + " " + r.Employee.LastName, r.Employee.EmployeeCode, r.Title, r.IsEarning, r.Amount,
                r.IsTaxable, r.StartYear, r.StartMonth, r.EndYear, r.EndMonth, r.Notes,
                r.StartYear * 12 + r.StartMonth <= period && (r.EndYear == null || r.EndYear * 12 + r.EndMonth >= period)))
            .ToListAsync();
    }

    [HttpPost("recurring")]
    public async Task<IActionResult> AddRecurring(SaveRecurringItemRequest req)
    {
        var r = new RecurringPayItem();
        await ApplyAsync(r, req);
        db.RecurringPayItems.Add(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("recurring/{id:guid}")]
    public async Task<IActionResult> UpdateRecurring(Guid id, SaveRecurringItemRequest req)
    {
        var r = await db.RecurringPayItems.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Item");
        await ApplyAsync(r, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Removes an item never paid; one already paid is stopped after last month instead, to keep the history.</summary>
    [HttpDelete("recurring/{id:guid}")]
    public async Task<IActionResult> DeleteRecurring(Guid id)
    {
        var r = await db.RecurringPayItems.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Item");
        var today = await clock.TodayAsync();
        if (r.StartYear * 12 + r.StartMonth >= today.Year * 12 + today.Month)
            db.RecurringPayItems.Remove(r);
        else
        {
            var last = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            r.EndYear = last.Year;
            r.EndMonth = last.Month;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(RecurringPayItem r, SaveRecurringItemRequest req)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == req.EmployeeId))
            throw new NotFoundException("Employee");
        if (req.Amount <= 0)
            throw new DomainException("Enter the monthly amount.");
        if (req.StartMonth is < 1 or > 12 || req.EndMonth is < 1 or > 12 || (req.EndYear is null) != (req.EndMonth is null))
            throw new DomainException("Check the months.");
        if (req.EndYear is { } ey && ey * 12 + req.EndMonth!.Value < req.StartYear * 12 + req.StartMonth)
            throw new DomainException("It ends before it starts.");
        r.EmployeeId = req.EmployeeId;
        r.Title = req.Title.Trim();
        r.IsEarning = req.IsEarning;
        r.Amount = req.Amount;
        r.IsTaxable = req.IsEarning && req.IsTaxable;
        r.StartYear = req.StartYear;
        r.StartMonth = req.StartMonth;
        r.EndYear = req.EndYear;
        r.EndMonth = req.EndMonth;
        r.Notes = Clean(req.Notes);
    }

    // ---------- Salary hold ----------

    [HttpGet("holds")]
    public async Task<List<SalaryHoldDto>> Holds() =>
        await db.SalaryHolds.OrderBy(h => h.ReleasedAtUtc != null).ThenByDescending(h => h.Year * 12 + h.Month).Take(300)
            .Select(h => new SalaryHoldDto(h.Id, h.EmployeeId, h.Employee!.FirstName + " " + h.Employee.LastName, h.Employee.EmployeeCode, h.Year, h.Month, h.Reason,
                h.HeldByName, h.CreatedAtUtc, h.ReleasedAtUtc, h.ReleasedByName)).ToListAsync();

    /// <summary>Holds someone's pay for a month: left out of the bank advice until released.</summary>
    [HttpPost("holds")]
    public async Task<IActionResult> Hold(HoldRequest req)
    {
        if (req.Month is < 1 or > 12 || req.Year is < 2000 or > 2100)
            throw new DomainException("Check the month.");
        if (!await db.Employees.AnyAsync(e => e.Id == req.EmployeeId))
            throw new NotFoundException("Employee");
        if (await db.PayrollRuns.AnyAsync(r => r.Year == req.Year && r.Month == req.Month && r.DisbursedAtUtc != null))
            throw new DomainException("This month's salaries are already paid out.");
        var h = await db.SalaryHolds.FirstOrDefaultAsync(x => x.EmployeeId == req.EmployeeId && x.Year == req.Year && x.Month == req.Month);
        if (h is { ReleasedAtUtc: null })
            throw new DomainException("This salary is already on hold.");
        if (h is null)
        {
            h = new SalaryHold { EmployeeId = req.EmployeeId, Year = req.Year, Month = req.Month };
            db.SalaryHolds.Add(h);
        }
        h.Reason = req.Reason.Trim();
        h.HeldByName = await LetterService.CurrentUserNameAsync(db, me);
        h.ReleasedAtUtc = null;
        h.ReleasedByName = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("holds/{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id)
    {
        var h = await db.SalaryHolds.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Hold");
        if (h.ReleasedAtUtc is not null)
            return NoContent();
        h.ReleasedAtUtc = clock.UtcNow;
        h.ReleasedByName = await LetterService.CurrentUserNameAsync(db, me);
        var month = new DateOnly(h.Year, h.Month, 1);
        await notify.ToEmployeesAsync([h.EmployeeId], NotificationKind.Payslip, $"Your salary for {month:MMMM yyyy} is released", null, "/me/payslips");
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Leave encashment ----------

    [HttpGet("encash/quote")]
    public async Task<EncashQuoteDto> Quote([FromQuery] Guid employeeId, [FromQuery] Guid leaveTypeId, [FromQuery] decimal days)
    {
        var (balance, rate, basis) = await EncashBasisAsync(employeeId, leaveTypeId);
        return new EncashQuoteDto(balance.Remaining, Math.Round(rate, 2), Math.Round(rate * Math.Max(0, days), 0), basis);
    }

    /// <summary>Pays unused leave: the days come off this year's balance and the amount is paid with this month's salary.</summary>
    [HttpPost("encash")]
    public async Task<object> Encash(EncashRequest req)
    {
        if (req.Days <= 0 || req.Days % 0.5m != 0)
            throw new DomainException("Encash whole or half days.");
        var (balance, rate, basis) = await EncashBasisAsync(req.EmployeeId, req.LeaveTypeId);
        if (req.Days > balance.Remaining)
            throw new DomainException($"Only {balance.Remaining:0.#} days are left.");
        var today = await clock.TodayAsync();
        var type = await db.LeaveTypes.Where(t => t.Id == req.LeaveTypeId).Select(t => t.Name).FirstAsync();
        var amount = Math.Round(rate * req.Days, 0);
        balance.Adjustment -= req.Days;
        balance.AdjustmentNote = $"Encashed {req.Days:0.#} days on {today:d MMM yyyy}" + (Clean(req.Note) is { } n ? $": {n}" : "");
        db.PayRequests.Add(new PayRequest
        {
            EmployeeId = req.EmployeeId, Kind = PayRequestKind.Allowance, Title = $"Leave encashment: {req.Days:0.#} days {type}", Amount = amount, IsTaxable = true,
            Year = today.Year, Month = today.Month, Reason = $"{req.Days:0.#} days × PKR {rate:N0} ({basis})", Status = RequestStatus.Approved, DecidedAtUtc = clock.UtcNow,
            DecisionNote = Clean(req.Note) ?? "Leave encashment",
        });
        await notify.ToEmployeesAsync([req.EmployeeId], NotificationKind.Payslip, $"{req.Days:0.#} days of {type} encashed",
            $"PKR {amount:N0} with your {today:MMMM} salary.", "/me/leave");
        await db.SaveChangesAsync();
        return new { amount };
    }

    private async Task<(LeaveBalance Balance, decimal Rate, string Basis)> EncashBasisAsync(Guid employeeId, Guid leaveTypeId)
    {
        var today = await clock.TodayAsync();
        await leave.EnsureBalancesAsync(employeeId, today.Year);
        var balance = await db.LeaveBalances.Include(b => b.LeaveType).FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == today.Year)
                      ?? throw new DomainException("No leave balance for this year.");
        if (!balance.LeaveType!.IsPaid)
            throw new DomainException("Unpaid leave cannot be encashed.");
        var salary = await db.EmployeeSalaries.Include(s => s.Components).Where(s => s.EmployeeId == employeeId && s.EffectiveFrom <= today)
                         .OrderByDescending(s => s.EffectiveFrom).FirstOrDefaultAsync() ?? throw new DomainException("This person has no salary set.");
        var onGross = (await db.PayrollSettings.FirstOrDefaultAsync())?.EncashOnGross ?? false;
        var monthly = onGross ? Gross(salary) : salary.BasicSalary;
        return (balance, monthly / 30m, onGross ? "gross ÷ 30" : "basic ÷ 30");
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
