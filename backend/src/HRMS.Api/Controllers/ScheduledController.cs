using System.ComponentModel.DataAnnotations;
using HRMS.Api.Auth;
using HRMS.Api.Infrastructure;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record ScheduledAlertDto(
    Guid Id, string Name, AlertKind Kind, int DaysBefore, int ProbationMonths, int SendHour, bool ToEmployee, bool ToManager, bool ToHr, bool ToEveryone,
    List<Guid> ExtraRecipientIds, List<Guid> DepartmentIds, string TitleTemplate, string? BodyTemplate, bool SendEmail, bool IsActive, DateOnly? LastRunOn);

public record SaveAlertRequest(
    [Required, StringLength(150)] string Name, AlertKind Kind, int DaysBefore, int ProbationMonths, int SendHour, bool ToEmployee, bool ToManager, bool ToHr,
    bool ToEveryone, List<Guid>? ExtraRecipientIds, List<Guid>? DepartmentIds, [Required, StringLength(200)] string TitleTemplate,
    [StringLength(2000)] string? BodyTemplate, bool SendEmail = true, bool IsActive = true);

public record AlertPreviewDto(int Count, List<string> Titles);

public record ScheduledReportDto(
    Guid Id, string Name, ReportKind Kind, ReportFrequency Frequency, int Day, int SendHour, List<Guid> RecipientIds, List<string> Recipients, string ExtraEmails,
    List<Guid> DepartmentIds, bool IsActive, DateTime? LastSentAtUtc, DateTime? NextRunAtUtc);

public record SaveReportRequest(
    [StringLength(150)] string Name, ReportKind Kind, ReportFrequency Frequency, int Day, int SendHour, List<Guid>? RecipientIds,
    [StringLength(2000)] string? ExtraEmails, List<Guid>? DepartmentIds, bool IsActive = true);

public record ScheduledRunDto(Guid Id, Guid? AlertId, Guid? ReportId, string Name, int Recipients, int Items, string? Note, bool Manual, DateTime CreatedAtUtc);

/// <summary>HR: alerts the system sends by itself and reports emailed on a schedule.</summary>
[ApiController]
[Route("api/scheduled")]
[Authorize(Policy = Policies.Hr)]
public class ScheduledController(HrmsDbContext db, ScheduledService scheduled, ICurrentUser me) : ControllerBase
{
    // ---------- Alerts ----------

    [HttpGet("alerts")]
    public async Task<List<ScheduledAlertDto>> Alerts() =>
        (await db.ScheduledAlerts.OrderBy(a => a.Name).ToListAsync()).Select(ToDto).ToList();

    [HttpPost("alerts")]
    public async Task<ScheduledAlertDto> AddAlert(SaveAlertRequest req)
    {
        var a = new ScheduledAlert();
        await ApplyAsync(a, req);
        db.ScheduledAlerts.Add(a);
        await db.SaveChangesAsync();
        return ToDto(a);
    }

    [HttpPut("alerts/{id:guid}")]
    public async Task<ScheduledAlertDto> UpdateAlert(Guid id, SaveAlertRequest req)
    {
        var a = await db.ScheduledAlerts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Alert");
        await ApplyAsync(a, req);
        await db.SaveChangesAsync();
        return ToDto(a);
    }

    [HttpDelete("alerts/{id:guid}")]
    public async Task<IActionResult> DeleteAlert(Guid id)
    {
        var a = await db.ScheduledAlerts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Alert");
        db.ScheduledAlerts.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Who the alert would be about today, without sending.</summary>
    [HttpGet("alerts/{id:guid}/preview")]
    public async Task<AlertPreviewDto> PreviewAlert(Guid id, [FromServices] TenantClock clock)
    {
        var a = await db.ScheduledAlerts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Alert");
        var subjects = await scheduled.SubjectsAsync(a, await clock.TodayAsync());
        return new AlertPreviewDto(subjects.Count, subjects.Take(20).Select(s => ScheduledService.Fill(a.TitleTemplate, s, a.DaysBefore)).ToList());
    }

    [HttpPost("alerts/{id:guid}/run")]
    public async Task<object> RunAlert(Guid id)
    {
        var a = await db.ScheduledAlerts.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Alert");
        return new { people = await scheduled.RunAlertAsync(a, manual: true) };
    }

    // ---------- Reports ----------

    [HttpGet("reports")]
    public async Task<List<ScheduledReportDto>> Reports()
    {
        var list = await db.ScheduledReports.OrderBy(r => r.Name).ToListAsync();
        var ids = list.SelectMany(r => HelpdeskService.Ids(r.RecipientIds)).Distinct().ToList();
        var names = await db.Employees.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName);
        return list.Select(r => ToDto(r, names)).ToList();
    }

