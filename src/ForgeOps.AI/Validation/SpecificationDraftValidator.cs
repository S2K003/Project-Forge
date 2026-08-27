using ForgeOps.Contracts.Ai;

namespace ForgeOps.AI.Validation;

/// <summary>
/// Deterministic validation of AI structured output (ProjectForge.md §9.2). Invalid AI
/// output must never silently enter the domain model — the gateway returns it as invalid.
/// </summary>
public static class SpecificationDraftValidator
{
    private const int MaxTitle = 160;
    private const int MaxSummary = 2000;
    private const int MaxStatement = 600;

    public static AiValidationResult Validate(SpecificationDraft? draft)
    {
        if (draft is null)
        {
            return AiValidationResult.Fail("Response could not be parsed as a specification draft.");
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            errors.Add("title is required.");
        }
        else if (draft.Title.Length > MaxTitle)
        {
            errors.Add($"title exceeds {MaxTitle} characters.");
        }

        if (string.IsNullOrWhiteSpace(draft.Summary))
        {
            errors.Add("summary is required.");
        }
        else if (draft.Summary.Length > MaxSummary)
        {
            errors.Add($"summary exceeds {MaxSummary} characters.");
        }

        var criteria = draft.AcceptanceCriteria ?? [];
        if (criteria.Count is < 1 or > 12)
        {
            errors.Add("acceptanceCriteria must contain between 1 and 12 items.");
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var criterion in criteria)
        {
            if (string.IsNullOrWhiteSpace(criterion.Id) || !seenIds.Add(criterion.Id))
            {
                errors.Add("each acceptance criterion needs a unique, non-empty id.");
                break;
            }

            if (string.IsNullOrWhiteSpace(criterion.Statement))
            {
                errors.Add($"acceptance criterion {criterion.Id} has an empty statement.");
            }
            else if (criterion.Statement.Length > MaxStatement)
            {
                errors.Add($"acceptance criterion {criterion.Id} exceeds {MaxStatement} characters.");
            }
        }

        return errors.Count == 0 ? AiValidationResult.Ok() : AiValidationResult.Fail([.. errors]);
    }
}
