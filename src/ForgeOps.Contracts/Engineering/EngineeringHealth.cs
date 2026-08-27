namespace ForgeOps.Contracts.Engineering;

/// <summary>
/// Deterministic engineering health score (ProjectForge.md §14). AI may explain it;
/// AI must never determine it. Every score carries its "Why?" breakdown.
/// </summary>
public sealed record EngineeringHealth
{
    /// <summary>0..100.</summary>
    public required int Score { get; init; }

    public IReadOnlyList<HealthComponent> Components { get; init; } = [];

    /// <summary>Plain-language "Why?" lines shown next to the score.</summary>
    public IReadOnlyList<HealthReason> Reasons { get; init; } = [];
}

public sealed record HealthComponent
{
    public required string Name { get; init; }

    /// <summary>Weight as a fraction of the total, e.g. 0.25 for Tests.</summary>
    public required double Weight { get; init; }

    /// <summary>Component score 0..100 before weighting.</summary>
    public required int Score { get; init; }
}

public enum ReasonKind
{
    Pass = 0,
    Warn = 1,
    Fail = 2
}

public sealed record HealthReason
{
    public required ReasonKind Kind { get; init; }

    public required string Text { get; init; }
}