    [HttpPost("reports")]
    public async Task<IActionResult> AddReport(SaveReportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new DomainException("Name the report.");
        var r = new ScheduledReport();
        await ApplyAsync(r, req);
        db.ScheduledReports.Add(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("reports/{id:guid}")]
    public async Task<IActionResult> UpdateReport(Guid id, SaveReportRequest req)
    {
        var r = await db.ScheduledReports.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Report");
        await ApplyAsync(r, req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("reports/{id:guid}")]
    public async Task<IActionResult> DeleteReport(Guid id)
    {
        var r = await db.ScheduledReports.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Report");
        db.ScheduledReports.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>The report as it would be sent now.</summary>
    [HttpGet("reports/{id:guid}/preview")]
    public async Task<ReportTable> PreviewReport(Guid id)
    {
        var r = await db.ScheduledReports.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Report");
        return await scheduled.BuildAsync(r);
    }

    /// <summary>A report kind with the given settings, before it is saved.</summary>
    [HttpPost("reports/preview")]
    public async Task<ReportTable> PreviewDraft(SaveReportRequest req)
    {
        var r = new ScheduledReport();
        await ApplyAsync(r, req with { Name = string.IsNullOrWhiteSpace(req.Name) ? "Preview" : req.Name }, preview: true);
        return await scheduled.BuildAsync(r);
    }

    [HttpGet("reports/{id:guid}/excel")]
    public async Task<IActionResult> Excel(Guid id)
    {
        var r = await db.ScheduledReports.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Report");
        var t = await scheduled.BuildAsync(r);
        return File(ScheduledService.ToExcel(t), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{r.Name}.xlsx");
    }

    /// <summary>Sends now: to everyone on the list, or only to the signed-in user as a test.</summary>
    [HttpPost("reports/{id:guid}/send")]
    public async Task<object> Send(Guid id, [FromQuery] bool test = false)
    {
        var r = await db.ScheduledReports.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Report");
        string? to = null;
        if (test)
            to = me.Email ?? throw new DomainException("Your login has no email address.");
        return new { recipients = await scheduled.SendReportAsync(r, manual: true, to) };
    }

    [HttpGet("runs")]
    public async Task<List<ScheduledRunDto>> Runs() =>
        await db.ScheduledRuns.OrderByDescending(r => r.CreatedAtUtc).Take(100)
            .Select(r => new ScheduledRunDto(r.Id, r.AlertId, r.ReportId, r.Name, r.Recipients, r.Items, r.Note, r.Manual, r.CreatedAtUtc)).ToListAsync();

    // ---------- Helpers ----------

    private static ScheduledAlertDto ToDto(ScheduledAlert a) => new(a.Id, a.Name, a.Kind, a.DaysBefore, a.ProbationMonths, a.SendHour, a.ToEmployee, a.ToManager,
        a.ToHr, a.ToEveryone, HelpdeskService.Ids(a.ExtraRecipientIds), HelpdeskService.Ids(a.DepartmentIds), a.TitleTemplate, a.BodyTemplate, a.SendEmail,
        a.IsActive, a.LastRunOn);

    private static ScheduledReportDto ToDto(ScheduledReport r, Dictionary<Guid, string> names)
    {
        var ids = HelpdeskService.Ids(r.RecipientIds);
        return new ScheduledReportDto(r.Id, r.Name, r.Kind, r.Frequency, r.Day, r.SendHour, ids, ids.Select(i => names.GetValueOrDefault(i) ?? "").ToList(),
            r.ExtraEmails, HelpdeskService.Ids(r.DepartmentIds), r.IsActive, r.LastSentAtUtc, r.NextRunAtUtc);
    }

    private async Task ApplyAsync(ScheduledAlert a, SaveAlertRequest r)
    {
        if (!Enum.IsDefined(r.Kind))
            throw new DomainException("Choose what the alert is about.");
        if (r.DaysBefore is < 0 or > 90 || r.SendHour is < 0 or > 23 || r.ProbationMonths is < 1 or > 24)
            throw new DomainException("Check the days, hour and probation months.");
        if (!r.ToEmployee && !r.ToManager && !r.ToHr && !r.ToEveryone && (r.ExtraRecipientIds ?? []).Count == 0)
            throw new DomainException("Choose who gets the alert.");
        await CheckPeopleAsync(r.ExtraRecipientIds, r.DepartmentIds);
        a.Name = r.Name.Trim();
        a.Kind = r.Kind;
        a.DaysBefore = r.Kind == AlertKind.NotCheckedIn ? 0 : r.DaysBefore;
        a.ProbationMonths = r.ProbationMonths;
        a.SendHour = r.SendHour;
        a.ToEmployee = r.ToEmployee;
        a.ToManager = r.ToManager;
        a.ToHr = r.ToHr;
        a.ToEveryone = r.ToEveryone;
        a.ExtraRecipientIds = string.Join(',', (r.ExtraRecipientIds ?? []).Distinct());
        a.DepartmentIds = string.Join(',', (r.DepartmentIds ?? []).Distinct());
        a.TitleTemplate = r.TitleTemplate.Trim();
        a.BodyTemplate = string.IsNullOrWhiteSpace(r.BodyTemplate) ? null : r.BodyTemplate.Trim();
        a.SendEmail = r.SendEmail;
        a.IsActive = r.IsActive;
    }

    private async Task ApplyAsync(ScheduledReport x, SaveReportRequest r, bool preview = false)
    {
        if (!Enum.IsDefined(r.Kind) || !Enum.IsDefined(r.Frequency))
            throw new DomainException("Choose the report and how often.");
        if (r.SendHour is < 0 or > 23 || r.Frequency == ReportFrequency.Weekly && r.Day is < 0 or > 6 || r.Frequency == ReportFrequency.Monthly && r.Day is < 1 or > 28)
            throw new DomainException("Check the day and hour.");
        var extra = ScheduledService.Emails(r.ExtraEmails ?? "");
        if (!preview && (r.RecipientIds ?? []).Count == 0 && extra.Count == 0)
            throw new DomainException("Choose who gets the report.");
        if (extra.Count > 20)
            throw new DomainException("At most 20 other email addresses.");
        await CheckPeopleAsync(r.RecipientIds, r.DepartmentIds);
        x.Name = r.Name.Trim();
        x.Kind = r.Kind;
        x.Frequency = r.Frequency;
        x.Day = r.Frequency == ReportFrequency.Daily ? 0 : r.Day;
        x.SendHour = r.SendHour;
        x.RecipientIds = string.Join(',', (r.RecipientIds ?? []).Distinct());
        x.ExtraEmails = string.Join(", ", extra);
        x.DepartmentIds = string.Join(',', (r.DepartmentIds ?? []).Distinct());
        x.IsActive = r.IsActive;
        x.NextRunAtUtc = r.IsActive ? await scheduled.NextRunAsync(x, DateTime.UtcNow) : null;
    }

    private async Task CheckPeopleAsync(List<Guid>? people, List<Guid>? departments)
    {
        var p = (people ?? []).Distinct().ToList();
        if (await db.Employees.CountAsync(e => p.Contains(e.Id)) != p.Count)
            throw new NotFoundException("Employee");
        var d = (departments ?? []).Distinct().ToList();
        if (await db.Departments.CountAsync(x => d.Contains(x.Id)) != d.Count)
            throw new NotFoundException("Department");
    }
}
