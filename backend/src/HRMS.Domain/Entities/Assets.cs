using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A kind of company property: laptop, phone, SIM, access card, vehicle...</summary>
public class AssetType : TenantEntity
{
    public string Name { get; set; } = "";
    /// <summary>One person may hold more than one at a time (e.g. SIMs); otherwise one each.</summary>
    public bool AllowMultiple { get; set; }
    /// <summary>Must come back before someone leaves (shows on their separation).</summary>
    public bool ReturnOnExit { get; set; } = true;
    /// <summary>Who hands these over and takes them back (comma-separated employee ids); HR when empty.</summary>
    public string HandlerIds { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

/// <summary>One item of company property.</summary>
public class Asset : TenantEntity
{
    public Guid TypeId { get; set; }
    public AssetType? Type { get; set; }
    /// <summary>The company's own label, e.g. AST-0042, printed on the sticker.</summary>
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    public string? SerialNumber { get; set; }
    public DateOnly? PurchasedOn { get; set; }
    public decimal? Cost { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    public AssetCondition Condition { get; set; } = AssetCondition.Good;
    public AssetStatus Status { get; set; } = AssetStatus.Available;
    public string? Notes { get; set; }
}

/// <summary>An asset in someone's hands, from hand-over to take-over.</summary>
public class AssetAssignment : TenantEntity
{
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly AssignedOn { get; set; }
    /// <summary>Temporary loans only: when it should come back.</summary>
    public DateOnly? DueBack { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public AssetCondition ConditionOut { get; set; }
    public AssetCondition? ConditionIn { get; set; }
    public string? HandedOverByName { get; set; }
    public string? ReceivedByName { get; set; }
    public string? Notes { get; set; }
    public Guid? RequestId { get; set; }
}

/// <summary>An employee asks for an asset or to give one back; approved through the asset chain, then fulfilled by a handler.</summary>
public class AssetRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public AssetRequestKind Kind { get; set; }
    /// <summary>New: what kind of asset.</summary>
    public Guid TypeId { get; set; }
    /// <summary>Return: the assignment being ended.</summary>
    public Guid? AssignmentId { get; set; }
    public string Reason { get; set; } = "";
    public DateOnly? NeededBy { get; set; }
    /// <summary>New and temporary: when it will come back.</summary>
    public DateOnly? ReturnBy { get; set; }
    public AssetRequestStatus Status { get; set; } = AssetRequestStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? FulfilledAtUtc { get; set; }
}

/// <summary>A set of asset types given together, e.g. the developer starter kit.</summary>
public class AssetKit : TenantEntity
{
    public string Name { get; set; } = "";
    /// <summary>Comma-separated asset type ids.</summary>
    public string TypeIds { get; set; } = "";
}
