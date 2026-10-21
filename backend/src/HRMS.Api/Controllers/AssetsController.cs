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

public record AssetTypeDto(Guid Id, string Name, bool AllowMultiple, bool ReturnOnExit, List<Guid> HandlerIds, List<string> Handlers, bool IsActive,
    int Total, int Available, int Assigned);

public record SaveAssetTypeRequest([Required, StringLength(100)] string Name, bool AllowMultiple, bool ReturnOnExit, List<Guid>? HandlerIds, bool IsActive = true);

public record AssetKitDto(Guid Id, string Name, List<Guid> TypeIds, List<string> Types);

public record SaveAssetKitRequest([Required, StringLength(100)] string Name, [Required] List<Guid> TypeIds);

public record SaveAssetRequest(
    Guid TypeId, [StringLength(50)] string? Tag, [Required, StringLength(200)] string Name, [StringLength(100)] string? SerialNumber,
    DateOnly? PurchasedOn, [Range(0, 1_000_000_000)] decimal? Cost, DateOnly? WarrantyUntil, AssetCondition Condition, [StringLength(1000)] string? Notes);

public record AssetStatusRequest(AssetStatus Status, [StringLength(1000)] string? Note);

public record AssignAssetRequest(Guid EmployeeId, DateOnly AssignedOn, DateOnly? DueBack, AssetCondition Condition, [StringLength(1000)] string? Notes);

public record ReturnAssetRequest(DateOnly ReturnedOn, AssetCondition Condition, [StringLength(1000)] string? Notes);

public record NewAssetRequestBody(Guid TypeId, [Required, StringLength(1000)] string Reason, DateOnly? NeededBy, DateOnly? ReturnBy);

public record ReturnRequestBody(Guid AssignmentId, [StringLength(1000)] string? Reason);

public record DecideAssetRequest([StringLength(1000)] string? Note);

public record FulfillAssetRequest(Guid? AssetId, DateOnly On, AssetCondition Condition, [StringLength(1000)] string? Notes);

public record AssetDto(
    Guid Id, string Tag, string Name, Guid TypeId, string Type, string? SerialNumber, DateOnly? PurchasedOn, decimal? Cost, DateOnly? WarrantyUntil,
    AssetCondition Condition, AssetStatus Status, string? Notes, Guid? HolderEmployeeId, string? Holder, Guid? AssignmentId, DateOnly? AssignedOn,
    DateOnly? DueBack, bool IsOverdue, bool CanManage);

public record AssignmentDto(
    Guid Id, Guid AssetId, string Tag, string AssetName, string Type, string? SerialNumber, Guid EmployeeId, string EmployeeName, DateOnly AssignedOn,
    DateOnly? DueBack, DateOnly? ReturnedOn, AssetCondition ConditionOut, AssetCondition? ConditionIn, string? HandedOverByName, string? ReceivedByName,
    string? Notes, bool IsOverdue, bool ReturnOnExit, bool ReturnRequested);

public record AssetRequestDto(
    Guid Id, AssetRequestKind Kind, Guid EmployeeId, string EmployeeName, string EmployeeCode, Guid TypeId, string Type, Guid? AssignmentId,
    string? AssetLabel, string Reason, DateOnly? NeededBy, DateOnly? ReturnBy, AssetRequestStatus Status, DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc, string? DecisionNote, bool CanFulfill);

public record AssetDetailDto(AssetDto Asset, List<AssignmentDto> History);

public record MyAssetsDto(List<AssignmentDto> Holding, List<AssignmentDto> Returned, List<AssetRequestDto> Requests);

public record EmployeeAssetsDto(List<AssignmentDto> Holding, List<AssignmentDto> Returned, List<AssetRequestDto> Open);

public record AssetSummaryDto(int Total, int Available, int Assigned, int InRepair, int Retired, int Lost, int OverdueReturns, int ToHandOver, int ToTakeBack, decimal Value);

