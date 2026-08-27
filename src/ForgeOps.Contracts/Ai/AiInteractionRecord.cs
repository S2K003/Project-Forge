namespace ForgeOps.Contracts.Ai;

/// <summary>
/// The audit record every meaningful AI interaction must produce (ProjectForge.md §2.1).
/// Keeps AI recommendation, deterministic validation and the human decision distinct.
/// </summary>
public sealed record AiInteractionRecord
{
    public required string Id { get; init; }

    public required string Provider { get; init; }

    public required string Model { get; init; }

    public string? ModelVersion { get; init; }

    public required string PromptVersion { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    public long LatencyMs { get; init; }

    /// <summary>Raw model text, before validation.</summary>
    public string RawResponse { get; init; } = string.Empty;

    /// <summary>Deterministic validation outcome of the structured output (§9.2).</summary>
    public required AiValidationResult Validation { get; init; }

    /// <summary>Model self-reported confidence, 0..1, when applicable.</summary>
    public double? Confidence { get; init; }

    /// <summary>True when the output is a bundled recording replayed in Demo Mode (§9A.2).</summary>
    public bool Simulated { get; init; }

    public HumanDecision? Decision { get; init; }
}

public sealed record AiValidationResult
{
    public required bool Valid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static AiValidationResult Ok() => new() { Valid = true };

    public static AiValidationResult Fail(params string[] errors) => new() { Valid = false, Errors = errors };
}

public enum HumanDecisionKind
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    AcceptedWithModification = 3
}

public sealed record HumanDecision
{
    public required HumanDecisionKind Kind { get; init; }

    public required string DecidedBy { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }

    public string? Reason { get; init; }
}
