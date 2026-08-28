using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using ForgeOps.AI.Prompts;
using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Forge;
using ForgeOps.Forge;
using Microsoft.Extensions.Logging;

namespace ForgeOps.AI;

/// <summary>
/// Turns an approved specification into a candidate implementation + tests, with a bounded
/// compile-error repair loop (ProjectForge.md §8 — bounded retry). The output is advisory:
/// <see cref="Forge.GeneratedCodeAuditor"/> and a human decide whether it runs / ships.
/// </summary>
public sealed class CodeGenerator
{
    private readonly IAiProvider _provider;
    private readonly RoslynCompiler _compiler;
    private readonly AiTelemetry _telemetry;
    private readonly ILogger<CodeGenerator> _logger;

    public CodeGenerator(
        IAiProvider provider,
        RoslynCompiler compiler,
        AiTelemetry telemetry,
        ILogger<CodeGenerator> logger)
    {
        _provider = provider;
        _compiler = compiler;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<CodeGenerationResult> GenerateAsync(
        string requirementText,
        SpecificationDraft specification,
        int maxRepairAttempts,
        CancellationToken cancellationToken = default)
    {
        var criteria = string.Join("\n", specification.AcceptanceCriteria.Select(c => $"- {c.Id}: {c.Statement}"));
        var context = CodeGenPrompts.BuildContext(requirementText, criteria);

        var stopwatch = Stopwatch.StartNew();
        var totalLatency = 0L;
        string provider = _provider.Name, model = "unknown", rawLast = string.Empty;

        CodeDraft? draft = null;
        var lastErrors = string.Empty;
        var attempt = 0;

        for (; attempt <= Math.Max(0, maxRepairAttempts); attempt++)
        {
            var request = new AiRequest
            {
                SystemInstructions = CodeGenPrompts.System,
                TrustedContext = attempt == 0
                    ? context
                    : context + "\n\n" + CodeGenPrompts.BuildRepairContext(lastErrors, RenderFiles(draft)),
                UntrustedContent = requirementText,
                PromptVersion = CodeGenPrompts.Version,
                SchemaName = nameof(CodeDraft)
            };

            var response = await _provider.GenerateAsync<CodeDraft>(request, cancellationToken).ConfigureAwait(false);
            totalLatency += response.LatencyMs;
            provider = response.Provider;
            model = response.Model;
            rawLast = response.RawText;

            if (response.Value is null || response.Value.Files.Count == 0)
            {
                _logger.LogWarning("Code generation attempt {Attempt}: no usable files returned", attempt);
                continue;
            }

            draft = response.Value;
            var files = ToGeneratedFiles(draft);

            var implSources = BuildImplCompileSet(files);
            var compile = _compiler.Compile("ForgeOps.Generated.ImplCheck", implSources);
            if (compile.Success)
            {
                stopwatch.Stop();
                return Build(files, draft, attempt, provider, model, rawLast, totalLatency, compiled: true);
            }

            lastErrors = string.Join("\n", compile.Errors.Select(e =>
                $"{e.File}({e.Line}): {e.Code}: {e.Message}").Take(10));
#pragma warning disable CA1873
            _logger.LogInformation("Code generation attempt {Attempt} did not compile:\n{Errors}", attempt, lastErrors);
#pragma warning restore CA1873
        }

        stopwatch.Stop();

        // Ran out of attempts. Return the last draft (if any) so the audit step can show why.
        var lastFiles = draft is null ? [] : ToGeneratedFiles(draft);
        return Build(lastFiles, draft, attempt - 1, provider, model, rawLast, totalLatency, compiled: false);
    }

    private CodeGenerationResult Build(
        IReadOnlyList<GeneratedFile> files,
        CodeDraft? draft,
        int repairAttempts,
        string provider,
        string model,
        string raw,
        long latencyMs,
        bool compiled)
    {
        _telemetry.RecordRequest("generate-implementation", latencyMs, success: compiled);

        var implementation = new GeneratedImplementation
        {
            Summary = draft?.Summary ?? "No implementation was produced.",
            Rationale = draft?.Rationale ?? string.Empty,
            Files = files,
            RepairAttempts = Math.Max(0, repairAttempts)
        };

        var interaction = new AiInteractionRecord
        {
            Id = Guid.NewGuid().ToString("n"),
            Provider = provider,
            Model = model,
            ModelVersion = model,
            PromptVersion = CodeGenPrompts.Version,
            RequestedAt = DateTimeOffset.UtcNow,
            LatencyMs = latencyMs,
            RawResponse = raw,
            Validation = compiled
                ? AiValidationResult.Ok()
                : AiValidationResult.Fail("Generated code did not compile within the repair budget."),
            Simulated = false
        };

        return new CodeGenerationResult(implementation, interaction, compiled);
    }
    private static Dictionary<string, string> BuildImplCompileSet(IReadOnlyList<GeneratedFile> files)
    {
        var sources = new Dictionary<string, string>
        {
            ["__Contract.cs"] = GeneratedSources.Contract,
            ["__ForgeTestKit.cs"] = GeneratedSources.TestKit,
        };
        foreach (var f in files.Where(f => f.Role == GeneratedFileRole.Implementation))
        {
            sources[f.Path] = f.Content;
        }

        return sources;
    }

    private static List<GeneratedFile> ToGeneratedFiles(CodeDraft draft) =>
        draft.Files
            .Where(f => !string.IsNullOrWhiteSpace(f.Content))
            .Select(f => new GeneratedFile
            {
                Path = string.IsNullOrWhiteSpace(f.Path) ? "Generated.cs" : f.Path.Trim(),
                Language = "csharp",
                Content = StripFences(f.Content),
                Role = string.Equals(f.Role, "test", StringComparison.OrdinalIgnoreCase)
                    ? GeneratedFileRole.Test
                    : GeneratedFileRole.Implementation
            })
            .ToList();

    private static string RenderFiles(CodeDraft? draft)
    {
        if (draft is null)
        {
            return "(none)";
        }

        var sb = new StringBuilder();
        foreach (var f in draft.Files)
        {
            sb.AppendLine($"// {f.Path} ({f.Role})").AppendLine(StripFences(f.Content)).AppendLine();
        }

        return sb.ToString();
    }

    private static string StripFences(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }
        }

        return trimmed.Trim();
    }

    private sealed record CodeDraft
    {
        [JsonPropertyName("summary")] public string Summary { get; init; } = string.Empty;
        [JsonPropertyName("rationale")] public string Rationale { get; init; } = string.Empty;
        [JsonPropertyName("files")] public List<CodeDraftFile> Files { get; init; } = [];
    }

    private sealed record CodeDraftFile
    {
        [JsonPropertyName("path")] public string Path { get; init; } = string.Empty;
        [JsonPropertyName("role")] public string Role { get; init; } = "implementation";
        [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
    }
}

public sealed record CodeGenerationResult(
    GeneratedImplementation Implementation,
    AiInteractionRecord Interaction,
    bool Compiled);
