using ForgeOps.Contracts.Ai;

namespace ForgeOps.Contracts.Forge;

/// <summary>
/// A file the AI produced as part of an implementation (ProjectForge.md §3 boundary: the
/// AI drafts a *candidate*; deterministic tooling and a human decide whether it ships).
/// </summary>
public sealed record GeneratedFile
{
    public required string Path { get; init; }
    public string Language { get; init; } = "csharp";
    public required string Content { get; init; }

    /// <summary>Role of the file so the UI and runner can treat impl vs test differently.</summary>
    public GeneratedFileRole Role { get; init; } = GeneratedFileRole.Implementation;
}

public enum GeneratedFileRole
{
    Implementation = 0,
    Test = 1
}

/// <summary>
/// What the forge pipeline is producing. A UI requirement generates a self-contained web
/// component that is rendered in a sandboxed iframe; a logic requirement generates a C#
/// component that is compiled and executed in the sandbox process.
/// </summary>
public enum ImplementationKind
{
    CSharpLogic = 0,
    WebComponent = 1
}

public enum ImplementationOrigin
{
    /// <summary>The model's output, compiled as-is.</summary>
    Model = 0,

    /// <summary>The model's output after one or more compile-error repair rounds.</summary>
    ModelWithRepairs = 1,

    /// <summary>
    /// The model's output did not compile within the repair budget; ForgeOps substituted
    /// its reference implementation so the walkthrough can complete. Clearly labelled as
    /// such in the UI — the model's last attempt and its errors are still shown.
    /// </summary>
    ReferenceFallback = 2
}

public sealed record GeneratedImplementation
{
    public required string Summary { get; init; }
    public string Rationale { get; init; } = string.Empty;
    public IReadOnlyList<GeneratedFile> Files { get; init; } = [];

    public ImplementationKind Kind { get; init; } = ImplementationKind.CSharpLogic;

    /// <summary>How many compile-error repair rounds it took to reach a build (0 = first try).</summary>
    public int RepairAttempts { get; init; }

    public ImplementationOrigin Origin { get; init; } = ImplementationOrigin.Model;

    /// <summary>When <see cref="Origin"/> is ReferenceFallback: the model's last non-compiling files.</summary>
    public IReadOnlyList<GeneratedFile> RejectedModelFiles { get; init; } = [];

    /// <summary>When <see cref="Origin"/> is ReferenceFallback: why the model's output was rejected.</summary>
    public string? RejectionDetail { get; init; }

    /// <summary>WebComponent only: behavioural checks the model proposed for its component.</summary>
    public IReadOnlyList<UiCheck> UiChecks { get; init; } = [];

    /// <summary>WebComponent only: what a reviewer should look at / try.</summary>
    public IReadOnlyList<string> ReviewNotes { get; init; } = [];
}

// --- Deterministic audit ----------------------------------------------------

public enum DiagnosticSeverity
{
    Hidden = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

public sealed record CompileDiagnostic
{
    public required DiagnosticSeverity Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? File { get; init; }
    public int Line { get; init; }
}

/// <summary>A use of an API the sandbox forbids (process, filesystem, network, native, emit).</summary>
public sealed record BannedApiFinding
{
    public required string Api { get; init; }
    public required string Reason { get; init; }
    public string? File { get; init; }
    public int Line { get; init; }
    public string Snippet { get; init; } = string.Empty;
}

public enum AuditVerdict
{
    Passed = 0,
    PassedWithWarnings = 1,
    Failed = 2
}

/// <summary>
/// The deterministic gate over generated code (ProjectForge.md §2.2, §13). If this fails,
/// the code is never executed — a human sees the evidence first.
/// </summary>
public sealed record AuditReport
{
    public ImplementationKind Kind { get; init; } = ImplementationKind.CSharpLogic;

    public required bool Compiled { get; init; }
    public int RepairAttempts { get; init; }
    public IReadOnlyList<CompileDiagnostic> Diagnostics { get; init; } = [];
    public IReadOnlyList<BannedApiFinding> BannedApis { get; init; } = [];
    public bool ArchitecturePassed { get; init; } = true;
    public IReadOnlyList<string> ArchitectureNotes { get; init; } = [];
    public required AuditVerdict Verdict { get; init; }

    /// <summary>True only when the audit permits the sandbox to execute the code.</summary>
    public bool ExecutionAllowed => Verdict != AuditVerdict.Failed;
}

// --- Sandboxed execution --------------------------------------------------

public enum TestOutcome
{
    Passed = 0,
    Failed = 1,
    Skipped = 2
}

public sealed record TestResult
{
    public required string Name { get; init; }
    public required TestOutcome Outcome { get; init; }
    public string? Message { get; init; }
    public double DurationMs { get; init; }

    /// <summary>Acceptance criteria this test is tagged with (canonical suite only).</summary>
    public IReadOnlyList<string> Criteria { get; init; } = [];
}

public enum TestSuiteKind
{
    /// <summary>Tests the model wrote for its own implementation.</summary>
    AiGenerated = 0,

