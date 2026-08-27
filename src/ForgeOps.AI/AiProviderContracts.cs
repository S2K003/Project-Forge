using ForgeOps.Contracts.Ai;

namespace ForgeOps.AI;

/// <summary>
/// The one abstraction feature code depends on (ProjectForge.md §9.1). Feature code must
/// never know whether the provider is local, tunnelled or cloud-hosted.
/// </summary>
public interface IAiProvider
{
    string Name { get; }

    Task<AiResponse<T>> GenerateAsync<T>(AiRequest request, CancellationToken cancellationToken = default)
        where T : class;
}

/// <summary>
/// A request with the three trust zones kept explicitly separate (ProjectForge.md §10):
/// system instructions, trusted application data, and untrusted repository/user content.
/// </summary>
public sealed record AiRequest
{
    public required string SystemInstructions { get; init; }

    public required string TrustedContext { get; init; }

    public string UntrustedContent { get; init; } = string.Empty;

    /// <summary>Identifier of the prompt template used, tracked in the audit record (§45).</summary>
    public required string PromptVersion { get; init; }

    /// <summary>Schema name for the expected structured output, for diagnostics.</summary>
    public required string SchemaName { get; init; }
}

public sealed record AiResponse<T> where T : class
{
    public required string Provider { get; init; }

    public required string Model { get; init; }

    public string? ModelVersion { get; init; }

    public required string PromptVersion { get; init; }

    public required string RawText { get; init; }

    public long LatencyMs { get; init; }

    public T? Value { get; init; }

    public double? Confidence { get; init; }

    public required AiValidationResult Validation { get; init; }

    public bool Simulated { get; init; }
}
