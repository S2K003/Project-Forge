using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Engineering;
using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;

namespace ForgeOps.Demo;

/// <summary>
/// A UI-shaped variant of the journey (ProjectForge.md §4): the requirement produces a
/// self-contained web component that ForgeOps renders in a locked-down sandboxed iframe.
/// Demo Mode replays a recording; Live Mode builds it for real with the local model.
/// </summary>
public static class LoyaltyCardJourney
{
    public const string ProjectKey = "loyalty-card";

    public static JourneyDefinition Build() => new()
    {
        ProjectKey = ProjectKey,
        ProjectName = "CustomerHub",
        RequirementText = "Show a customer's loyalty status as a compact card: points balance, tier, and the last three activity entries.",
        Kind = ImplementationKind.WebComponent,
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
            Run(),
            Refine(),
            Merge(),
            Telemetry(),
            Health()
        ]
    };

    private static JourneyStep SignIn() => new()
    {
        Order = 0,
        Kind = JourneyStepKind.SignIn,
        Title = "Sign in",
        Caption = "Demo Mode entry point — not real authentication.",
        State = JourneyStepState.Ready,
        Payload = new StepPayload { Notes = ["Scripted entry into the loyalty-card walkthrough."] }
    };

    private static JourneyStep Requirement() => new()
    {
        Order = 1,
        Kind = JourneyStepKind.Requirement,
        Title = "Create requirement",
        Caption = "A UI ask enters the pipeline — ForgeOps classifies it as a web component.",
        Payload = new StepPayload
        {
            Notes =
            [
                "Requirement: \"Show a customer's loyalty status as a compact card…\"",
                "RequirementClassifier → WebComponent (UI signals: card, show, tier, activity)."
            ]
        }
    };

    private static JourneyStep Specification() => new()
    {
        Order = 2,
        Kind = JourneyStepKind.Specification,
        Title = "AI specification",
        Caption = "qwen2.5-coder:14b drafts acceptance criteria for the card. Advisory only.",
        SimulatedThinkingMs = 1700,
        Payload = new StepPayload { Specification = Spec, AiInteraction = Recorded("spec.v1", 3600, 0.74) }
    };

    private static JourneyStep HumanReview() => new()
    {
        Order = 3,
        Kind = JourneyStepKind.HumanReview,
        Title = "Human review",
        Caption = "An engineer approves the specification before the component is built.",
        Payload = new StepPayload
        {
            Specification = Spec,
            AiInteraction = Recorded("spec.v1", 3600, 0.74) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.Accepted,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-28T10:02:00Z"),
                    Reason = "Approved. Dark theme, compact, no external assets."
                }
            }
        }
    };

    private static JourneyStep Implementation() => new()
    {
        Order = 4,
        Kind = JourneyStepKind.Implementation,
        Title = "AI implementation",
        Caption = "qwen2.5-coder:14b builds a self-contained HTML component from the approved spec.",
        SimulatedThinkingMs = 3000,
        Payload = new StepPayload
        {
            Implementation = new GeneratedImplementation
            {
                Summary = "A compact dark loyalty card: points balance, tier badge and the last three activity rows.",
                Rationale = "Single HTML document, inline CSS/SVG only, hard-coded sample data. Flexbox layout, system font stack, one amber accent for the tier badge.",
                Kind = ImplementationKind.WebComponent,
                Origin = ImplementationOrigin.Model,
                RepairAttempts = 0,
                Files = [new GeneratedFile { Path = "index.html", Language = "html", Content = CardHtmlDraft }],
                UiChecks = Checks,
                ReviewNotes =
                [
                    "The three activity rows should be legible and right-aligned on points.",
                    "The spec asks for progress to the next tier — check the card actually shows it."
                ]
            },
            AiInteraction = Recorded("webcomp.v3", 15200, 0.7)
        }
    };

    private static JourneyStep Audit() => new()
    {
        Order = 5,
        Kind = JourneyStepKind.Audit,
        Title = "Deterministic audit",
        Caption = "HtmlAuditor: self-contained check + banned-pattern scan. No model involved.",
        SimulatedThinkingMs = 900,
        Payload = new StepPayload
        {
            Audit = new AuditReport
            {
                Kind = ImplementationKind.WebComponent,
                Compiled = true,
                RepairAttempts = 0,
                Diagnostics = [],
                BannedApis = [],
                ArchitecturePassed = true,
                ArchitectureNotes = ["Self-contained document; inline styles/scripts only; no external resources."],
                Verdict = AuditVerdict.Passed
            },
            Notes =
            [
                "0 uses of fetch / XHR / WebSocket / import() / eval / storage / parent access / external src.",
                "The audit permits the component to be rendered in the sandboxed iframe (§10)."
            ]
        }
    };

    private static JourneyStep QualityGates() => new()
    {
        Order = 6,
        Kind = JourneyStepKind.QualityGates,
        Title = "Quality gates",
        Caption = "Deterministic checks over the generated markup.",
        SimulatedThinkingMs = 1400,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Parse", GateStatus.Passed, 1, ["Valid HTML document"]),
                Gate("Self-contained", GateStatus.Passed, 1, ["Inline CSS/SVG only; no <link>, no <script src>"]),
                Gate("Banned-pattern scan", GateStatus.Passed, 1, ["0 findings"]),
                Gate("Behavioural checks", GateStatus.Pending, 0)
            ],
            Notes = ["The behavioural checks run in the browser when the component is rendered at Run & verify."]
        }
    };

    private static JourneyStep AiReview() => new()
    {
        Order = 7,
        Kind = JourneyStepKind.AiReview,
        Title = "AI review",
        Caption = "qwen2.5-coder:14b reviews the generated markup.",
        SimulatedThinkingMs = 2000,
        Payload = new StepPayload
        {
            ReviewFindings =
            [
                new AiReviewFinding
                {
                    Severity = FindingSeverity.High,
                    Classification = AiClassification.Likely,
                    Finding = "The card does not render progress to the next tier — there is no progress element.",
                    Evidence = "index.html — no <progress> / [role=progressbar] / .bar; the \"620 / 1,000 to Platinum\" line is missing too.",
                    Recommendation = "Add the tier progress bar and label. This is acceptance criterion AC-2.",
                    Confidence = 0.8
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Low,
                    Classification = AiClassification.Confirmed,
                    Finding = "Colour contrast on the muted activity timestamps is around 4.3:1.",
                    Evidence = "index.html — .activity time { color: #7b8494 }",
                    Recommendation = "Lighten to at least #9aa4b2 for WCAG AA on small text (§26).",
                    Confidence = 0.88
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Low,
                    Classification = AiClassification.Suggestion,
                    Finding = "The card has a fixed width of 360px.",
                    Evidence = "index.html — .card { width: 360px }",
                    Recommendation = "Use max-width so it adapts on narrow screens (§27).",
                    Confidence = 0.6
                }
            ],
            AiInteraction = Recorded("codereview.v1", 4800, 0.71)
        }
    };

    private static JourneyStep HumanDecision() => new()
    {
        Order = 8,
        Kind = JourneyStepKind.HumanDecision,
        Title = "Human decision",
        Caption = "The engineer approves rendering the component.",
        Payload = new StepPayload
        {
            AiInteraction = Recorded("codereview.v1", 4800, 0.71) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.Accepted,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-28T10:26:00Z"),
                    Reason = "Audit is clean. The AI flagged a missing tier-progress bar (AC-2) — render it, see it with our own eyes, then have the AI close the gap."
                }
            },
            Notes = ["AI built it → deterministic audit passed → human approved → now it renders (§52)."]
        }
    };

    private static JourneyStep Run() => new()
    {
        Order = 9,
        Kind = JourneyStepKind.AcceptanceRun,
        Title = "Run & verify",
        Caption = "The generated component, rendered in a locked-down sandboxed iframe.",
        SimulatedThinkingMs = 1200,
        Payload = new StepPayload
        {
            Ui = new UiPreview
            {
                DocumentHtml = CardHtmlDraft,
                Checks = Checks,
                ReviewNotes =
                [
                    "AC-2 asks for progress to the next tier — this first attempt does not render it.",
                    "The muted activity timestamps look faint — check the contrast."
                ],
                Rendered = false
            },
            Acceptance =
            [
                Acc("AC-1", "The card shows the current points balance prominently."),
                Acc("AC-2", "The card shows the loyalty tier and progress to the next tier."),
                Acc("AC-3", "The card lists the three most recent activity entries with dates and amounts."),
                Acc("AC-4", "The component is self-contained and renders with no external requests.")
            ],
            Notes =
            [
                "It renders — but the tier progress bar (AC-2) is missing, and the self-check for it fails (3 / 4).",
                "This does not merge. The AI must refine the component."
            ]
        }
    };

    private static JourneyStep Refine() => new()
    {
        Order = 10,
        Kind = JourneyStepKind.Refine,
        Title = "AI refinement",
        Caption = "The AI regenerates the component to close AC-2, then ForgeOps re-audits and re-renders it.",
        SimulatedThinkingMs = 3000,
        Payload = new StepPayload
        {
            Implementation = new GeneratedImplementation
            {
                Summary = "Added the tier progress bar with the \"620 / 1,000 to Platinum\" label and lightened the muted timestamps for WCAG AA.",
                Rationale = "Same single self-contained document. New .bar element driven by aria-valuenow; .activity time colour raised to #9aa4b2. No other markup changed.",
                Kind = ImplementationKind.WebComponent,
                Origin = ImplementationOrigin.ModelWithRepairs,
                RepairAttempts = 0,
                Files = [new GeneratedFile { Path = "index.html", Language = "html", Content = CardHtml }],
                UiChecks = Checks,
                ReviewNotes =
                [
                    "The tier progress bar should visually match \"620 / 1,000 to Platinum\".",
                    "The three activity rows should be legible and right-aligned on points."
                ]
            },
            Audit = new AuditReport
            {
                Kind = ImplementationKind.WebComponent,
                Compiled = true,
                RepairAttempts = 0,
                Diagnostics = [],
                BannedApis = [],
                ArchitecturePassed = true,
                ArchitectureNotes = ["Self-contained document; inline styles/scripts only; no external resources."],
                Verdict = AuditVerdict.Passed
            },
            Ui = new UiPreview
            {
                DocumentHtml = CardHtml,
                Checks = Checks,
                ReviewNotes =
                [
                    "The tier progress bar should visually match \"620 / 1,000 to Platinum\".",
                    "The three activity rows should be legible and right-aligned on points."
                ],
                Rendered = false
            },
            Acceptance =
            [
                Acc("AC-1", "The card shows the current points balance prominently."),
                Acc("AC-2", "The card shows the loyalty tier and progress to the next tier."),
                Acc("AC-3", "The card lists the three most recent activity entries with dates and amounts."),
                Acc("AC-4", "The component is self-contained and renders with no external requests.")
            ],
            Refinement = new RefinementRound
            {
                Round = 1,
                AddressedCriteria = ["AC-2"],
                Summary = "Added the tier progress bar and label; lightened the muted timestamps.",
                AllCriteriaMet = true
            },
            AiInteraction = Recorded("webcomp.refine.v1", 9800, 0.8) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.Accepted,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-28T10:34:00Z"),
                    Reason = "The progress bar is there now and matches the numbers. Timestamps are readable. Ship it."
                }
            },
            Notes =
            [
                "Round 1 — the AI regenerated addressing AC-2. The self-check for tier progress now passes (4 / 4).",
                "The updated component is rendered above; verify the progress bar by eye."
            ]
        }
    };

    private static JourneyStep Merge() => new()
    {
        Order = 11,
        Kind = JourneyStepKind.Merge,
        Title = "Merge",
        Caption = "The refined component is green and a human approved it. It ships.",
        SimulatedThinkingMs = 900,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Parse", GateStatus.Passed, 1),
                Gate("Self-contained", GateStatus.Passed, 1),
                Gate("Banned-pattern scan", GateStatus.Passed, 1),
                Gate("Behavioural checks", GateStatus.Passed, 1, ["4 / 4 in the sandboxed iframe after 1 refinement round"])
            ],
            PullRequest = new PullRequestSummary
            {
                Number = 148, Title = "Add loyalty status card component", Branch = "feature/loyalty-card",
                FilesChanged = 1, Additions = 148, Deletions = 9
            },
            Notes = ["PR #148 merged. The first attempt missed AC-2; the AI closed it in one refinement round, then a human approved."]
        }
    };

    private static JourneyStep Telemetry() => new()
    {
        Order = 12,
        Kind = JourneyStepKind.Telemetry,
        Title = "Telemetry",
        Caption = "The forge pipeline for UI components is observable too.",
        Payload = new StepPayload
        {
            Telemetry =
            [
                new TelemetrySample { Metric = "forgeops_ai_request_duration p95", Value = "15.2 s", Detail = "webcomp.v3 via AI Bridge" },
                new TelemetrySample { Metric = "forgeops_forge_refinement_rounds", Value = "1", Detail = "to close AC-2" },
                new TelemetrySample { Metric = "forgeops_ui_checks_passed_ratio", Value = "4 / 4", Detail = "3 / 4 before refinement" },
                new TelemetrySample { Metric = "forgeops_ai_bridge_up", Value = "1" }
            ]
        }
    };

    private static JourneyStep Health() => new()
    {
        Order = 13,
        Kind = JourneyStepKind.EngineeringHealth,
        Title = "Engineering health",
        Caption = "Deterministic score with a full \"Why?\".",
        Payload = new StepPayload
        {
            Health = new EngineeringHealth
            {
                Score = 83,
                Components =
                [
                    new HealthComponent { Name = "Tests", Weight = 0.25, Score = 78 },
                    new HealthComponent { Name = "Architecture", Weight = 0.20, Score = 95 },
                    new HealthComponent { Name = "Security", Weight = 0.20, Score = 96 },
                    new HealthComponent { Name = "Code quality", Weight = 0.15, Score = 80 },
                    new HealthComponent { Name = "Observability", Weight = 0.10, Score = 66 },
                    new HealthComponent { Name = "Delivery", Weight = 0.05, Score = 82 },
                    new HealthComponent { Name = "Documentation", Weight = 0.05, Score = 60 }
                ],
                Reasons =
                [
                    new HealthReason { Kind = ReasonKind.Pass, Text = "4 / 4 behavioural checks passed in the sandbox (after 1 refinement round)" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "Component is fully self-contained — 0 external requests" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "Banned-pattern scan clean" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "The first attempt missed AC-2 (tier progress); the self-check caught it and the AI closed it" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "No visual regression baseline for the component yet" }
                ]
            }
        }
    };

    // ------------------------------------------------------------- shared

    private static readonly SpecificationDraft Spec = new()
    {
        Title = "Loyalty status card",
        Summary = "A compact card component showing a customer's loyalty points, tier and recent activity, using hard-coded sample data and no external assets.",
        AcceptanceCriteria =
        [
            new AcceptanceCriterion { Id = "AC-1", Statement = "The card shows the current points balance prominently." },
            new AcceptanceCriterion { Id = "AC-2", Statement = "The card shows the loyalty tier and progress to the next tier." },
            new AcceptanceCriterion { Id = "AC-3", Statement = "The card lists the three most recent activity entries with dates and amounts." },
            new AcceptanceCriterion { Id = "AC-4", Statement = "The component is self-contained and renders with no external network requests." }
        ],
        OutOfScope = ["Live data / API wiring.", "Editing or redeeming points."],
        OpenQuestions = ["Should the card be a fixed size or fluid?"]
    };

    private static readonly IReadOnlyList<UiCheck> Checks =
    [
        new UiCheck { Title = "Points balance is shown", Script = "return /6[,.]?200/.test(document.body.textContent)" },
        new UiCheck { Title = "Tier is shown", Script = "return /gold/i.test(document.body.textContent)" },
        new UiCheck { Title = "Progress to next tier is shown", Script = "return document.querySelector('progress, [role=progressbar], .bar, .progress') !== null" },
        new UiCheck { Title = "Three activity rows are listed", Script = "return document.querySelectorAll('.activity li, .activity-row, ul.activity > li').length === 3" }
    ];

    // The model's first attempt — renders, but omits the tier progress bar (fails AC-2's self-check).
    private const string CardHtmlDraft =
        """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Loyalty status</title>
        <style>
          :root { color-scheme: dark; }
          * { box-sizing: border-box; margin: 0; }
          body {
            font: 14px/1.5 -apple-system, "Segoe UI", Roboto, system-ui, sans-serif;
            background: radial-gradient(600px 400px at 20% 0%, #16181f, #0a0b0f);
            color: #e8ebf1;
            min-height: 100vh;
            display: grid;
            place-items: center;
            padding: 28px;
          }
          .card {
            width: 360px;
            max-width: 100%;
            background: linear-gradient(180deg, rgba(255,255,255,0.05), rgba(255,255,255,0.02));
            border: 1px solid rgba(255,255,255,0.09);
            border-radius: 16px;
            padding: 22px;
            box-shadow: 0 24px 60px -20px rgba(0,0,0,0.7);
          }
          .head { display: flex; align-items: center; justify-content: space-between; }
          .who { font-weight: 600; }
          .who small { display: block; color: #9aa4b2; font-weight: 400; }
          .tier {
            font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase;
            color: #f0a94e; border: 1px solid rgba(240,169,78,0.4);
            background: rgba(240,169,78,0.12); padding: 4px 9px; border-radius: 999px;
          }
          .points { margin: 18px 0 18px; font-size: 40px; font-weight: 700; letter-spacing: -0.02em; }
          .points span { font-size: 14px; font-weight: 500; color: #9aa4b2; }
          .activity { list-style: none; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 12px; }
          .activity li { display: flex; justify-content: space-between; padding: 7px 0; }
          .activity .label { color: #cfd6e0; }
          .activity time { color: #7b8494; font-size: 12px; }
          .activity .amt { font-variant-numeric: tabular-nums; font-weight: 600; }
          .activity .amt.pos { color: #49cf89; }
          .activity .amt.neg { color: #f0556e; }
        </style>
        </head>
        <body>
          <div class="card">
            <div class="head">
              <div class="who">Alice Nguyen<small>Member since 2021</small></div>
              <div class="tier">Gold</div>
            </div>
            <div class="points">6,200 <span>points</span></div>
            <ul class="activity">
              <li><span><span class="label">Order #4821</span><br><time>28 Aug 2026</time></span><span class="amt pos">+129</span></li>
              <li><span><span class="label">Order #4790</span><br><time>21 Aug 2026</time></span><span class="amt pos">+64</span></li>
              <li><span><span class="label">Refund #4771</span><br><time>14 Aug 2026</time></span><span class="amt neg">−40</span></li>
            </ul>
          </div>
        </body>
        </html>
        """;

    // The refined component — adds the tier progress bar (AC-2) and lightens the muted timestamps.
    private const string CardHtml =
        """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Loyalty status</title>
        <style>
          :root { color-scheme: dark; }
          * { box-sizing: border-box; margin: 0; }
          body {
            font: 14px/1.5 -apple-system, "Segoe UI", Roboto, system-ui, sans-serif;
            background: radial-gradient(600px 400px at 20% 0%, #16181f, #0a0b0f);
            color: #e8ebf1;
            min-height: 100vh;
            display: grid;
            place-items: center;
            padding: 28px;
          }
          .card {
            width: 360px;
            max-width: 100%;
            background: linear-gradient(180deg, rgba(255,255,255,0.05), rgba(255,255,255,0.02));
            border: 1px solid rgba(255,255,255,0.09);
            border-radius: 16px;
            padding: 22px;
            box-shadow: 0 24px 60px -20px rgba(0,0,0,0.7);
          }
          .head { display: flex; align-items: center; justify-content: space-between; }
          .who { font-weight: 600; }
          .who small { display: block; color: #9aa4b2; font-weight: 400; }
          .tier {
            font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase;
            color: #f0a94e; border: 1px solid rgba(240,169,78,0.4);
            background: rgba(240,169,78,0.12); padding: 4px 9px; border-radius: 999px;
          }
          .points { margin: 18px 0 4px; font-size: 40px; font-weight: 700; letter-spacing: -0.02em; }
          .points span { font-size: 14px; font-weight: 500; color: #9aa4b2; }
          .to-next { color: #9aa4b2; font-size: 12px; }
          .bar { height: 8px; border-radius: 999px; background: rgba(255,255,255,0.08); margin: 10px 0 20px; overflow: hidden; }
          .bar > i { display: block; height: 100%; width: 62%; background: linear-gradient(90deg, #f0a94e, #d97b2b); }
          .activity { list-style: none; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 12px; }
          .activity li { display: flex; justify-content: space-between; padding: 7px 0; }
          .activity .label { color: #cfd6e0; }
          .activity time { color: #9aa4b2; font-size: 12px; }
          .activity .amt { font-variant-numeric: tabular-nums; font-weight: 600; }
          .activity .amt.pos { color: #49cf89; }
          .activity .amt.neg { color: #f0556e; }
        </style>
        </head>
        <body>
          <div class="card">
            <div class="head">
              <div class="who">Alice Nguyen<small>Member since 2021</small></div>
              <div class="tier">Gold</div>
            </div>
            <div class="points">6,200 <span>points</span></div>
            <div class="to-next">620 / 1,000 to <strong>Platinum</strong></div>
            <div class="bar" role="progressbar" aria-valuenow="62" aria-valuemin="0" aria-valuemax="100"><i></i></div>
            <ul class="activity">
              <li><span><span class="label">Order #4821</span><br><time>28 Aug 2026</time></span><span class="amt pos">+129</span></li>
              <li><span><span class="label">Order #4790</span><br><time>21 Aug 2026</time></span><span class="amt pos">+64</span></li>
              <li><span><span class="label">Refund #4771</span><br><time>14 Aug 2026</time></span><span class="amt neg">−40</span></li>
            </ul>
          </div>
        </body>
        </html>
        """;

    private static QualityGate Gate(string name, GateStatus status, int seconds, IReadOnlyList<string>? evidence = null) => new()
    {
        Name = name,
        Status = status,
        Duration = TimeSpan.FromSeconds(seconds),
        Evidence = evidence ?? [],
        Timestamp = DateTimeOffset.Parse("2026-08-28T10:10:00Z").AddSeconds(seconds)
    };

    private static AcceptanceOutcome Acc(string id, string statement) => new()
    {
        CriterionId = id,
        Statement = statement,
        Status = AcceptanceStatus.NotCovered
    };

    private static AiInteractionRecord Recorded(string promptVersion, long latencyMs, double confidence) => new()
    {
        Id = $"demo-card-{promptVersion}",
        Provider = "OllamaBridge (recorded)",
        Model = "qwen2.5-coder:14b",
        ModelVersion = "qwen2.5-coder:14b",
        PromptVersion = promptVersion,
        RequestedAt = DateTimeOffset.Parse("2026-08-28T10:00:00Z"),
        LatencyMs = latencyMs,
        RawResponse = string.Empty,
        Validation = AiValidationResult.Ok(),
        Confidence = confidence,
        Simulated = true
    };
}
