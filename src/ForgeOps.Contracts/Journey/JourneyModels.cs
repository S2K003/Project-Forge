using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Engineering;
using ForgeOps.Contracts.Forge;

namespace ForgeOps.Contracts.Journey;

/// <summary>
/// The one coherent story ForgeOps tells (ProjectForge.md §4, §30). Demo Mode replays
/// this end-to-end from bundled fixtures; Live Mode performs it for real against a
/// seeded project, the AI Bridge, and the sandboxed code runner.
/// </summary>
public enum JourneyStepKind
{
    SignIn = 0,
    Requirement = 1,
    Specification = 2,
    HumanReview = 3,
    Implementation = 4,
    Audit = 5,
    QualityGates = 6,
    AiReview = 7,
    HumanDecision = 8,
    AcceptanceRun = 9,

    /// <summary>
    /// The AI regenerates the artefact to close any unmet acceptance criteria or apply
    /// human feedback; ForgeOps re-audits and re-runs it. Repeatable (ProjectForge.md §4,
    /// §52 — AI proposes, evidence and a human decide).
    /// </summary>
    Refine = 10,
    Merge = 11,
    Telemetry = 12,
    EngineeringHealth = 13
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

    /// <summary>What this journey's requirement produces — logic or a UI component.</summary>
    public Forge.ImplementationKind Kind { get; init; } = Forge.ImplementationKind.CSharpLogic;

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

    // --- forge pipeline (generate → audit → run) ---
    public GeneratedImplementation? Implementation { get; init; }

    public AuditReport? Audit { get; init; }

    public TestRunResult? AiTestRun { get; init; }

    public TestRunResult? CanonicalTestRun { get; init; }

    public ScenarioRun? Scenario { get; init; }

    public UiPreview? Ui { get; init; }

    public IReadOnlyList<AcceptanceOutcome>? Acceptance { get; init; }

    public RefinementRound? Refinement { get; init; }

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
