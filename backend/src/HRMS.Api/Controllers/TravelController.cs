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

public record TravelSettingsDto(int MinNoticeDays, decimal PerDiemDomestic, decimal PerDiemInternational, int SettleWithinDays, List<Guid> DeskIds, List<string> Desk);

public record SaveTravelSettingsRequest(int MinNoticeDays, decimal PerDiemDomestic, decimal PerDiemInternational, int SettleWithinDays, List<Guid>? DeskIds);

public record TravelReasonDto(Guid Id, string Name, bool IsActive, int Trips);

public record SaveTravelReasonRequest([Required, StringLength(100)] string Name, bool IsActive = true);

public record TravelMeDto(bool IsDesk, bool IsHr, int MinNoticeDays, decimal PerDiemDomestic, decimal PerDiemInternational, int SettleWithinDays);

public record TravelLegRequest([Required, StringLength(100)] string From, [Required, StringLength(100)] string To, DateOnly Date, TravelMode Mode, [StringLength(150)] string? Preference);

public record SaveTravelRequest(
    Guid? ReasonId, [Required, StringLength(2000)] string Purpose, TripType TripType, bool IsInternational, DateOnly? ReturnOn, [Required] List<TravelLegRequest> Legs,
    [StringLength(500)] string? UrgentReason, bool NeedsHotel, [StringLength(100)] string? HotelCity, DateOnly? CheckIn, DateOnly? CheckOut, bool NeedsPickDrop,
    [StringLength(500)] string? PickDropNotes, decimal? EstimatedCost, decimal? AdvanceRequested);

public record TravelListItemDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? Department, string Route, string Destination, TripType TripType,
    bool IsInternational, DateOnly DepartOn, DateOnly ReturnOn, string Purpose, string? Reason, TravelStatus Status, SettlementStatus Settlement,
    bool Urgent, bool NeedsHotel, decimal? AdvanceRequested, decimal? AdvancePaid, decimal BookedCost, bool SettlementOverdue, DateTime CreatedAtUtc);

public record TravelLegDto(Guid Id, int Order, string From, string To, DateOnly Date, TravelMode Mode, string? Preference, string? BookingRef, decimal? BookingCost);

public record TravelExpenseDto(Guid Id, DateOnly Date, ExpenseCategory Category, string Description, decimal Amount);

public record TravelDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, Guid? ReasonId, string? Reason, string Purpose, TripType TripType, bool IsInternational,
    DateOnly DepartOn, DateOnly ReturnOn, string? UrgentReason, bool NeedsHotel, string? HotelCity, DateOnly? CheckIn, DateOnly? CheckOut, bool NeedsPickDrop,
    string? PickDropNotes, decimal? EstimatedCost, decimal? AdvanceRequested, TravelStatus Status, string? DecisionNote, string? HotelName, string? HotelRef,
    decimal? HotelCost, string? BookingNotes, string? BookedByName, DateTime? BookedAtUtc, decimal? AdvancePaid, DateOnly? AdvancePaidOn, string? AdvancePaidVia,
    SettlementStatus Settlement, int? PerDiemDays, decimal? PerDiemRate, decimal PerDiemRateNow, string? SettlementNote, string? SettlementDecisionNote,
    DateTime? SettledAtUtc, decimal Claimed, decimal Net, decimal BookedCost, DateOnly SettleBy, List<TravelLegDto> Legs, List<TravelExpenseDto> Expenses,
    bool IsMine, bool IsDesk, bool IsHr, DateTime CreatedAtUtc);

public record BookTravelRequest(List<LegBooking>? Legs, [StringLength(150)] string? HotelName, [StringLength(100)] string? HotelRef, decimal? HotelCost, [StringLength(2000)] string? Notes);

public record AdvanceRequest(decimal Amount, DateOnly PaidOn, [StringLength(100)] string? Via);

public record TravelExpenseRequest(DateOnly Date, ExpenseCategory Category, [Required, StringLength(500)] string Description, decimal Amount);

public record SettleRequest([Required] List<TravelExpenseRequest> Expenses, int PerDiemDays, [StringLength(2000)] string? Note);

public record TravelDecisionRequest([StringLength(1000)] string? Note);

public record TravelReportRow(string Name, int Trips, decimal Booked, decimal Claimed);

public record TravelReportDto(
    int Trips, int Upcoming, int ToBook, int SettlementsWaiting, int SettlementsOverdue, decimal AdvancesOutstanding, decimal BookedCost, decimal ClaimedCost,
    List<TravelReportRow> ByDepartment, List<TravelReportRow> ByDestination, List<TravelReportRow> ByMonth);

