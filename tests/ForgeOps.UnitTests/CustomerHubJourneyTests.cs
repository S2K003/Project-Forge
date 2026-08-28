using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;
using ForgeOps.Demo;

namespace ForgeOps.UnitTests;

/// <summary>
/// Demo Mode must always tell the complete §4 story including generate → audit → run
/// (ProjectForge.md §9A.2). These guard the bundled fixture against accidental gaps.
/// </summary>
public sealed class CustomerHubJourneyTests
{
    private static readonly JourneyDefinition Journey = CustomerHubJourney.Build();

    [Fact]
    public void Covers_every_journey_step_kind_in_order()
    {
        var kinds = Journey.Steps.Select(s => s.Kind).ToArray();
        Assert.Equal(Enum.GetValues<JourneyStepKind>(), kinds);
        Assert.Equal(Enumerable.Range(0, kinds.Length), Journey.Steps.Select(s => s.Order));
    }

    [Fact]
    public void Generates_an_implementation_with_an_impl_file_and_a_test_file()
    {
        var impl = Journey.Steps.Single(s => s.Kind == JourneyStepKind.Implementation).Payload.Implementation!;
        Assert.Contains(impl.Files, f => f.Role == GeneratedFileRole.Implementation);
        Assert.Contains(impl.Files, f => f.Role == GeneratedFileRole.Test);
    }

    [Fact]
    public void The_recorded_audit_permits_execution()
    {
        var audit = Journey.Steps.Single(s => s.Kind == JourneyStepKind.Audit).Payload.Audit!;
        Assert.True(audit.Compiled);
        Assert.Empty(audit.BannedApis);
        Assert.True(audit.ExecutionAllowed);
    }

    [Fact]
    public void Every_acceptance_criterion_is_satisfied_by_an_executed_test()
    {
        var run = Journey.Steps.Single(s => s.Kind == JourneyStepKind.AcceptanceRun).Payload;
        Assert.NotNull(run.CanonicalTestRun);
        Assert.True(run.CanonicalTestRun!.AllPassed);
        Assert.All(run.Acceptance!, a =>
        {
            Assert.Equal(AcceptanceStatus.Satisfied, a.Status);
            Assert.NotEmpty(a.EvidenceTests);
        });
    }

    [Fact]
    public void Every_recorded_ai_interaction_is_flagged_simulated()
    {
        var interactions = Journey.Steps
            .Select(s => s.Payload.AiInteraction)
            .Where(i => i is not null)
            .ToArray();

        Assert.NotEmpty(interactions);
        Assert.All(interactions, i => Assert.True(i!.Simulated));
    }

    [Fact]
    public void Engineering_health_is_deterministic_and_explained()
    {
        var health = Journey.Steps.Single(s => s.Kind == JourneyStepKind.EngineeringHealth).Payload.Health!;
        Assert.InRange(health.Score, 0, 100);
        Assert.NotEmpty(health.Reasons);
        Assert.Equal(1.0, health.Components.Sum(c => c.Weight), precision: 2);
    }
}
