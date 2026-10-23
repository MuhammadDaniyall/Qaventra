using System.ComponentModel.DataAnnotations;
using ClosedXML.Excel;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain.Common;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record ReportSettingsDto(List<string> Signatures);

public record SaveReportSettingsRequest([MaxLength(5)] List<string> Signatures);

/// <summary>The report catalog: list, run with filters, Excel, and the signature lines printed under reports.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.Approver)]
public class ReportsController(HrmsDbContext db, ReportCatalog catalog, TenantClock clock) : ControllerBase
{
    [HttpGet]
    public IEnumerable<ReportDefinition> List() => catalog.Available();

    [HttpPost("{key}")]
    public Task<ReportResult> Run(string key, ReportRequest req) => catalog.RunAsync(key, req);

    [HttpPost("{key}/excel")]
    public async Task<IActionResult> Excel(string key, ReportRequest req)
    {
        var r = await catalog.RunAsync(key, req);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(r.Title.Length > 31 ? r.Title[..31] : r.Title);
        for (var c = 0; c < r.Headers.Count; c++)
            ws.Cell(1, c + 1).Value = r.Headers[c];
        var rows = r.Totals is null ? r.Rows : [.. r.Rows, r.Totals];
        for (var i = 0; i < rows.Count; i++)
            for (var c = 0; c < rows[i].Count; c++)
            {
                var text = rows[i][c];
                // Numbers go in as numbers so Excel can sum them.
                if (r.Numeric[c] && decimal.TryParse(text.Replace(",", "").Replace(" h", ""), out var n))
                    ws.Cell(i + 2, c + 1).Value = n;
                else
                    ws.Cell(i + 2, c + 1).Value = text;
            }
        Services.Excel.StyleTable(ws, Math.Max(1, r.Headers.Count), rows.Count + 1);
        if (r.Totals is not null)
            ws.Row(rows.Count + 1).Style.Font.Bold = true;
        var name = $"{r.Title} {await clock.TodayAsync():yyyy-MM-dd}.xlsx";
        return File(Services.Excel.Save(wb), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    /// <summary>Titles printed as signature lines under every report, e.g. "Prepared by", "HR Manager", "CFO".</summary>
    [HttpGet("settings")]
    public async Task<ReportSettingsDto> Settings()
    {
        var s = await db.ReportSettings.FirstOrDefaultAsync();
        return new ReportSettingsDto(s is null || string.IsNullOrEmpty(s.Signatures) ? [] : s.Signatures.Split('\n').ToList());
    }

    [HttpPut("settings"), Authorize(Policy = Policies.Hr)]
    public async Task<IActionResult> SaveSettings(SaveReportSettingsRequest req)
    {
        var lines = req.Signatures.Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (lines.Count > 5 || lines.Any(l => l.Length > 60))
            throw new DomainException("Up to 5 signature lines of 60 characters.");
        var s = await db.ReportSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new ReportSettings();
            db.ReportSettings.Add(s);
        }
        s.Signatures = string.Join('\n', lines);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
