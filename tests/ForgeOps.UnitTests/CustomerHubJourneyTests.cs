using ForgeOps.Contracts.Engineering;
using ForgeOps.Contracts.Journey;
using ForgeOps.Demo;

namespace ForgeOps.UnitTests;

/// <summary>
/// Demo Mode must always tell the complete §4 story (ProjectForge.md §9A.2). These guard
/// the bundled fixture against accidental gaps.
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
    public void Has_at_least_one_blocking_quality_gate_failure()
    {
        var gates = Journey.Steps.Single(s => s.Kind == JourneyStepKind.QualityGates).Payload.Gates!;
        Assert.Contains(gates, g => g is { Status: GateStatus.Failed, Blocking: true });
    }

    [Fact]
    public void All_gates_pass_after_the_fix()
    {
        var gates = Journey.Steps.Single(s => s.Kind == JourneyStepKind.Merge).Payload.Gates!;
        Assert.All(gates, g => Assert.Equal(GateStatus.Passed, g.Status));
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
