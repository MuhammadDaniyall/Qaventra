namespace HRMS.Domain.Common;

/// <summary>
/// Every row knows when it was added and last changed, and by whom. HrmsDbContext stamps these on save; services never
/// set them. CreatedBy/ModifiedBy are a display snapshot such as "Ali Raza (EMP-0042)", "Sara Khan (Owner)" or "System",
/// so screens show them without joins; the *ById columns hold the login's user id. Null on rows older than the stamps.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedById { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public Guid? ModifiedById { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>Every row that belongs to one company. The DbContext filters these by the signed-in tenant.</summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}

/// <summary>Thrown for business-rule failures; the API turns it into a 400 response.</summary>
public class DomainException(string message) : Exception(message);
