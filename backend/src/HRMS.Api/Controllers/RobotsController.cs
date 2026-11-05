using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HRMS.Api.Auth;
using HRMS.Api.Services;
using HRMS.Domain;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Controllers;

public record RobotDto(Guid Id, RobotKind Kind, string Name, string About, string[] Does, bool CanPropose, bool Enabled, RobotAutonomy Autonomy,
    Guid? OwnerUserId, string? OwnerName, int RunHour, int RunsPerMonth, int RunsThisMonth, DateTime? LastRunAtUtc, string? LastSummary, int Pending);
public record RobotOwnerDto(Guid Id, string Name);
public record AiTeamDto(bool Stopped, string? StoppedBy, bool AiOn, List<RobotDto> Robots, List<RobotOwnerDto> Owners, int Pending);
public record RobotUpdateRequest(bool Enabled, RobotAutonomy Autonomy, Guid? OwnerUserId, [Range(0, 23)] int RunHour, [Range(1, 500)] int RunsPerMonth);
public record RobotRunDto(Guid Id, Guid RobotId, string RobotName, bool Manual, string? StartedByName, RobotRunStatus Status, DateTime StartedAtUtc,
    DateTime? FinishedAtUtc, string? Summary, List<string> Steps, List<RobotFinding> Findings, int Proposals, bool UsedAi, string? Error);
public record RobotProposalDto(Guid Id, Guid RobotId, string RobotName, string Action, string Title, string Detail, string? Link, RobotProposalStatus Status,
    DateTime CreatedAtUtc, string? DecidedByName, DateTime? DecidedAtUtc, string? Note, bool CanEdit);
public record RobotDecideRequest(bool Approve, [StringLength(1000)] string? Note, [StringLength(3000)] string? Body);
public record RobotStopRequest(bool Stopped);

/// <summary>AI Team: the company's AI robot employees, their runs (every step logged) and the changes they propose.</summary>
[ApiController]
[Route("api/ai-team")]
[Authorize(Policy = Policies.CompanyUser)]
[Authorize(Policy = Policies.Hr)]
public class RobotsController(RobotService robots, AiAssistantService ai, HrmsDbContext db) : ControllerBase
{
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<AiTeamDto> Get()
    {
        var list = await robots.EnsureAsync();
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == db.CurrentTenantId);
        var owners = await db.Users.AsNoTracking().Where(u => u.IsActive && (u.Role == UserRole.CompanyAdmin || u.Role == UserRole.HrManager))
            .OrderBy(u => u.FullName).Select(u => new RobotOwnerDto(u.Id, u.FullName)).ToListAsync();
        var pending = await db.AiRobotProposals.Where(p => p.Status == RobotProposalStatus.Pending)
            .GroupBy(p => p.RobotId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        var result = new List<RobotDto>();
        foreach (var r in list)
        {
            var info = RobotService.Catalog[r.Kind];
            result.Add(new(r.Id, r.Kind, r.Name, info.About, info.Does, info.CanPropose, r.Enabled, r.Autonomy, r.OwnerUserId,
                owners.FirstOrDefault(o => o.Id == r.OwnerUserId)?.Name, r.RunHour, r.RunsPerMonth, await robots.RunsThisMonthAsync(r.Id),
                r.LastRunAtUtc, r.LastSummary, pending.GetValueOrDefault(r.Id)));
        }
        return new(tenant.RobotsStopped, tenant.RobotsStoppedBy, ai.Enabled, result, owners, pending.Values.Sum());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, RobotUpdateRequest req)
    {
        await robots.UpdateAsync(id, req.Enabled, req.Autonomy, req.OwnerUserId, req.RunHour, req.RunsPerMonth);
        return NoContent();
    }

    [HttpPost("{id:guid}/run")]
    public async Task<RobotRunDto> Run(Guid id)
    {
        var run = await robots.RunAsync(id, manual: true);
        var name = await db.AiRobots.Where(r => r.Id == run.RobotId).Select(r => r.Name).FirstAsync();
        return ToDto(run, name);
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop(RobotStopRequest req)
    {
        await robots.SetStoppedAsync(req.Stopped);
        return NoContent();
    }

    [HttpGet("runs")]
    public async Task<List<RobotRunDto>> Runs([FromQuery] Guid? robotId, [FromQuery] int take = 30)
    {
        var q = db.AiRobotRuns.AsNoTracking().Include(r => r.Robot).AsQueryable();
        if (robotId is { } rid) q = q.Where(r => r.RobotId == rid);
        var rows = await q.OrderByDescending(r => r.StartedAtUtc).Take(Math.Clamp(take, 1, 100)).ToListAsync();
        return rows.Select(r => ToDto(r, r.Robot?.Name ?? "")).ToList();
    }

    [HttpGet("proposals")]
    public async Task<List<RobotProposalDto>> Proposals([FromQuery] bool pending = true)
    {
        var q = db.AiRobotProposals.AsNoTracking().Include(p => p.Robot).AsQueryable();
        q = pending ? q.Where(p => p.Status == RobotProposalStatus.Pending) : q.Where(p => p.Status != RobotProposalStatus.Pending);
        var rows = await q.OrderByDescending(p => pending ? p.CreatedAtUtc : p.DecidedAtUtc ?? p.CreatedAtUtc).Take(200).ToListAsync();
        return rows.Select(p => new RobotProposalDto(p.Id, p.RobotId, p.Robot?.Name ?? "", p.Action, p.Title, p.Detail, p.Link, p.Status, p.CreatedAtUtc,
            p.DecidedByName, p.DecidedAtUtc, p.Note, p.Action is "helpdesk_reply" or "notify_employee")).ToList();
    }

    [HttpPost("proposals/{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, RobotDecideRequest req)
    {
        await robots.DecideAsync(id, req.Approve, req.Note, req.Body);
        return NoContent();
    }

    static RobotRunDto ToDto(AiRobotRun r, string name) => new(r.Id, r.RobotId, name, r.Manual, r.StartedByName, r.Status, r.StartedAtUtc, r.FinishedAtUtc,
        r.Summary, JsonSerializer.Deserialize<List<string>>(r.StepsJson, Web) ?? [], JsonSerializer.Deserialize<List<RobotFinding>>(r.FindingsJson, Web) ?? [],
        r.Proposals, r.UsedAi, r.Error);
}
