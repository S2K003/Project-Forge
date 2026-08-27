namespace ForgeOps.Contracts.Ai;

/// <summary>
/// A single AI code-review finding (ProjectForge.md §18). Confidence is a model
/// self-report in [0,1] — never presented as a statistical probability (§18, §2.1).
/// </summary>
public sealed record AiReviewFinding
{
    public required FindingSeverity Severity { get; init; }

    public required AiClassification Classification { get; init; }

    public required string Finding { get; init; }

    /// <summary>Concrete evidence reference, e.g. "PaymentWebhookHandler.cs:47".</summary>
    public required string Evidence { get; init; }

    public required string Recommendation { get; init; }

    /// <summary>Model self-reported confidence, 0..1.</summary>
    public double Confidence { get; init; }
}
