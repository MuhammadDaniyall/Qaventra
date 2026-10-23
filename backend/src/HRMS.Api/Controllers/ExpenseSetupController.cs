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

public record ExpenseGradeRateDto(Guid GradeId, [Range(0, 10_000_000)] decimal? Rate, [Range(0, 100_000_000)] decimal? YearlyLimit);

public record ExpenseTypeDto(Guid Id, string Name, ExpenseCategory Category, ExpenseCalculation Calculation, string? Unit, decimal? Rate, decimal? PerClaimLimit,
    decimal? YearlyLimit, bool ReceiptRequired, List<Guid> GradeIds, bool IsActive, List<ExpenseGradeRateDto> GradeRates);

public record SaveExpenseTypeRequest([Required, StringLength(150)] string Name, ExpenseCategory Category, ExpenseCalculation Calculation, [StringLength(30)] string? Unit,
    [Range(0, 10_000_000)] decimal? Rate, [Range(0, 100_000_000)] decimal? PerClaimLimit, [Range(0, 100_000_000)] decimal? YearlyLimit, bool ReceiptRequired,
    List<Guid>? GradeIds, bool IsActive, List<ExpenseGradeRateDto>? GradeRates);

/// <summary>What a person may still claim this year for a type, at their rate.</summary>
public record ExpenseTypeForMeDto(Guid Id, string Name, ExpenseCategory Category, ExpenseCalculation Calculation, string? Unit, decimal? Rate, decimal? PerClaimLimit,
    decimal? YearlyLimit, decimal UsedThisYear, bool ReceiptRequired);

public record DistanceRouteDto(Guid Id, string FromCity, string ToCity, decimal Km);
public record SaveDistanceRouteRequest([Required, StringLength(100)] string FromCity, [Required, StringLength(100)] string ToCity, [Range(0.1, 10_000)] decimal Km);

public record ExpenseAdvanceDto(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string Purpose, decimal Amount, RequestStatus Status,
    decimal Adjusted, decimal Outstanding, DateTime? ClosedAtUtc, decimal Recovered, DateTime CreatedAtUtc, string? DecisionNote);

public record RequestAdvanceRequest([Required, StringLength(300)] string Purpose, [Range(1, 10_000_000)] decimal Amount, Guid? EmployeeId = null);

