using ForgeOps.Contracts.Forge;
using Microsoft.Extensions.Logging;

namespace ForgeOps.Forge;

/// <summary>
/// The deterministic half of the forge pipeline (ProjectForge.md §2 — AI assists, tooling
/// and humans decide). Given a candidate implementation the model produced, it audits it,
/// and — only if the audit allows and a human has approved upstream — executes both the
/// model's own tests and ForgeOps' canonical acceptance suite in the sandbox.
/// </summary>
public sealed class ForgePipeline
{
    private readonly GeneratedCodeAuditor _auditor;
    private readonly RoslynCompiler _compiler;
    private readonly SandboxRunner _runner;
    private readonly ILogger<ForgePipeline> _logger;

    public ForgePipeline(
        GeneratedCodeAuditor auditor,
        RoslynCompiler compiler,
        SandboxRunner runner,
        ILogger<ForgePipeline> logger)
    {
        _auditor = auditor;
        _compiler = compiler;
        _runner = runner;
        _logger = logger;
    }

    public bool RunnerAvailable => _runner.Available;

    public async Task<ForgeResult> RunAsync(
        GeneratedImplementation implementation,
        bool execute,
        CancellationToken cancellationToken = default)
    {
        if (implementation.Kind == ImplementationKind.WebComponent)
        {
            return BuildWebComponentResult(implementation);
        }

        var audit = _auditor.Audit(implementation.Files, implementation.RepairAttempts);

        // Always compute the acceptance map so the UI can show "not covered" up front.
        var acceptance = GeneratedSources.CriteriaStatements
            .Select(kvp => new AcceptanceOutcome
            {
                CriterionId = kvp.Key,
                Statement = kvp.Value,
                Status = AcceptanceStatus.NotCovered
            })
            .ToList();

        if (!audit.Report.ExecutionAllowed || !execute || !_runner.Available || audit.ImplementationImage is null)
        {
            return new ForgeResult
            {
                Implementation = implementation,
                Audit = audit.Report,
                Acceptance = acceptance
            };
        }

        var implFiles = implementation.Files
            .Where(f => f.Role == GeneratedFileRole.Implementation)
            .ToDictionary(f => f.Path, f => f.Content);
        var aiTestFiles = implementation.Files
            .Where(f => f.Role == GeneratedFileRole.Test)
            .ToDictionary(f => f.Path, f => f.Content);

        var common = new Dictionary<string, string>
        {
            ["__Contract.cs"] = GeneratedSources.Contract,
            ["__ForgeTestKit.cs"] = GeneratedSources.TestKit,
        };

        // --- the model's own tests ---
        TestRunResult? aiRun = null;
        if (aiTestFiles.Count > 0)
        {
            var aiSources = Merge(common, implFiles, aiTestFiles);
            var aiCompile = _compiler.Compile("ForgeOps.Generated.AiTests", aiSources);
            aiRun = aiCompile.Success && aiCompile.AssemblyImage is not null
                ? await _runner.RunAsync(TestSuiteKind.AiGenerated, aiCompile.AssemblyImage, cancellationToken)
                : new TestRunResult
                {
                    Suite = TestSuiteKind.AiGenerated,
                    Executed = false,
                    RunnerDetail = "The model's own tests did not compile: "
                        + string.Join("; ", aiCompile.Errors.Select(e => $"{e.Code} {e.Message}").Take(3))
                };
        }

        // --- scripted walkthrough: run the model's code with concrete inputs, show outputs ---
        ScenarioRun? scenario = null;
        var scenarioSources = Merge(common, implFiles,
            new Dictionary<string, string> { ["__Walkthrough.cs"] = GeneratedSources.ScenarioSuite });
        var scenarioCompile = _compiler.Compile("ForgeOps.Generated.Walkthrough", scenarioSources);
        if (scenarioCompile.Success && scenarioCompile.AssemblyImage is not null)
        {
            scenario = await _runner.RunScenarioAsync(scenarioCompile.AssemblyImage, cancellationToken);
        }
        else
        {
            scenario = new ScenarioRun
            {
                Executed = false,
                Detail = "Walkthrough did not compile against the implementation: "
                    + string.Join("; ", scenarioCompile.Errors.Select(e => $"{e.Code} {e.Message}").Take(3))
            };
        }

        // --- ForgeOps' canonical acceptance suite ---
        var canonicalSources = Merge(common, implFiles,
            new Dictionary<string, string> { ["__Canonical.cs"] = GeneratedSources.CanonicalSuite });
        var canonicalCompile = _compiler.Compile("ForgeOps.Generated.Canonical", canonicalSources);

        TestRunResult canonicalRun;
        if (canonicalCompile.Success && canonicalCompile.AssemblyImage is not null)
        {
            canonicalRun = await _runner.RunAsync(TestSuiteKind.Canonical, canonicalCompile.AssemblyImage, cancellationToken);
            acceptance = MapAcceptance(canonicalRun);
        }
        else
        {
            canonicalRun = new TestRunResult
            {
                Suite = TestSuiteKind.Canonical,
                Executed = false,
                RunnerDetail = "The implementation does not satisfy the contract well enough to run acceptance tests: "
                    + string.Join("; ", canonicalCompile.Errors.Select(e => $"{e.Code} {e.Message}").Take(3))
            };
        }

        return new ForgeResult
        {
            Implementation = implementation,
            Audit = audit.Report,
            AiTestRun = aiRun,
            CanonicalTestRun = canonicalRun,
            Scenario = scenario,
            Acceptance = acceptance
        };
    }

    private static ForgeResult BuildWebComponentResult(GeneratedImplementation implementation)
    {
        var html = implementation.Files.FirstOrDefault(f => f.Role == GeneratedFileRole.Implementation)?.Content ?? string.Empty;
        var report = GeneratedCodeAuditor.AuditWebComponent(html, implementation.RepairAttempts);

        // Acceptance for a UI component is human-judged against the criteria (§2.1, §15) —
        // ForgeOps renders it and runs the model's behavioural checks in the sandboxed iframe.
        var ui = new UiPreview
        {
            DocumentHtml = report.Verdict == AuditVerdict.Failed ? string.Empty : html,
            Checks = report.Verdict == AuditVerdict.Failed ? [] : implementation.UiChecks,
            ReviewNotes = implementation.ReviewNotes,
            Rendered = false
        };

        return new ForgeResult
        {
            Implementation = implementation,
            Audit = report,
            Ui = ui,
            Acceptance = []
        };
    }

    private static List<AcceptanceOutcome> MapAcceptance(TestRunResult canonical)
    {
        return GeneratedSources.CriteriaStatements.Select(kvp =>
        {
            var tests = canonical.Results.Where(r => r.Criteria.Contains(kvp.Key)).ToList();
            var status = tests.Count == 0
                ? AcceptanceStatus.NotCovered
                : tests.All(t => t.Outcome == TestOutcome.Passed)
                    ? AcceptanceStatus.Satisfied
                    : AcceptanceStatus.NotSatisfied;

            return new AcceptanceOutcome
            {
                CriterionId = kvp.Key,
                Statement = kvp.Value,
                Status = status,
                EvidenceTests = tests.Select(t => t.Name).ToList()
            };
        }).ToList();
    }

    private static Dictionary<string, string> Merge(params IReadOnlyDictionary<string, string>[] sets)
    {
        var result = new Dictionary<string, string>();
        foreach (var set in sets)
        {
            foreach (var (k, v) in set)
            {
                result[k] = v;
            }
        }

        return result;
    }
}
