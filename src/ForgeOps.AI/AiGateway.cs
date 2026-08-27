using System.Diagnostics;
using ForgeOps.AI.Prompts;
using ForgeOps.AI.Validation;
using ForgeOps.Contracts.Ai;
using Microsoft.Extensions.Logging;

namespace ForgeOps.AI;

/// <summary>
/// The single entry point for AI work (ProjectForge.md §9). No feature calls a provider
/// directly. Responsible for prompt selection, provider invocation, deterministic
/// validation of structured output, and producing the audit record (§2.1).
/// </summary>
public sealed class AiGateway
{
    private readonly IAiProvider _provider;
    private readonly PromptManager _prompts;
    private readonly AiTelemetry _telemetry;
    private readonly ILogger<AiGateway> _logger;

    public AiGateway(
        IAiProvider provider,
        PromptManager prompts,
        AiTelemetry telemetry,
        ILogger<AiGateway> logger)
    {
        _provider = provider;
        _prompts = prompts;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<SpecificationGenerationResult> GenerateSpecificationAsync(
        string requirementText,
        string? projectName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requirementText))
        {
            throw new ArgumentException("Requirement text is required.", nameof(requirementText));
        }

        var template = _prompts.SpecificationFromRequirement;
        var request = new AiRequest
        {
            SystemInstructions = template.SystemInstructions,
            TrustedContext = $"Project: {projectName ?? "(unspecified)"}",
            UntrustedContent = requirementText.Trim(),
            PromptVersion = template.Version,
            SchemaName = template.SchemaName
        };

        var stopwatch = Stopwatch.StartNew();
        AiResponse<SpecificationDraft> response;
        try
        {
            response = await _provider.GenerateAsync<SpecificationDraft>(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AiException)
        {
            stopwatch.Stop();
            _telemetry.RecordRequest("generate-specification", stopwatch.ElapsedMilliseconds, success: false);
            throw;
        }

        stopwatch.Stop();

        // Deterministic validation always wins over the provider's optimism (§9.2).
        var validation = SpecificationDraftValidator.Validate(response.Value);
        if (!validation.Valid)
        {
            _logger.LogWarning(
                "AI specification draft rejected by validator: {Errors}",
                string.Join("; ", validation.Errors));
        }

        _telemetry.RecordRequest("generate-specification", response.LatencyMs, success: validation.Valid);

        var interaction = new AiInteractionRecord
        {
            Id = Guid.NewGuid().ToString("n"),
            Provider = response.Provider,
            Model = response.Model,
            ModelVersion = response.ModelVersion,
            PromptVersion = response.PromptVersion,
            RequestedAt = DateTimeOffset.UtcNow,
            LatencyMs = response.LatencyMs,
            RawResponse = response.RawText,
            Validation = validation,
            Confidence = response.Confidence,
            Simulated = response.Simulated,
            Decision = null
        };

        return new SpecificationGenerationResult(
            validation.Valid ? response.Value : null,
            interaction);
    }
}

public sealed record SpecificationGenerationResult(
    SpecificationDraft? Draft,
    AiInteractionRecord Interaction);
