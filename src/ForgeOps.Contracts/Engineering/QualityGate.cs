namespace ForgeOps.Contracts.Engineering;

public enum GateStatus
{
    Pending = 0,
    Running = 1,
    Passed = 2,
    Failed = 3,
    Skipped = 4,
    Cancelled = 5
}

/// <summary>One entry in the deterministic quality pipeline (ProjectForge.md §13).</summary>
public sealed record QualityGate
{
    public required string Name { get; init; }

    public required GateStatus Status { get; init; }

    public TimeSpan Duration { get; init; }

    /// <summary>Machine-readable evidence lines a human can trace back to source/output.</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>A critical deterministic failure here can block the final quality state (§13).</summary>
    public bool Blocking { get; init; }
}
