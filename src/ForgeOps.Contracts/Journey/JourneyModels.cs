using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Engineering;

namespace ForgeOps.Contracts.Journey;

/// <summary>
/// The one coherent story ForgeOps tells (ProjectForge.md §4, §30). Demo Mode replays
/// this end-to-end from bundled fixtures; Live Mode performs it for real against a
/// seeded project and the AI Bridge.
/// </summary>
public enum JourneyStepKind
{
    SignIn = 0,
    Requirement = 1,
    Specification = 2,
    HumanReview = 3,
    ArchitectureAnalysis = 4,
    PullRequest = 5,
    QualityGates = 6,
    AiReview = 7,
    HumanDecision = 8,
    Merge = 9,
    Telemetry = 10,
    EngineeringHealth = 11
}

public enum JourneyStepState
{
    Locked = 0,
    Ready = 1,
    Active = 2,
    Complete = 3,
    Blocked = 4
}

public sealed record JourneyDefinition
{
    public required string ProjectKey { get; init; }

    public required string ProjectName { get; init; }

    public required string RequirementText { get; init; }

    public required IReadOnlyList<JourneyStep> Steps { get; init; }
}

public sealed record JourneyStep
{
    public required int Order { get; init; }

    public required JourneyStepKind Kind { get; init; }

    public required string Title { get; init; }

    public required string Caption { get; init; }

    public JourneyStepState State { get; init; } = JourneyStepState.Locked;

    /// <summary>Simulated "thinking" duration used in Demo Mode for AI steps (§9A.2).</summary>
    public int SimulatedThinkingMs { get; init; }

    public StepPayload Payload { get; init; } = new();
}

/// <summary>
/// Everything a step might render. Only the fields relevant to <see cref="JourneyStep.Kind"/>
/// are populated; the rest stay null. Kept as one bag so a single fixture file describes
/// the whole journey.
/// </summary>
public sealed record StepPayload
{
    public SpecificationDraft? Specification { get; init; }

    public AiInteractionRecord? AiInteraction { get; init; }

    public IReadOnlyList<ArchitectureFinding>? ArchitectureFindings { get; init; }

    public PullRequestSummary? PullRequest { get; init; }

    public IReadOnlyList<QualityGate>? Gates { get; init; }

    public IReadOnlyList<AiReviewFinding>? ReviewFindings { get; init; }

    public IReadOnlyList<TelemetrySample>? Telemetry { get; init; }

    public EngineeringHealth? Health { get; init; }

    public IReadOnlyList<string>? Notes { get; init; }
}

public sealed record PullRequestSummary
{
    public required int Number { get; init; }

    public required string Title { get; init; }

    public required string Branch { get; init; }

    public required int FilesChanged { get; init; }

    public int Additions { get; init; }

    public int Deletions { get; init; }

    public IReadOnlyList<string> ChangedFiles { get; init; } = [];
}

public sealed record TelemetrySample
{
    public required string Metric { get; init; }

    public required string Value { get; init; }

    public string? Detail { get; init; }
}
