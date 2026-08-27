namespace ForgeOps.Contracts;

/// <summary>
/// The live view of the AI Bridge (ProjectForge.md §7.2, §9A.1). This is telemetry,
/// not a configuration toggle — <see cref="Up"/> reflects an actual probe result.
/// </summary>
public sealed record AiBridgeStatus
{
    public required bool Up { get; init; }

    /// <summary>Model reported by the bridge, when reachable (e.g. "qwen3:8b").</summary>
    public string? Model { get; init; }

    public required DateTimeOffset CheckedAt { get; init; }

    public long LatencyMs { get; init; }

    /// <summary>Human-readable detail for the connection gate ("AI Bridge Offline", timeout, etc.).</summary>
    public string Detail { get; init; } = string.Empty;

    public static AiBridgeStatus Offline(string detail) => new()
    {
        Up = false,
        CheckedAt = DateTimeOffset.UtcNow,
        Detail = detail
    };
}