    /// <summary>ForgeOps' own acceptance suite, derived deterministically from the criteria.</summary>
    Canonical = 1
}

public sealed record TestRunResult
{
    public required TestSuiteKind Suite { get; init; }
    public required bool Executed { get; init; }
    public IReadOnlyList<TestResult> Results { get; init; } = [];
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public double DurationMs { get; init; }
    public bool TimedOut { get; init; }
    public string Stdout { get; init; } = string.Empty;
    public string RunnerDetail { get; init; } = string.Empty;

    public bool AllPassed => Executed && Failed == 0 && !TimedOut && Passed > 0;
}

/// <summary>
/// One step of a scripted walkthrough executed against the generated implementation: an
/// action performed and the concrete result it produced. This is the "show me it running"
/// surface — real output from the AI's own code, not a pass/fail count.
/// </summary>
public sealed record ScenarioStep
{
    public required string Action { get; init; }
    public required string Output { get; init; }

    /// <summary>Set when the generated code threw while performing this step.</summary>
    public string? Error { get; init; }
}

public sealed record ScenarioRun
{
    public required bool Executed { get; init; }
    public IReadOnlyList<ScenarioStep> Steps { get; init; } = [];

    /// <summary>Anything the generated code wrote to the console during the walkthrough.</summary>
    public string Stdout { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
    public bool Faulted { get; init; }
}

// --- Web component preview ------------------------------------------------

/// <summary>
/// A behavioural check for a generated web component, authored by the model. The script is
/// JavaScript evaluated inside the sandboxed iframe after the component loads; it must
/// return a truthy value (or not throw) to pass. Deterministic verification of the parts
/// that can be checked; the rest is human visual judgment (ProjectForge.md §15, §2.1).
/// </summary>
public sealed record UiCheck
{
    public required string Title { get; init; }
    public required string Script { get; init; }
}

public sealed record UiCheckResult
{
    public required string Title { get; init; }
    public required bool Passed { get; init; }
    public string? Detail { get; init; }
}

/// <summary>Everything the frontend needs to render and self-check a generated web component.</summary>
public sealed record UiPreview
{
    /// <summary>The complete, self-contained HTML document the model produced.</summary>
    public required string DocumentHtml { get; init; }

    public IReadOnlyList<UiCheck> Checks { get; init; } = [];

    /// <summary>What a reviewer should look at / try, in plain language.</summary>
    public IReadOnlyList<string> ReviewNotes { get; init; } = [];

    /// <summary>Populated by the browser after it renders the component and runs the checks.</summary>
    public IReadOnlyList<UiCheckResult> Results { get; init; } = [];

    public bool Rendered { get; init; }
}

public enum AcceptanceStatus
{
    Satisfied = 0,
    NotSatisfied = 1,
    NotCovered = 2
}

/// <summary>Maps one acceptance criterion to the tests that exercised it (ProjectForge.md §15).</summary>
public sealed record AcceptanceOutcome
{
    public required string CriterionId { get; init; }
    public required string Statement { get; init; }
    public required AcceptanceStatus Status { get; init; }
    public IReadOnlyList<string> EvidenceTests { get; init; } = [];
}

/// <summary>
/// One round of refinement: the AI regenerated the artefact to address specific gaps and
/// optional human feedback (ProjectForge.md §52 — the AI proposes an improvement, evidence
/// re-verifies it, a human decides).
/// </summary>
public sealed record RefinementRound
{
    public required int Round { get; init; }

    /// <summary>Acceptance-criterion ids the previous run did not satisfy.</summary>
    public IReadOnlyList<string> AddressedCriteria { get; init; } = [];

    /// <summary>Free-text change the human asked for (optional).</summary>
    public string? Feedback { get; init; }

    /// <summary>One line on what changed, from the model.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>True when every acceptance criterion is satisfied after this round.</summary>
    public bool AllCriteriaMet { get; init; }
}

/// <summary>The full outcome of the forge pipeline for one requirement.</summary>
public sealed record ForgeResult
{
    public required GeneratedImplementation Implementation { get; init; }
    public required AuditReport Audit { get; init; }
    public TestRunResult? AiTestRun { get; init; }
    public TestRunResult? CanonicalTestRun { get; init; }
    public ScenarioRun? Scenario { get; init; }
    public UiPreview? Ui { get; init; }
    public IReadOnlyList<AcceptanceOutcome> Acceptance { get; init; } = [];
    public AiInteractionRecord? Interaction { get; init; }

    /// <summary>Set when this result came from a refinement round rather than the first generation.</summary>
    public RefinementRound? Refinement { get; init; }

    public bool RequirementSatisfied => Implementation.Kind == ImplementationKind.WebComponent
        // UI acceptance is human visual judgment (§2.1); the audit gates rendering and the
        // model's own checks are advisory evidence shown alongside.
        ? Audit.Verdict != AuditVerdict.Failed && (Ui?.Rendered ?? false)
        : Audit.Verdict != AuditVerdict.Failed
          && (CanonicalTestRun?.AllPassed ?? false)
          && Acceptance.All(a => a.Status == AcceptanceStatus.Satisfied);
}
