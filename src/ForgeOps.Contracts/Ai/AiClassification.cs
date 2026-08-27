namespace ForgeOps.Contracts.Ai;

/// <summary>
/// Every AI finding must declare how strongly it is believed (ProjectForge.md §18).
/// Ordered weakest → strongest for display grouping.
/// </summary>
public enum AiClassification
{
    Suggestion = 0,
    Possible = 1,
    Likely = 2,
    Confirmed = 3
}

/// <summary>Severity shared by AI findings and deterministic architecture/quality findings.</summary>
public enum FindingSeverity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
