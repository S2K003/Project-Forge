using ForgeOps.Contracts;
using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Engineering;
using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;
using ForgeOps.Demo;

namespace ForgeOps.Web.Services;

/// <summary>
/// Drives the CustomerHub journey (ProjectForge.md §4, §30) for both modes.
///
/// Demo Mode: every step is bundled fixture data; AI steps show a simulated "thinking"
/// pause then reveal a pre-recorded, clearly-labelled result (§9A.2).
///
/// Live Mode: the same seeded project, but the Specification step performs a real AI
/// Bridge call through the API. A bridge-offline result is surfaced so the page can raise
/// the connection gate (§9A.1) — never silently faked (§9.3).
/// </summary>
public sealed class JourneyPlayer
{
    private readonly ForgeOpsApiClient _api;
    private List<JourneyStep> _steps = [];

    public JourneyPlayer(ForgeOpsApiClient api) => _api = api;

    public JourneyDefinition? Definition { get; private set; }
    public AppMode Mode { get; private set; }
    public int CurrentIndex { get; private set; }
    public bool IsThinking { get; private set; }
    public string? ThinkingLabel { get; private set; }

    public IReadOnlyList<JourneyStep> Steps => _steps;
    public JourneyStep? Current => _steps.Count > 0 ? _steps[CurrentIndex] : null;
    public bool IsComplete => Current is { Kind: JourneyStepKind.EngineeringHealth, State: JourneyStepState.Active or JourneyStepState.Complete };
    public bool IsLastStep => CurrentIndex == _steps.Count - 1;

    public event Action? Changed;

    public void Load(AppMode mode)
    {
        Mode = mode;
        var definition = CustomerHubJourney.Build();
        Definition = definition;

        _steps = [.. definition.Steps.Select((s, i) => s with
        {
            State = i == 0 ? JourneyStepState.Active : JourneyStepState.Locked
        })];

        CurrentIndex = 0;
        IsThinking = false;
        ThinkingLabel = null;
        _liveImplementation = null;
        Changed?.Invoke();
    }

    public void GoTo(int index)
    {
        if (index < 0 || index >= _steps.Count || IsThinking)
        {
            return;
        }

        // Only revisit steps that have already been reached.
        if (_steps[index].State is JourneyStepState.Locked)
        {
            return;
        }

        CurrentIndex = index;
        Changed?.Invoke();
    }

    public async Task<AdvanceResult> AdvanceAsync()
    {
        if (IsThinking || CurrentIndex >= _steps.Count - 1)
        {
            return AdvanceResult.Done;
        }

        var nextIndex = CurrentIndex + 1;
        var nextStep = _steps[nextIndex];

        if (Mode == AppMode.Live)
        {
            var live = nextStep.Kind switch
            {
                JourneyStepKind.Specification => await RunLiveSpecificationAsync(nextStep),
                JourneyStepKind.Implementation => await RunLiveImplementationAsync(nextStep),
                JourneyStepKind.AcceptanceRun => await RunLiveAcceptanceAsync(nextStep),
                _ when nextStep.SimulatedThinkingMs > 0 => await SimulateAndOk(nextStep),
                _ => AdvanceResult.Ok
            };

            if (live.Kind != AdvanceResultKind.Ok)
            {
                return live;
            }
        }
        else if (Mode == AppMode.Demo && nextStep.SimulatedThinkingMs > 0)
        {
            await SimulateThinkingAsync(nextStep);
        }

        _steps[CurrentIndex] = _steps[CurrentIndex] with { State = JourneyStepState.Complete };
        _steps[nextIndex] = _steps[nextIndex] with { State = JourneyStepState.Active };
        CurrentIndex = nextIndex;
        Changed?.Invoke();

        return AdvanceResult.Ok;
    }

    public void Reset() => Load(Mode);

    private async Task SimulateThinkingAsync(JourneyStep step)
    {
        IsThinking = true;
        ThinkingLabel = LabelFor(step.Kind);
        Changed?.Invoke();

        try
        {
            await Task.Delay(step.SimulatedThinkingMs);
        }
        finally
        {
            IsThinking = false;
            ThinkingLabel = null;
        }
    }

    private async Task<AdvanceResult> SimulateAndOk(JourneyStep step)
    {
        await SimulateThinkingAsync(step);
        return AdvanceResult.Ok;
    }

    private ForgeOps.Contracts.Forge.GeneratedImplementation? _liveImplementation;

