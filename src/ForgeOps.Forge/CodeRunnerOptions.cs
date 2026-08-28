namespace ForgeOps.Forge;

/// <summary>
/// Execution posture for generated code. On a shared free-tier host, set
/// <see cref="Enabled"/> to false — the pipeline still generates, compiles and audits;
/// only the sandboxed run is withheld (ProjectForge.md §10 — automation guardrails).
/// </summary>
public sealed class CodeRunnerOptions
{
    public const string SectionName = "CodeRunner";

    /// <summary>When false, ForgePipeline stops after the audit and never launches the sandbox.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Wall-clock budget for a single sandbox process.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>Max compile-error repair rounds the generator is allowed (informational here).</summary>
    public int MaxRepairAttempts { get; set; } = 2;

    /// <summary>
    /// Explicit path to <c>ForgeOps.Forge.Sandbox.dll</c>. When null it is resolved next to
    /// the host under <c>sandbox/</c>.
    /// </summary>
    public string? SandboxAssemblyPath { get; set; }
}
