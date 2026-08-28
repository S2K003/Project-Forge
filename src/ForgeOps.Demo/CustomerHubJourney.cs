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
/// Live Mode performs the identical steps for real: qwen2.5-coder:14b drafts the spec and the
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
            Refine(),
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
        Caption = "qwen2.5-coder:14b drafts a testable specification. Advisory only.",
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
        Caption = "qwen2.5-coder:14b writes the implementation and its own tests from the approved spec.",
        SimulatedThinkingMs = 3200,
        Payload = new StepPayload
        {
            Implementation = new GeneratedImplementation
            {
                Summary = "In-memory LoyaltyService: floor(net total) points on paid orders ≥ 1.00, reversible on refund.",
                Rationale =
                    "Points are added to a per-customer balance dictionary and every credit is logged "
                    + "to the ledger. Refunds look up the ledger entry and post a compensating entry. "
                    + "All state is in-memory; no external dependencies.",
                RepairAttempts = 1,
                Origin = ImplementationOrigin.ModelWithRepairs,
                Files =
                [
                    new GeneratedFile { Path = "LoyaltyService.cs", Role = GeneratedFileRole.Implementation, Content = ImplSourceWeak },
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
        Caption = "qwen2.5-coder:14b reviews the generated diff.",
        SimulatedThinkingMs = 2400,
        Payload = new StepPayload
        {
            ReviewFindings =
            [
                new AiReviewFinding
                {
                    Severity = FindingSeverity.High,
                    Classification = AiClassification.Likely,
                    Finding = "OnPaymentConfirmed is not idempotent — a redelivered payment-confirmed event credits points again.",
                    Evidence = "LoyaltyService.cs:14",
                    Recommendation = "Track awarded order ids and no-op on a repeat. This is acceptance criterion AC-2.",
                    Confidence = 0.83
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Low,
                    Classification = AiClassification.Confirmed,
                    Finding = "Refund reversal reads CustomerId from the ledger, which is populated on credit.",
                    Evidence = "LoyaltyService.cs:26",
                    Recommendation = "Fine, but storing the award as a record would be clearer.",
                    Confidence = 0.9
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
                    Reason = "Audit is clean. The AI flagged a possible idempotency gap — run the acceptance suite and let the evidence decide."
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
                Passed = 3,
                Failed = 0,
                Skipped = 0,
                DurationMs = 39,
                RunnerDetail = "3 test(s) in 39 ms — the model wrote no test for duplicate delivery",
                Results =
                [
                    Pass("LoyaltyServiceTests.Awards_points_for_paid_order"),
                    Pass("LoyaltyServiceTests.No_points_for_unpaid_order"),
                    Pass("LoyaltyServiceTests.Refund_reverses_points")
                ]
            },
            CanonicalTestRun = new TestRunResult
            {
                Suite = TestSuiteKind.Canonical,
                Executed = true,
                Passed = 5,
                Failed = 1,
                Skipped = 0,
                DurationMs = 40,
                RunnerDetail = "6 test(s) in 40 ms — 1 failed (sandboxed child process)",
                Results =
                [
                    Pass("LoyaltyAcceptance.Awards_floor_of_net_total_on_paid_order", "AC-1"),
                    new TestResult
                    {
                        Name = "LoyaltyAcceptance.Duplicate_payment_event_credits_points_at_most_once",
                        Outcome = TestOutcome.Failed,
                        Message = "ForgeAssertException: Expected <42>, got <84>.",
                        DurationMs = 4,
                        Criteria = ["AC-2"]
                    },
                    Pass("LoyaltyAcceptance.Full_refund_reverses_awarded_points", "AC-3"),
                    Pass("LoyaltyAcceptance.Below_minimum_qualifying_value_awards_nothing", "AC-4"),
                    Pass("LoyaltyAcceptance.Crediting_writes_an_audit_entry_for_the_order", "AC-5"),
                    Pass("LoyaltyAcceptance.Unpaid_order_awards_nothing", "AC-1")
                ]
            },
            Scenario = new ScenarioRun
            {
                Executed = true,
                Detail = "6 step(s) in 12 ms (sandboxed)",
                Steps =
                [
                    Step("OnPaymentConfirmed( ORD-1001 · alice · $42.90 · paid )", "alice balance = 42 points"),
                    Step("OnPaymentConfirmed( ORD-1001 ) again  — duplicate webhook delivery", "alice balance = 84 points  ← double-credited"),
                    Step("OnPaymentConfirmed( ORD-1002 · bob · $0.80 · paid )  — below the $1.00 minimum", "bob balance = 0 points"),
                    Step("OnPaymentConfirmed( ORD-1003 · carol · $120.00 · NOT paid )", "carol balance = 0 points"),
                    Step("OnOrderRefunded( ORD-1001 )  — alice's order is fully refunded", "alice balance = 42 points  ← refund reversed only one credit"),
                    Step("Final loyalty ledger",
                        "\n  ORD-1001   alice  +42   purchase\n  ORD-1001   alice  +42   purchase\n  ORD-1001   alice  -42   refund")
                ]
            },
            Acceptance =
            [
                Acc("AC-1", "A paid order credits floor(net total) points to the customer.", AcceptanceStatus.Satisfied,
                    "LoyaltyAcceptance.Awards_floor_of_net_total_on_paid_order", "LoyaltyAcceptance.Unpaid_order_awards_nothing"),
                Acc("AC-2", "A redelivered payment-confirmed event credits points at most once.", AcceptanceStatus.NotSatisfied,
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
                "The code runs — but it is wrong. AC-2 fails: a redelivered event double-credits (42 → 84).",
                "The model's own tests all passed; ForgeOps' canonical suite caught the gap (§31, §51).",
                "This does not merge. The AI must refine the code."
            ]
        }
    };

    private static JourneyStep Refine() => new()
    {
        Order = 10,
        Kind = JourneyStepKind.Refine,
        Title = "AI refinement",
        Caption = "The AI regenerates the code to close AC-2, then ForgeOps re-audits and re-runs it.",
        SimulatedThinkingMs = 3000,
        Payload = new StepPayload
        {
            Implementation = new GeneratedImplementation
            {
                Summary = "Made point crediting idempotent per order and added a duplicate-delivery test.",
                Rationale =
                    "Awarded order ids are now tracked in a dictionary; a repeat OnPaymentConfirmed for the "
                    + "same order is a no-op. Refunds reverse the single recorded award. All other behaviour is unchanged.",
                Kind = ImplementationKind.CSharpLogic,
                Origin = ImplementationOrigin.ModelWithRepairs,
                RepairAttempts = 0,
                Files =
                [
                    new GeneratedFile { Path = "LoyaltyService.cs", Role = GeneratedFileRole.Implementation, Content = ImplSource },
                    new GeneratedFile { Path = "LoyaltyServiceTests.cs", Role = GeneratedFileRole.Test, Content = AiTestSource }
                ]
            },
            Audit = new AuditReport
            {
                Compiled = true,
                RepairAttempts = 0,
                Diagnostics = [],
                BannedApis = [],
                ArchitecturePassed = true,
                ArchitectureNotes = ["Implements ILoyaltyService; sealed; no public mutable state; namespace CustomerHub.Loyalty."],
                Verdict = AuditVerdict.Passed
            },
            AiTestRun = new TestRunResult
            {
                Suite = TestSuiteKind.AiGenerated,
                Executed = true,
                Passed = 4,
                Failed = 0,
                DurationMs = 41,
                RunnerDetail = "4 test(s) in 41 ms (sandboxed)",
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
                DurationMs = 38,
                RunnerDetail = "6 test(s) in 38 ms — all passed (sandboxed)",
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
            Scenario = new ScenarioRun
            {
                Executed = true,
                Detail = "6 step(s) in 11 ms (sandboxed)",
                Steps =
                [
                    Step("OnPaymentConfirmed( ORD-1001 · alice · $42.90 · paid )", "alice balance = 42 points"),
                    Step("OnPaymentConfirmed( ORD-1001 ) again  — duplicate webhook delivery", "alice balance = 42 points  (unchanged — idempotent)"),
                    Step("OnPaymentConfirmed( ORD-1002 · bob · $0.80 · paid )  — below the $1.00 minimum", "bob balance = 0 points"),
                    Step("OnPaymentConfirmed( ORD-1003 · carol · $120.00 · NOT paid )", "carol balance = 0 points"),
                    Step("OnOrderRefunded( ORD-1001 )  — alice's order is fully refunded", "alice balance = 0 points"),
                    Step("Final loyalty ledger", "\n  ORD-1001   alice  +42   purchase\n  ORD-1001   alice  -42   refund")
                ]
            },
            Acceptance =
            [
                Acc("AC-1", "A paid order credits floor(net total) points to the customer.", AcceptanceStatus.Satisfied, "LoyaltyAcceptance.Awards_floor_of_net_total_on_paid_order"),
                Acc("AC-2", "A redelivered payment-confirmed event credits points at most once.", AcceptanceStatus.Satisfied, "LoyaltyAcceptance.Duplicate_payment_event_credits_points_at_most_once"),
                Acc("AC-3", "A full refund reverses the points that were awarded.", AcceptanceStatus.Satisfied, "LoyaltyAcceptance.Full_refund_reverses_awarded_points"),
                Acc("AC-4", "A purchase below the minimum qualifying value credits nothing.", AcceptanceStatus.Satisfied, "LoyaltyAcceptance.Below_minimum_qualifying_value_awards_nothing"),
                Acc("AC-5", "Crediting writes an audit entry recording the order and reason.", AcceptanceStatus.Satisfied, "LoyaltyAcceptance.Crediting_writes_an_audit_entry_for_the_order")
            ],
            Refinement = new RefinementRound
            {
                Round = 1,
                AddressedCriteria = ["AC-2"],
                Summary = "Made point crediting idempotent per order and added a duplicate-delivery test.",
                AllCriteriaMet = true
            },
            AiInteraction = Recorded("impl.refine.v1", 12400, 0.79) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.Accepted,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-28T09:52:00Z"),
                    Reason = "AC-2 now passes; the other criteria still pass. Accepting the refinement."
                }
            },
            Notes =
            [
                "Round 1 — the AI regenerated addressing AC-2. Canonical suite: 6 / 6. All 5 acceptance criteria satisfied.",
                "The updated LoyaltyService.cs and its execution trace are shown above."
            ]
        }
    };

    private static JourneyStep Merge() => new()
    {
        Order = 11,
        Kind = JourneyStepKind.Merge,
        Title = "Merge",
        Caption = "The refined code is green and a human approved it. PR #142 merges.",
        SimulatedThinkingMs = 1200,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Compile (Roslyn)", GateStatus.Passed, 2),
                Gate("Banned-API scan", GateStatus.Passed, 1),
                Gate("Architecture", GateStatus.Passed, 2),
                Gate("AI-authored tests", GateStatus.Passed, 4, evidence: ["4 / 4 (after refinement)"]),
                Gate("Acceptance (canonical)", GateStatus.Passed, 4, evidence: ["6 / 6 — AC-1..AC-5 satisfied after 1 refinement round"])
            ],
            PullRequest = new PullRequestSummary
            {
                Number = 142,
                Title = "Implement customer loyalty points",
                Branch = "feature/loyalty-points",
                FilesChanged = 2,
                Additions = 104,
                Deletions = 6
            },
            Notes = ["PR #142 merged to main at 10:06. Every blocking gate green; acceptance proven by execution after the AI closed AC-2."]
        }
    };

    private static JourneyStep Telemetry() => new()
    {
        Order = 12,
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
        Order = 13,
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
                    new HealthReason { Kind = ReasonKind.Pass, Text = "5 / 5 acceptance criteria satisfied by executed tests (after 1 refinement round)" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "Deterministic audit clean — 0 banned APIs, architecture rules pass" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "10 / 10 tests passing in the sandbox (AI + canonical)" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "The first generated implementation shipped an AC-2 defect that only the canonical suite caught" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "Generated code emits no telemetry yet (OBS-001 suggestion open)" }
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

    // The model's first attempt — awards points but is NOT idempotent (fails canonical AC-2).
    private const string ImplSourceWeak =
        """
        namespace CustomerHub.Loyalty;

        using System;
        using System.Collections.Generic;
        using System.Linq;

        public sealed class LoyaltyService : ILoyaltyService
        {
            private const decimal MinimumQualifyingValue = 1.00m;

            private readonly Dictionary<string, int> _balances = new();
            private readonly List<LedgerEntry> _ledger = new();

            public IReadOnlyList<LedgerEntry> Ledger => _ledger;

            public void OnPaymentConfirmed(Order order)
            {
                ArgumentNullException.ThrowIfNull(order);

                if (!order.IsPaid || order.NetTotal < MinimumQualifyingValue)
                    return;

                var points = (int)Math.Floor(order.NetTotal);
                Adjust(order.CustomerId, points);
                _ledger.Add(new LedgerEntry(order.OrderId, order.CustomerId, points, "purchase", DateTimeOffset.UtcNow));
            }

            public void OnOrderRefunded(string orderId)
            {
                var entry = _ledger.FirstOrDefault(e => e.OrderId == orderId && e.Reason == "purchase");
                if (entry is null)
                    return;

                Adjust(entry.CustomerId, -entry.Points);
                _ledger.Add(new LedgerEntry(orderId, entry.CustomerId, -entry.Points, "refund", DateTimeOffset.UtcNow));
            }

            public int BalanceFor(string customerId) =>
                _balances.TryGetValue(customerId, out var balance) ? balance : 0;

            private void Adjust(string customerId, int delta) =>
                _balances[customerId] = BalanceFor(customerId) + delta;
        }
        """;

    // The refined implementation — idempotent per order (all criteria pass).
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

    private static ScenarioStep Step(string action, string output) => new() { Action = action, Output = output };

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
        Model = "qwen2.5-coder:14b",
        ModelVersion = "qwen2.5-coder:14b",
        PromptVersion = promptVersion,
        RequestedAt = DateTimeOffset.Parse("2026-08-28T09:12:00Z"),
        LatencyMs = latencyMs,
        RawResponse = string.Empty,
        Validation = AiValidationResult.Ok(),
        Confidence = confidence,
        Simulated = true
    };
}
