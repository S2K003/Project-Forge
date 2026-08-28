using ForgeOps.AI;
using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Forge;
using ForgeOps.Forge;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeOps.UnitTests;

/// <summary>
/// When the model cannot produce compiling code, ForgeOps substitutes its reference
/// implementation — clearly labelled (ProjectForge.md §49). The result must still be
/// executable and satisfy every acceptance criterion.
/// </summary>
public sealed class CodeGeneratorFallbackTests
{
    private sealed class GarbageProvider : IAiProvider
    {
        public string Name => "Garbage";

        public Task<AiResponse<T>> GenerateAsync<T>(AiRequest request, CancellationToken cancellationToken = default)
            where T : class => Task.FromResult(new AiResponse<T>
            {
                Provider = Name,
                Model = "garbage",
                PromptVersion = request.PromptVersion,
                RawText = "not json",
                Validation = AiValidationResult.Fail("unusable"),
                Value = null
            });
    }

    [Fact]
    public async Task Falls_back_to_the_reference_implementation_when_the_model_never_compiles()
    {
        var compiler = new RoslynCompiler();
        var generator = new CodeGenerator(
            new GarbageProvider(),
            compiler,
            new AiTelemetry(new DummyMeterFactory()),
            NullLogger<CodeGenerator>.Instance);

        var spec = new SpecificationDraft
        {
            Title = "Loyalty",
            Summary = "points",
            AcceptanceCriteria = [new AcceptanceCriterion { Id = "AC-1", Statement = "x" }]
        };

        var result = await generator.GenerateAsync("req", spec, maxRepairAttempts: 1, allowReferenceFallback: true);

        Assert.Equal(ImplementationOrigin.ReferenceFallback, result.Implementation.Origin);
        Assert.True(result.Compiled);

        // The fallback implementation must actually pass the deterministic audit.
        var audit = new GeneratedCodeAuditor(compiler).Audit(result.Implementation.Files, 0);
        Assert.True(audit.Report.Compiled);
        Assert.True(audit.Report.ExecutionAllowed);
    }

    private sealed class DummyMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) => new(options);
        public void Dispose() { }
    }
}
