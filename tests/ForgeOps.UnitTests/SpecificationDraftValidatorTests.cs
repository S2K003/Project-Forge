using ForgeOps.AI.Validation;
using ForgeOps.Contracts.Ai;

namespace ForgeOps.UnitTests;

public sealed class SpecificationDraftValidatorTests
{
    private static SpecificationDraft Valid() => new()
    {
        Title = "Loyalty points",
        Summary = "Award points on paid orders.",
        AcceptanceCriteria =
        [
            new AcceptanceCriterion { Id = "AC-1", Statement = "Given a paid order, Then points are credited once." }
        ]
    };

    [Fact]
    public void Accepts_a_well_formed_draft() =>
        Assert.True(SpecificationDraftValidator.Validate(Valid()).Valid);

    [Fact]
    public void Rejects_null() =>
        Assert.False(SpecificationDraftValidator.Validate(null).Valid);

    [Fact]
    public void Rejects_missing_title() =>
        Assert.False(SpecificationDraftValidator.Validate(Valid() with { Title = "" }).Valid);

    [Fact]
    public void Rejects_zero_acceptance_criteria() =>
        Assert.False(SpecificationDraftValidator.Validate(Valid() with { AcceptanceCriteria = [] }).Valid);

    [Fact]
    public void Rejects_duplicate_criterion_ids()
    {
        var draft = Valid() with
        {
            AcceptanceCriteria =
            [
                new AcceptanceCriterion { Id = "AC-1", Statement = "one" },
                new AcceptanceCriterion { Id = "AC-1", Statement = "two" }
            ]
        };

        Assert.False(SpecificationDraftValidator.Validate(draft).Valid);
    }
}
