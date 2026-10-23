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

public record PieceCategoryDto(Guid Id, string Name, int Items);

public record SavePieceCategoryRequest([Required, StringLength(100)] string Name);

public record PieceItemDto(Guid Id, Guid? CategoryId, string? Category, string Name, string Unit, decimal Rate, decimal? DailyTarget, bool IsActive, int SpecialRates);

public record SavePieceItemRequest(Guid? CategoryId, [Required, StringLength(150)] string Name, [Required, StringLength(30)] string Unit, decimal Rate, decimal? DailyTarget,
    bool IsActive = true);

public record PieceRateDto(Guid Id, Guid EmployeeId, string EmployeeName, decimal Rate);

public record SavePieceRateRequest(Guid EmployeeId, decimal Rate);

public record PieceEntryDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, Guid ItemId, string Item, string Unit, DateOnly Date, decimal Quantity, decimal Rate,
    decimal Amount, decimal? Efficiency, string? Notes, string? EnteredByName, RequestStatus Status, string? DecidedByName, string? DecisionNote, bool Paid,
    bool CanDecide, bool CanDelete);

public record PieceLineRequest(Guid EmployeeId, decimal Quantity, [StringLength(500)] string? Notes);

/// <summary>One day, one item, many people: a production sheet. Without EmployeeId lines it is the signed-in user's own entry.</summary>
public record SavePieceEntriesRequest(Guid ItemId, DateOnly Date, [Required] List<PieceLineRequest> Lines);

public record DecidePiecesRequest([Required] List<Guid> Ids, bool Approve, [StringLength(500)] string? Note);

public record PieceSummaryRowDto(string Name, decimal Quantity, decimal Amount, int Days, decimal? Efficiency);

public record PieceReportDto(decimal Amount, decimal Quantity, int People, int Pending, decimal PendingAmount, List<PieceSummaryRowDto> ByItem, List<PieceSummaryRowDto> ByEmployee);

/// <summary>
/// Piece work: items paid per unit, daily production entered by the worker or as a sheet by their supervisor or HR,
/// approval by the line manager or HR, and payment with the month's salary.
/// </summary>
[ApiController]
[Route("api/piece-work")]
[Authorize(Policy = Policies.CompanyUser)]
public class PieceWorkController(HrmsDbContext db, TenantClock clock, ICurrentUser me) : ControllerBase
{
    // ---------- Setup (HR) ----------

    [HttpGet("categories")]
    public async Task<List<PieceCategoryDto>> Categories() =>
        await db.PieceCategories.OrderBy(c => c.Name).Select(c => new PieceCategoryDto(c.Id, c.Name, db.PieceItems.Count(i => i.CategoryId == c.Id))).ToListAsync();

