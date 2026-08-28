using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Engineering;
using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;

namespace ForgeOps.Demo;

/// <summary>
/// The canonical CustomerHub script (ProjectForge.md §30, §31), now carrying the full
/// generate → audit → run pipeline. This is the single source of truth for Demo Mode —
/// compiled into the WASM bundle so the walkthrough works with no backend, no AI Bridge,
/// no code runner and no network (§9A.2).
///
/// Live Mode performs the identical steps for real: qwen3:8b drafts the spec and the
/// implementation, Roslyn audits it, and the sandbox executes the tests.
/// </summary>
public static class CustomerHubJourney
{
    public const string ProjectKey = "customerhub";

    public static JourneyDefinition Build() => new()
    {
        ProjectKey = ProjectKey,
        ProjectName = "CustomerHub",
        RequirementText = "Customers should receive loyalty points after successful purchases.",
        Steps =
        [
            SignIn(),
            Requirement(),
            Specification(),
            HumanReview(),
            Implementation(),
            Audit(),
            QualityGates(),
            AiReview(),
            HumanDecision(),
            AcceptanceRun(),
            Merge(),
            Telemetry(),
            Health()
        ]
    };

    // ------------------------------------------------------------------ steps

    private static JourneyStep SignIn() => new()
    {
        Order = 0,
        Kind = JourneyStepKind.SignIn,
        Title = "Sign in",
        Caption = "Demo Mode entry point — not real authentication.",
        State = JourneyStepState.Ready,
        Payload = new StepPayload
        {
            Notes =
            [
                "In Live Mode this is real authentication with role-based access (§22).",
                "In Demo Mode it is a scripted entry into the CustomerHub walkthrough."
            ]
        }
    };

    private static JourneyStep Requirement() => new()
    {
        Order = 1,
        Kind = JourneyStepKind.Requirement,
        Title = "Create requirement",
        Caption = "A one-line product ask enters the governance pipeline.",
        Payload = new StepPayload
        {
            Notes =
            [
                "Requirement: \"Customers should receive loyalty points after successful purchases.\"",
                "Raw requirement text is treated as untrusted input to the AI Gateway (§10)."
            ]
        }
    };

    private static JourneyStep Specification() => new()
    {
        Order = 2,
        Kind = JourneyStepKind.Specification,
        Title = "AI specification",
        Caption = "qwen3:8b drafts a testable specification. Advisory only.",
        SimulatedThinkingMs = 1900,
        Payload = new StepPayload
        {
            Specification = SpecDraft,
            AiInteraction = Recorded("spec.v1", 4120, 0.71),
            Notes = ["AI output passed deterministic schema validation (§9.2): 5 acceptance criteria, all testable."]
        }
    };

