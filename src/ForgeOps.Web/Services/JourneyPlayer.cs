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

    private string? _requirementOverride;

    /// <summary>The requirement being built — the user's edit in Live Mode, else the scenario's.</summary>
    public string RequirementText => string.IsNullOrWhiteSpace(_requirementOverride)
        ? Definition?.RequirementText ?? string.Empty
        : _requirementOverride!;

    public bool RequirementEdited => !string.IsNullOrWhiteSpace(_requirementOverride)
        && _requirementOverride!.Trim() != (Definition?.RequirementText ?? string.Empty).Trim();

    /// <summary>Live Mode: the user edited the requirement before generating the specification.</summary>
    public void SetRequirement(string text)
    {
        _requirementOverride = text;
        Changed?.Invoke();
    }

    private string? _refineFeedback;
    private int _refineRound;

    /// <summary>Live Mode: an optional free-text change the user typed on the Refine step.</summary>
    public string RefineFeedback => _refineFeedback ?? string.Empty;

    public void SetRefineFeedback(string text)
    {
        _refineFeedback = text;
        Changed?.Invoke();
    }

    public IReadOnlyList<JourneyStep> Steps => _steps;
    public JourneyStep? Current => _steps.Count > 0 ? _steps[CurrentIndex] : null;
    public bool IsComplete => Current is { Kind: JourneyStepKind.EngineeringHealth, State: JourneyStepState.Active or JourneyStepState.Complete };
    public bool IsLastStep => CurrentIndex == _steps.Count - 1;

    public event Action? Changed;

    public string JourneyKey { get; private set; } = JourneyCatalog.DefaultKey;

    public void Load(AppMode mode, string? journeyKey = null)
    {
        Mode = mode;
        JourneyKey = string.IsNullOrWhiteSpace(journeyKey) ? JourneyCatalog.DefaultKey : journeyKey;
        var definition = JourneyCatalog.Build(JourneyKey);
        Definition = definition;

        _steps = [.. definition.Steps.Select((s, i) => s with
        {
            State = i == 0 ? JourneyStepState.Active : JourneyStepState.Locked
        })];

        CurrentIndex = 0;
        IsThinking = false;
        ThinkingLabel = null;
        _liveImplementation = null;
        _requirementOverride = null;
        _refineFeedback = null;
        _refineRound = 0;
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
                JourneyStepKind.Refine => await RunLiveRefineAsync(nextStep),
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
        ThinkingLabel = "The local model is building the implementation…";
        Changed?.Invoke();

        try
        {
            var spec = _steps.First(s => s.Kind == JourneyStepKind.Specification).Payload.Specification;
            if (spec is null)
            {
                return AdvanceResult.ModelError("No approved specification is available.");
            }

            // Let the API classify the (possibly user-edited) requirement rather than assuming
            // the scenario's kind.
            var result = await _api.ForgeGenerateAsync(RequirementText, spec, Definition!.ProjectName, kind: null);
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
            var isUi = forge.Implementation.Kind == ForgeOps.Contracts.Forge.ImplementationKind.WebComponent;

            _steps[step.Order] = step with
            {
                Caption = isUi
                    ? "The local model built this web component live."
                    : "The local model wrote this implementation and tests live.",
                Payload = step.Payload with
                {
                    Implementation = forge.Implementation,
                    AiInteraction = forge.Interaction,
                    Notes = isUi
                        ? [$"Live generation — {forge.Implementation.RepairAttempts} audit-repair round(s). The component is rendered at Run & verify."]
                        :
                        [
                            $"Live generation — {forge.Implementation.RepairAttempts} compile-repair round(s), {forge.Implementation.Files.Count} file(s).",
                            result.Response.RunnerDisabled
                                ? "The sandbox runner is disabled on this host; the acceptance run will be skipped."
                                : "The audit and sandbox run follow."
                        ]
                }
            };

            ApplyAudit(forge);

            var runIndex = _steps.FindIndex(s => s.Kind == JourneyStepKind.AcceptanceRun);
            if (runIndex >= 0)
            {
                _steps[runIndex] = _steps[runIndex] with
                {
                    Caption = isUi
                        ? "The generated component, rendered in a locked-down sandboxed iframe."
                        : "The sandbox executes the acceptance suite against the generated code.",
                    // Reset stale recorded payloads — the requirement may have classified differently.
                    Payload = _steps[runIndex].Payload with
                    {
                        Ui = isUi ? forge.Ui : null,
                        Scenario = null,
                        AiTestRun = null,
                        CanonicalTestRun = null,
                        Acceptance = SpecAcceptance()
                    }
                };
            }

            ClearRecordedReviewIfEdited();
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

        // Web components are rendered client-side — the Run step's payload was already
        // populated from the generation call.
        if (_liveImplementation.Kind == ForgeOps.Contracts.Forge.ImplementationKind.WebComponent)
        {
            return AdvanceResult.Ok;
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
                    Scenario = forge.Scenario,
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

    /// <summary>
    /// Live Mode: ask the model to regenerate the artefact to close whatever the last run left
    /// unmet, plus any free-text change the user typed, then re-audit and re-run it (§52).
    /// </summary>
    private async Task<AdvanceResult> RunLiveRefineAsync(JourneyStep step)
    {
        if (_liveImplementation is null)
        {
            return AdvanceResult.ModelError("No generated implementation is available to refine.");
        }

        var spec = _steps.FirstOrDefault(s => s.Kind == JourneyStepKind.Specification)?.Payload.Specification;
        if (spec is null)
        {
            return AdvanceResult.ModelError("No approved specification is available.");
        }

        var runStep = _steps.FirstOrDefault(s => s.Kind == JourneyStepKind.AcceptanceRun)?.Payload;
        var isUi = _liveImplementation.Kind == ForgeOps.Contracts.Forge.ImplementationKind.WebComponent;

        var unmet = isUi
            ? []
            : (runStep?.Acceptance ?? [])
                .Where(a => a.Status != AcceptanceStatus.Satisfied)
                .Select(a => a.CriterionId)
                .ToList();

        var failingChecks = isUi
            ? (runStep?.Ui?.Results ?? []).Where(r => !r.Passed).Select(r => r.Title).ToList()
            : (runStep?.CanonicalTestRun?.Results ?? [])
                .Where(r => r.Outcome == TestOutcome.Failed)
                .Select(r => r.Name)
                .ToList();

        var feedback = string.IsNullOrWhiteSpace(_refineFeedback) ? null : _refineFeedback!.Trim();

        if (unmet.Count == 0 && failingChecks.Count == 0 && feedback is null)
        {
            _steps[step.Order] = step with
            {
                Caption = "Nothing to refine — the first attempt met every criterion.",
                Payload = step.Payload with
                {
                    Implementation = _liveImplementation,
                    Ui = isUi ? runStep?.Ui : null,
                    Scenario = runStep?.Scenario,
                    Acceptance = runStep?.Acceptance,
                    CanonicalTestRun = runStep?.CanonicalTestRun,
                    AiTestRun = runStep?.AiTestRun,
                    Refinement = new RefinementRound { Round = 0, Summary = "No refinement needed.", AllCriteriaMet = true },
                    AiInteraction = null,
                    Notes = ["The first generated artefact already passed. Type a change below and use “Refine again” to iterate."]
                }
            };
            return AdvanceResult.Ok;
        }

        IsThinking = true;
        ThinkingLabel = "The local model is regenerating the artefact to close the gaps…";
        Changed?.Invoke();

        try
        {
            _refineRound++;
            var result = await _api.ForgeRefineAsync(
                RequirementText, spec, _liveImplementation, unmet, failingChecks, feedback, _refineRound);

            if (result.IsBridgeOffline)
            {
                return AdvanceResult.BridgeOffline(result.FailureDetail ?? "AI Bridge is offline.");
            }

            if (!result.Ok || result.Response is null)
            {
                return AdvanceResult.ModelError(result.FailureDetail ?? "Refinement failed.");
            }

            var forge = result.Response.Result;
            _liveImplementation = forge.Implementation;
            _refineFeedback = null;

            _steps[step.Order] = step with
            {
                Caption = isUi
                    ? "The local model regenerated the component; it is re-rendered below."
                    : "The local model regenerated the code; ForgeOps re-audited and re-ran it.",
                Payload = step.Payload with
                {
                    Implementation = forge.Implementation,
                    Audit = forge.Audit,
                    Ui = isUi ? forge.Ui : null,
                    Scenario = forge.Scenario,
                    AiTestRun = forge.AiTestRun,
                    CanonicalTestRun = forge.CanonicalTestRun,
                    Acceptance = forge.Acceptance.Count > 0 ? forge.Acceptance : SpecAcceptance(),
                    Refinement = forge.Refinement ?? new RefinementRound
                    {
                        Round = _refineRound,
                        AddressedCriteria = unmet,
                        Feedback = feedback,
                        Summary = forge.Implementation.Summary,
                        AllCriteriaMet = forge.RequirementSatisfied
                    },
                    AiInteraction = forge.Interaction,
                    Notes = isUi
                        ? [$"Refinement round {_refineRound} — verify the component above by eye."]
                        : [BuildAcceptanceNote(forge)]
                }
            };

            ApplyAudit(forge);
            if (!isUi)
            {
                ApplyAcceptanceToGates(forge);
            }

            return AdvanceResult.Ok;
        }
        finally
        {
            IsThinking = false;
            ThinkingLabel = null;
        }
    }

    /// <summary>Live Mode: re-run the Refine step in place (the user asked for another change).</summary>
    public async Task<AdvanceResult> RefineAgainAsync()
    {
        var idx = _steps.FindIndex(s => s.Kind == JourneyStepKind.Refine);
        if (idx < 0 || Mode != AppMode.Live || IsThinking)
        {
            return AdvanceResult.Done;
        }

        var result = await RunLiveRefineAsync(_steps[idx]);
        Changed?.Invoke();
        return result;
    }

    /// <summary>The spec's acceptance criteria as a reviewer checklist (UI acceptance is human-judged, §2.1).</summary>
    private List<AcceptanceOutcome> SpecAcceptance()
    {
        var specStep = _steps.FirstOrDefault(s => s.Kind == JourneyStepKind.Specification);
        var spec = specStep?.Payload.Specification;
        return spec is null
            ? []
            : spec.AcceptanceCriteria
                .Select(c => new AcceptanceOutcome { CriterionId = c.Id, Statement = c.Statement, Status = AcceptanceStatus.NotCovered })
                .ToList();
    }

    /// <summary>Called from the browser once the sandboxed iframe has rendered and reported its checks.</summary>
    public void SetUiResults(IReadOnlyList<ForgeOps.Contracts.Forge.UiCheckResult> results)
    {
        // The Refine step also renders a component preview — update whichever step the results
        // came from (the current one if it carries a Ui), else the acceptance run.
        var runIndex = _steps[CurrentIndex].Payload.Ui is { DocumentHtml.Length: > 0 }
            ? CurrentIndex
            : _steps.FindIndex(s => s.Kind == JourneyStepKind.AcceptanceRun);
        if (runIndex < 0 || _steps[runIndex].Payload.Ui is not { } ui)
        {
            return;
        }

        _steps[runIndex] = _steps[runIndex] with
        {
            Payload = _steps[runIndex].Payload with
            {
                Ui = ui with { Rendered = true, Results = results }
            }
        };
        Changed?.Invoke();
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
        var web = a.Kind == Contracts.Forge.ImplementationKind.WebComponent;
        var gates = new List<QualityGate>
        {
            SimpleGate(web ? "Parse" : "Compile (Roslyn)", a.Compiled ? GateStatus.Passed : GateStatus.Failed,
                a.Compiled ? [web ? "Valid HTML document" : $"built after {a.RepairAttempts} repair round(s)"] : a.Diagnostics.Where(d => d.Severity == Contracts.Forge.DiagnosticSeverity.Error).Select(d => $"{d.Code}: {d.Message}").ToList()),
            SimpleGate(web ? "Banned-pattern scan" : "Banned-API scan", a.BannedApis.Count == 0 ? GateStatus.Passed : GateStatus.Failed,
                a.BannedApis.Count == 0 ? ["0 findings"] : a.BannedApis.Select(b => $"{b.File}:{b.Line} {b.Api}").ToList(), blocking: true),
            SimpleGate(web ? "Self-contained" : "Architecture", a.ArchitecturePassed ? GateStatus.Passed : GateStatus.Failed, a.ArchitectureNotes.ToList()),
            web
                ? SimpleGate("Behavioural checks", GateStatus.Pending, ["run in the sandboxed iframe at Run & verify"])
                : SimpleGate("AI-authored tests", GateStatus.Pending, []),
        };
        if (!web)
        {
            gates.Add(SimpleGate("Acceptance (canonical)", GateStatus.Pending, []));
        }

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

    /// <summary>
    /// When the user has edited the requirement, the seeded AI-review findings (about the
    /// scenario's fixed artefact) no longer apply — replace them with an honest note.
    /// </summary>
    private void ClearRecordedReviewIfEdited()
    {
        if (!RequirementEdited)
        {
            return;
        }

        var idx = _steps.FindIndex(s => s.Kind == JourneyStepKind.AiReview);
        if (idx >= 0)
        {
            _steps[idx] = _steps[idx] with
            {
                Caption = "AI review over the generated artefact.",
                Payload = _steps[idx].Payload with
                {
                    ReviewFindings = [],
                    AiInteraction = null,
                    Notes = ["AI code review over a custom artefact is not wired in this build — the deterministic audit and the run step above are the live evidence."]
                }
            };
        }
    }

    private async Task<AdvanceResult> RunLiveSpecificationAsync(JourneyStep step)
    {
        IsThinking = true;
        ThinkingLabel = "The local model is drafting the specification…";
        Changed?.Invoke();

        try
        {
            if (string.IsNullOrWhiteSpace(RequirementText))
            {
                return AdvanceResult.ModelError("Enter a requirement first.");
            }

            var result = await _api.GenerateSpecificationAsync(RequirementText, Definition!.ProjectName);

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
                Caption = "The local model drafted this specification live. Advisory only.",
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
        JourneyStepKind.Specification => "The local model is drafting the specification…",
        JourneyStepKind.Implementation => "The local model is writing the implementation and tests…",
        JourneyStepKind.Audit => "Running Roslyn compile, analyzers and the banned-API scan…",
        JourneyStepKind.QualityGates => "Executing the quality gate pipeline…",
        JourneyStepKind.AiReview => "The local model is reviewing the generated diff…",
        JourneyStepKind.AcceptanceRun => "Executing the acceptance suite in the sandbox…",
        JourneyStepKind.Refine => "The local model is regenerating the artefact to close the gaps…",
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