[ApiController]
[Route("api/travel")]
[Authorize(Policy = Policies.CompanyUser)]
public class TravelController(HrmsDbContext db, TravelService travel, TenantClock clock, ICurrentUser me) : ControllerBase
{
    [HttpGet("me")]
    public async Task<TravelMeDto> Me()
    {
        var s = await travel.SettingsAsync();
        return new TravelMeDto(await travel.IsDeskAsync(), me.IsHr(), s.MinNoticeDays, s.PerDiemDomestic, s.PerDiemInternational, s.SettleWithinDays);
    }

    // ---------- Settings (HR) ----------

    [HttpGet("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<TravelSettingsDto> Settings()
    {
        var s = await travel.SettingsAsync();
        var ids = HelpdeskService.Ids(s.DeskIds);
        var names = await db.Employees.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        return new TravelSettingsDto(s.MinNoticeDays, s.PerDiemDomestic, s.PerDiemInternational, s.SettleWithinDays, ids, ids.Select(i => names.GetValueOrDefault(i) ?? "").ToList());
    }

    [HttpPut("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveSettings(SaveTravelSettingsRequest req)
    {
        if (req.MinNoticeDays is < 0 or > 90 || req.SettleWithinDays is < 1 or > 180)
            throw new DomainException("Notice is 0 to 90 days and settlement 1 to 180 days.");
        if (req.PerDiemDomestic < 0 || req.PerDiemInternational < 0)
            throw new DomainException("Allowances cannot be negative.");
        var ids = (req.DeskIds ?? []).Distinct().ToList();
        if (await db.Employees.CountAsync(e => ids.Contains(e.Id)) != ids.Count)
            throw new NotFoundException("Employee");
        var s = await db.TravelSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new TravelSettings();
            db.TravelSettings.Add(s);
        }
        s.MinNoticeDays = req.MinNoticeDays;
        s.PerDiemDomestic = req.PerDiemDomestic;
        s.PerDiemInternational = req.PerDiemInternational;
        s.SettleWithinDays = req.SettleWithinDays;
        s.DeskIds = string.Join(',', ids);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("reasons")]
    public async Task<List<TravelReasonDto>> Reasons() =>
        await db.TravelReasons.OrderBy(r => r.Name)
            .Select(r => new TravelReasonDto(r.Id, r.Name, r.IsActive, db.TravelRequests.Count(t => t.ReasonId == r.Id))).ToListAsync();

    [HttpPost("reasons"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddReason(SaveTravelReasonRequest req)
    {
        var name = req.Name.Trim();
        if (await db.TravelReasons.AnyAsync(r => r.Name == name))
            throw new DomainException("This reason already exists.");
        db.TravelReasons.Add(new TravelReason { Name = name, IsActive = req.IsActive });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("reasons/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateReason(Guid id, SaveTravelReasonRequest req)
    {
        var r = await db.TravelReasons.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Reason");
        var name = req.Name.Trim();
        if (await db.TravelReasons.AnyAsync(x => x.Name == name && x.Id != id))
            throw new DomainException("This reason already exists.");
        r.Name = name;
        r.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("reasons/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteReason(Guid id)
    {
        var r = await db.TravelReasons.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Reason");
        if (await db.TravelRequests.AnyAsync(t => t.ReasonId == id))
            throw new DomainException("Trips use this reason. Turn it off instead.");
        db.TravelReasons.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Trips ----------

    [HttpGet("my")]
    public async Task<List<TravelListItemDto>> My() =>
        await ListAsync(db.TravelRequests.Where(t => t.EmployeeId == me.EmployeeId));

    /// <summary>Desk and HR: every trip, filtered by what needs doing.</summary>
    [HttpGet("all")]
    public async Task<List<TravelListItemDto>> All([FromQuery] string? filter, [FromQuery] string? search)
    {
        if (!await travel.IsDeskAsync())
            throw new ForbiddenException("Only HR and the travel desk see all trips.");
        var q = db.TravelRequests.AsQueryable();
        q = filter switch
        {
            "book" => q.Where(t => t.Status == TravelStatus.Approved),
            "advance" => q.Where(t => (t.Status == TravelStatus.Approved || t.Status == TravelStatus.Booked) && t.AdvanceRequested > 0 && t.AdvancePaid == null),
            "settle" => q.Where(t => t.Settlement == SettlementStatus.Submitted),
            "unsettled" => q.Where(t => (t.Status == TravelStatus.Approved || t.Status == TravelStatus.Booked) && t.Settlement != SettlementStatus.Settled),
            "pending" => q.Where(t => t.Status == TravelStatus.Pending),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(t => (t.Employee!.FirstName + " " + t.Employee.LastName).Contains(s) || t.Employee.EmployeeCode.Contains(s) || t.Purpose.Contains(s)
                             || t.Legs.Any(l => l.From.Contains(s) || l.To.Contains(s)));
        }
        return await ListAsync(q);
    }

    [HttpGet("{id:guid}")]
    public async Task<TravelDto> Get(Guid id) => await ToDtoAsync(await travel.LoadAsync(id));

    [HttpPost]
    public async Task<TravelDto> Create(SaveTravelRequest req)
    {
        var t = await travel.RequestAsync(new TravelInput(req.ReasonId, req.Purpose, req.TripType, req.IsInternational, req.ReturnOn,
            req.Legs.Select(l => new TravelLegInput(l.From, l.To, l.Date, l.Mode, l.Preference)).ToList(), req.UrgentReason, req.NeedsHotel, req.HotelCity,
            req.CheckIn, req.CheckOut, req.NeedsPickDrop, req.PickDropNotes, req.EstimatedCost, req.AdvanceRequested));
        return await ToDtoAsync(await travel.LoadAsync(t.Id));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, TravelDecisionRequest? req)
    {
        await travel.DecideAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, TravelDecisionRequest? req)
    {
        await travel.DecideAsync(id, false, req?.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, TravelDecisionRequest? req)
    {
        await travel.CancelAsync(id, req?.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/booking")]
    public async Task<IActionResult> Book(Guid id, BookTravelRequest req)
    {
        await travel.BookAsync(id, new BookingInput(req.Legs ?? [], req.HotelName, req.HotelRef, req.HotelCost, req.Notes));
        return NoContent();
    }

    [HttpPost("{id:guid}/advance")]
    public async Task<IActionResult> Advance(Guid id, AdvanceRequest req)
    {
        await travel.PayAdvanceAsync(id, req.Amount, req.PaidOn, req.Via);
        return NoContent();
    }

    [HttpPost("{id:guid}/settlement")]
    public async Task<IActionResult> Settle(Guid id, SettleRequest req)
    {
        await travel.SubmitSettlementAsync(id, new TravelSettlementInput(
            req.Expenses.Select(e => new TravelExpenseInput(e.Date, e.Category, e.Description, e.Amount)).ToList(), req.PerDiemDays, req.Note));
        return NoContent();
    }

    [HttpPost("{id:guid}/settlement/approve")]
    public async Task<IActionResult> ApproveSettlement(Guid id, TravelDecisionRequest? req)
    {
        await travel.DecideSettlementAsync(id, true, req?.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/settlement/return")]
    public async Task<IActionResult> ReturnSettlement(Guid id, TravelDecisionRequest? req)
    {
        await travel.DecideSettlementAsync(id, false, req?.Note);
        return NoContent();
    }

    // ---------- Report ----------

    [HttpGet("report")]
    public async Task<TravelReportDto> Report([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        if (!await travel.IsDeskAsync())
            throw new ForbiddenException("Only HR and the travel desk see travel reports.");
        var today = await clock.TodayAsync();
        var start = from ?? new DateOnly(today.Year, 1, 1);
        var end = to ?? new DateOnly(today.Year, 12, 31);
        var settle = (await travel.SettingsAsync()).SettleWithinDays;
        var trips = await db.TravelRequests.Include(t => t.Legs).Include(t => t.Expenses)
            .Where(t => t.Status != TravelStatus.Rejected && t.Status != TravelStatus.Cancelled && t.DepartOn >= start && t.DepartOn <= end)
            .Select(t => new { Trip = t, Department = t.Employee!.Department != null ? t.Employee.Department.Name : "No department" })
            .ToListAsync();
        var outstanding = await db.TravelRequests.Where(t => t.AdvancePaid > 0 && t.Settlement != SettlementStatus.Settled).SumAsync(t => t.AdvancePaid ?? 0);
        TravelReportRow Row(string name, IEnumerable<TravelRequest> list)
        {
            var l = list.ToList();
            return new TravelReportRow(name, l.Count, l.Sum(TravelService.BookedCost), l.Where(t => t.Settlement == SettlementStatus.Settled).Sum(TravelService.Claimed));
        }
        var active = trips.Where(x => x.Trip.Status is TravelStatus.Approved or TravelStatus.Booked).Select(x => x.Trip).ToList();
        return new TravelReportDto(
            trips.Count,
            active.Count(t => t.DepartOn >= today),
            trips.Count(x => x.Trip.Status == TravelStatus.Approved),
            trips.Count(x => x.Trip.Settlement == SettlementStatus.Submitted),
            active.Count(t => t.Settlement is SettlementStatus.None or SettlementStatus.Returned && t.ReturnOn.AddDays(settle) < today),
            outstanding,
            trips.Sum(x => TravelService.BookedCost(x.Trip)),
            trips.Where(x => x.Trip.Settlement == SettlementStatus.Settled).Sum(x => TravelService.Claimed(x.Trip)),
            trips.GroupBy(x => x.Department).Select(g => Row(g.Key, g.Select(x => x.Trip))).OrderByDescending(r => r.Booked + r.Claimed).ToList(),
            trips.GroupBy(x => TravelService.Destination(x.Trip)).Select(g => Row(g.Key, g.Select(x => x.Trip))).OrderByDescending(r => r.Trips).Take(10).ToList(),
            trips.GroupBy(x => x.Trip.DepartOn.ToString("yyyy-MM")).OrderBy(g => g.Key).Select(g => Row(g.Key, g.Select(x => x.Trip))).ToList());
    }

    // ---------- Helpers ----------

    private async Task<List<TravelListItemDto>> ListAsync(IQueryable<TravelRequest> q)
    {
        var today = await clock.TodayAsync();
        var settle = (await travel.SettingsAsync()).SettleWithinDays;
        var rows = await q.Include(t => t.Legs).OrderByDescending(t => t.DepartOn).Take(500)
            .Select(t => new
            {
                Trip = t, t.Employee!.FirstName, t.Employee.LastName, t.Employee.EmployeeCode,
                Department = t.Employee.Department != null ? t.Employee.Department.Name : null,
                Reason = db.TravelReasons.Where(r => r.Id == t.ReasonId).Select(r => r.Name).FirstOrDefault(),
            }).ToListAsync();
        return rows.Select(r =>
        {
            var t = r.Trip;
            var overdue = t.Status is TravelStatus.Approved or TravelStatus.Booked && t.Settlement is SettlementStatus.None or SettlementStatus.Returned
                          && t.ReturnOn.AddDays(settle) < today;
            return new TravelListItemDto(t.Id, t.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, r.Department, TravelService.Route(t), TravelService.Destination(t),
                t.TripType, t.IsInternational, t.DepartOn, t.ReturnOn, t.Purpose, r.Reason, t.Status, t.Settlement, t.UrgentReason is not null, t.NeedsHotel,
                t.AdvanceRequested, t.AdvancePaid, TravelService.BookedCost(t), overdue, t.CreatedAtUtc);
        }).ToList();
    }

    private async Task<TravelDto> ToDtoAsync(TravelRequest t)
    {
        var s = await travel.SettingsAsync();
        var reason = t.ReasonId is { } rid ? await db.TravelReasons.Where(r => r.Id == rid).Select(r => r.Name).FirstOrDefaultAsync() : null;
        var desk = await travel.IsDeskAsync();
        return new TravelDto(t.Id, t.EmployeeId, t.Employee!.FullName, t.Employee.EmployeeCode, t.ReasonId, reason, t.Purpose, t.TripType, t.IsInternational,
            t.DepartOn, t.ReturnOn, t.UrgentReason, t.NeedsHotel, t.HotelCity, t.CheckIn, t.CheckOut, t.NeedsPickDrop, t.PickDropNotes, t.EstimatedCost,
            t.AdvanceRequested, t.Status, t.DecisionNote, t.HotelName, t.HotelRef, t.HotelCost, t.BookingNotes, t.BookedByName, t.BookedAtUtc, t.AdvancePaid,
            t.AdvancePaidOn, t.AdvancePaidVia, t.Settlement, t.PerDiemDays, t.PerDiemRate, t.IsInternational ? s.PerDiemInternational : s.PerDiemDomestic,
            t.SettlementNote, t.SettlementDecisionNote, t.SettledAtUtc, TravelService.Claimed(t), TravelService.Net(t), TravelService.BookedCost(t),
            t.ReturnOn.AddDays(s.SettleWithinDays),
            t.Legs.OrderBy(l => l.Order).Select(l => new TravelLegDto(l.Id, l.Order, l.From, l.To, l.Date, l.Mode, l.Preference, l.BookingRef, l.BookingCost)).ToList(),
            t.Expenses.OrderBy(e => e.Date).Select(e => new TravelExpenseDto(e.Id, e.Date, e.Category, e.Description, e.Amount)).ToList(),
            t.EmployeeId == me.EmployeeId, desk, me.IsHr(), t.CreatedAtUtc);
    }
}
