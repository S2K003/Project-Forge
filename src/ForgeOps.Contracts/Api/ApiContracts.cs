using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Forge;

namespace ForgeOps.Contracts.Api;

/// <summary>Live Mode request: turn a raw requirement into a specification draft (§32).</summary>
public sealed record GenerateSpecificationRequest
{
    public required string RequirementText { get; init; }

    public string? ProjectName { get; init; }
}

public sealed record GenerateSpecificationResponse
{
    public required SpecificationDraft Draft { get; init; }

    public required AiInteractionRecord Interaction { get; init; }
}

// --- Forge pipeline -------------------------------------------------------

/// <summary>
/// Live Mode request: generate a candidate implementation + tests from an approved
/// specification, run the deterministic audit, and (if the audit allows) execute the
/// tests in the sandbox. The AI never decides to ship — a human still does.
/// </summary>
public sealed record ForgeRequest
{
    public required string RequirementText { get; init; }
    public required SpecificationDraft Specification { get; init; }
    public string? ProjectName { get; init; }

    /// <summary>When false, stop after audit and do not execute anything.</summary>
    public bool Execute { get; init; } = true;
}

public sealed record ForgeResponse
{
    public required ForgeResult Result { get; init; }

    /// <summary>True when the code runner is disabled on this host (§ execution posture).</summary>
    public bool RunnerDisabled { get; init; }
}

/// <summary>
/// Execute an implementation that was already generated (no new AI call). Used when a human
/// has approved running the exact code they reviewed.
/// </summary>
public sealed record ExecuteImplementationRequest
{
    public required GeneratedImplementation Implementation { get; init; }
}
