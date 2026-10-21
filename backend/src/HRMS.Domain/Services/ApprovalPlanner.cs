namespace HRMS.Domain.Services;

public record FlowStep(ApproverKind Kind, Guid? EmployeeId = null);

public record PlannedStep(ApproverKind Kind, Guid? ApproverEmployeeId);

/// <summary>
/// Turns a configured chain into the people who approve one request:
/// line manager → requester's manager (HR if there is none); second-level manager → manager's manager (skipped if none);
/// department head → head of the requester's department (skipped if none); a specific employee as given. Steps that would make requesters approve themselves are dropped, as are repeats of the
/// previous approver. An empty result means the request is approved automatically.
/// </summary>
public static class ApprovalPlanner
{
    /// <summary>The default when a company has not set up a chain: the line manager, or HR when there is no manager.</summary>
    public static readonly IReadOnlyList<FlowStep> DefaultFlow = [new(ApproverKind.LineManager)];

    public static IReadOnlyList<PlannedStep> Plan(
        IReadOnlyList<FlowStep> flow, Guid requesterId, Guid? managerId, Guid? secondManagerId, Guid? departmentHeadId = null)
    {
        var planned = new List<PlannedStep>();
        foreach (var step in flow)
        {
            PlannedStep? p = step.Kind switch
            {
                ApproverKind.LineManager => managerId is { } m ? new(ApproverKind.LineManager, m) : new(ApproverKind.Hr, null),
                ApproverKind.SecondLevelManager => secondManagerId is { } s ? new(ApproverKind.SecondLevelManager, s) : null,
                ApproverKind.SpecificEmployee => step.EmployeeId is { } e ? new(ApproverKind.SpecificEmployee, e) : null,
                ApproverKind.DepartmentHead => departmentHeadId is { } h ? new(ApproverKind.DepartmentHead, h) : null,
                _ => new(ApproverKind.Hr, null),
            };
            if (p is null || p.ApproverEmployeeId == requesterId)
                continue;
            // The same person (or HR twice) in a row decides once.
            if (planned.Count > 0 && planned[^1].ApproverEmployeeId == p.ApproverEmployeeId
                && (p.ApproverEmployeeId is not null || planned[^1].Kind == p.Kind))
                continue;
            planned.Add(p);
        }
        return planned;
    }
}
