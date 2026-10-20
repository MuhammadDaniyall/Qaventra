using HRMS.Domain.Common;

namespace HRMS.Domain.Entities;

/// <summary>A file attached to a request or an employee's record. The bytes live in <see cref="AttachmentContent"/>.</summary>
public class Attachment : TenantEntity
{
    public AttachmentOwner OwnerType { get; set; }
    /// <summary>Id of the leave request, separation, employee (for documents), etc.</summary>
    public Guid OwnerId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    /// <summary>Employee documents only.</summary>
    public DocumentCategory? Category { get; set; }
    /// <summary>Employee documents only, e.g. CNIC or passport expiry.</summary>
    public DateOnly? ExpiresOn { get; set; }
    public string? Note { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
}

/// <summary>File bytes, kept apart so listing attachments never loads them. Shares its Id with the attachment.</summary>
public class AttachmentContent : TenantEntity
{
    public byte[] Data { get; set; } = [];
}
