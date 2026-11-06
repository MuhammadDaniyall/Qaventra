using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A package the platform owner sells: price, employee limit and the modules it includes. Prices are PKR per month.</summary>
public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Monthly price per licensed employee (0 when the plan is flat-priced).</summary>
    public decimal PricePerEmployee { get; set; }
    /// <summary>Monthly flat fee (0 when priced only per employee).</summary>
    public decimal FlatPrice { get; set; }
    /// <summary>Employee limit; null = unlimited.</summary>
    public int? MaxEmployees { get; set; }
    /// <summary>Comma-separated module keys the plan includes; core modules are always on.</summary>
    public string Modules { get; set; } = "";
    /// <summary>Free days for a new company on this plan.</summary>
    public int TrialDays { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum InvoiceStatus
{
    Unpaid = 0,
    /// <summary>Bank transfer proof uploaded; waiting for the platform owner.</summary>
    AwaitingApproval = 1,
    Paid = 2,
    Cancelled = 3,
}

/// <summary>A bill for one or more billing cycles. Paying it (online, or approved bank transfer) records a payment and extends the subscription.</summary>
public class TenantInvoice : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Number { get; set; } = "";
    public decimal Amount { get; set; }
    /// <summary>Billing cycles this invoice covers.</summary>
    public int Periods { get; set; } = 1;
    public DateOnly IssuedOn { get; set; }
    public string? Description { get; set; }
    public InvoiceStatus Status { get; set; }
    /// <summary>Gateway used: "manual" or an online provider key.</summary>
    public string? Provider { get; set; }
    /// <summary>The provider's order / tracker id, or the bank transfer reference the company typed.</summary>
    public string? ProviderReference { get; set; }
    public string? ProofFileName { get; set; }
    public string? ProofContentType { get; set; }
    public byte[]? ProofData { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public Guid? PaymentId { get; set; }
}

/// <summary>Every payment webhook seen, keyed by provider and event, so a repeated delivery is applied once.</summary>
public class PaymentWebhookEvent : BaseEntity
{
    public string Provider { get; set; } = "";
    public string EventKey { get; set; } = "";
    public Guid? InvoiceId { get; set; }
    public string? Result { get; set; }
}
