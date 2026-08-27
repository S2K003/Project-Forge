using ForgeOps.Contracts.Ai;

namespace ForgeOps.Contracts.Engineering;

/// <summary>
/// A deterministic architecture-rule violation (ProjectForge.md §12, §19).
/// Evidence comes from static analysis, never from a model.
/// </summary>
public sealed record ArchitectureFinding
{
    public required string RuleId { get; init; }

    public required string Name { get; init; }

    public required FindingSeverity Severity { get; init; }

    public required string Description { get; init; }

    /// <summary>e.g. "ForgeOps.Application/LoyaltyService.cs → ForgeOps.Infrastructure/Db/LoyaltyStore.cs".</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    public string? RemediationGuidance { get; init; }
}
