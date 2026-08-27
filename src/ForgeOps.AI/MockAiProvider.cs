using System.Text.Json;
using ForgeOps.Contracts.Ai;

namespace ForgeOps.AI;

/// <summary>
/// Deterministic provider for tests and offline development (ProjectForge.md §9.1).
/// Not used by Demo Mode — Demo Mode replays bundled fixtures in the browser (§9A.2).
/// </summary>
public sealed class MockAiProvider : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "Mock";

    public Task<AiResponse<T>> GenerateAsync<T>(AiRequest request, CancellationToken cancellationToken = default)
        where T : class
    {
        object? value = typeof(T) == typeof(SpecificationDraft)
            ? new SpecificationDraft
            {
                Title = "Loyalty points on successful purchase",
                Summary = "Award loyalty points to a customer once a purchase is confirmed as paid.",
                AcceptanceCriteria =
                [
                    new AcceptanceCriterion { Id = "AC-1", Statement = "Given a paid order, When payment is confirmed, Then points equal to 1 per currency unit are credited once." },
                    new AcceptanceCriterion { Id = "AC-2", Statement = "Given a payment webhook is received twice, When processed, Then points are credited at most once." },
                    new AcceptanceCriterion { Id = "AC-3", Statement = "Given a refunded order, When the refund settles, Then the awarded points are reversed." }
                ],
                OpenQuestions = ["Do partial refunds reverse points proportionally?"]
            }
            : null;

        var raw = value is null ? "{}" : JsonSerializer.Serialize(value, JsonOptions);

        return Task.FromResult(new AiResponse<T>
        {
            Provider = Name,
            Model = "mock",
            PromptVersion = request.PromptVersion,
            RawText = raw,
            LatencyMs = 5,
            Value = value as T,
            Validation = value is null
                ? AiValidationResult.Fail($"MockAiProvider has no fixture for {typeof(T).Name}.")
                : AiValidationResult.Ok(),
            Simulated = false
        });
    }
}
