using HRMS.Domain.Common;

namespace HRMS.Api.Controllers;

/// <summary>
/// "Added … by … · Modified … by …" for any record, shown by the RecordStamp component on screens. Add it to a DTO as
/// <c>RecordStampsDto Stamps</c>; in a LINQ projection write it out (<c>new RecordStampsDto(r.CreatedAtUtc, r.CreatedBy,
/// r.ModifiedAtUtc, r.ModifiedBy)</c>) so EF can translate it, and use <see cref="Of"/> for loaded entities.
/// </summary>
public record RecordStampsDto(DateTime CreatedAtUtc, string? CreatedBy, DateTime? ModifiedAtUtc, string? ModifiedBy)
{
    public static RecordStampsDto Of(BaseEntity e) => new(e.CreatedAtUtc, e.CreatedBy, e.ModifiedAtUtc, e.ModifiedBy);
}
