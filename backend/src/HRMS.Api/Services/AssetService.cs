using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record HandOver(Guid EmployeeId, DateOnly AssignedOn, DateOnly? DueBack, AssetCondition Condition, string? Notes);

/// <summary>
/// Company property: who holds what, hand-over and take-over, and requests for new assets or returns.
/// HR and each type's handlers manage assets; new-asset requests go through the asset approval chain first.
/// </summary>
public class AssetService(HrmsDbContext db, ICurrentUser me, TenantClock clock, ApprovalEngine approvals, NotificationService notify)
{
    public bool IsHandler(AssetType type) => me.IsHr() || HelpdeskService.Has(type.HandlerIds, me.EmployeeId);

    /// <summary>Types the signed-in user may hand over and take back (all for HR).</summary>
    public IQueryable<Guid> HandledTypeIds()
    {
        if (me.IsHr())
            return db.AssetTypes.Select(t => t.Id);
        var text = (me.EmployeeId ?? Guid.Empty).ToString();
        return db.AssetTypes.Where(t => t.HandlerIds.Contains(text)).Select(t => t.Id);
    }

    public async Task<bool> IsAnyHandlerAsync() => me.IsHr() || await HandledTypeIds().AnyAsync();

    public async Task<string> NextTagAsync()
    {
        var tags = await db.Assets.Where(a => a.Tag.StartsWith("AST-")).Select(a => a.Tag).ToListAsync();
        var max = tags.Select(t => int.TryParse(t[4..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"AST-{max + 1:D4}";
    }

    /// <summary>Hands an available asset to an employee.</summary>
    public async Task<AssetAssignment> AssignAsync(Guid assetId, HandOver h, Guid? requestId = null)
    {
        var asset = await db.Assets.Include(a => a.Type).FirstOrDefaultAsync(a => a.Id == assetId) ?? throw new NotFoundException("Asset");
        RequireHandler(asset.Type!);
        if (asset.Status != AssetStatus.Available)
            throw new DomainException($"{asset.Tag} is not available ({Label(asset.Status).ToLowerInvariant()}).");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == h.EmployeeId) ?? throw new NotFoundException("Employee");
        if (!employee.IsCurrent)
            throw new DomainException($"{employee.FullName} no longer works here.");
        if (!asset.Type!.AllowMultiple && await db.AssetAssignments.AnyAsync(x => x.EmployeeId == h.EmployeeId && x.ReturnedOn == null && x.Asset!.TypeId == asset.TypeId))
            throw new DomainException($"{employee.FirstName} already has a {asset.Type.Name.ToLowerInvariant()}. Take that back first, or allow more than one for this type.");
        if (h.DueBack is { } due && due < h.AssignedOn)
            throw new DomainException("The return date is before the hand-over date.");
        if (h.AssignedOn > (await clock.TodayAsync()).AddDays(30))
            throw new DomainException("Record the hand-over on the day it happens.");

        var assignment = new AssetAssignment
        {
            AssetId = asset.Id,
            EmployeeId = employee.Id,
            AssignedOn = h.AssignedOn,
            DueBack = h.DueBack,
            ConditionOut = h.Condition,
            HandedOverByName = await MyNameAsync(),
            Notes = Clean(h.Notes),
            RequestId = requestId,
        };
        db.AssetAssignments.Add(assignment);
        asset.Status = AssetStatus.Assigned;
        asset.Condition = h.Condition;
        await notify.ToEmployeesAsync([employee.Id], NotificationKind.Asset, $"{asset.Name} ({asset.Tag}) handed over to you",
            h.DueBack is { } d ? $"Please return it by {d:d MMM yyyy}." : null, "/me/assets");
        await db.SaveChangesAsync();
        return assignment;
    }

    /// <summary>Takes an asset back. Damaged ones go to repair; the rest are available again.</summary>
    public async Task ReturnAsync(Guid assignmentId, DateOnly returnedOn, AssetCondition condition, string? notes)
    {
        var a = await db.AssetAssignments.Include(x => x.Asset).ThenInclude(x => x!.Type).FirstOrDefaultAsync(x => x.Id == assignmentId)
                ?? throw new NotFoundException("Assignment");
        RequireHandler(a.Asset!.Type!);
        if (a.ReturnedOn is not null)
            throw new DomainException("This asset has already been returned.");
        if (returnedOn < a.AssignedOn)
            throw new DomainException("The return date is before it was handed over.");
        a.ReturnedOn = returnedOn;
        a.ConditionIn = condition;
        a.ReceivedByName = await MyNameAsync();
        if (Clean(notes) is { } n)
            a.Notes = a.Notes is null ? n : $"{a.Notes}\n{n}";
        a.Asset.Condition = condition;
        a.Asset.Status = condition == AssetCondition.Damaged ? AssetStatus.InRepair : AssetStatus.Available;
        foreach (var r in await db.AssetRequests.Where(r => r.AssignmentId == a.Id && (r.Status == AssetRequestStatus.Pending || r.Status == AssetRequestStatus.Approved)).ToListAsync())
        {
            r.Status = AssetRequestStatus.Fulfilled;
            r.FulfilledAtUtc = clock.UtcNow;
        }
        await notify.ToEmployeesAsync([a.EmployeeId], NotificationKind.Asset, $"{a.Asset.Name} ({a.Asset.Tag}) received back", null, "/me/assets");
        await db.SaveChangesAsync();
    }

    /// <summary>Repair, retire, lost or available again. A lost asset that was with someone ends their assignment.</summary>
    public async Task SetStatusAsync(Guid assetId, AssetStatus status, string? note)
    {
        var asset = await db.Assets.Include(a => a.Type).FirstOrDefaultAsync(a => a.Id == assetId) ?? throw new NotFoundException("Asset");
        RequireHandler(asset.Type!);
        if (status == AssetStatus.Assigned)
            throw new DomainException("Hand the asset over to someone to assign it.");
        if (asset.Status == AssetStatus.Assigned)
        {
            if (status != AssetStatus.Lost)
                throw new DomainException("Take the asset back first.");
            var open = await db.AssetAssignments.FirstAsync(x => x.AssetId == asset.Id && x.ReturnedOn == null);
            open.ReturnedOn = await clock.TodayAsync();
            open.ReceivedByName = await MyNameAsync();
            open.Notes = string.IsNullOrWhiteSpace(note) ? "Reported lost" : $"Reported lost: {note.Trim()}";
        }
        asset.Status = status;
        if (Clean(note) is { } n)
            asset.Notes = asset.Notes is null ? n : $"{asset.Notes}\n{n}";
        await db.SaveChangesAsync();
    }

    // ---------- Requests ----------

    public async Task<AssetRequest> RequestNewAsync(Guid typeId, string reason, DateOnly? neededBy, DateOnly? returnBy)
    {
        var employeeId = MyId();
        var type = await db.AssetTypes.FirstOrDefaultAsync(t => t.Id == typeId && t.IsActive) ?? throw new NotFoundException("Asset type");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Say why you need it.");
        var today = await clock.TodayAsync();
        if (returnBy is { } rb && rb < (neededBy ?? today))
            throw new DomainException("The return date must be after you get it.");
        if (await db.AssetRequests.AnyAsync(r => r.EmployeeId == employeeId && r.TypeId == typeId && r.Kind == AssetRequestKind.New
                                                 && (r.Status == AssetRequestStatus.Pending || r.Status == AssetRequestStatus.Approved)))
            throw new DomainException($"You already have a {type.Name.ToLowerInvariant()} request open.");

        var request = new AssetRequest
        {
            EmployeeId = employeeId, Kind = AssetRequestKind.New, TypeId = type.Id, Reason = reason.Trim(), NeededBy = neededBy, ReturnBy = returnBy,
        };
        db.AssetRequests.Add(request);
        var summary = $"{type.Name}" + (returnBy is { } r2 ? $" until {r2:d MMM}" : "") + $" · {request.Reason}";
        var approval = await approvals.StartAsync(ApprovalRequestType.Asset, request.Id, employeeId, summary.Length > 300 ? summary[..300] : summary);
        if (approval.Status == ApprovalStatus.Approved)
            await ApproveAsync(request, type, "Approved automatically");
        await db.SaveChangesAsync();
        return request;
    }

    /// <summary>Returns need no approval: the handler is asked to take the asset back.</summary>
    public async Task<AssetRequest> RequestReturnAsync(Guid assignmentId, string reason)
    {
        var employeeId = MyId();
        var a = await db.AssetAssignments.Include(x => x.Asset).ThenInclude(x => x!.Type).FirstOrDefaultAsync(x => x.Id == assignmentId && x.EmployeeId == employeeId)
                ?? throw new NotFoundException("Asset");
        if (a.ReturnedOn is not null)
            throw new DomainException("You have already returned this.");
        if (await db.AssetRequests.AnyAsync(r => r.AssignmentId == assignmentId && (r.Status == AssetRequestStatus.Pending || r.Status == AssetRequestStatus.Approved)))
            throw new DomainException("You have already asked to return this.");
        var request = new AssetRequest
        {
            EmployeeId = employeeId, Kind = AssetRequestKind.Return, TypeId = a.Asset!.TypeId, AssignmentId = a.Id,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Return" : reason.Trim(), Status = AssetRequestStatus.Approved, DecidedAtUtc = clock.UtcNow,
        };
        db.AssetRequests.Add(request);
        var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        await NotifyHandlersAsync(a.Asset.Type!, $"{name} wants to return {a.Asset.Name} ({a.Asset.Tag})", request.Reason);
        await db.SaveChangesAsync();
        return request;
    }

    public async Task DecideAsync(Guid id, bool approve, string? note)
    {
        var request = await db.AssetRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Asset request");
        if (request.Status != AssetRequestStatus.Pending)
            throw new DomainException("This request is not waiting for approval.");
        var outcome = await approvals.DecideAsync(ApprovalRequestType.Asset, request.Id, request.EmployeeId, approve, note);
        if (outcome == ApprovalStatus.Approved)
            await ApproveAsync(request, await db.AssetTypes.FirstAsync(t => t.Id == request.TypeId), note);
        else if (outcome == ApprovalStatus.Rejected)
        {
            request.Status = AssetRequestStatus.Rejected;
            request.DecidedAtUtc = clock.UtcNow;
            request.DecisionNote = note;
        }
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid id)
    {
        var request = await db.AssetRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Asset request");
        if (request.EmployeeId != me.EmployeeId && !me.IsHr())
            throw new ForbiddenException("You cannot cancel this request.");
        if (request.Status is not (AssetRequestStatus.Pending or AssetRequestStatus.Approved))
            throw new DomainException("This request can no longer be cancelled.");
        if (request.Status == AssetRequestStatus.Pending)
            await approvals.CancelAsync(ApprovalRequestType.Asset, request.Id);
        request.Status = AssetRequestStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    /// <summary>Handler: hand over an asset for an approved new-asset request, or take one back for a return request.</summary>
    public async Task FulfillAsync(Guid id, Guid? assetId, DateOnly on, AssetCondition condition, string? notes)
    {
        var request = await db.AssetRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException("Asset request");
        if (request.Status != AssetRequestStatus.Approved)
            throw new DomainException(request.Status == AssetRequestStatus.Pending ? "This request is still waiting for approval." : "This request is already closed.");
        if (request.Kind == AssetRequestKind.Return)
        {
            await ReturnAsync(request.AssignmentId!.Value, on, condition, notes);
            return;
        }
        if (assetId is not { } aid)
            throw new DomainException("Choose the asset to hand over.");
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == aid) ?? throw new NotFoundException("Asset");
        if (asset.TypeId != request.TypeId)
            throw new DomainException("That asset is not the kind that was asked for.");
        await AssignAsync(aid, new HandOver(request.EmployeeId, on, request.ReturnBy, condition, notes), request.Id);
        request.Status = AssetRequestStatus.Fulfilled;
        request.FulfilledAtUtc = clock.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>HR: queue a starter kit for someone, one approved request per asset type, for the handlers to hand over.</summary>
    public async Task<int> ApplyKitAsync(Guid kitId, Guid employeeId)
    {
        var kit = await db.AssetKits.FirstOrDefaultAsync(k => k.Id == kitId) ?? throw new NotFoundException("Kit");
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new NotFoundException("Employee");
        var typeIds = HelpdeskService.Ids(kit.TypeIds);
        var types = await db.AssetTypes.Where(t => typeIds.Contains(t.Id) && t.IsActive).ToListAsync();
        var open = await db.AssetRequests.Where(r => r.EmployeeId == employeeId && r.Kind == AssetRequestKind.New
                                                     && (r.Status == AssetRequestStatus.Pending || r.Status == AssetRequestStatus.Approved))
            .Select(r => r.TypeId).ToListAsync();
        var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        var count = 0;
        foreach (var t in types.Where(t => !open.Contains(t.Id)))
        {
            db.AssetRequests.Add(new AssetRequest
            {
                EmployeeId = employeeId, Kind = AssetRequestKind.New, TypeId = t.Id, Reason = $"{kit.Name} kit",
                Status = AssetRequestStatus.Approved, DecidedAtUtc = clock.UtcNow, DecisionNote = "Starter kit from HR",
            });
            await NotifyHandlersAsync(t, $"Hand over a {t.Name.ToLowerInvariant()} to {name}", $"{kit.Name} kit");
            count++;
        }
        await db.SaveChangesAsync();
        return count;
    }

    private async Task ApproveAsync(AssetRequest request, AssetType type, string? note)
    {
        request.Status = AssetRequestStatus.Approved;
        request.DecidedAtUtc = clock.UtcNow;
        request.DecisionNote = note;
        var name = await db.Employees.Where(e => e.Id == request.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync();
        await NotifyHandlersAsync(type, $"Hand over a {type.Name.ToLowerInvariant()} to {name}", request.Reason);
    }

    private async Task NotifyHandlersAsync(AssetType type, string title, string? body)
    {
        var handlers = HelpdeskService.Ids(type.HandlerIds);
        if (handlers.Count > 0)
            await notify.ToEmployeesAsync(handlers, NotificationKind.Asset, title, body, "/assets?tab=requests");
        else
            await notify.ToHrAsync(NotificationKind.Asset, title, body, "/assets?tab=requests");
    }

    public static string Label(AssetStatus s) => s switch
    {
        AssetStatus.InRepair => "In repair",
        _ => s.ToString(),
    };

    private void RequireHandler(AssetType type)
    {
        if (!IsHandler(type))
            throw new ForbiddenException($"Only HR and the {type.Name} handlers can do this.");
    }

    private Task<string?> MyNameAsync() =>
        me.UserId is { } uid ? db.Users.IgnoreQueryFilters().Where(u => u.Id == uid).Select(u => (string?)u.FullName).FirstOrDefaultAsync() : Task.FromResult<string?>(null);

    private Guid MyId() => me.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record.");

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > 1000 ? s.Trim()[..1000] : s.Trim();
}
