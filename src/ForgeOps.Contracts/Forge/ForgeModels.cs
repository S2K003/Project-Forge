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

    /// <summary>How many compile-error repair rounds it took to reach a build (0 = first try).</summary>
    public int RepairAttempts { get; init; }

    public ImplementationOrigin Origin { get; init; } = ImplementationOrigin.Model;

    /// <summary>When <see cref="Origin"/> is ReferenceFallback: the model's last non-compiling files.</summary>
    public IReadOnlyList<GeneratedFile> RejectedModelFiles { get; init; } = [];

    /// <summary>When <see cref="Origin"/> is ReferenceFallback: why the model's output was rejected.</summary>
    public string? RejectionDetail { get; init; }
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

/// <summary>The full outcome of the forge pipeline for one requirement.</summary>
public sealed record ForgeResult
{
    public required GeneratedImplementation Implementation { get; init; }
    public required AuditReport Audit { get; init; }
    public TestRunResult? AiTestRun { get; init; }
    public TestRunResult? CanonicalTestRun { get; init; }
    public IReadOnlyList<AcceptanceOutcome> Acceptance { get; init; } = [];
    public AiInteractionRecord? Interaction { get; init; }

    public bool RequirementSatisfied =>
        Audit.Verdict != AuditVerdict.Failed
        && (CanonicalTestRun?.AllPassed ?? false)
        && Acceptance.All(a => a.Status == AcceptanceStatus.Satisfied);
}
