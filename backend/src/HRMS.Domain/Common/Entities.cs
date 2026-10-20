namespace HRMS.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
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