    [HttpPost("categories"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddCategory(SavePieceCategoryRequest req)
    {
        var name = req.Name.Trim();
        if (await db.PieceCategories.AnyAsync(c => c.Name == name))
            throw new DomainException("This category already exists.");
        db.PieceCategories.Add(new PieceCategory { Name = name });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("categories/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var c = await db.PieceCategories.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Category");
        if (await db.PieceItems.AnyAsync(i => i.CategoryId == id))
            throw new DomainException("Items use this category.");
        db.PieceCategories.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("items")]
    public async Task<List<PieceItemDto>> Items() =>
        await db.PieceItems.OrderBy(i => i.Name).Select(i => new PieceItemDto(i.Id, i.CategoryId, db.PieceCategories.Where(c => c.Id == i.CategoryId).Select(c => c.Name).FirstOrDefault(),
            i.Name, i.Unit, i.Rate, i.DailyTarget, i.IsActive, db.PieceRates.Count(r => r.ItemId == i.Id))).ToListAsync();

    [HttpPost("items"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddItem(SavePieceItemRequest req)
    {
        var i = new PieceItem();
        await ApplyAsync(i, req);
        db.PieceItems.Add(i);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("items/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateItem(Guid id, SavePieceItemRequest req)
    {
        var i = await db.PieceItems.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Item");
        await ApplyAsync(i, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("items/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var i = await db.PieceItems.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Item");
        if (await db.PieceEntries.AnyAsync(e => e.ItemId == id))
            throw new DomainException("Work has been entered for this item. Turn it off instead.");
        db.PieceItems.Remove(i);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("items/{id:guid}/rates"), Authorize(Policy = Policies.Hr)]
    public async Task<List<PieceRateDto>> Rates(Guid id) =>
        await db.PieceRates.Where(r => r.ItemId == id)
            .Select(r => new PieceRateDto(r.Id, r.EmployeeId, db.Employees.Where(e => e.Id == r.EmployeeId).Select(e => e.FirstName + " " + e.LastName).First(), r.Rate)).ToListAsync();

    [HttpPut("items/{id:guid}/rates"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SetRate(Guid id, SavePieceRateRequest req)
    {
        if (!await db.PieceItems.AnyAsync(i => i.Id == id))
            throw new NotFoundException("Item");
        if (!await db.Employees.AnyAsync(e => e.Id == req.EmployeeId))
            throw new NotFoundException("Employee");
        if (req.Rate < 0)
            throw new DomainException("The rate cannot be negative.");
        var r = await db.PieceRates.FirstOrDefaultAsync(x => x.ItemId == id && x.EmployeeId == req.EmployeeId);
        if (r is null)
            db.PieceRates.Add(new PieceRate { ItemId = id, EmployeeId = req.EmployeeId, Rate = req.Rate });
        else
            r.Rate = req.Rate;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("rates/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteRate(Guid id)
    {
        var r = await db.PieceRates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Rate");
        db.PieceRates.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Entries ----------

    /// <summary>Own entries, or (for managers and HR) the team's, filtered by month and status.</summary>
    [HttpGet("entries")]
    public async Task<List<PieceEntryDto>> Entries([FromQuery] int? year, [FromQuery] int? month, [FromQuery] bool team = false, [FromQuery] RequestStatus? status = null,
        [FromQuery] Guid? employeeId = null)
    {
        var today = await clock.TodayAsync();
        var from = new DateOnly(year ?? today.Year, month ?? today.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var q = db.PieceEntries.Where(e => e.Date >= from && e.Date <= to);
        q = team ? q.Where(e => Team().Contains(e.EmployeeId)) : q.Where(e => e.EmployeeId == me.EmployeeId);
        if (status is { } s)
            q = q.Where(e => e.Status == s);
        if (employeeId is { } eid)
            q = q.Where(e => e.EmployeeId == eid);
        return await ProjectAsync(q.OrderByDescending(e => e.Date).ThenBy(e => e.Employee!.FirstName).Take(2000));
    }

    [HttpPost("entries")]
    public async Task<object> Save(SavePieceEntriesRequest req)
    {
        var item = await db.PieceItems.FirstOrDefaultAsync(i => i.Id == req.ItemId && i.IsActive) ?? throw new NotFoundException("Item");
        var today = await clock.TodayAsync();
        if (req.Date > today)
            throw new DomainException("Enter work on or after the day it was done.");
        if (req.Date < today.AddDays(-45))
            throw new DomainException("Work older than 45 days cannot be entered.");
        var lines = req.Lines.Where(l => l.Quantity != 0).ToList();
        if (lines.Count == 0)
            throw new DomainException("Enter at least one quantity.");
        if (lines.Any(l => l.Quantity < 0 || l.Quantity > 100000))
            throw new DomainException("Quantities must be positive.");
        var ids = lines.Select(l => l.EmployeeId).Distinct().ToList();
        if (ids.Count != lines.Count)
            throw new DomainException("Each person appears once on a sheet.");
        var self = ids.Count == 1 && ids[0] == me.EmployeeId;
        var team = await Team().Where(id => ids.Contains(id)).ToListAsync();
        if (!self && team.Count != ids.Count)
            throw new ForbiddenException("You can enter work only for yourself or your team.");
        if (await db.Employees.CountAsync(e => ids.Contains(e.Id) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)) != ids.Count)
            throw new DomainException("Some people do not work here any more.");
        if (await db.PieceEntries.AnyAsync(e => ids.Contains(e.EmployeeId) && e.ItemId == item.Id && e.Date == req.Date && e.Status != RequestStatus.Rejected && e.Status != RequestStatus.Cancelled))
            throw new DomainException($"{item.Name} on {req.Date:d MMM} is already entered for someone here. Change that entry instead.");

        var rates = await db.PieceRates.Where(r => r.ItemId == item.Id && ids.Contains(r.EmployeeId)).ToDictionaryAsync(r => r.EmployeeId, r => r.Rate);
        var by = await LetterService.CurrentUserNameAsync(db, me);
        // Entries made by a supervisor or HR for others count as approved; a worker's own entry waits for their manager.
        var approved = !self || me.IsHr();
        foreach (var l in lines)
        {
            var rate = rates.GetValueOrDefault(l.EmployeeId, item.Rate);
            db.PieceEntries.Add(new PieceEntry
            {
                EmployeeId = l.EmployeeId, ItemId = item.Id, Date = req.Date, Quantity = l.Quantity, Rate = rate, Amount = Math.Round(rate * l.Quantity, 2),
                Notes = string.IsNullOrWhiteSpace(l.Notes) ? null : l.Notes.Trim(), EnteredByName = by,
                Status = approved ? RequestStatus.Approved : RequestStatus.Pending, DecidedByName = approved ? by : null, DecidedAtUtc = approved ? clock.UtcNow : null,
            });
        }
        await db.SaveChangesAsync();
        return new { added = lines.Count, approved };
    }

    [HttpPost("entries/decide")]
    public async Task<object> Decide(DecidePiecesRequest req)
    {
        var team = Team();
        var list = await db.PieceEntries.Where(e => req.Ids.Contains(e.Id) && e.Status == RequestStatus.Pending && team.Contains(e.EmployeeId) && e.EmployeeId != me.EmployeeId).ToListAsync();
        if (list.Count == 0)
            throw new DomainException("None of these are waiting for you.");
        if (!req.Approve && string.IsNullOrWhiteSpace(req.Note))
            throw new DomainException("Say why the work is not accepted.");
        var by = await LetterService.CurrentUserNameAsync(db, me);
        foreach (var e in list)
        {
            e.Status = req.Approve ? RequestStatus.Approved : RequestStatus.Rejected;
            e.DecidedByName = by;
            e.DecidedAtUtc = clock.UtcNow;
            e.DecisionNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        }
        await db.SaveChangesAsync();
        return new { decided = list.Count };
    }

    [HttpDelete("entries/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var e = await db.PieceEntries.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Entry");
        if (!CanDelete(e.EmployeeId, e.Status, e.PayrollRunId, await Team().AnyAsync(t => t == e.EmployeeId)))
            throw new ForbiddenException("This entry can no longer be removed.");
        db.PieceEntries.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("team")]
    public async Task<List<LookupDto>> TeamList() =>
        await db.Employees.Where(e => Team().Contains(e.Id) && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)).OrderBy(e => e.FirstName)
            .Select(e => new LookupDto(e.Id, e.EmployeeCode + " · " + e.FirstName + " " + e.LastName)).ToListAsync();

    [HttpGet("report")]
    public async Task<PieceReportDto> Report([FromQuery] int? year, [FromQuery] int? month)
    {
        var today = await clock.TodayAsync();
        var from = new DateOnly(year ?? today.Year, month ?? today.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var rows = await db.PieceEntries.Where(e => e.Date >= from && e.Date <= to && Team().Contains(e.EmployeeId)
                                                    && (e.Status == RequestStatus.Approved || e.Status == RequestStatus.Pending))
            .Select(e => new { e.EmployeeId, Name = e.Employee!.FirstName + " " + e.Employee.LastName, Item = e.Item!.Name, e.Item.Unit, e.Item.DailyTarget, e.Date, e.Quantity, e.Amount, e.Status })
            .ToListAsync();
        var ok = rows.Where(r => r.Status == RequestStatus.Approved).ToList();
        var byItem = ok.GroupBy(r => r.Item).Select(g =>
        {
            var target = g.First().DailyTarget;
            var days = g.Select(r => (r.EmployeeId, r.Date)).Distinct().Count();
            return new PieceSummaryRowDto($"{g.Key} ({g.First().Unit})", g.Sum(r => r.Quantity), g.Sum(r => r.Amount), days,
                target is > 0 && days > 0 ? Math.Round(100 * g.Sum(r => r.Quantity) / (target.Value * days), 0) : null);
        }).OrderByDescending(r => r.Amount).ToList();
        var byEmployee = ok.GroupBy(r => r.Name).Select(g =>
        {
            var withTarget = g.Where(r => r.DailyTarget is > 0).ToList();
            var eff = withTarget.Count > 0 ? Math.Round(100 * withTarget.Average(r => r.Quantity / r.DailyTarget!.Value), 0) : (decimal?)null;
            return new PieceSummaryRowDto(g.Key, g.Sum(r => r.Quantity), g.Sum(r => r.Amount), g.Select(r => r.Date).Distinct().Count(), eff);
        }).OrderByDescending(r => r.Amount).ToList();
        var pending = rows.Where(r => r.Status == RequestStatus.Pending).ToList();
        return new PieceReportDto(ok.Sum(r => r.Amount), ok.Sum(r => r.Quantity), ok.Select(r => r.EmployeeId).Distinct().Count(), pending.Count, pending.Sum(r => r.Amount), byItem, byEmployee);
    }

    // ---------- Helpers ----------

    /// <summary>People whose work the signed-in user enters and approves: everyone for HR, direct reports for managers.</summary>
    private IQueryable<Guid> Team()
    {
        if (me.IsHr())
            return db.Employees.Select(e => e.Id);
        var mine = me.EmployeeId ?? Guid.Empty;
        return db.Employees.Where(e => e.ManagerId == mine).Select(e => e.Id);
    }

    private bool CanDelete(Guid employeeId, RequestStatus status, Guid? runId, bool inTeam) =>
        runId is null && (me.IsHr() || (employeeId == me.EmployeeId && status == RequestStatus.Pending) || (inTeam && status != RequestStatus.Rejected));

    private async Task<List<PieceEntryDto>> ProjectAsync(IQueryable<PieceEntry> q)
    {
        var team = await Team().ToListAsync();
        var rows = await q.Select(e => new
        {
            E = e, Name = e.Employee!.FirstName + " " + e.Employee.LastName, e.Employee.EmployeeCode, Item = e.Item!.Name, e.Item.Unit, e.Item.DailyTarget,
        }).ToListAsync();
        return rows.Select(r =>
        {
            var e = r.E;
            var inTeam = team.Contains(e.EmployeeId);
            return new PieceEntryDto(e.Id, e.EmployeeId, r.Name, r.EmployeeCode, e.ItemId, r.Item, r.Unit, e.Date, e.Quantity, e.Rate, e.Amount,
                r.DailyTarget is > 0 ? Math.Round(100 * e.Quantity / r.DailyTarget.Value, 0) : null, e.Notes, e.EnteredByName, e.Status, e.DecidedByName, e.DecisionNote,
                e.PaidAtUtc != null, e.Status == RequestStatus.Pending && inTeam && e.EmployeeId != me.EmployeeId, CanDelete(e.EmployeeId, e.Status, e.PayrollRunId, inTeam));
        }).ToList();
    }

    private async Task ApplyAsync(PieceItem i, SavePieceItemRequest r)
    {
        var name = r.Name.Trim();
        if (await db.PieceItems.AnyAsync(x => x.Name == name && x.Id != i.Id))
            throw new DomainException("An item with this name exists.");
        if (r.CategoryId is { } c && !await db.PieceCategories.AnyAsync(x => x.Id == c))
            throw new NotFoundException("Category");
        if (r.Rate < 0 || r.DailyTarget is < 0)
            throw new DomainException("Rate and target cannot be negative.");
        i.Name = name;
        i.CategoryId = r.CategoryId;
        i.Unit = r.Unit.Trim();
        i.Rate = r.Rate;
        i.DailyTarget = r.DailyTarget is > 0 ? r.DailyTarget : null;
        i.IsActive = r.IsActive;
    }
}
