using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Services;

public record AttachmentAccess(bool CanView, bool CanAdd, bool CanDeleteOthers);

public record NewAttachment(string FileName, byte[] Data, DocumentCategory? Category, DateOnly? ExpiresOn, string? Note);

/// <summary>Stores files on requests and employee records, and decides who may see, add or remove them.</summary>
public class AttachmentService(HrmsDbContext db, ICurrentUser me, ApprovalEngine approvals)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxPerOwner = 30;

    /// <summary>Allowed file types by extension. HTML, SVG and scripts are never accepted, so files cannot run in the browser.</summary>
    public static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    /// <summary>
    /// Leave: the employee and their approvers see it; the employee adds files while it is pending; HR always.
    /// Separation: the employee and HR; the employee adds while it is open.
    /// Employee documents: the employee and HR; both may add.
    /// The uploader may remove their own file while they may still add; HR may remove any.
    /// </summary>
    public async Task<AttachmentAccess> AccessAsync(AttachmentOwner type, Guid ownerId)
    {
        var hr = me.IsHr();
        switch (type)
        {
            case AttachmentOwner.Leave:
            {
                var r = await db.LeaveRequests.Where(x => x.Id == ownerId).Select(x => new { x.EmployeeId, x.Status }).FirstOrDefaultAsync()
                    ?? throw new NotFoundException("Leave request");
                var owner = r.EmployeeId == me.EmployeeId;
                var view = owner || await approvals.IsApproverAsync(ApprovalRequestType.Leave, ownerId, r.EmployeeId);
                return new(view, hr || (owner && r.Status == LeaveStatus.Pending), hr);
            }
            case AttachmentOwner.Separation:
            {
                var s = await db.Separations.Where(x => x.Id == ownerId).Select(x => new { x.EmployeeId, x.Status }).FirstOrDefaultAsync()
                    ?? throw new NotFoundException("Separation");
                var owner = s.EmployeeId == me.EmployeeId;
                var open = s.Status is SeparationStatus.Requested or SeparationStatus.InProgress;
                return new(owner || hr, hr || (owner && open), hr);
            }
            case AttachmentOwner.EmployeeDocument:
            {
                if (!await db.Employees.AnyAsync(e => e.Id == ownerId))
                    throw new NotFoundException("Employee");
                var self = ownerId == me.EmployeeId;
                return new(self || hr, self || hr, hr);
            }
            case AttachmentOwner.Announcement:
            {
                var a = await db.Announcements.Where(x => x.Id == ownerId).Select(x => new { x.DepartmentIds }).FirstOrDefaultAsync()
                    ?? throw new NotFoundException("Announcement");
                var dept = me.EmployeeId is { } mine ? await db.Employees.Where(e => e.Id == mine).Select(e => e.DepartmentId).FirstOrDefaultAsync() : null;
                return new(hr || AnnouncementService.InAudience(a.DepartmentIds, dept), hr, hr);
            }
            default:
                throw new NotFoundException("Attachment owner");
        }
    }

    public async Task<Attachment> AddAsync(AttachmentOwner type, Guid ownerId, NewAttachment file)
    {
        var access = await AccessAsync(type, ownerId);
        if (!access.CanView)
            throw new NotFoundException("Attachment owner");
        if (!access.CanAdd)
            throw new ForbiddenException("You cannot add files here.");

        var name = Path.GetFileName(file.FileName.Trim());
        var ext = Path.GetExtension(name);
        if (!Types.TryGetValue(ext, out var contentType))
            throw new DomainException("Upload a PDF, an image (JPG, PNG, WebP) or a Word/Excel file.");
        if (file.Data.Length == 0 || file.Data.Length > MaxBytes)
            throw new DomainException("Files must be smaller than 5 MB.");
        if (await db.Attachments.CountAsync(a => a.OwnerType == type && a.OwnerId == ownerId) >= MaxPerOwner)
            throw new DomainException($"At most {MaxPerOwner} files can be attached here.");
        if (type != AttachmentOwner.EmployeeDocument && (file.Category is not null || file.ExpiresOn is not null))
            throw new DomainException("Document type and expiry apply to employee documents only.");

        var attachment = new Attachment
        {
            OwnerType = type,
            OwnerId = ownerId,
            FileName = name.Length > 200 ? name[^200..] : name,
            ContentType = contentType,
            SizeBytes = file.Data.Length,
            Category = file.Category,
            ExpiresOn = file.ExpiresOn,
            Note = file.Note,
            UploadedByUserId = me.UserId,
            UploadedByName = await LetterService.CurrentUserNameAsync(db, me),
        };
        db.Attachments.Add(attachment);
        db.AttachmentContents.Add(new AttachmentContent { Id = attachment.Id, Data = file.Data });
        await db.SaveChangesAsync();
        return attachment;
    }

    public async Task<(Attachment Attachment, byte[] Data)> ReadAsync(Guid id)
    {
        var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("File");
        if (!(await AccessAsync(a.OwnerType, a.OwnerId)).CanView)
            throw new NotFoundException("File");
        var data = await db.AttachmentContents.Where(c => c.Id == id).Select(c => c.Data).FirstAsync();
        return (a, data);
    }

    public async Task DeleteAsync(Guid id)
    {
        var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("File");
        var access = await AccessAsync(a.OwnerType, a.OwnerId);
        if (!access.CanView)
            throw new NotFoundException("File");
        var mine = a.UploadedByUserId == me.UserId && access.CanAdd;
        if (!mine && !access.CanDeleteOthers)
            throw new ForbiddenException("You cannot remove this file.");
        db.Attachments.Remove(a);
        await db.SaveChangesAsync();
    }

    /// <summary>Whether the signed-in user may delete this file (for showing the button).</summary>
    public static bool CanDelete(Attachment a, AttachmentAccess access, ICurrentUser me) =>
        access.CanDeleteOthers || (a.UploadedByUserId == me.UserId && access.CanAdd);
}