    private async Task<AdvanceResult> RunLiveImplementationAsync(JourneyStep step)
    {
        IsThinking = true;
        ThinkingLabel = "qwen3:8b is writing the implementation and tests…";
        Changed?.Invoke();

        try
        {
            var spec = Definition!.Steps.First(s => s.Kind == JourneyStepKind.Specification).Payload.Specification;
            if (spec is null)
            {
                return AdvanceResult.ModelError("No approved specification is available.");
            }

            var result = await _api.ForgeGenerateAsync(Definition.RequirementText, spec, Definition.ProjectName);
            if (result.IsBridgeOffline)
            {
                return AdvanceResult.BridgeOffline(result.FailureDetail ?? "AI Bridge is offline.");
            }

            if (!result.Ok || result.Response is null)
            {
                return AdvanceResult.ModelError(result.FailureDetail ?? "Code generation failed.");
            }

            var forge = result.Response.Result;
            _liveImplementation = forge.Implementation;

            _steps[step.Order] = step with
            {
                Caption = "qwen3:8b wrote this implementation and tests live.",
                Payload = step.Payload with
                {
                    Implementation = forge.Implementation,
                    AiInteraction = forge.Interaction,
                    Notes =
                    [
                        $"Live generation — {forge.Implementation.RepairAttempts} compile-repair round(s), "
                        + $"{forge.Implementation.Files.Count} file(s).",
                        result.Response.RunnerDisabled
                            ? "The sandbox runner is disabled on this host; the acceptance run will be skipped."
                            : "The audit and sandbox run follow."
                    ]
                }
            };

            // Feed the deterministic audit into the Audit + Quality Gate steps.
            ApplyAudit(forge);
            return AdvanceResult.Ok;
        }
        finally
        {
            IsThinking = false;
            ThinkingLabel = null;
        }
    }

    private async Task<AdvanceResult> RunLiveAcceptanceAsync(JourneyStep step)
    {
        if (_liveImplementation is null)
        {
            return AdvanceResult.ModelError("No generated implementation is available to run.");
        }

        IsThinking = true;
        ThinkingLabel = "Executing the acceptance suite in the sandbox…";
        Changed?.Invoke();

        try
        {
            var result = await _api.ForgeExecuteAsync(_liveImplementation);
            if (!result.Ok || result.Response is null)
            {
                return AdvanceResult.ModelError(result.FailureDetail ?? "The sandbox run failed.");
            }

            var forge = result.Response.Result;

            _steps[step.Order] = step with
            {
                Caption = result.Response.RunnerDisabled
                    ? "The code runner is disabled on this host — generation and audit still ran for real."
                    : "The sandbox executed the acceptance suite against the generated code.",
                Payload = step.Payload with
                {
                    AiTestRun = forge.AiTestRun,
                    CanonicalTestRun = forge.CanonicalTestRun,
                    Acceptance = forge.Acceptance,
                    Notes = result.Response.RunnerDisabled
                        ? ["Set CodeRunner:Enabled=true on a machine you control to execute the generated code."]
                        : [BuildAcceptanceNote(forge)]
                }
            };

            ApplyAcceptanceToGates(forge);
            return AdvanceResult.Ok;
        }
        finally
        {
            IsThinking = false;
            ThinkingLabel = null;
        }
    }

    private static string BuildAcceptanceNote(ForgeOps.Contracts.Forge.ForgeResult forge)
    {
        var c = forge.CanonicalTestRun;
        var total = c is null ? 0 : c.Passed + c.Failed;
        var satisfied = forge.Acceptance.Count(a => a.Status == ForgeOps.Contracts.Forge.AcceptanceStatus.Satisfied);
        return $"Requirement satisfied: {forge.RequirementSatisfied}. "
             + $"Canonical suite {c?.Passed ?? 0}/{total} passed · {satisfied}/{forge.Acceptance.Count} acceptance criteria met.";
    }

    private void ApplyAudit(ForgeResult forge)
    {
        var auditIndex = _steps.FindIndex(s => s.Kind == JourneyStepKind.Audit);
        if (auditIndex >= 0)
        {
            _steps[auditIndex] = _steps[auditIndex] with
            {
                Payload = _steps[auditIndex].Payload with { Audit = forge.Audit }
            };
        }

        // Rebuild the quality-gate timeline from the real audit so Live Mode shows real state.
        var gatesIndex = _steps.FindIndex(s => s.Kind == JourneyStepKind.QualityGates);
        if (gatesIndex < 0)
        {
            return;
        }

        var a = forge.Audit;
        var gates = new List<QualityGate>
        {
            SimpleGate("Compile (Roslyn)", a.Compiled ? GateStatus.Passed : GateStatus.Failed,
                a.Compiled ? [$"built after {a.RepairAttempts} repair round(s)"] : a.Diagnostics.Where(d => d.Severity == Contracts.Forge.DiagnosticSeverity.Error).Select(d => $"{d.Code}: {d.Message}").ToList()),
            SimpleGate("Banned-API scan", a.BannedApis.Count == 0 ? GateStatus.Passed : GateStatus.Failed,
                a.BannedApis.Count == 0 ? ["0 findings"] : a.BannedApis.Select(b => $"{b.File}:{b.Line} {b.Api}").ToList(), blocking: true),
            SimpleGate("Architecture", a.ArchitecturePassed ? GateStatus.Passed : GateStatus.Failed, a.ArchitectureNotes.ToList()),
            SimpleGate("AI-authored tests", GateStatus.Pending, []),
            SimpleGate("Acceptance (canonical)", GateStatus.Pending, [])
        };

        _steps[gatesIndex] = _steps[gatesIndex] with { Payload = _steps[gatesIndex].Payload with { Gates = gates } };
    }