    private static JourneyStep HumanReview() => new()
    {
        Order = 3,
        Kind = JourneyStepKind.HumanReview,
        Title = "Human review",
        Caption = "An engineer accepts the specification before any code is generated.",
        Payload = new StepPayload
        {
            Specification = SpecDraft,
            AiInteraction = Recorded("spec.v1", 4120, 0.71) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.AcceptedWithModification,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-28T09:14:00Z"),
                    Reason = "Accepted. AC-4 minimum qualifying value fixed at 1.00 for the demo market."
                }
            },
            Notes =
            [
                "The specification is now human-approved. AI did not decide — a person did (§2.1).",
                "This approval is what unlocks code generation."
            ]
        }
    };

    private static JourneyStep Implementation() => new()
    {
        Order = 4,
        Kind = JourneyStepKind.Implementation,
        Title = "AI implementation",
        Caption = "qwen3:8b writes the implementation and its own tests from the approved spec.",
        SimulatedThinkingMs = 3200,
        Payload = new StepPayload
        {
            Implementation = new GeneratedImplementation
            {
                Summary = "In-memory LoyaltyService: floor(net total) points on paid orders ≥ 1.00, idempotent per order, reversible on refund.",
                Rationale =
                    "Awarded points are tracked in a dictionary keyed by OrderId so a redelivered "
                    + "payment-confirmed event is a no-op. Refunds look up the recorded award and post a "
                    + "compensating ledger entry. All state is in-memory; no external dependencies.",
                RepairAttempts = 1,
                Origin = ImplementationOrigin.ModelWithRepairs,
                Files =
                [
                    new GeneratedFile { Path = "LoyaltyService.cs", Role = GeneratedFileRole.Implementation, Content = ImplSource },
                    new GeneratedFile { Path = "LoyaltyServiceTests.cs", Role = GeneratedFileRole.Test, Content = AiTestSource }
                ]
            },
            AiInteraction = Recorded("impl.v3", 21850, 0.68),
            Notes =
            [
                "First compile failed (missing `using System;`); the generator fed the error back and the model fixed it — 1 repair round.",
                "Opened as PR #142 on branch feature/loyalty-points (2 files, +96 −0)."
            ]
        }
    };

    private static JourneyStep Audit() => new()
    {
        Order = 5,
        Kind = JourneyStepKind.Audit,
        Title = "Deterministic audit",
        Caption = "Roslyn compile, analyzers, banned-API scan, architecture rules. No model involved.",
        SimulatedThinkingMs = 1400,
        Payload = new StepPayload
        {
            Audit = new AuditReport
            {
                Compiled = true,
                RepairAttempts = 1,
                Diagnostics =
                [
                    new CompileDiagnostic
                    {
                        Severity = Contracts.Forge.DiagnosticSeverity.Info,
                        Code = "CA1805",
                        Message = "Member is explicitly initialized to its default value.",
                        File = "LoyaltyService.cs",
                        Line = 12
                    }
                ],
                BannedApis = [],
                ArchitecturePassed = true,
                ArchitectureNotes =
                [
                    "Implements ILoyaltyService; sealed; no public mutable state; namespace CustomerHub.Loyalty."
                ],
                Verdict = AuditVerdict.Passed
            },
            Notes =
            [
                "Compiled against a curated reference set — System.IO / System.Net / interop are not even resolvable.",
                "Banned-API scan: 0 findings. The audit permits sandboxed execution (§10)."
            ]
        }
    };

    private static JourneyStep QualityGates() => new()
    {
        Order = 6,
        Kind = JourneyStepKind.QualityGates,
        Title = "Quality gates",
        Caption = "Deterministic pipeline over the generated code. AI cannot override these (§13).",
        SimulatedThinkingMs = 2200,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Compile (Roslyn)", GateStatus.Passed, 2, evidence: ["0 errors after 1 repair round"]),
                Gate("Format", GateStatus.Passed, 1),
                Gate("Static analysis", GateStatus.Passed, 3, warnings: ["CA1805 (info) in LoyaltyService.cs:12"]),
                Gate("Banned-API scan", GateStatus.Passed, 1, evidence: ["0 uses of process / filesystem / network / interop"]),
                Gate("Architecture", GateStatus.Passed, 2, evidence: ["Sealed, implements the contract, no hidden state"]),
                Gate("AI-authored tests", GateStatus.Passed, 4, evidence: ["4 passed / 4 (sandboxed)"]),
                Gate("Acceptance (canonical)", GateStatus.Pending, 0)
            ],
            Notes = ["The canonical acceptance suite runs at the Acceptance step, after a human approves execution."]
        }
    };

    private static JourneyStep AiReview() => new()
    {
        Order = 7,
        Kind = JourneyStepKind.AiReview,
        Title = "AI review",
        Caption = "qwen3:8b reviews the generated diff.",
        SimulatedThinkingMs = 2400,
        Payload = new StepPayload
        {
            ReviewFindings =
            [
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Medium,
                    Classification = AiClassification.Likely,
                    Finding = "OnOrderRefunded assumes a ledger entry exists for the order id before reading CustomerId.",
                    Evidence = "LoyaltyService.cs:34",
                    Recommendation = "The early-return on the awarded-orders dictionary already guards this, but assert it or use the stored award record directly.",
                    Confidence = 0.72
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Low,
                    Classification = AiClassification.Confirmed,
                    Finding = "Idempotency is handled by tracking awarded orders in a dictionary keyed by OrderId.",
                    Evidence = "LoyaltyService.cs:22",
                    Recommendation = "Good. This directly satisfies acceptance criterion AC-2.",
                    Confidence = 0.94
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Low,
                    Classification = AiClassification.Suggestion,
                    Finding = "Points crediting emits no telemetry.",
                    Evidence = "LoyaltyService.cs",
                    Recommendation = "Emit a counter (points awarded / reversed) so the behaviour is observable in production (OBS-001).",
                    Confidence = 0.5
                }
            ],
            AiInteraction = Recorded("codereview.v1", 6120, 0.74),
            Notes = ["Every finding carries a classification: Confirmed / Likely / Possible / Suggestion (§18)."]
        }
    };

    private static JourneyStep HumanDecision() => new()
    {
        Order = 8,
        Kind = JourneyStepKind.HumanDecision,
        Title = "Human decision",
        Caption = "The engineer approves running the generated code against the acceptance suite.",
        Payload = new StepPayload
        {
            AiInteraction = Recorded("codereview.v1", 6120, 0.74) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.Accepted,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-28T09:41:00Z"),
                    Reason = "Audit is clean and the idempotency approach looks right. Approving execution of the acceptance suite."
                }
            },
            Notes =
            [
                "AI generated → deterministic audit passed → human approved execution → now the code runs (§52).",
                "Nothing has executed the model's code until this point (§10)."
            ]
        }
    };

    private static JourneyStep AcceptanceRun() => new()
    {
        Order = 9,
        Kind = JourneyStepKind.AcceptanceRun,
        Title = "Run & verify",
        Caption = "The sandbox executes ForgeOps' canonical acceptance suite against the generated code.",
        SimulatedThinkingMs = 2600,
        Payload = new StepPayload
        {
            AiTestRun = new TestRunResult
            {
                Suite = TestSuiteKind.AiGenerated,
                Executed = true,
                Passed = 4,
                Failed = 0,
                Skipped = 0,
                DurationMs = 41,
                RunnerDetail = "4 test(s) in 41 ms (sandboxed child process, 20s budget)",
                Results =
                [
                    Pass("LoyaltyServiceTests.Awards_points_for_paid_order"),
                    Pass("LoyaltyServiceTests.No_points_for_unpaid_order"),
                    Pass("LoyaltyServiceTests.Refund_reverses_points"),
                    Pass("LoyaltyServiceTests.Duplicate_event_credits_once")
                ]
            },
            CanonicalTestRun = new TestRunResult
            {
                Suite = TestSuiteKind.Canonical,
                Executed = true,
                Passed = 6,
                Failed = 0,
                Skipped = 0,
                DurationMs = 38,
                RunnerDetail = "6 test(s) in 38 ms (sandboxed child process)",
                Results =
                [
                    Pass("LoyaltyAcceptance.Awards_floor_of_net_total_on_paid_order", "AC-1"),
                    Pass("LoyaltyAcceptance.Duplicate_payment_event_credits_points_at_most_once", "AC-2"),
                    Pass("LoyaltyAcceptance.Full_refund_reverses_awarded_points", "AC-3"),
                    Pass("LoyaltyAcceptance.Below_minimum_qualifying_value_awards_nothing", "AC-4"),
                    Pass("LoyaltyAcceptance.Crediting_writes_an_audit_entry_for_the_order", "AC-5"),
                    Pass("LoyaltyAcceptance.Unpaid_order_awards_nothing", "AC-1")
                ]
            },
            Acceptance =
            [
                Acc("AC-1", "A paid order credits floor(net total) points to the customer.", AcceptanceStatus.Satisfied,
                    "LoyaltyAcceptance.Awards_floor_of_net_total_on_paid_order", "LoyaltyAcceptance.Unpaid_order_awards_nothing"),
                Acc("AC-2", "A redelivered payment-confirmed event credits points at most once.", AcceptanceStatus.Satisfied,
                    "LoyaltyAcceptance.Duplicate_payment_event_credits_points_at_most_once"),
                Acc("AC-3", "A full refund reverses the points that were awarded.", AcceptanceStatus.Satisfied,
                    "LoyaltyAcceptance.Full_refund_reverses_awarded_points"),
                Acc("AC-4", "A purchase below the minimum qualifying value credits nothing.", AcceptanceStatus.Satisfied,
                    "LoyaltyAcceptance.Below_minimum_qualifying_value_awards_nothing"),
                Acc("AC-5", "Crediting writes an audit entry recording the order and reason.", AcceptanceStatus.Satisfied,
                    "LoyaltyAcceptance.Crediting_writes_an_audit_entry_for_the_order")
            ],
            Notes =
            [
                "All 5 acceptance criteria are satisfied by executed tests — the requirement is met by code that runs.",
                "On an earlier attempt the model's implementation double-credited points on duplicate events; the canonical AC-2 test caught it and the model regenerated (§31)."
            ]
        }
    };

    private static JourneyStep Merge() => new()
    {
        Order = 10,
        Kind = JourneyStepKind.Merge,
        Title = "Merge",
        Caption = "Deterministic evidence is green and a human approved. PR #142 merges.",
        SimulatedThinkingMs = 1200,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Compile (Roslyn)", GateStatus.Passed, 2),
                Gate("Banned-API scan", GateStatus.Passed, 1),
                Gate("Architecture", GateStatus.Passed, 2),
                Gate("AI-authored tests", GateStatus.Passed, 4, evidence: ["4 / 4"]),
                Gate("Acceptance (canonical)", GateStatus.Passed, 4, evidence: ["6 / 6 — AC-1..AC-5 satisfied"])
            ],
            PullRequest = new PullRequestSummary
            {
                Number = 142,
                Title = "Implement customer loyalty points",
                Branch = "feature/loyalty-points",
                FilesChanged = 2,
                Additions = 96,
                Deletions = 0
            },
            Notes = ["PR #142 merged to main at 10:02. Every blocking gate green; acceptance proven by execution."]
        }
    };

    private static JourneyStep Telemetry() => new()
    {
        Order = 11,
        Kind = JourneyStepKind.Telemetry,
        Title = "Telemetry",
        Caption = "The forge pipeline is itself observable.",
        Payload = new StepPayload
        {
            Telemetry =
            [
                new TelemetrySample { Metric = "forgeops_ai_request_duration p95", Value = "21.8 s", Detail = "impl.v1 via AI Bridge" },
                new TelemetrySample { Metric = "forgeops_ai_bridge_up", Value = "1", Detail = "bridge reachable" },
                new TelemetrySample { Metric = "forgeops_forge_repair_rounds", Value = "1", Detail = "compile-error repairs before green" },
                new TelemetrySample { Metric = "forgeops_sandbox_run_duration p95", Value = "79 ms", Detail = "canonical + AI suites" },
                new TelemetrySample { Metric = "forgeops_acceptance_satisfied_ratio", Value = "5 / 5" },
                new TelemetrySample { Metric = "forgeops_background_jobs_total", Value = "37", Detail = "0 failed" }
            ],
            Notes = ["Repair rounds and acceptance ratio are first-class metrics — the generator's reliability is measurable."]
        }
    };

    private static JourneyStep Health() => new()
    {
        Order = 12,
        Kind = JourneyStepKind.EngineeringHealth,
        Title = "Engineering health",
        Caption = "Deterministic score, with a full \"Why?\" (§14, §15).",
        Payload = new StepPayload
        {
            Health = new EngineeringHealth
            {
                Score = 88,
                Components =
                [
                    new HealthComponent { Name = "Tests", Weight = 0.25, Score = 92 },
                    new HealthComponent { Name = "Architecture", Weight = 0.20, Score = 96 },
                    new HealthComponent { Name = "Security", Weight = 0.20, Score = 94 },
                    new HealthComponent { Name = "Code quality", Weight = 0.15, Score = 82 },
                    new HealthComponent { Name = "Observability", Weight = 0.10, Score = 72 },
                    new HealthComponent { Name = "Delivery", Weight = 0.05, Score = 80 },
                    new HealthComponent { Name = "Documentation", Weight = 0.05, Score = 66 }
                ],
                Reasons =
                [
                    new HealthReason { Kind = ReasonKind.Pass, Text = "5 / 5 acceptance criteria satisfied by executed tests" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "Deterministic audit clean — 0 banned APIs, architecture rules pass" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "10 / 10 tests passing in the sandbox (AI + canonical)" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "Generated code emits no telemetry yet (OBS-001 suggestion open)" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "Loyalty module documentation is a stub" }
                ]
            }
        }
    };

    // ------------------------------------------------------------- shared data

    private static readonly SpecificationDraft SpecDraft = new()
    {
        Title = "Loyalty points awarded on confirmed purchase",
        Summary =
            "When a customer's purchase is confirmed as paid, credit loyalty points equal to floor(order net "
            + "total), 1 point per whole currency unit. Crediting must be exactly-once and reversible on refund.",
        AcceptanceCriteria =
        [
            new AcceptanceCriterion { Id = "AC-1", Statement = "Given a paid order, When the payment is confirmed, Then loyalty points equal to floor(order net total) are credited to the customer." },
            new AcceptanceCriterion { Id = "AC-2", Statement = "Given the same payment-confirmed event is delivered more than once, When it is processed, Then points are credited at most once for that order." },
            new AcceptanceCriterion { Id = "AC-3", Statement = "Given an order that awarded points is fully refunded, When the refund settles, Then the previously awarded points are reversed." },
            new AcceptanceCriterion { Id = "AC-4", Statement = "Given a purchase below the minimum qualifying value, When payment is confirmed, Then no points are credited." },
            new AcceptanceCriterion { Id = "AC-5", Statement = "Given points are credited or reversed, When the operation completes, Then an audit entry records the order, amount and reason." }
        ],
        OutOfScope = ["Tiered earn rates or promotional multipliers.", "Point expiry."],
        OpenQuestions =
        [
            "Do partial refunds reverse points proportionally, or only full refunds?",
            "Is the minimum qualifying value configurable per market?"
        ]
    };

    private const string ImplSource =
        """
        namespace CustomerHub.Loyalty;

        using System;
        using System.Collections.Generic;
        using System.Linq;

        public sealed class LoyaltyService : ILoyaltyService
        {
            private const decimal MinimumQualifyingValue = 1.00m;

            private readonly Dictionary<string, int> _awardedByOrder = new();
            private readonly Dictionary<string, int> _balances = new();
            private readonly List<LedgerEntry> _ledger = new();

            public IReadOnlyList<LedgerEntry> Ledger => _ledger;

            public void OnPaymentConfirmed(Order order)
            {
                ArgumentNullException.ThrowIfNull(order);

                if (!order.IsPaid || order.NetTotal < MinimumQualifyingValue)
                    return;

                if (_awardedByOrder.ContainsKey(order.OrderId))
                    return; // redelivered event — no-op (AC-2)

                var points = (int)Math.Floor(order.NetTotal);
                _awardedByOrder[order.OrderId] = points;
                Adjust(order.CustomerId, points);
                _ledger.Add(new LedgerEntry(order.OrderId, order.CustomerId, points, "purchase", DateTimeOffset.UtcNow));
            }

            public void OnOrderRefunded(string orderId)
            {
                if (!_awardedByOrder.TryGetValue(orderId, out var points))
                    return;

                _awardedByOrder.Remove(orderId);
                var customerId = _ledger.First(e => e.OrderId == orderId).CustomerId;
                Adjust(customerId, -points);
                _ledger.Add(new LedgerEntry(orderId, customerId, -points, "refund", DateTimeOffset.UtcNow));
            }

            public int BalanceFor(string customerId) =>
                _balances.TryGetValue(customerId, out var balance) ? balance : 0;

            private void Adjust(string customerId, int delta) =>
                _balances[customerId] = BalanceFor(customerId) + delta;
        }
        """;

    private const string AiTestSource =
        """
        namespace CustomerHub.Loyalty.Tests;

        using CustomerHub.Loyalty;
        using ForgeOps.Generated;

        public static class LoyaltyServiceTests
        {
            [ForgeFact]
            public static void Awards_points_for_paid_order()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(new Order("o1", "c1", 25.50m, true));
                Check.Equal(25, svc.BalanceFor("c1"));
            }

            [ForgeFact]
            public static void No_points_for_unpaid_order()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(new Order("o2", "c1", 25.50m, false));
                Check.Equal(0, svc.BalanceFor("c1"));
            }

            [ForgeFact]
            public static void Refund_reverses_points()
            {
                var svc = new LoyaltyService();
                svc.OnPaymentConfirmed(new Order("o3", "c1", 40m, true));
                svc.OnOrderRefunded("o3");
                Check.Equal(0, svc.BalanceFor("c1"));
            }

            [ForgeFact]
            public static void Duplicate_event_credits_once()
            {
                var svc = new LoyaltyService();
                var order = new Order("o4", "c1", 40m, true);
                svc.OnPaymentConfirmed(order);
                svc.OnPaymentConfirmed(order);
                Check.Equal(40, svc.BalanceFor("c1"));
            }
        }
        """;

    // ------------------------------------------------------------- builders

    private static QualityGate Gate(
        string name,
        GateStatus status,
        int seconds,
        bool blocking = false,
        IReadOnlyList<string>? evidence = null,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null) => new()
    {
        Name = name,
        Status = status,
        Duration = TimeSpan.FromSeconds(seconds),
        Blocking = blocking,
        Evidence = evidence ?? [],
        Errors = errors ?? [],
        Warnings = warnings ?? [],
        Timestamp = DateTimeOffset.Parse("2026-08-28T09:20:00Z").AddSeconds(seconds)
    };

    private static TestResult Pass(string name, params string[] criteria) => new()
    {
        Name = name,
        Outcome = TestOutcome.Passed,
        DurationMs = 3,
        Criteria = criteria
    };

    private static AcceptanceOutcome Acc(string id, string statement, AcceptanceStatus status, params string[] tests) => new()
    {
        CriterionId = id,
        Statement = statement,
        Status = status,
        EvidenceTests = tests
    };

    private static AiInteractionRecord Recorded(string promptVersion, long latencyMs, double confidence) => new()
    {
        Id = $"demo-{promptVersion}",
        Provider = "OllamaBridge (recorded)",
        Model = "qwen3:8b",
        ModelVersion = "qwen3:8b@2025-05",
        PromptVersion = promptVersion,
        RequestedAt = DateTimeOffset.Parse("2026-08-28T09:12:00Z"),
        LatencyMs = latencyMs,
        RawResponse = string.Empty,
        Validation = AiValidationResult.Ok(),
        Confidence = confidence,
        Simulated = true
    };
}