/// <summary>Expense advances: request, approve (paid with the next salary), set claims against, close (rest recovered).</summary>
public class ExpenseAdvanceService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals)
{
    public async Task<ExpenseAdvance> RequestAsync(Guid employeeId, RequestAdvanceRequest req)
    {
        if (await db.ExpenseAdvances.AnyAsync(a => a.EmployeeId == employeeId && a.Status == RequestStatus.Pending))
            throw new DomainException("An advance is already waiting for approval.");
        var a = new ExpenseAdvance { EmployeeId = employeeId, Purpose = req.Purpose.Trim(), Amount = Math.Round(req.Amount, 0) };
        db.ExpenseAdvances.Add(a);
        var approval = await approvals.StartAsync(ApprovalRequestType.ExpenseAdvance, a.Id, employeeId, $"Expense advance Rs {a.Amount:N0} · {a.Purpose}");
        if (approval.Status == ApprovalStatus.Approved)
            await ApplyAsync(a, "Approved automatically");
        await db.SaveChangesAsync();
        return a;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var a = await db.ExpenseAdvances.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Advance");
        if (a.Status != RequestStatus.Pending) throw new DomainException("This advance is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.ExpenseAdvance, a.Id, a.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
            await ApplyAsync(a, note);
        else if (outcome == ApprovalStatus.Rejected)
        {
            a.Status = RequestStatus.Rejected;
            a.DecidedAtUtc = clock.UtcNow;
            a.DecisionNote = note;
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid id)
    {
        var a = await db.ExpenseAdvances.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == me.EmployeeId) ?? throw new NotFoundException("Advance");
        if (a.Status != RequestStatus.Pending) throw new DomainException("Only pending requests can be cancelled.");
        await approvals.CancelAsync(ApprovalRequestType.ExpenseAdvance, a.Id);
        a.Status = RequestStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    /// <summary>Approved: paid with this month's salary as a tax-free addition.</summary>
    private async Task ApplyAsync(ExpenseAdvance a, string? note)
    {
        a.Status = RequestStatus.Approved;
        a.DecidedAtUtc = clock.UtcNow;
        a.DecisionNote = note;
        var today = await clock.TodayAsync();
        db.PayRequests.Add(new PayRequest
        {
            EmployeeId = a.EmployeeId, Kind = PayRequestKind.Allowance, Title = "Expense advance", Amount = a.Amount, IsTaxable = false,
            Year = today.Year, Month = today.Month, Reason = a.Purpose, Status = RequestStatus.Approved, DecidedAtUtc = clock.UtcNow,
        });
    }

    /// <summary>No more claims against it; whatever was not spent is recovered from the next salary.</summary>
    public async Task CloseAsync(Guid id)
    {
        var a = await db.ExpenseAdvances.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Advance");
        if (a.Status != RequestStatus.Approved || a.ClosedAtUtc is not null) throw new DomainException("Only an open approved advance can be closed.");
        if (await db.ExpenseClaims.AnyAsync(c => c.AdvanceId == id && c.Status == ExpenseStatus.Pending))
            throw new DomainException("A claim against this advance is still waiting for approval.");
        var rest = a.Amount - a.Adjusted;
        a.ClosedAtUtc = clock.UtcNow;
        a.Recovered = Math.Max(0, rest);
        if (rest > 0)
        {
            var today = await clock.TodayAsync();
            db.PayRequests.Add(new PayRequest
            {
                EmployeeId = a.EmployeeId, Kind = PayRequestKind.Deduction, Title = "Unspent expense advance", Amount = rest, IsTaxable = false,
                Year = today.Year, Month = today.Month, Reason = a.Purpose, Status = RequestStatus.Approved, DecidedAtUtc = clock.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }
}

[ApiController]
[Route("api/expense-setup")]
[Authorize(Policy = Policies.CompanyUser)]
public class ExpenseSetupController(HrmsDbContext db, ExpenseAdvanceService advances, OnBehalfService onBehalf, ApprovalEngine approvals, TenantClock clock, ICurrentUser me)
    : ControllerBase
{
    // ---------- Types ----------

    [HttpGet("types"), Authorize(Policy = Policies.Hr)]
    public async Task<List<ExpenseTypeDto>> Types() =>
        (await db.ExpenseTypes.Include(t => t.GradeRates).OrderBy(t => t.Name).ToListAsync()).Select(ToDto).ToList();

    private static ExpenseTypeDto ToDto(ExpenseType t) => new(t.Id, t.Name, t.Category, t.Calculation, t.Unit, t.Rate, t.PerClaimLimit, t.YearlyLimit, t.ReceiptRequired,
        t.GradeIds is { } g ? HelpdeskService.Ids(g) : [], t.IsActive, t.GradeRates.Select(r => new ExpenseGradeRateDto(r.GradeId, r.Rate, r.YearlyLimit)).ToList());

    /// <summary>Types the person (or someone they file for) may claim, with their rate and what is left this year.</summary>
    [HttpGet("types/for")]
    public async Task<List<ExpenseTypeForMeDto>> TypesFor([FromQuery] Guid? employeeId)
    {
        var id = await onBehalf.ResolveAsync(employeeId);
        var grade = await db.Employees.Where(e => e.Id == id).Select(e => e.GradeId).FirstAsync();
        var year = (await clock.TodayAsync()).Year;
        var start = new DateOnly(year, 1, 1);
        var used = await db.ExpenseClaims.Where(c => c.EmployeeId == id && (c.Status == ExpenseStatus.Pending || c.Status == ExpenseStatus.Approved || c.Status == ExpenseStatus.Paid))
            .SelectMany(c => c.Items).Where(i => i.ExpenseTypeId != null && i.Date >= start)
            .GroupBy(i => i.ExpenseTypeId!.Value).Select(g => new { g.Key, Sum = g.Sum(i => i.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        return (await db.ExpenseTypes.Include(t => t.GradeRates).Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync())
            .Where(t => t.GradeIds is not { Length: > 0 } g || (grade is { } gr && HelpdeskService.Has(g, gr)))
            .Select(t =>
            {
                var own = t.GradeRates.FirstOrDefault(r => r.GradeId == grade);
                return new ExpenseTypeForMeDto(t.Id, t.Name, t.Category, t.Calculation, t.Unit, own?.Rate ?? t.Rate, t.PerClaimLimit, own?.YearlyLimit ?? t.YearlyLimit,
                    used.GetValueOrDefault(t.Id), t.ReceiptRequired);
            }).ToList();
    }

    [HttpPost("types"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddType(SaveExpenseTypeRequest req)
    {
        var t = new ExpenseType();
        db.ExpenseTypes.Add(t);
        await ApplyAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateType(Guid id, SaveExpenseTypeRequest req)
    {
        var t = await db.ExpenseTypes.Include(x => x.GradeRates).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Expense type");
        await ApplyAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteType(Guid id)
    {
        var t = await db.ExpenseTypes.Include(x => x.GradeRates).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Expense type");
        if (await db.ExpenseClaims.SelectMany(c => c.Items).AnyAsync(i => i.ExpenseTypeId == id)) throw new DomainException("Claims use this type. Turn it off instead.");
        db.ExpenseTypes.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(ExpenseType t, SaveExpenseTypeRequest req)
    {
        if (!Enum.IsDefined(req.Category) || !Enum.IsDefined(req.Calculation)) throw new DomainException("Choose a category and how it is worked out.");
        if (req.Calculation != ExpenseCalculation.Amount && req.Rate is not > 0) throw new DomainException("Enter the rate.");
        var name = req.Name.Trim();
        if (await db.ExpenseTypes.AnyAsync(x => x.Id != t.Id && x.Name == name)) throw new DomainException($"There is already a type called {name}.");
        var grades = (req.GradeIds ?? []).Distinct().ToList();
        var rates = (req.GradeRates ?? []).GroupBy(r => r.GradeId).Select(g => g.First()).ToList();
        var allGrades = grades.Concat(rates.Select(r => r.GradeId)).Distinct().ToList();
        if (await db.OrgLookups.CountAsync(l => allGrades.Contains(l.Id) && l.Kind == LookupKind.Grade) != allGrades.Count) throw new DomainException("Choose grades from the list.");
        t.Name = name;
        t.Category = req.Category;
        t.Calculation = req.Calculation;
        t.Unit = string.IsNullOrWhiteSpace(req.Unit) ? (req.Calculation == ExpenseCalculation.Distance ? "km" : null) : req.Unit.Trim();
        t.Rate = req.Calculation == ExpenseCalculation.Amount ? null : req.Rate;
        t.PerClaimLimit = req.PerClaimLimit;
        t.YearlyLimit = req.YearlyLimit;
        t.ReceiptRequired = req.ReceiptRequired;
        t.GradeIds = grades.Count > 0 ? string.Join(',', grades) : null;
        t.IsActive = req.IsActive;
        db.ExpenseTypeRates.RemoveRange(t.GradeRates);
        foreach (var r in rates.Where(r => r.Rate is not null || r.YearlyLimit is not null))
            db.ExpenseTypeRates.Add(new ExpenseTypeRate { ExpenseTypeId = t.Id, GradeId = r.GradeId, Rate = r.Rate, YearlyLimit = r.YearlyLimit });
    }

    // ---------- Distances ----------

    [HttpGet("routes")]
    public Task<List<DistanceRouteDto>> Routes() =>
        db.DistanceRoutes.OrderBy(r => r.FromCity).ThenBy(r => r.ToCity).Select(r => new DistanceRouteDto(r.Id, r.FromCity, r.ToCity, r.Km)).ToListAsync();

    [HttpGet("distance")]
    public async Task<object> Distance([FromQuery] string from, [FromQuery] string to) => new { km = await ExpenseService.DistanceAsync(db, from ?? "", to ?? "") };

    [HttpPost("routes"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddRoute(SaveDistanceRouteRequest req)
    {
        var (a, b) = (req.FromCity.Trim(), req.ToCity.Trim());
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) throw new DomainException("Choose two different cities.");
        if (await ExpenseService.DistanceAsync(db, a, b) is not null) throw new DomainException($"{a} to {b} is already on the list.");
        db.DistanceRoutes.Add(new DistanceRoute { FromCity = a, ToCity = b, Km = req.Km });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("routes/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteRoute(Guid id)
    {
        db.DistanceRoutes.Remove(await db.DistanceRoutes.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Route"));
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Advances ----------

    [HttpGet("advances/my")]
    public Task<List<ExpenseAdvanceDto>> MyAdvances() => ProjectAsync(db.ExpenseAdvances.Where(a => a.EmployeeId == me.EmployeeId));

    /// <summary>Open approved advances of a person, to set a claim against.</summary>
    [HttpGet("advances/open")]
    public async Task<List<ExpenseAdvanceDto>> Open([FromQuery] Guid? employeeId)
    {
        var id = await onBehalf.ResolveAsync(employeeId);
        return await ProjectAsync(db.ExpenseAdvances.Where(a => a.EmployeeId == id && a.Status == RequestStatus.Approved && a.ClosedAtUtc == null));
    }

    [HttpPost("advances")]
    public async Task<IActionResult> RequestAdvance(RequestAdvanceRequest req)
    {
        await advances.RequestAsync(await onBehalf.ResolveAsync(req.EmployeeId), req);
        return NoContent();
    }

    [HttpPost("advances/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await advances.CancelAsync(id);
        return NoContent();
    }

    [HttpPost("advances/{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, ExemptionDecisionRequest? req)
    {
        await advances.DecideAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("advances/{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, ExemptionDecisionRequest? req)
    {
        await advances.DecideAsync(id, false, req?.Note);
        return NoContent();
    }

    [HttpPost("advances/{id:guid}/close"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> Close(Guid id)
    {
        await advances.CloseAsync(id);
        return NoContent();
    }

    [HttpGet("advances"), Authorize(Policy = Policies.Approver)]
    public async Task<List<ExpenseAdvanceDto>> All([FromQuery] RequestStatus? status)
    {
        var q = db.ExpenseAdvances.AsQueryable();
        if (status is { } s) q = q.Where(a => a.Status == s);
        if (!me.IsHr())
        {
            var waiting = await approvals.WaitingForMeAsync(ApprovalRequestType.ExpenseAdvance);
            q = q.Where(a => waiting.Contains(a.Id));
        }
        return await ProjectAsync(q);
    }

    private Task<List<ExpenseAdvanceDto>> ProjectAsync(IQueryable<ExpenseAdvance> q) =>
        q.OrderByDescending(a => a.CreatedAtUtc).Take(300)
            .Select(a => new ExpenseAdvanceDto(a.Id, a.EmployeeId, a.Employee!.FirstName + " " + a.Employee.LastName, a.Employee.EmployeeCode, a.Purpose, a.Amount, a.Status,
                a.Adjusted, a.ClosedAtUtc == null ? a.Amount - a.Adjusted : 0, a.ClosedAtUtc, a.Recovered, a.CreatedAtUtc, a.DecisionNote)).ToListAsync();
}
