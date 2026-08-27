namespace ForgeOps.Contracts.Ai;

/// <summary>
/// Structured AI output for requirement → specification (ProjectForge.md §9.2, Phase 3).
/// This is an <b>advisory draft</b>. It only becomes a Specification after human review.
/// </summary>
public sealed record SpecificationDraft
{
    public required string Title { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria { get; init; } = [];

    public IReadOnlyList<string> OutOfScope { get; init; } = [];

    public IReadOnlyList<string> OpenQuestions { get; init; } = [];
}

public sealed record AcceptanceCriterion
{
    public required string Id { get; init; }

    /// <summary>Given / When / Then style statement.</summary>
    public required string Statement { get; init; }

    public bool Testable { get; init; } = true;
}
