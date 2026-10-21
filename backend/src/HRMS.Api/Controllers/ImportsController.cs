using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Api.Controllers;

public class ImportUploadForm
{
    public IFormFile? File { get; set; }
    /// <summary>Employees only: create departments and designations that do not exist yet.</summary>
    public bool CreateMissing { get; set; }
}

/// <summary>Excel bulk imports. Upload with commit=false to check every row; commit=true imports only if all rows are valid.</summary>
[ApiController]
[Route("api/imports")]
[Authorize(Policy = Policies.CompanyUser)]
[Authorize(Policy = Policies.Hr)]
public class ImportsController(ImportService imports) : ControllerBase
{
    [HttpGet("{kind}/template")]
    public async Task<IActionResult> Template(string kind)
    {
        var bytes = kind switch
        {
            "employees" => await imports.EmployeeTemplateAsync(),
            "attendance" => await imports.AttendanceTemplateAsync(),
            _ => null,
        };
        return bytes is null ? NotFound() : File(bytes, Excel.ContentType, $"{kind}-import-template.xlsx");
    }

    [HttpPost("{kind}")]
    [RequestSizeLimit(Excel.MaxUploadBytes + 64 * 1024)]
    public async Task<ActionResult<ImportResult>> Upload(string kind, [FromForm] ImportUploadForm form, [FromQuery] bool commit = false)
    {
        if (form.File is null || form.File.Length == 0)
            throw new DomainException("Choose an Excel file to upload.");
        if (form.File.Length > Excel.MaxUploadBytes)
            throw new DomainException("The file must be smaller than 5 MB.");
        using var buffer = new MemoryStream();
        await form.File.CopyToAsync(buffer);
        buffer.Position = 0;
        var result = kind switch
        {
            "employees" => await imports.ImportEmployeesAsync(buffer, commit, form.CreateMissing),
            "attendance" => await imports.ImportAttendanceAsync(buffer, commit),
            _ => null,
        };
        if (result is null)
            return NotFound();
        // A commit with problems saves nothing; say so with 400 and return the row-by-row report.
        return commit && !result.Committed ? BadRequest(result) : result;
    }
}
