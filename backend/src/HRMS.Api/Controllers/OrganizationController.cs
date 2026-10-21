using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record DepartmentDto(Guid Id, string Name, string? Code, int Employees, Guid? HeadEmployeeId = null, string? Head = null);
public record SaveDepartmentRequest([Required, StringLength(150)] string Name, [StringLength(30)] string? Code, Guid? HeadEmployeeId = null);

public record DesignationDto(Guid Id, string Title, int Employees);
public record SaveDesignationRequest([Required, StringLength(150)] string Title);

[ApiController]
[Route("api")]
[Authorize(Policy = Policies.CompanyUser)]
public class OrganizationController(HrmsDbContext db) : ControllerBase
{
    [HttpGet("departments")]
    public Task<List<DepartmentDto>> Departments() =>
        db.Departments.OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, db.Employees.Count(e => e.DepartmentId == d.Id), d.HeadEmployeeId,
                db.Employees.Where(e => e.Id == d.HeadEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault()))
            .ToListAsync();

    [HttpPost("departments"), Authorize(Policy = Policies.Hr)]
    public async Task<DepartmentDto> CreateDepartment(SaveDepartmentRequest req)
    {
        await ValidateHeadAsync(req.HeadEmployeeId);
        var d = new Department { Name = req.Name.Trim(), Code = req.Code?.Trim(), HeadEmployeeId = req.HeadEmployeeId };
        db.Departments.Add(d);
        await db.SaveChangesAsync();
        return new DepartmentDto(d.Id, d.Name, d.Code, 0, d.HeadEmployeeId);
    }

    [HttpPut("departments/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateDepartment(Guid id, SaveDepartmentRequest req)
    {
        var d = await db.Departments.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Department");
        await ValidateHeadAsync(req.HeadEmployeeId);
        d.Name = req.Name.Trim();
        d.Code = req.Code?.Trim();
        d.HeadEmployeeId = req.HeadEmployeeId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ValidateHeadAsync(Guid? headId)
    {
        if (headId is { } h && !await db.Employees.AnyAsync(e => e.Id == h && (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.Probation)))
            throw new DomainException("Choose a department head who currently works here.");
    }

    [HttpDelete("departments/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteDepartment(Guid id)
    {
        var d = await db.Departments.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Department");
        if (await db.Employees.AnyAsync(e => e.DepartmentId == id))
            throw new DomainException("Move this department's employees before deleting it.");
        db.Departments.Remove(d);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("designations")]
    public Task<List<DesignationDto>> Designations() =>
        db.Designations.OrderBy(d => d.Title)
            .Select(d => new DesignationDto(d.Id, d.Title, db.Employees.Count(e => e.DesignationId == d.Id)))
            .ToListAsync();

    [HttpPost("designations"), Authorize(Policy = Policies.Hr)]
    public async Task<DesignationDto> CreateDesignation(SaveDesignationRequest req)
    {
        var d = new Designation { Title = req.Title.Trim() };
        db.Designations.Add(d);
        await db.SaveChangesAsync();
        return new DesignationDto(d.Id, d.Title, 0);
    }

    [HttpPut("designations/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> UpdateDesignation(Guid id, SaveDesignationRequest req)
    {
        var d = await db.Designations.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Designation");
        d.Title = req.Title.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("designations/{id:guid}"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> DeleteDesignation(Guid id)
    {
        var d = await db.Designations.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Designation");
        if (await db.Employees.AnyAsync(e => e.DesignationId == id))
            throw new DomainException("Reassign employees with this designation before deleting it.");
        db.Designations.Remove(d);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
