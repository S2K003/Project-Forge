using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Engineering;
using ForgeOps.Contracts.Journey;

namespace ForgeOps.Demo;

/// <summary>
/// The canonical CustomerHub script (ProjectForge.md §30, §31). This is the single
/// source of truth for Demo Mode — compiled into the WASM bundle so the walkthrough
/// works with no backend, no AI Bridge and no network (§9A.2).
///
/// The same definition is served by the API (<c>GET /api/demo/journey</c>) so Live Mode
/// can replay the identical story against a seeded project.
/// </summary>
public static class CustomerHubJourney
{
    public const string ProjectKey = "customerhub";

    public static JourneyDefinition Build()
    {
        return new JourneyDefinition
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
                ArchitectureAnalysis(),
                PullRequest(),
                QualityGates(),
                AiReview(),
                HumanDecision(),
                Merge(),
                Telemetry(),
                Health()
            ]
        };
    }

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
            Specification = new SpecificationDraft
            {
                Title = "Loyalty points awarded on confirmed purchase",
                Summary =
                    "When a customer's purchase is confirmed as paid, credit loyalty points equal to the order net "
                    + "value (1 point per whole currency unit). Crediting must be exactly-once and reversible on refund.",
                AcceptanceCriteria =
                [
                    new AcceptanceCriterion { Id = "AC-1", Statement = "Given a paid order, When the payment is confirmed, Then loyalty points equal to floor(order net total) are credited to the customer." },
                    new AcceptanceCriterion { Id = "AC-2", Statement = "Given the same payment-confirmed event is delivered more than once, When it is processed, Then points are credited at most once for that order." },
                    new AcceptanceCriterion { Id = "AC-3", Statement = "Given an order that awarded points is fully refunded, When the refund settles, Then the previously awarded points are reversed." },
                    new AcceptanceCriterion { Id = "AC-4", Statement = "Given a purchase below the minimum qualifying value, When payment is confirmed, Then no points are credited." },
                    new AcceptanceCriterion { Id = "AC-5", Statement = "Given points are credited or reversed, When the operation completes, Then an audit entry records the order, amount and reason." }
                ],
                OutOfScope =
                [
                    "Tiered earn rates or promotional multipliers.",
                    "Point expiry."
                ],
                OpenQuestions =
                [
                    "Do partial refunds reverse points proportionally, or only full refunds?",
                    "Is the minimum qualifying value configurable per market?"
                ]
            },
            AiInteraction = RecordedInteraction(
                promptVersion: "spec.v1",
                latencyMs: 4120,
                confidence: 0.71,
                raw: "{\"title\":\"Loyalty points awarded on confirmed purchase\", ...}"),
            Notes = ["AI output passed deterministic schema validation (§9.2): 5 acceptance criteria, all testable."]
        }
    };

    private static JourneyStep HumanReview() => new()
    {
        Order = 3,
        Kind = JourneyStepKind.HumanReview,
        Title = "Human review",
        Caption = "An engineer accepts the specification before any work proceeds.",
        Payload = new StepPayload
        {
            AiInteraction = RecordedInteraction(
                promptVersion: "spec.v1",
                latencyMs: 4120,
                confidence: 0.71,
                raw: string.Empty) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.AcceptedWithModification,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-28T09:14:00Z"),
                    Reason = "Accepted. AC-4 minimum value fixed at 1.00 for the demo market; open questions logged as follow-ups."
                }
            },
            Notes =
            [
                "The specification is now human-approved. AI did not decide — a person did (§2.1).",
                "Approval is recorded in the audit trail (§20)."
            ]
        }
    };

    private static JourneyStep ArchitectureAnalysis() => new()
    {
        Order = 4,
        Kind = JourneyStepKind.ArchitectureAnalysis,
        Title = "Architecture analysis",
        Caption = "Deterministic rules scan the implementation branch.",
        SimulatedThinkingMs = 1200,
        Payload = new StepPayload
        {
            ArchitectureFindings =
            [
                new ArchitectureFinding
                {
                    RuleId = "ARCH-001",
                    Name = "Application must not reference Infrastructure",
                    Severity = FindingSeverity.High,
                    Description =
                        "LoyaltyPointsService in the Application layer takes a direct dependency on an EF Core "
                        + "DbContext from Infrastructure, bypassing the application's own abstractions.",
                    Evidence =
                    [
                        "ForgeOps.Application/Loyalty/LoyaltyPointsService.cs:14  →  ForgeOps.Infrastructure/Persistence/CustomerHubDbContext.cs",
                        "ForgeOps.Application/Loyalty/LoyaltyPointsService.cs:41  →  ForgeOps.Infrastructure.Persistence namespace"
                    ],
                    RemediationGuidance = "Depend on an ILoyaltyLedger abstraction defined in Application; implement it in Infrastructure."
                }
            ],
            Notes =
            [
                "42 architecture rules evaluated — 41 passed, 1 failed.",
                "This finding is deterministic static analysis, not an AI opinion (§2.2)."
            ]
        }
    };

    private static JourneyStep PullRequest() => new()
    {
        Order = 5,
        Kind = JourneyStepKind.PullRequest,
        Title = "Pull request",
        Caption = "Implementation opened for review.",
        Payload = new StepPayload
        {
            PullRequest = new PullRequestSummary
            {
                Number = 142,
                Title = "Implement customer loyalty points",
                Branch = "feature/loyalty-points",
                FilesChanged = 17,
                Additions = 612,
                Deletions = 48,
                ChangedFiles =
                [
                    "src/CustomerHub.Application/Loyalty/LoyaltyPointsService.cs",
                    "src/CustomerHub.Application/Loyalty/AwardPointsCommand.cs",
                    "src/CustomerHub.Domain/Loyalty/LoyaltyAccount.cs",
                    "src/CustomerHub.Domain/Loyalty/PointsLedgerEntry.cs",
                    "src/CustomerHub.Infrastructure/Persistence/CustomerHubDbContext.cs",
                    "src/CustomerHub.Api/Webhooks/PaymentWebhookHandler.cs",
                    "src/CustomerHub.Api/Endpoints/LoyaltyEndpoints.cs",
                    "tests/CustomerHub.UnitTests/Loyalty/LoyaltyPointsServiceTests.cs",
                    "… 9 more"
                ]
            }
        }
    };

    private static JourneyStep QualityGates() => new()
    {
        Order = 6,
        Kind = JourneyStepKind.QualityGates,
        Title = "Quality gates",
        Caption = "Deterministic pipeline runs. AI cannot override these (§13).",
        SimulatedThinkingMs = 2600,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Build", GateStatus.Passed, 21, evidence: ["dotnet build -c Release: 0 errors, 0 warnings"]),
                Gate("Format", GateStatus.Passed, 3, evidence: ["dotnet format --verify-no-changes: clean"]),
                Gate("Static analysis", GateStatus.Passed, 12, evidence: ["Roslyn analyzers: 0 new violations"]),
                Gate("Unit tests", GateStatus.Passed, 34, evidence: ["128 passed / 128"]),
                Gate("Integration tests", GateStatus.Passed, 71, warnings: ["No integration test covers duplicate webhook delivery"]),
                Gate("Architecture", GateStatus.Failed, 8, blocking: true,
                    errors: ["ARCH-001 — Application → Infrastructure dependency (LoyaltyPointsService.cs:14)"]),
                Gate("Security", GateStatus.Failed, 9, blocking: true,
                    errors: ["SEC-002 — POST /api/loyalty/adjust has no authorization attribute"]),
                Gate("Coverage", GateStatus.Passed, 5, warnings: ["Line coverage 68% is below the 75% target"]),
                Gate("AI review", GateStatus.Pending, 0)
            ],
            Notes =
            [
                "Two blocking deterministic failures. The final quality state cannot pass until these clear (§13).",
                "Coverage is a warning, not a block — configured that way in the rule set (§12)."
            ]
        }
    };

    private static JourneyStep AiReview() => new()
    {
        Order = 7,
        Kind = JourneyStepKind.AiReview,
        Title = "AI review",
        Caption = "qwen3:8b reviews the diff and explains the deterministic findings.",
        SimulatedThinkingMs = 2400,
        Payload = new StepPayload
        {
            ReviewFindings =
            [
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Critical,
                    Classification = AiClassification.Likely,
                    Finding = "Payment-confirmed webhook can award loyalty points more than once.",
                    Evidence = "src/CustomerHub.Api/Webhooks/PaymentWebhookHandler.cs:47",
                    Recommendation =
                        "Make crediting idempotent: persist a unique key per (orderId, event type) and no-op on replay. "
                        + "This directly supports acceptance criterion AC-2.",
                    Confidence = 0.9
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.High,
                    Classification = AiClassification.Confirmed,
                    Finding = "Manual points adjustment endpoint is unauthenticated.",
                    Evidence = "src/CustomerHub.Api/Endpoints/LoyaltyEndpoints.cs:33",
                    Recommendation = "Require the EngineeringManager or Administrator role. Matches deterministic finding SEC-002.",
                    Confidence = 0.97
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Medium,
                    Classification = AiClassification.Possible,
                    Finding = "No integration test exercises duplicate webhook delivery.",
                    Evidence = "tests/CustomerHub.IntegrationTests/ (no matching test)",
                    Recommendation = "Add a test that posts the same payment-confirmed event twice and asserts a single ledger entry.",
                    Confidence = 0.64
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Low,
                    Classification = AiClassification.Suggestion,
                    Finding = "Points crediting emits no telemetry.",
                    Evidence = "src/CustomerHub.Application/Loyalty/LoyaltyPointsService.cs",
                    Recommendation = "Emit a counter (points awarded / reversed) so the behaviour is observable in production (OBS-001).",
                    Confidence = 0.5
                }
            ],
            AiInteraction = RecordedInteraction(
                promptVersion: "review.v1",
                latencyMs: 8730,
                confidence: 0.78,
                raw: "{\"findings\":[ ... 4 findings ... ]}"),
            Notes =
            [
                "Every finding carries a classification: Confirmed / Likely / Possible / Suggestion (§18).",
                "The CRITICAL finding corroborates a real gap in AC-2 coverage — but it is still advisory until a human acts."
            ]
        }
    };

    private static JourneyStep HumanDecision() => new()
    {
        Order = 8,
        Kind = JourneyStepKind.HumanDecision,
        Title = "Human decision",
        Caption = "The engineer decides what to do with each recommendation.",
        Payload = new StepPayload
        {
            AiInteraction = RecordedInteraction(
                promptVersion: "review.v1",
                latencyMs: 8730,
                confidence: 0.78,
                raw: string.Empty) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.Accepted,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-28T09:41:00Z"),
                    Reason =
                        "Accepting CRITICAL (idempotency) and HIGH (authorization) as blockers. "
                        + "Integration test accepted. Telemetry suggestion accepted as a follow-up, not a blocker."
                }
            },
            Notes =
            [
                "AI recommended → deterministic evidence confirmed → human judged → decision recorded (§52).",
                "The decision and its reason are appended to the audit trail (§20)."
            ]
        }
    };

    private static JourneyStep Merge() => new()
    {
        Order = 9,
        Kind = JourneyStepKind.Merge,
        Title = "Fix & merge",
        Caption = "Developer fixes the blockers; gates re-run green.",
        SimulatedThinkingMs = 2000,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Build", GateStatus.Passed, 20),
                Gate("Format", GateStatus.Passed, 3),
                Gate("Static analysis", GateStatus.Passed, 12),
                Gate("Unit tests", GateStatus.Passed, 37, evidence: ["134 passed / 134"]),
                Gate("Integration tests", GateStatus.Passed, 78, evidence: ["Duplicate-webhook test added and passing"]),
                Gate("Architecture", GateStatus.Passed, 8, evidence: ["ARCH-001 cleared — LoyaltyPointsService depends on ILoyaltyLedger"]),
                Gate("Security", GateStatus.Passed, 9, evidence: ["SEC-002 cleared — adjust endpoint requires EngineeringManager"]),
                Gate("Coverage", GateStatus.Passed, 5, evidence: ["Line coverage 79%"]),
                Gate("AI review", GateStatus.Passed, 9, evidence: ["No unresolved CRITICAL or HIGH findings"])
            ],
            PullRequest = new PullRequestSummary
            {
                Number = 142,
                Title = "Implement customer loyalty points",
                Branch = "feature/loyalty-points",
                FilesChanged = 21,
                Additions = 704,
                Deletions = 51
            },
            Notes = ["PR #142 merged to main at 10:02. All blocking gates green."]
        }
    };

    private static JourneyStep Telemetry() => new()
    {
        Order = 10,
        Kind = JourneyStepKind.Telemetry,
        Title = "Telemetry",
        Caption = "The change is observable in production.",
        Payload = new StepPayload
        {
            Telemetry =
            [
                new TelemetrySample { Metric = "http.server.request.duration p95", Value = "142 ms", Detail = "loyalty endpoints" },
                new TelemetrySample { Metric = "db.client.operation.duration p95", Value = "11 ms" },
                new TelemetrySample { Metric = "forgeops_ai_request_duration p95", Value = "8.7 s", Detail = "qwen3:8b via AI Bridge" },
                new TelemetrySample { Metric = "forgeops_ai_bridge_up", Value = "1", Detail = "bridge reachable" },
                new TelemetrySample { Metric = "forgeops_loyalty_points_awarded_total", Value = "1,284", Detail = "since merge" },
                new TelemetrySample { Metric = "forgeops_background_jobs_total", Value = "37", Detail = "0 failed" }
            ],
            Notes = ["The telemetry suggestion from AI review is now live — points awarded is a real counter."]
        }
    };

    private static JourneyStep Health() => new()
    {
        Order = 11,
        Kind = JourneyStepKind.EngineeringHealth,
        Title = "Engineering health",
        Caption = "Deterministic score, with a full \"Why?\" (§14, §15).",
        Payload = new StepPayload
        {
            Health = new EngineeringHealth
            {
                Score = 87,
                Components =
                [
                    new HealthComponent { Name = "Tests", Weight = 0.25, Score = 88 },
                    new HealthComponent { Name = "Architecture", Weight = 0.20, Score = 98 },
                    new HealthComponent { Name = "Security", Weight = 0.20, Score = 92 },
                    new HealthComponent { Name = "Code quality", Weight = 0.15, Score = 80 },
                    new HealthComponent { Name = "Observability", Weight = 0.10, Score = 70 },
                    new HealthComponent { Name = "Delivery", Weight = 0.05, Score = 75 },
                    new HealthComponent { Name = "Documentation", Weight = 0.05, Score = 65 }
                ],
                Reasons =
                [
                    new HealthReason { Kind = ReasonKind.Pass, Text = "42 architecture rules passed" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "No circular dependencies" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "Security checks passed (18 / 18)" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "134 / 134 tests passing" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "Observability coverage below target on 2 modules" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "Documentation for loyalty module is a stub" }
                ]
            }
        }
    };

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

    private static AiInteractionRecord RecordedInteraction(
        string promptVersion,
        long latencyMs,
        double confidence,
        string raw) => new()
    {
        Id = $"demo-{promptVersion}",
        Provider = "OllamaBridge (recorded)",
        Model = "qwen3:8b",
        ModelVersion = "qwen3:8b@2025-05",
        PromptVersion = promptVersion,
        RequestedAt = DateTimeOffset.Parse("2026-08-28T09:12:00Z"),
        LatencyMs = latencyMs,
        RawResponse = raw,
        Validation = AiValidationResult.Ok(),
        Confidence = confidence,
        Simulated = true
    };
}
