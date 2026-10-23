using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record CareersJobDto(
    string Slug, string Title, string? Department, string? Location, EmploymentType EmploymentType, string Description, string? Requirements,
    decimal? SalaryMin, decimal? SalaryMax, DateOnly? ClosesOn, DateTime? PublishedAtUtc);

public record CareersPageDto(string Company, string? City, List<CareersJobDto> Jobs);

/// <summary>The public careers page: a company's open jobs and the application form. No sign-in.</summary>
[ApiController]
[Route("api/careers")]
[AllowAnonymous]
public class CareersController(HrmsDbContext db, RecruitmentService hiring, TenantClock clock) : ControllerBase
{
    [HttpGet("{company}")]
    public async Task<CareersPageDto> Page(string company)
    {
        var tenant = await TenantAsync(company);
        var jobs = await (await OpenJobsAsync()).OrderByDescending(p => p.PublishedAtUtc).ToListAsync();
        return new CareersPageDto(tenant.Name, tenant.City, await ToDtosAsync(jobs));
    }

    [HttpGet("{company}/{job}")]
    public async Task<CareersJobDto> Job(string company, string job)
    {
        await TenantAsync(company);
        var post = await (await OpenJobsAsync()).FirstOrDefaultAsync(p => p.Slug == job) ?? throw new NotFoundException("Job");
        return (await ToDtosAsync([post]))[0];
    }

    [HttpPost("{company}/{job}/apply")]
    [EnableRateLimiting("careers")]
    [RequestSizeLimit(AttachmentService.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Apply(string company, string job, [FromForm] CandidateForm form)
    {
        await TenantAsync(company);
        var post = await (await OpenJobsAsync()).FirstOrDefaultAsync(p => p.Slug == job) ?? throw new NotFoundException("Job");
        var cv = await form.ReadCvAsync() ?? throw new Domain.Common.DomainException("Attach your CV.");
        await hiring.ApplyAsync(post, form.ToInput() with { ReferredBy = null }, CandidateSource.CareersPage, form.CoverLetter, cv);
        return NoContent();
    }

    /// <summary>Finds the company and scopes the rest of the request to it. Unknown, inactive and expired companies look the same.</summary>
    private async Task<Tenant> TenantAsync(string slug)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.CareersSlug == slug.ToLower()) ?? throw new NotFoundException("Careers page");
        db.UseTenant(tenant.Id);
        if (tenant.AccessOn(await clock.TodayAsync()) != TenantAccess.Allowed)
            throw new NotFoundException("Careers page");
        return tenant;
    }

    private async Task<IQueryable<JobPost>> OpenJobsAsync()
    {
        var today = await clock.TodayAsync();
        return db.JobPosts.Where(p => p.Status == JobPostStatus.Open && (p.ClosesOn == null || p.ClosesOn >= today));
    }

    private async Task<List<CareersJobDto>> ToDtosAsync(List<JobPost> posts)
    {
        var deptIds = posts.Select(p => p.DepartmentId).ToList();
        var depts = await db.Departments.Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
        return posts.Select(p => new CareersJobDto(p.Slug, p.Title, p.DepartmentId is { } d ? depts.GetValueOrDefault(d) : null, p.Location, p.EmploymentType,
            p.Description, p.Requirements, p.ShowSalary ? p.SalaryMin : null, p.ShowSalary ? p.SalaryMax : null, p.ClosesOn, p.PublishedAtUtc)).ToList();
    }
}
