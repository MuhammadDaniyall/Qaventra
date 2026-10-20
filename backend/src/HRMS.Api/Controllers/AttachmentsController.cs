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
