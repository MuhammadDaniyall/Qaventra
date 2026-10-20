using HRMS.Domain;
using HRMS.Domain.Services;

namespace HRMS.Tests.Domain;

public class ApprovalPlannerTests
{
    private static readonly Guid Worker = Guid.NewGuid(), Lead = Guid.NewGuid(), Director = Guid.NewGuid(), Cfo = Guid.NewGuid();

    [Fact]
    public void Default_flow_goes_to_the_line_manager_or_hr_when_there_is_none()
    {
        Assert.Equal([new PlannedStep(ApproverKind.LineManager, Lead)], ApprovalPlanner.Plan(ApprovalPlanner.DefaultFlow, Worker, Lead, Director));
        Assert.Equal([new PlannedStep(ApproverKind.Hr, null)], ApprovalPlanner.Plan(ApprovalPlanner.DefaultFlow, Worker, null, null));
    }

    [Fact]
    public void Builds_a_multi_level_chain_and_skips_missing_second_manager()
    {
        FlowStep[] flow = [new(ApproverKind.LineManager), new(ApproverKind.SecondLevelManager), new(ApproverKind.SpecificEmployee, Cfo), new(ApproverKind.Hr)];
        Assert.Equal(
            [new(ApproverKind.LineManager, Lead), new(ApproverKind.SecondLevelManager, Director), new(ApproverKind.SpecificEmployee, Cfo), new PlannedStep(ApproverKind.Hr, null)],
            ApprovalPlanner.Plan(flow, Worker, Lead, Director));
        Assert.Equal(3, ApprovalPlanner.Plan(flow, Worker, Lead, null).Count);
    }

    [Fact]
    public void Nobody_approves_their_own_request_and_repeats_collapse()
    {
        FlowStep[] flow = [new(ApproverKind.SpecificEmployee, Cfo), new(ApproverKind.LineManager)];
        // The CFO's own request: their step is dropped.
        Assert.Equal([new PlannedStep(ApproverKind.LineManager, Director)], ApprovalPlanner.Plan(flow, Cfo, Director, null));
        // The CFO is also the line manager: one step, not two.
        Assert.Equal([new PlannedStep(ApproverKind.SpecificEmployee, Cfo)], ApprovalPlanner.Plan(flow, Worker, Cfo, null));
    }

    [Fact]
    public void Empty_flow_means_automatic_approval()
    {
        Assert.Empty(ApprovalPlanner.Plan([], Worker, Lead, Director));
    }
}