    private static QualityGate SimpleGate(string name, GateStatus status, IReadOnlyList<string> lines, bool blocking = false) => new()
    {
        Name = name,
        Status = status,
        Blocking = blocking,
        Evidence = status == GateStatus.Failed ? [] : lines,
        Errors = status == GateStatus.Failed ? lines : [],
        Timestamp = DateTimeOffset.UtcNow
    };

    private void ApplyAcceptanceToGates(ForgeResult forge)
    {
        var gatesIndex = _steps.FindIndex(s => s.Kind == JourneyStepKind.QualityGates);
        if (gatesIndex < 0 || _steps[gatesIndex].Payload.Gates is null)
        {
            return;
        }

        var updated = _steps[gatesIndex].Payload.Gates!
            .Select(g => g.Name switch
            {
                "AI-authored tests" => GateFromRun(g, forge.AiTestRun, blocking: false),
                "Acceptance (canonical)" => GateFromRun(g, forge.CanonicalTestRun, blocking: true),
                _ => g
            })
            .ToList();

        _steps[gatesIndex] = _steps[gatesIndex] with { Payload = _steps[gatesIndex].Payload with { Gates = updated } };
    }

    private static QualityGate GateFromRun(QualityGate gate, TestRunResult? run, bool blocking)
    {
        if (run is null || !run.Executed)
        {
            return gate with { Status = GateStatus.Skipped, Blocking = blocking, Evidence = [run?.RunnerDetail ?? "not run"] };
        }

        var passed = run.AllPassed;
        return gate with
        {
            Status = passed ? GateStatus.Passed : GateStatus.Failed,
            Blocking = blocking,
            Evidence = passed ? [$"{run.Passed} / {run.Passed + run.Failed} passed"] : [],
            Errors = passed ? [] : run.Results.Where(r => r.Outcome == TestOutcome.Failed).Select(r => $"{r.Name}: {r.Message}").ToList()
        };
    }

    private async Task<AdvanceResult> RunLiveSpecificationAsync(JourneyStep step)
    {
        IsThinking = true;
        ThinkingLabel = "Calling qwen3:8b through the AI Bridge…";
        Changed?.Invoke();

        try
        {
            var result = await _api.GenerateSpecificationAsync(
                Definition!.RequirementText, Definition.ProjectName);

            if (result.IsBridgeOffline)
            {
                return AdvanceResult.BridgeOffline(result.FailureDetail ?? "AI Bridge is offline.");
            }

            if (!result.Ok || result.Response is null)
            {
                return AdvanceResult.ModelError(result.FailureDetail ?? "The model returned an unusable response.");
            }

            var response = result.Response;
            _steps[step.Order] = step with
            {
                Caption = "qwen3:8b drafted this specification live. Advisory only.",
                Payload = step.Payload with
                {
                    Specification = response.Draft,
                    AiInteraction = response.Interaction,
                    Notes = [$"Live AI Bridge call — provider {response.Interaction.Provider}, {response.Interaction.LatencyMs} ms, prompt {response.Interaction.PromptVersion}."]
                }
            };

            return AdvanceResult.Ok;
        }
        finally
        {
            IsThinking = false;
            ThinkingLabel = null;
        }
    }

    private static string LabelFor(JourneyStepKind kind) => kind switch
    {
        JourneyStepKind.Specification => "qwen3:8b is drafting the specification…",
        JourneyStepKind.Implementation => "qwen3:8b is writing the implementation and tests…",
        JourneyStepKind.Audit => "Running Roslyn compile, analyzers and the banned-API scan…",
        JourneyStepKind.QualityGates => "Executing the quality gate pipeline…",
        JourneyStepKind.AiReview => "qwen3:8b is reviewing the generated diff…",
        JourneyStepKind.AcceptanceRun => "Executing the acceptance suite in the sandbox…",
        JourneyStepKind.Merge => "Confirming every gate is green…",
        _ => "Working…"
    };
}

public enum AdvanceResultKind
{
    Ok,
    Done,
    BridgeOffline,
    ModelError
}

public sealed record AdvanceResult(AdvanceResultKind Kind, string? Detail = null)
{
    public static readonly AdvanceResult Ok = new(AdvanceResultKind.Ok);
    public static readonly AdvanceResult Done = new(AdvanceResultKind.Done);
    public static AdvanceResult BridgeOffline(string detail) => new(AdvanceResultKind.BridgeOffline, detail);
    public static AdvanceResult ModelError(string detail) => new(AdvanceResultKind.ModelError, detail);
}
