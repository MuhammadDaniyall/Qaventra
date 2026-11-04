using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace HRMS.Api.Controllers;

public record AttachmentDto(
    Guid Id, string FileName, string ContentType, long SizeBytes, DocumentCategory? Category, DateOnly? ExpiresOn,
    string? Note, string? UploadedByName, DateTime CreatedAtUtc, bool CanDelete);

public record AttachmentListDto(bool CanAdd, List<AttachmentDto> Items);

public record DocumentSettingDto(AttachmentOwner OwnerType, string Label, bool Enabled, int MaxSizeMb, int Files, long Bytes);
public record DocumentStorageDto(List<DocumentSettingDto> Modules, int ReceiptFiles, long ReceiptBytes, long TotalBytes);
public record SaveDocumentSettingRequest(bool Enabled, [Range(1, 5)] int MaxSizeMb);

public class UploadAttachmentForm
{
    [Required]
    public IFormFile File { get; set; } = null!;
    public DocumentCategory? Category { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    [StringLength(500)]
    public string? Note { get; set; }
}

[ApiController]
[Route("api/attachments")]
[Authorize(Policy = Policies.CompanyUser)]
public class AttachmentsController(HrmsDbContext db, AttachmentService attachments, ICurrentUser me) : ControllerBase
{
    private static readonly Dictionary<AttachmentOwner, string> ModuleLabels = new()
    {
        [AttachmentOwner.EmployeeDocument] = "Employee documents",
        [AttachmentOwner.Leave] = "Leave requests",
        [AttachmentOwner.Separation] = "Resignations and exits",
        [AttachmentOwner.Announcement] = "Announcements",
        [AttachmentOwner.Ticket] = "Help desk tickets",
        [AttachmentOwner.Candidate] = "Candidates (CVs)",
        [AttachmentOwner.Travel] = "Travel requests",
        [AttachmentOwner.TrainingRequest] = "Training requests",
    };

    /// <summary>Per module: attachments on or off, the size limit, and how much space files take.</summary>
    [HttpGet("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<DocumentStorageDto> Settings()
    {
        var settings = await db.DocumentSettings.ToDictionaryAsync(s => s.OwnerType);
        var used = await db.Attachments.GroupBy(a => a.OwnerType).Select(g => new { g.Key, Files = g.Count(), Bytes = g.Sum(a => a.SizeBytes) }).ToDictionaryAsync(x => x.Key);
        var modules = ModuleLabels.Select(m => new DocumentSettingDto(m.Key, m.Value, settings.GetValueOrDefault(m.Key)?.Enabled ?? true, settings.GetValueOrDefault(m.Key)?.MaxSizeMb ?? 5,
            used.GetValueOrDefault(m.Key)?.Files ?? 0, used.GetValueOrDefault(m.Key)?.Bytes ?? 0)).ToList();
        var receipts = await db.ExpenseReceipts.Select(r => (long)r.Data.Length).ToListAsync();
        return new DocumentStorageDto(modules, receipts.Count, receipts.Sum(), modules.Sum(m => m.Bytes) + receipts.Sum());
    }

    [HttpPut("settings/{ownerType}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveSetting(AttachmentOwner ownerType, SaveDocumentSettingRequest req)
    {
        if (!ModuleLabels.ContainsKey(ownerType))
            return NotFound();
        var s = await db.DocumentSettings.FirstOrDefaultAsync(x => x.OwnerType == ownerType);
        if (s is null)
            db.DocumentSettings.Add(s = new DocumentSetting { OwnerType = ownerType });
        s.Enabled = req.Enabled;
        s.MaxSizeMb = req.MaxSizeMb;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{ownerType}/{ownerId:guid}")]
    public async Task<AttachmentListDto> List(AttachmentOwner ownerType, Guid ownerId)
    {
        var access = await attachments.AccessAsync(ownerType, ownerId);
        if (!access.CanView)
            throw new Infrastructure.NotFoundException("Attachments");
        var items = await db.Attachments.Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId)
            .OrderByDescending(a => a.CreatedAtUtc).ToListAsync();
        return new AttachmentListDto(access.CanAdd, items.Select(a => ToDto(a, AttachmentService.CanDelete(a, access, me))).ToList());
    }

    [HttpPost("{ownerType}/{ownerId:guid}")]
    [RequestSizeLimit(AttachmentService.MaxBytes + 64 * 1024)]
    public async Task<AttachmentDto> Upload(AttachmentOwner ownerType, Guid ownerId, [FromForm] UploadAttachmentForm form)
    {
        if (form.File.Length > AttachmentService.MaxBytes)
            throw new Domain.Common.DomainException("Files must be smaller than 5 MB.");
        using var buffer = new MemoryStream();
        await form.File.CopyToAsync(buffer);
        var a = await attachments.AddAsync(ownerType, ownerId, new NewAttachment(form.File.FileName, buffer.ToArray(), form.Category, form.ExpiresOn, form.Note));
        return ToDto(a, canDelete: true);
    }

    /// <summary>Images and PDFs open in the browser; other types download. Never sniffed as HTML.</summary>
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id)
    {
        var (a, data) = await attachments.ReadAsync(id);
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        var inline = a.ContentType is "application/pdf" || a.ContentType.StartsWith("image/");
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(a.FileName);
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        return File(data, a.ContentType);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await attachments.DeleteAsync(id);
        return NoContent();
    }

    private static AttachmentDto ToDto(Attachment a, bool canDelete) => new(
        a.Id, a.FileName, a.ContentType, a.SizeBytes, a.Category, a.ExpiresOn, a.Note, a.UploadedByName, a.CreatedAtUtc, canDelete);
}
