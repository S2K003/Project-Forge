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

    private const int MinMeaningfulCssChars = 400;

    /// <summary>
    /// Generate a self-contained web component from an approved specification, with up to
    /// two repair rounds (deterministic audit + a "no meaningful CSS" gate). The component
    /// is later rendered in a sandboxed iframe.
    /// </summary>
    public async Task<CodeGenerationResult> GenerateWebComponentAsync(
        string requirementText,
        SpecificationDraft specification,
        CancellationToken cancellationToken = default)
    {
        var criteria = string.Join("\n", specification.AcceptanceCriteria.Select(c => $"- {c.Id}: {c.Statement}"));
        var context = CodeGenPrompts.BuildContext(requirementText, criteria);

        long latency = 0;
        string provider = _provider.Name, model = "unknown", raw = string.Empty;
        WebDraft? draft = null;
        string? repairContext = null;
        var attempts = 0;

        for (; attempts <= 2; attempts++)
        {
            var request = new AiRequest
            {
                SystemInstructions = CodeGenPrompts.WebComponentSystem,
                TrustedContext = repairContext is null ? context : context + "\n\n" + repairContext,
                UntrustedContent = requirementText,
                PromptVersion = CodeGenPrompts.WebComponentVersion,
                SchemaName = nameof(WebDraft)
            };

            var response = await _provider.GenerateAsync<WebDraft>(request, cancellationToken).ConfigureAwait(false);
            latency += response.LatencyMs;
            provider = response.Provider;
            model = response.Model;
            raw = response.RawText;

            if (response.Value is null || string.IsNullOrWhiteSpace(response.Value.Html))
            {
                continue;
            }

            draft = response.Value;
            var html = StripFences(draft.Html);
            var banned = HtmlAuditor.Scan(html);
            var (structureOk, _) = HtmlAuditor.CheckStructure(html);

            if (banned.Count > 0 || !structureOk)
            {
                repairContext = CodeGenPrompts.BuildWebRepairContext(
                    string.Join("\n", banned.Select(b => $"line {b.Line}: {b.Api} — {b.Reason}")
                        .DefaultIfEmpty("structure: not a self-contained document")),
                    html);
                continue;
            }

            var cssLength = StyleLength(html);
            if (cssLength < MinMeaningfulCssChars && attempts < 2)
            {
#pragma warning disable CA1873
                _logger.LogInformation("Web component has trivial CSS ({Chars} chars) — requesting a style pass", cssLength);
#pragma warning restore CA1873
                repairContext = CodeGenPrompts.BuildWebStyleRepairContext(html);
                continue;
            }

            return BuildWeb(html, draft, attempts, provider, model, raw, latency, valid: true);
        }

        var lastHtml = draft is null ? string.Empty : StripFences(draft.Html);
        return BuildWeb(lastHtml, draft, attempts - 1, provider, model, raw, latency, valid: !string.IsNullOrWhiteSpace(lastHtml));
    }

    private static int StyleLength(string html)
    {
        var total = 0;
        var idx = 0;
        while ((idx = html.IndexOf("<style", idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var open = html.IndexOf('>', idx);
            var close = open >= 0 ? html.IndexOf("</style", open, StringComparison.OrdinalIgnoreCase) : -1;
            if (open < 0 || close < 0)
            {
                break;
            }

            total += close - open - 1;
            idx = close + 7;
        }

        return total;
    }

    // --- Refinement -------------------------------------------------------

    /// <summary>
    /// Regenerate a C# implementation + tests to close the given unmet acceptance criteria
    /// and/or apply human feedback. Bounded compile-error repair; on failure the current
    /// implementation is kept unchanged.
    /// </summary>
    public async Task<CodeGenerationResult> RefineImplementationAsync(
        string requirementText,
        SpecificationDraft specification,
        GeneratedImplementation current,
        IReadOnlyList<string> unmetCriteria,
        string? feedback,
        CancellationToken cancellationToken = default)
    {
        var criteria = string.Join("\n", specification.AcceptanceCriteria.Select(c => $"- {c.Id}: {c.Statement}"));
        var unmet = string.Join("\n", specification.AcceptanceCriteria
            .Where(c => unmetCriteria.Contains(c.Id))
            .Select(c => $"- {c.Id}: {c.Statement}"));
        var currentRendered = string.Join("\n\n", current.Files.Select(f => $"// {f.Path}\n{f.Content}"));

        long latency = 0;
        string provider = _provider.Name, model = "unknown", raw = string.Empty;
        CodeDraft? draft = null;
        var lastErrors = string.Empty;

        for (var attempt = 0; attempt <= 2; attempt++)
        {
            var request = new AiRequest
            {
                SystemInstructions = CodeGenPrompts.RefineSystem,
                TrustedContext = attempt == 0
                    ? CodeGenPrompts.BuildRefineContext(requirementText, criteria, unmet, feedback, currentRendered)
                    : CodeGenPrompts.BuildRefineContext(requirementText, criteria, unmet, feedback, currentRendered)
                      + "\n\n" + CodeGenPrompts.BuildRepairContext(lastErrors, RenderFiles(draft)),
                UntrustedContent = feedback ?? requirementText,
                PromptVersion = CodeGenPrompts.RefineVersion,
                SchemaName = nameof(CodeDraft)
            };

            var response = await _provider.GenerateAsync<CodeDraft>(request, cancellationToken).ConfigureAwait(false);
            latency += response.LatencyMs;
            provider = response.Provider;
            model = response.Model;
            raw = response.RawText;

            if (response.Value is null || response.Value.Files.Count == 0)
            {
                continue;
            }

            draft = response.Value;
            var files = ToGeneratedFiles(draft);
            var compile = _compiler.Compile("ForgeOps.Generated.RefineCheck", BuildImplCompileSet(files));
            if (compile.Success)
            {
                _telemetry.RecordRequest("refine-implementation", latency, success: true);
                return BuildRefined(files, draft.Summary, draft.Rationale, provider, model, raw, latency,
                    ImplementationKind.CSharpLogic, compiled: true);
            }

            lastErrors = string.Join("\n", compile.Errors.Select(e => $"{e.File}({e.Line}): {e.Code}: {e.Message}").Take(10));
        }

        _telemetry.RecordRequest("refine-implementation", latency, success: false);
        _logger.LogWarning("Refinement did not produce compiling code — keeping the current implementation.");
        return new CodeGenerationResult(
            current with { Summary = current.Summary + " (refinement did not compile — unchanged)" },
            RefineInteraction(provider, model, raw, latency, CodeGenPrompts.RefineVersion, valid: false),
            Compiled: true);
    }

    /// <summary>Regenerate a web component to fix failing checks / apply feedback. Keeps styling.</summary>
    public async Task<CodeGenerationResult> RefineWebComponentAsync(
        string requirementText,
        SpecificationDraft specification,
        GeneratedImplementation current,
        IReadOnlyList<string> failingChecks,
        string? feedback,
        CancellationToken cancellationToken = default)
    {
        var criteria = string.Join("\n", specification.AcceptanceCriteria.Select(c => $"- {c.Id}: {c.Statement}"));
        var currentHtml = current.Files.FirstOrDefault(f => f.Role == GeneratedFileRole.Implementation)?.Content ?? "";
        var failing = failingChecks.Count == 0 ? "(none reported)" : string.Join("\n", failingChecks.Select(c => $"- {c}"));

        long latency = 0;
        string provider = _provider.Name, model = "unknown", raw = string.Empty;
        WebDraft? draft = null;
        string? repairContext = null;

        for (var attempt = 0; attempt <= 2; attempt++)
        {
            var request = new AiRequest
            {
                SystemInstructions = CodeGenPrompts.WebComponentRefineSystem,
                TrustedContext = (repairContext ?? CodeGenPrompts.BuildRefineContext(
                    requirementText, criteria, "Failing checks:\n" + failing, feedback, currentHtml)),
                UntrustedContent = feedback ?? requirementText,
                PromptVersion = CodeGenPrompts.WebComponentRefineVersion,
                SchemaName = nameof(WebDraft)
            };

            var response = await _provider.GenerateAsync<WebDraft>(request, cancellationToken).ConfigureAwait(false);
            latency += response.LatencyMs;
            provider = response.Provider;
            model = response.Model;
            raw = response.RawText;

            if (response.Value is null || string.IsNullOrWhiteSpace(response.Value.Html))
            {
                continue;
            }

            draft = response.Value;
            var html = StripFences(draft.Html);
            var banned = HtmlAuditor.Scan(html);
            var (structureOk, _) = HtmlAuditor.CheckStructure(html);

            if (banned.Count > 0 || !structureOk)
            {
                repairContext = CodeGenPrompts.BuildWebRepairContext(
                    string.Join("\n", banned.Select(b => $"line {b.Line}: {b.Api} — {b.Reason}")
                        .DefaultIfEmpty("structure: not a self-contained document")), html);
                continue;
            }

            if (StyleLength(html) < MinMeaningfulCssChars && attempt < 2)
            {
                repairContext = CodeGenPrompts.BuildWebStyleRepairContext(html);
                continue;
            }

            _telemetry.RecordRequest("refine-web-component", latency, success: true);
            var files = new List<GeneratedFile> { new() { Path = "index.html", Language = "html", Content = html } };
            var impl = BuildRefined(files, draft.Summary, draft.Rationale, provider, model, raw, latency,
                ImplementationKind.WebComponent, compiled: true);
            return impl with
            {
                Implementation = impl.Implementation with
                {
                    UiChecks = (draft.Checks ?? [])
                        .Where(c => !string.IsNullOrWhiteSpace(c.Title) && !string.IsNullOrWhiteSpace(c.Script))
                        .Select(c => new UiCheck { Title = c.Title.Trim(), Script = c.Script.Trim() }).ToList(),
                    ReviewNotes = (draft.ReviewNotes ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
                }
            };
        }

        _telemetry.RecordRequest("refine-web-component", latency, success: false);
        return new CodeGenerationResult(
            current with { Summary = current.Summary + " (refinement failed the audit — unchanged)" },
            RefineInteraction(provider, model, raw, latency, CodeGenPrompts.WebComponentRefineVersion, valid: false),
            Compiled: true);
    }

    private static CodeGenerationResult BuildRefined(
        IReadOnlyList<GeneratedFile> files, string summary, string rationale,
        string provider, string model, string raw, long latencyMs, ImplementationKind kind, bool compiled)
    {
        var implementation = new GeneratedImplementation
        {
            Summary = string.IsNullOrWhiteSpace(summary) ? "Refined implementation." : summary,
            Rationale = rationale,
            Kind = kind,
            Files = files,
            Origin = ImplementationOrigin.ModelWithRepairs
        };

        var prompt = kind == ImplementationKind.WebComponent
            ? CodeGenPrompts.WebComponentRefineVersion
            : CodeGenPrompts.RefineVersion;

        return new CodeGenerationResult(implementation,
            RefineInteraction(provider, model, raw, latencyMs, prompt, valid: compiled), compiled);
    }

    private static AiInteractionRecord RefineInteraction(
        string provider, string model, string raw, long latencyMs, string promptVersion, bool valid) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Provider = provider,
        Model = model,
        ModelVersion = model,
        PromptVersion = promptVersion,
        RequestedAt = DateTimeOffset.UtcNow,
        LatencyMs = latencyMs,
        RawResponse = raw,
        Validation = valid ? AiValidationResult.Ok() : AiValidationResult.Fail("Refinement did not produce a usable artefact."),
        Simulated = false
    };

    private CodeGenerationResult BuildWeb(
        string html, WebDraft? draft, int repairAttempts,
        string provider, string model, string raw, long latencyMs, bool valid)
    {
        _telemetry.RecordRequest("generate-web-component", latencyMs, success: valid);

        var implementation = new GeneratedImplementation
        {
            Summary = draft?.Summary ?? "No component was produced.",
            Rationale = draft?.Rationale ?? string.Empty,
            Kind = ImplementationKind.WebComponent,
            RepairAttempts = Math.Max(0, repairAttempts),
            Origin = repairAttempts > 0 ? ImplementationOrigin.ModelWithRepairs : ImplementationOrigin.Model,
            Files = [new GeneratedFile { Path = "index.html", Language = "html", Content = html }],
            UiChecks = (draft?.Checks ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Title) && !string.IsNullOrWhiteSpace(c.Script))
                .Select(c => new UiCheck { Title = c.Title.Trim(), Script = c.Script.Trim() })
                .ToList(),
            ReviewNotes = (draft?.ReviewNotes ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
        };

        var interaction = new AiInteractionRecord
        {
            Id = Guid.NewGuid().ToString("n"),
            Provider = provider,
            Model = model,
            ModelVersion = model,
            PromptVersion = CodeGenPrompts.WebComponentVersion,
            RequestedAt = DateTimeOffset.UtcNow,
            LatencyMs = latencyMs,
            RawResponse = raw,
            Validation = valid ? AiValidationResult.Ok() : AiValidationResult.Fail("Generated document failed the deterministic audit."),
            Simulated = false
        };

        return new CodeGenerationResult(implementation, interaction, valid);
    }

    private sealed record WebDraft
    {
        [JsonPropertyName("summary")] public string Summary { get; init; } = "";
        [JsonPropertyName("rationale")] public string Rationale { get; init; } = "";
        [JsonPropertyName("html")] public string Html { get; init; } = "";
        [JsonPropertyName("checks")] public List<WebCheck> Checks { get; init; } = [];
        [JsonPropertyName("reviewNotes")] public List<string> ReviewNotes { get; init; } = [];
    }

    private sealed record WebCheck
    {
        [JsonPropertyName("title")] public string Title { get; init; } = "";
        [JsonPropertyName("script")] public string Script { get; init; } = "";
    }

    public async Task<CodeGenerationResult> GenerateAsync(
        string requirementText,
        SpecificationDraft specification,
        int maxRepairAttempts,
        bool allowReferenceFallback = true,
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
            var compile = _compiler.Compile("ForgeOps.Generated.ImplCheck", BuildImplCompileSet(files));
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

        var lastFiles = draft is null ? [] : ToGeneratedFiles(draft);

        if (allowReferenceFallback)
        {
            _logger.LogWarning("Code generation did not converge after {Attempts} attempts — using the reference implementation.", attempt);
            return BuildReferenceFallback(lastFiles, lastErrors, provider, model, rawLast, totalLatency, attempt - 1);
        }

        // Return the last draft so the audit step can show exactly why it failed.
        return Build(lastFiles, draft, attempt - 1, provider, model, rawLast, totalLatency, compiled: false);
    }

    private CodeGenerationResult BuildReferenceFallback(
        IReadOnlyList<GeneratedFile> rejectedFiles,
        string rejectionDetail,
        string provider,
        string model,
        string raw,
        long latencyMs,
        int repairAttempts)
    {
        _telemetry.RecordRequest("generate-implementation", latencyMs, success: false);

        // Keep the model's tests only if they compile against the reference implementation.
        var referenceFiles = new List<GeneratedFile>
        {
            new() { Path = "LoyaltyService.cs", Role = GeneratedFileRole.Implementation, Content = GeneratedSources.ReferenceImplementation }
        };

        var modelTests = rejectedFiles.FirstOrDefault(f => f.Role == GeneratedFileRole.Test);
        var testFile = new GeneratedFile
        {
            Path = "LoyaltyServiceTests.cs",
            Role = GeneratedFileRole.Test,
            Content = GeneratedSources.ReferenceTests
        };

        if (modelTests is not null)
        {
            var withModelTests = _compiler.Compile("ForgeOps.Generated.FallbackCheck", new Dictionary<string, string>
            {
                ["__Contract.cs"] = GeneratedSources.Contract,
                ["__ForgeTestKit.cs"] = GeneratedSources.TestKit,
                ["LoyaltyService.cs"] = GeneratedSources.ReferenceImplementation,
                [modelTests.Path] = modelTests.Content
            });
            if (withModelTests.Success)
            {
                testFile = modelTests with { Path = "LoyaltyServiceTests.cs" };
            }
        }

        referenceFiles.Add(testFile);

        var implementation = new GeneratedImplementation
        {
            Summary = "ForgeOps reference implementation (the model's output did not compile).",
            Rationale = "Idempotency is keyed by order id; refunds reverse the recorded award and post a compensating ledger entry.",
            Files = referenceFiles,
            RepairAttempts = Math.Max(0, repairAttempts),
            Origin = ImplementationOrigin.ReferenceFallback,
            RejectedModelFiles = rejectedFiles,
            RejectionDetail = string.IsNullOrWhiteSpace(rejectionDetail)
                ? "The model did not return usable files."
                : rejectionDetail
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
            Validation = AiValidationResult.Fail("Model output did not compile; ForgeOps substituted its reference implementation."),
            Simulated = false
        };

        return new CodeGenerationResult(implementation, interaction, Compiled: true);
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
            RepairAttempts = Math.Max(0, repairAttempts),
            Origin = repairAttempts > 0 ? ImplementationOrigin.ModelWithRepairs : ImplementationOrigin.Model
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
