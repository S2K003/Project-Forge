using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;
using ForgeOps.Demo;
using ForgeOps.Forge;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ForgeOps.UnitTests;

/// <summary>
/// Exercises the deterministic forge pipeline end to end — Roslyn compile, banned-API
/// audit, and the real sandbox subprocess — against the recorded reference implementation.
/// No AI involved, so it runs in CI.
/// </summary>
public sealed class ForgePipelineTests
{
    private static GeneratedImplementation RecordedImplementation() =>
        CustomerHubJourney.Build().Steps
            .Single(s => s.Kind == JourneyStepKind.Implementation)
            .Payload.Implementation!;

    private static ForgePipeline BuildPipeline()
    {
        var compiler = new RoslynCompiler();
        var auditor = new GeneratedCodeAuditor(compiler);
        var options = Options.Create(new CodeRunnerOptions { Enabled = true, TimeoutSeconds = 30 });
        var runner = new SandboxRunner(options, NullLogger<SandboxRunner>.Instance);
        return new ForgePipeline(auditor, compiler, runner, NullLogger<ForgePipeline>.Instance);
    }

    [Fact]
    public void Recorded_implementation_compiles_and_passes_the_banned_api_audit()
    {
        var auditor = new GeneratedCodeAuditor(new RoslynCompiler());
        var result = auditor.Audit(RecordedImplementation().Files, repairAttempts: 0);

        Assert.True(result.Report.Compiled, string.Join("\n", result.Report.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Report.BannedApis);
        Assert.True(result.Report.ExecutionAllowed);
    }

    [Fact]
    public async Task Recorded_implementation_satisfies_every_acceptance_criterion_when_run()
    {
        var pipeline = BuildPipeline();

        if (!pipeline.RunnerAvailable)
        {
            // The sandbox executable is not next to the test host in this environment.
            return;
        }

        var result = await pipeline.RunAsync(RecordedImplementation(), execute: true, CancellationToken.None);

        Assert.NotNull(result.CanonicalTestRun);
        Assert.True(result.CanonicalTestRun!.AllPassed,
            $"{result.CanonicalTestRun.RunnerDetail}\n" +
            string.Join("\n", result.CanonicalTestRun.Results
                .Where(r => r.Outcome != TestOutcome.Passed)
                .Select(r => $"{r.Name}: {r.Message}")));

        Assert.All(result.Acceptance, a => Assert.Equal(AcceptanceStatus.Satisfied, a.Status));
        Assert.True(result.RequirementSatisfied);
    }

    [Fact]
    public async Task Scenario_walkthrough_executes_the_generated_code_and_reports_real_output()
    {
        var pipeline = BuildPipeline();
        if (!pipeline.RunnerAvailable)
        {
            return;
        }

        var result = await pipeline.RunAsync(RecordedImplementation(), execute: true, CancellationToken.None);

        Assert.NotNull(result.Scenario);
        Assert.True(result.Scenario!.Executed, result.Scenario.Detail);
        Assert.False(result.Scenario.Faulted);

        // The recorded reference impl awards floor($42.90) = 42, stays idempotent, and reverses on refund.
        Assert.Contains(result.Scenario.Steps, s => s.Output.Contains("alice balance = 42 points"));
        Assert.Contains(result.Scenario.Steps, s => s.Output.Contains("unchanged"));
        Assert.Contains(result.Scenario.Steps, s => s.Action.StartsWith("OnOrderRefunded") && s.Output.Contains("alice balance = 0"));
    }

    [Fact]
    public async Task A_non_idempotent_implementation_fails_the_canonical_duplicate_event_test()
    {
        var pipeline = BuildPipeline();
        if (!pipeline.RunnerAvailable)
        {
            return;
        }

        // Same as the reference impl but WITHOUT the idempotency guard.
        var broken = new GeneratedImplementation
        {
            Summary = "Non-idempotent loyalty implementation (deliberate).",
            Files =
            [
                new GeneratedFile
                {
                    Path = "LoyaltyService.cs",
                    Role = GeneratedFileRole.Implementation,
                    Content =
                        """
                        namespace CustomerHub.Loyalty;
                        using System;
                        using System.Collections.Generic;
                        using System.Linq;
                        public sealed class LoyaltyService : ILoyaltyService
                        {
                            private readonly Dictionary<string,int> _balances = new();
                            private readonly List<LedgerEntry> _ledger = new();
                            public IReadOnlyList<LedgerEntry> Ledger => _ledger;
                            public void OnPaymentConfirmed(Order order)
                            {
                                if (!order.IsPaid || order.NetTotal < 1.00m) return;
                                var points = (int)Math.Floor(order.NetTotal);
                                _balances[order.CustomerId] = BalanceFor(order.CustomerId) + points;
                                _ledger.Add(new LedgerEntry(order.OrderId, order.CustomerId, points, "purchase", DateTimeOffset.UtcNow));
                            }
                            public void OnOrderRefunded(string orderId)
                            {
                                var e = _ledger.FirstOrDefault(x => x.OrderId == orderId);
                                if (e is null) return;
                                _balances[e.CustomerId] = BalanceFor(e.CustomerId) - e.Points;
                            }
                            public int BalanceFor(string customerId) => _balances.TryGetValue(customerId, out var b) ? b : 0;
                        }
                        """
                }
            ]
        };

        var result = await pipeline.RunAsync(broken, execute: true, CancellationToken.None);

        Assert.True(result.Audit.ExecutionAllowed); // compiles, no banned APIs — audit can't catch a logic bug
        var ac2 = result.Acceptance.Single(a => a.CriterionId == "AC-2");
        Assert.Equal(AcceptanceStatus.NotSatisfied, ac2.Status);
        Assert.False(result.RequirementSatisfied);
    }
}
