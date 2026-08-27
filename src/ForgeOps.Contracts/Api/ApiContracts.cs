using ForgeOps.Contracts.Ai;

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