[ApiController]
[Route("api/assets")]
[Authorize(Policy = Policies.CompanyUser)]
public class AssetsController(HrmsDbContext db, AssetService assets, TenantClock clock, ICurrentUser me) : ControllerBase
{
    // ---------- Types and kits ----------

    [HttpGet("types")]
    public async Task<List<AssetTypeDto>> Types()
    {
        var hr = me.IsHr();
        var list = await db.AssetTypes.Where(t => hr || t.IsActive).OrderBy(t => t.Name).ToListAsync();
        var people = list.SelectMany(t => HelpdeskService.Ids(t.HandlerIds)).Distinct().ToList();
        var names = await db.Employees.Where(e => people.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        var counts = await db.Assets.GroupBy(a => new { a.TypeId, a.Status }).Select(g => new { g.Key.TypeId, g.Key.Status, Count = g.Count() }).ToListAsync();
        return list.Select(t =>
        {
            var ids = HelpdeskService.Ids(t.HandlerIds);
            var c = counts.Where(x => x.TypeId == t.Id).ToList();
            return new AssetTypeDto(t.Id, t.Name, t.AllowMultiple, t.ReturnOnExit, ids, ids.Select(i => names.GetValueOrDefault(i) ?? "").ToList(), t.IsActive,
                c.Sum(x => x.Count), c.Where(x => x.Status == AssetStatus.Available).Sum(x => x.Count), c.Where(x => x.Status == AssetStatus.Assigned).Sum(x => x.Count));
        }).ToList();
    }

    [HttpPost("types"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> CreateType(SaveAssetTypeRequest req)
    {
        var t = new AssetType();
        await ApplyTypeAsync(t, req);
        db.AssetTypes.Add(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateType(Guid id, SaveAssetTypeRequest req)
    {
        var t = await db.AssetTypes.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Asset type");
        await ApplyTypeAsync(t, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("types/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteType(Guid id)
    {
        var t = await db.AssetTypes.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Asset type");
        if (await db.Assets.AnyAsync(a => a.TypeId == id) || await db.AssetRequests.AnyAsync(r => r.TypeId == id))
            throw new DomainException("This type is in use. Turn it off instead.");
        db.AssetTypes.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("kits"), Authorize(Policy = Policies.Hr)]
    public async Task<List<AssetKitDto>> Kits()
    {
        var kits = await db.AssetKits.OrderBy(k => k.Name).ToListAsync();
        var types = await db.AssetTypes.ToDictionaryAsync(t => t.Id, t => t.Name);
        return kits.Select(k =>
        {
            var ids = HelpdeskService.Ids(k.TypeIds).Where(types.ContainsKey).ToList();
            return new AssetKitDto(k.Id, k.Name, ids, ids.Select(i => types[i]).ToList());
        }).ToList();
    }

    [HttpPost("kits"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> CreateKit(SaveAssetKitRequest req)
    {
        var k = new AssetKit();
        await ApplyKitAsync(k, req);
        db.AssetKits.Add(k);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("kits/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateKit(Guid id, SaveAssetKitRequest req)
    {
        var k = await db.AssetKits.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Kit");
        await ApplyKitAsync(k, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("kits/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteKit(Guid id)
    {
        var k = await db.AssetKits.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Kit");
        db.AssetKits.Remove(k);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("kits/{id:guid}/apply/{employeeId:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<object> ApplyKit(Guid id, Guid employeeId) => new { requests = await assets.ApplyKitAsync(id, employeeId) };

    // ---------- Inventory ----------

    [HttpGet("me")]
    public async Task<object> Me() => new { isHandler = await assets.IsAnyHandlerAsync() };

    [HttpGet("summary")]
    public async Task<AssetSummaryDto> Summary()
    {
        var types = assets.HandledTypeIds();
        var q = db.Assets.Where(a => types.Contains(a.TypeId));
        var byStatus = await q.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var today = await clock.TodayAsync();
        var requests = db.AssetRequests.Where(r => r.Status == AssetRequestStatus.Approved && types.Contains(r.TypeId));
        return new AssetSummaryDto(byStatus.Values.Sum(), byStatus.GetValueOrDefault(AssetStatus.Available), byStatus.GetValueOrDefault(AssetStatus.Assigned),
            byStatus.GetValueOrDefault(AssetStatus.InRepair), byStatus.GetValueOrDefault(AssetStatus.Retired), byStatus.GetValueOrDefault(AssetStatus.Lost),
            await db.AssetAssignments.CountAsync(x => x.ReturnedOn == null && x.DueBack < today && types.Contains(x.Asset!.TypeId)),
            await requests.CountAsync(r => r.Kind == AssetRequestKind.New), await requests.CountAsync(r => r.Kind == AssetRequestKind.Return),
            await q.Where(a => a.Status != AssetStatus.Retired && a.Status != AssetStatus.Lost).SumAsync(a => a.Cost ?? 0));
    }

    /// <summary>The inventory for the types the signed-in user handles (everything for HR).</summary>
    [HttpGet]
    public async Task<List<AssetDto>> List([FromQuery] AssetStatus? status, [FromQuery] Guid? typeId, [FromQuery] string? search, [FromQuery] bool overdue = false)
    {
        var types = assets.HandledTypeIds();
        var q = db.Assets.Where(a => types.Contains(a.TypeId));
        if (status is { } s) q = q.Where(a => a.Status == s);
        if (typeId is { } t) q = q.Where(a => a.TypeId == t);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var x = search.Trim();
            var holders = db.AssetAssignments.Where(a => a.ReturnedOn == null && (a.Employee!.FirstName.Contains(x) || a.Employee.LastName.Contains(x))).Select(a => a.AssetId);
            q = q.Where(a => a.Tag.Contains(x) || a.Name.Contains(x) || (a.SerialNumber != null && a.SerialNumber.Contains(x)) || holders.Contains(a.Id));
        }
        if (overdue)
        {
            var today = await clock.TodayAsync();
            var late = db.AssetAssignments.Where(a => a.ReturnedOn == null && a.DueBack < today).Select(a => a.AssetId);
            q = q.Where(a => late.Contains(a.Id));
        }
        return await ProjectAssetsAsync(q.OrderBy(a => a.Tag).Take(1000));
    }

    [HttpGet("{id:guid}")]
    public async Task<AssetDetailDto> Get(Guid id)
    {
        var types = assets.HandledTypeIds();
        var item = (await ProjectAssetsAsync(db.Assets.Where(a => a.Id == id && types.Contains(a.TypeId)))).SingleOrDefault() ?? throw new NotFoundException("Asset");
        return new AssetDetailDto(item, await ProjectAssignmentsAsync(db.AssetAssignments.Where(a => a.AssetId == id).OrderByDescending(a => a.AssignedOn)));
    }

    [HttpPost]
    public async Task<AssetDto> Create(SaveAssetRequest req)
    {
        var a = new Asset();
        await ApplyAssetAsync(a, req);
        db.Assets.Add(a);
        await db.SaveChangesAsync();
        return (await ProjectAssetsAsync(db.Assets.Where(x => x.Id == a.Id))).Single();
    }

    [HttpPut("{id:guid}")]
    public async Task<AssetDto> Update(Guid id, SaveAssetRequest req)
    {
        var a = await db.Assets.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Asset");
        await ApplyAssetAsync(a, req);
        await db.SaveChangesAsync();
        return (await ProjectAssetsAsync(db.Assets.Where(x => x.Id == a.Id))).Single();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, AssetStatusRequest req)
    {
        await assets.SetStatusAsync(id, req.Status, req.Note);
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, AssignAssetRequest req)
    {
        await assets.AssignAsync(id, new HandOver(req.EmployeeId, req.AssignedOn, req.DueBack, req.Condition, req.Notes));
        return NoContent();
    }

    [HttpPost("assignments/{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, ReturnAssetRequest req)
    {
        await assets.ReturnAsync(id, req.ReturnedOn, req.Condition, req.Notes);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<FileContentResult> Export()
    {
        var rows = await List(null, null, null);
        var bytes = Excel.Write("Assets", rows,
            new("Tag", r => r.Tag), new("Name", r => r.Name), new("Type", r => r.Type), new("Serial number", r => r.SerialNumber),
            new("Status", r => AssetService.Label(r.Status)), new("Condition", r => r.Condition.ToString()), new("Held by", r => r.Holder),
            new("Since", r => r.AssignedOn), new("Due back", r => r.DueBack), new("Purchased", r => r.PurchasedOn), new("Cost", r => r.Cost, "#,##0"),
            new("Warranty until", r => r.WarrantyUntil), new("Notes", r => r.Notes));
        return File(bytes, Excel.ContentType, $"assets-{await clock.TodayAsync():yyyy-MM-dd}.xlsx");
    }

    // ---------- People ----------

    [HttpGet("my")]
    public async Task<MyAssetsDto> My()
    {
        var mine = me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");
        var all = await ProjectAssignmentsAsync(db.AssetAssignments.Where(a => a.EmployeeId == mine).OrderByDescending(a => a.AssignedOn));
        return new MyAssetsDto(all.Where(a => a.ReturnedOn is null).ToList(), all.Where(a => a.ReturnedOn is not null).Take(20).ToList(),
            await ProjectRequestsAsync(db.AssetRequests.Where(r => r.EmployeeId == mine).OrderByDescending(r => r.CreatedAtUtc).Take(30)));
    }

    /// <summary>What an employee holds, for their profile and their separation clearance.</summary>
    [HttpGet("employees/{employeeId:guid}"), Authorize(Policy = Policies.Approver)]
    public async Task<EmployeeAssetsDto> ForEmployee(Guid employeeId)
    {
        if (me.Role == UserRole.Manager && !await db.Employees.AnyAsync(e => e.Id == employeeId && (e.ManagerId == me.EmployeeId || e.Id == me.EmployeeId)))
            throw new ForbiddenException("You can only view your own team.");
        var all = await ProjectAssignmentsAsync(db.AssetAssignments.Where(a => a.EmployeeId == employeeId).OrderByDescending(a => a.AssignedOn));
        return new EmployeeAssetsDto(all.Where(a => a.ReturnedOn is null).ToList(), all.Where(a => a.ReturnedOn is not null).ToList(),
            await ProjectRequestsAsync(db.AssetRequests.Where(r => r.EmployeeId == employeeId && (r.Status == AssetRequestStatus.Pending || r.Status == AssetRequestStatus.Approved))));
    }

    // ---------- Requests ----------

    [HttpPost("requests/new")]
    public async Task<AssetRequestDto> RequestNew(NewAssetRequestBody req)
    {
        var r = await assets.RequestNewAsync(req.TypeId, req.Reason, req.NeededBy, req.ReturnBy);
        return (await ProjectRequestsAsync(db.AssetRequests.Where(x => x.Id == r.Id))).Single();
    }

    [HttpPost("requests/return")]
    public async Task<AssetRequestDto> RequestReturn(ReturnRequestBody req)
    {
        var r = await assets.RequestReturnAsync(req.AssignmentId, req.Reason ?? "");
        return (await ProjectRequestsAsync(db.AssetRequests.Where(x => x.Id == r.Id))).Single();
    }

    /// <summary>Handlers: approved requests waiting for hand-over or take-over (status=Approved), or history.</summary>
    [HttpGet("requests")]
    public Task<List<AssetRequestDto>> Requests([FromQuery] AssetRequestStatus status = AssetRequestStatus.Approved)
    {
        var types = assets.HandledTypeIds();
        return ProjectRequestsAsync(db.AssetRequests.Where(r => r.Status == status && types.Contains(r.TypeId)).OrderBy(r => r.CreatedAtUtc).Take(300));
    }

    [HttpPost("requests/{id:guid}/approve"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Approve(Guid id, DecideAssetRequest req)
    {
        await assets.DecideAsync(id, approve: true, req.Note);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/reject"), Authorize(Policy = Policies.Approver)]
    public async Task<IActionResult> Reject(Guid id, DecideAssetRequest req)
    {
        await assets.DecideAsync(id, approve: false, req.Note);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await assets.CancelAsync(id);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/fulfill")]
    public async Task<IActionResult> Fulfill(Guid id, FulfillAssetRequest req)
    {
        await assets.FulfillAsync(id, req.AssetId, req.On, req.Condition, req.Notes);
        return NoContent();
    }

    // ---------- Helpers ----------

    private async Task<List<AssetDto>> ProjectAssetsAsync(IQueryable<Asset> q)
    {
        var today = await clock.TodayAsync();
        var handled = (await assets.HandledTypeIds().ToListAsync()).ToHashSet();
        var rows = await q.Select(a => new
        {
            a.Id, a.Tag, a.Name, a.TypeId, Type = a.Type!.Name, a.SerialNumber, a.PurchasedOn, a.Cost, a.WarrantyUntil, a.Condition, a.Status, a.Notes,
            Open = db.AssetAssignments.Where(x => x.AssetId == a.Id && x.ReturnedOn == null)
                .Select(x => new { x.Id, x.EmployeeId, Name = x.Employee!.FirstName + " " + x.Employee.LastName, x.AssignedOn, x.DueBack }).FirstOrDefault(),
        }).ToListAsync();
        return rows.Select(r => new AssetDto(r.Id, r.Tag, r.Name, r.TypeId, r.Type, r.SerialNumber, r.PurchasedOn, r.Cost, r.WarrantyUntil, r.Condition, r.Status,
            r.Notes, r.Open?.EmployeeId, r.Open?.Name, r.Open?.Id, r.Open?.AssignedOn, r.Open?.DueBack, r.Open?.DueBack < today, handled.Contains(r.TypeId))).ToList();
    }

    private async Task<List<AssignmentDto>> ProjectAssignmentsAsync(IQueryable<AssetAssignment> q)
    {
        var today = await clock.TodayAsync();
        var rows = await q.Select(a => new
        {
            a.Id, a.AssetId, a.Asset!.Tag, a.Asset.Name, Type = a.Asset.Type!.Name, a.Asset.SerialNumber, a.EmployeeId,
            EmployeeName = a.Employee!.FirstName + " " + a.Employee.LastName, a.AssignedOn, a.DueBack, a.ReturnedOn, a.ConditionOut, a.ConditionIn,
            a.HandedOverByName, a.ReceivedByName, a.Notes, a.Asset.Type.ReturnOnExit,
            Requested = db.AssetRequests.Any(r => r.AssignmentId == a.Id && (r.Status == AssetRequestStatus.Pending || r.Status == AssetRequestStatus.Approved)),
        }).ToListAsync();
        return rows.Select(r => new AssignmentDto(r.Id, r.AssetId, r.Tag, r.Name, r.Type, r.SerialNumber, r.EmployeeId, r.EmployeeName, r.AssignedOn, r.DueBack,
            r.ReturnedOn, r.ConditionOut, r.ConditionIn, r.HandedOverByName, r.ReceivedByName, r.Notes, r.ReturnedOn == null && r.DueBack < today,
            r.ReturnOnExit, r.Requested)).ToList();
    }

    private async Task<List<AssetRequestDto>> ProjectRequestsAsync(IQueryable<AssetRequest> q)
    {
        var handled = (await assets.HandledTypeIds().ToListAsync()).ToHashSet();
        var rows = await q.Select(r => new
        {
            r.Id, r.Kind, r.EmployeeId, r.Employee!.FirstName, r.Employee.LastName, r.Employee.EmployeeCode, r.TypeId,
            Type = db.AssetTypes.Where(t => t.Id == r.TypeId).Select(t => t.Name).FirstOrDefault(),
            r.AssignmentId,
            Asset = db.AssetAssignments.Where(a => a.Id == r.AssignmentId).Select(a => a.Asset!.Name + " (" + a.Asset.Tag + ")").FirstOrDefault(),
            r.Reason, r.NeededBy, r.ReturnBy, r.Status, r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote,
        }).ToListAsync();
        return rows.Select(r => new AssetRequestDto(r.Id, r.Kind, r.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, r.TypeId, r.Type ?? "", r.AssignmentId,
            r.Asset, r.Reason, r.NeededBy, r.ReturnBy, r.Status, r.CreatedAtUtc, r.DecidedAtUtc, r.DecisionNote,
            r.Status == AssetRequestStatus.Approved && handled.Contains(r.TypeId))).ToList();
    }

    private async Task ApplyAssetAsync(Asset a, SaveAssetRequest req)
    {
        var type = await db.AssetTypes.FirstOrDefaultAsync(t => t.Id == req.TypeId) ?? throw new NotFoundException("Asset type");
        if (!assets.IsHandler(type))
            throw new ForbiddenException($"Only HR and the {type.Name} handlers can manage these.");
        if (a.Id != Guid.Empty && a.TypeId != Guid.Empty && a.TypeId != type.Id && a.Status == AssetStatus.Assigned)
            throw new DomainException("Take the asset back before changing its type.");
        var tag = string.IsNullOrWhiteSpace(req.Tag) ? (string.IsNullOrEmpty(a.Tag) ? await assets.NextTagAsync() : a.Tag) : req.Tag.Trim().ToUpperInvariant();
        if (await db.Assets.AnyAsync(x => x.Id != a.Id && x.Tag == tag))
            throw new DomainException($"Tag {tag} is already used.");
        if (!string.IsNullOrWhiteSpace(req.SerialNumber) && await db.Assets.AnyAsync(x => x.Id != a.Id && x.TypeId == type.Id && x.SerialNumber == req.SerialNumber.Trim()))
            throw new DomainException($"Another {type.Name.ToLowerInvariant()} has serial number {req.SerialNumber.Trim()}.");
        a.TypeId = type.Id;
        a.Tag = tag;
        a.Name = req.Name.Trim();
        a.SerialNumber = string.IsNullOrWhiteSpace(req.SerialNumber) ? null : req.SerialNumber.Trim();
        a.PurchasedOn = req.PurchasedOn;
        a.Cost = req.Cost;
        a.WarrantyUntil = req.WarrantyUntil;
        a.Condition = req.Condition;
        a.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
    }

    private async Task ApplyTypeAsync(AssetType t, SaveAssetTypeRequest req)
    {
        var name = req.Name.Trim();
        if (await db.AssetTypes.AnyAsync(x => x.Id != t.Id && x.Name == name))
            throw new DomainException($"There is already a {name} type.");
        var handlers = (req.HandlerIds ?? []).Distinct().ToList();
        if (handlers.Count > 30 || await db.Employees.CountAsync(e => handlers.Contains(e.Id)) != handlers.Count)
            throw new DomainException("Choose up to 30 handlers who work here.");
        t.Name = name;
        t.AllowMultiple = req.AllowMultiple;
        t.ReturnOnExit = req.ReturnOnExit;
        t.HandlerIds = string.Join(',', handlers);
        t.IsActive = req.IsActive;
    }

    private async Task ApplyKitAsync(AssetKit k, SaveAssetKitRequest req)
    {
        var name = req.Name.Trim();
        if (await db.AssetKits.AnyAsync(x => x.Id != k.Id && x.Name == name))
            throw new DomainException($"There is already a {name} kit.");
        var ids = (req.TypeIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
            throw new DomainException("Pick at least one asset type.");
        if (await db.AssetTypes.CountAsync(t => ids.Contains(t.Id)) != ids.Count)
            throw new NotFoundException("Asset type");
        k.Name = name;
        k.TypeIds = string.Join(',', ids);
    }
}
