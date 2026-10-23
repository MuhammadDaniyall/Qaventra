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

public record LeaveReasonDto(Guid Id, Guid LeaveTypeId, string Name, decimal? MaxDays, bool RequiresDocument, bool IsActive);

public record SaveLeaveReasonRequest(Guid LeaveTypeId, [Required, StringLength(100)] string Name, [Range(0.5, 365)] decimal? MaxDays, bool RequiresDocument, bool IsActive = true);

public record OpeningBalanceRow(Guid EmployeeId, string Name, string Code, Guid LeaveTypeId, string LeaveType, decimal Entitled, decimal CarriedForward, decimal Adjustment,
    decimal Used, decimal Remaining);

public record OpeningBalanceLine(Guid EmployeeId, Guid LeaveTypeId, decimal CarriedForward, decimal Adjustment);

public record SaveOpeningBalancesRequest(int Year, [Required] List<OpeningBalanceLine> Lines, [StringLength(200)] string? Note);

/// <summary>Leave reasons per type, and opening balances (brought forward from before the system, or corrected) for a year.</summary>
[ApiController]
[Route("api/leave-rules")]
[Authorize(Policy = Policies.CompanyUser)]
public class LeaveRulesController(HrmsDbContext db, LeaveService leave) : ControllerBase
{
    /// <summary>Everyone reads the reasons (the leave form needs them).</summary>
    [HttpGet("reasons")]
    public async Task<List<LeaveReasonDto>> Reasons() =>
        await db.LeaveReasons.OrderBy(r => r.Name).Select(r => new LeaveReasonDto(r.Id, r.LeaveTypeId, r.Name, r.MaxDays, r.RequiresDocument, r.IsActive)).ToListAsync();

    [HttpPost("reasons"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> AddReason(SaveLeaveReasonRequest req)
    {
        var r = new LeaveReason();
        await ApplyAsync(r, req);
        db.LeaveReasons.Add(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("reasons/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateReason(Guid id, SaveLeaveReasonRequest req)
    {
        var r = await db.LeaveReasons.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Reason");
        await ApplyAsync(r, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("reasons/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteReason(Guid id)
    {
        var r = await db.LeaveReasons.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Reason");
        if (await db.LeaveRequests.AnyAsync(l => l.ReasonId == id))
            throw new DomainException("Leave requests use this reason. Turn it off instead.");
        db.LeaveReasons.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(LeaveReason r, SaveLeaveReasonRequest req)
    {
        if (!await db.LeaveTypes.AnyAsync(t => t.Id == req.LeaveTypeId))
            throw new NotFoundException("Leave type");
        var name = req.Name.Trim();
        if (await db.LeaveReasons.AnyAsync(x => x.LeaveTypeId == req.LeaveTypeId && x.Name == name && x.Id != r.Id))
            throw new DomainException("This leave type already has that reason.");
        r.LeaveTypeId = req.LeaveTypeId;
        r.Name = name;
        r.MaxDays = req.MaxDays;
        r.RequiresDocument = req.RequiresDocument;
        r.IsActive = req.IsActive;
    }

    // ---------- Opening balances ----------

    /// <summary>Every current employee's balances for a year (created if missing), for HR to review and set.</summary>
    [HttpGet("balances"), Authorize(Policy = Policies.Hr)]
    public async Task<List<OpeningBalanceRow>> Balances([FromQuery] int year)
    {
        if (year is < 2000 or > 2100)
            throw new DomainException("Choose a year.");
        var ids = await db.Employees.Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation).Select(e => e.Id).ToListAsync();
        foreach (var id in ids)
            await leave.EnsureBalancesAsync(id, year);
        await db.SaveChangesAsync();
        return await db.LeaveBalances.Where(b => b.Year == year && ids.Contains(b.EmployeeId) && !b.LeaveType!.IsCompensatory)
            .OrderBy(b => b.LeaveType!.Name)
            .Select(b => new
            {
                b.EmployeeId, b.LeaveTypeId, Type = b.LeaveType!.Name, b.Entitled, b.CarriedForward, b.Adjustment, b.Used, b.Credited,
                Name = db.Employees.Where(e => e.Id == b.EmployeeId).Select(e => e.FirstName + " " + e.LastName).First(),
                Code = db.Employees.Where(e => e.Id == b.EmployeeId).Select(e => e.EmployeeCode).First(),
            }).ToListAsync()
            .ContinueWith(t => t.Result.OrderBy(x => x.Name).ThenBy(x => x.Type)
                .Select(x => new OpeningBalanceRow(x.EmployeeId, x.Name, x.Code, x.LeaveTypeId, x.Type, x.Entitled, x.CarriedForward, x.Adjustment, x.Used,
                    x.Entitled + x.CarriedForward + x.Adjustment + x.Credited - x.Used)).ToList());
    }

    /// <summary>Sets brought-forward days and adjustments for many people at once (e.g. when starting on the system).</summary>
    [HttpPut("balances"), Authorize(Policy = Policies.Hr)]
    public async Task<object> SaveBalances(SaveOpeningBalancesRequest req)
    {
        if (req.Lines.Count > 5000)
            throw new DomainException("At most 5,000 lines at a time.");
        if (req.Lines.Any(l => l.CarriedForward < 0 || l.CarriedForward > 365 || Math.Abs(l.Adjustment) > 365))
            throw new DomainException("Check the numbers: days must be between 0 and 365.");
        var changed = 0;
        foreach (var l in req.Lines)
        {
            await leave.EnsureBalancesAsync(l.EmployeeId, req.Year);
            var b = db.LeaveBalances.Local.FirstOrDefault(x => x.EmployeeId == l.EmployeeId && x.LeaveTypeId == l.LeaveTypeId && x.Year == req.Year)
                    ?? await db.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == l.EmployeeId && x.LeaveTypeId == l.LeaveTypeId && x.Year == req.Year)
                    ?? throw new NotFoundException("Leave balance");
            if (b.CarriedForward == l.CarriedForward && b.Adjustment == l.Adjustment)
                continue;
            b.CarriedForward = l.CarriedForward;
            b.Adjustment = l.Adjustment;
            b.AdjustmentNote = string.IsNullOrWhiteSpace(req.Note) ? "Opening balance" : req.Note.Trim();
            changed++;
        }
        await db.SaveChangesAsync();
        return new { changed };
    }
}
