using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Engineering;
using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;

namespace ForgeOps.Demo;

/// <summary>
/// The default Demo Mode walkthrough (ProjectForge.md §4, §30): a genuinely complex UI
/// requirement — an operator console for a multi-level parking structure — becomes a
/// single self-contained, interactive HTML component that ForgeOps renders in a
/// locked-down sandboxed iframe.
///
/// The first generated attempt has a real domain-modelling defect (it counts reserved
/// bays as available), which the behavioural self-checks catch; the AI then refines it
/// and ForgeOps re-audits and re-renders. Demo Mode replays this recording; Live Mode
/// builds the same thing for real with the local model.
/// </summary>
public static class ParkingDeckJourney
{
    public const string ProjectKey = "parking-deck";

    public static JourneyDefinition Build() => new()
    {
        ProjectKey = ProjectKey,
        ProjectName = "RampControl",
        RequirementText =
            "Build an operator dashboard for a multi-level parking structure — a responsive control-room "
            + "screen. Show every bay across three decks as a live status grid (free, occupied, reserved, "
            + "EV-charging, accessible) with a running occupancy meter. An attendant can assign a licence "
            + "plate to a free bay, release a bay, and read each ticket's dwell time and running charge — "
            + "first 15 minutes free, then metered per hour with a daily cap. Call out any deck with no free "
            + "bays. Dark styling, one self-contained file, no build step.",
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
        Payload = new StepPayload { Notes = ["Scripted entry into the parking-deck walkthrough."] }
    };

    private static JourneyStep Requirement() => new()
    {
        Order = 1,
        Kind = JourneyStepKind.Requirement,
        Title = "Create requirement",
        Caption = "A non-trivial UI ask enters the pipeline — ForgeOps classifies it as a web component.",
        Payload = new StepPayload
        {
            Notes =
            [
                "Requirement: \"Build an operator dashboard for a multi-level parking structure…\"",
                "RequirementClassifier → WebComponent (UI signals: dashboard, screen, grid, show, responsive, style).",
                "This is a whole small application, not a card — live state, timers, interaction, money."
            ]
        }
    };

    private static JourneyStep Specification() => new()
    {
        Order = 2,
        Kind = JourneyStepKind.Specification,
        Title = "AI specification",
        Caption = "qwen2.5-coder:14b turns the ask into six testable acceptance criteria. Advisory only.",
        SimulatedThinkingMs = 2100,
        Payload = new StepPayload { Specification = Spec, AiInteraction = Recorded("spec.v1", 5200, 0.72) }
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
            AiInteraction = Recorded("spec.v1", 5200, 0.72) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.AcceptedWithModification,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-29T09:04:00Z"),
                    Reason = "Accepted. Pinned the tariff for the demo: $3.50/hr, 15-min grace, $28 daily cap. Availability = free bays only."
                }
            }
        }
    };

    private static JourneyStep Implementation() => new()
    {
        Order = 4,
        Kind = JourneyStepKind.Implementation,
        Title = "AI implementation",
        Caption = "qwen2.5-coder:14b builds the whole console as one self-contained document.",
        SimulatedThinkingMs = 3600,
        Payload = new StepPayload
        {
            Implementation = new GeneratedImplementation
            {
                Summary =
                    "A dark parking-operations console: 44 bays across three decks as a status grid, a live "
                    + "occupancy strip, click-to-assign a plate, click-to-open a ticket with dwell time and a "
                    + "metered charge. All state in memory, one <script>, no dependencies.",
                Rationale =
                    "Single HTML document. A plain object model (decks → bays), synchronous render on every "
                    + "mutation, a 1-second tick for the clock and live dwell timers. Monospace control-room "
                    + "styling. Fee = ceil(minutes) beyond a 15-minute grace, $3.50/hr, capped at $28.",
                Kind = ImplementationKind.WebComponent,
                Origin = ImplementationOrigin.Model,
                RepairAttempts = 0,
                Files = [new GeneratedFile { Path = "index.html", Language = "html", Content = DeckDraftHtml }],
                UiChecks = Checks,
                ReviewNotes =
                [
                    "Assigning and releasing bays should feel instant — the grid re-renders synchronously.",
                    "Check the headline 'available' number against the criteria: reserved bays are not free."
                ]
            },
            AiInteraction = Recorded("webcomp.v3", 41200, 0.66)
        }
    };

    private static JourneyStep Audit() => new()
    {
        Order = 5,
        Kind = JourneyStepKind.Audit,
        Title = "Deterministic audit",
        Caption = "HtmlAuditor: self-contained check + banned-pattern scan. No model involved.",
        SimulatedThinkingMs = 1100,
        Payload = new StepPayload
        {
            Audit = WebAuditPassed,
            Notes =
            [
                "0 uses of fetch / XHR / WebSocket / import() / eval / storage / parent access / external src.",
                "One inline <script>, one inline <style>, no <link>. The audit permits sandboxed rendering (§10)."
            ]
        }
    };

    private static JourneyStep QualityGates() => new()
    {
        Order = 6,
        Kind = JourneyStepKind.QualityGates,
        Title = "Quality gates",
        Caption = "Deterministic checks over the generated markup.",
        SimulatedThinkingMs = 1500,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Parse", GateStatus.Passed, 1, ["Valid HTML document, 44 bay nodes"]),
                Gate("Self-contained", GateStatus.Passed, 1, ["Inline CSS/JS only; no <link>, no <script src>"]),
                Gate("Banned-pattern scan", GateStatus.Passed, 1, ["0 findings"]),
                Gate("Behavioural checks", GateStatus.Pending, 0)
            ],
            Notes = ["The six behavioural checks run in the browser when the component is rendered at Run & verify."]
        }
    };

    private static JourneyStep AiReview() => new()
    {
        Order = 7,
        Kind = JourneyStepKind.AiReview,
        Title = "AI review",
        Caption = "qwen2.5-coder:14b reviews its own markup and logic.",
        SimulatedThinkingMs = 2400,
        Payload = new StepPayload
        {
            ReviewFindings =
            [
                new AiReviewFinding
                {
                    Severity = FindingSeverity.High,
                    Classification = AiClassification.Likely,
                    Finding = "Availability counts every bay that is not occupied — so reserved bays are reported as free.",
                    Evidence = "index.html — availOf(): d.bays.filter(b => b.state !== 'occupied')",
                    Recommendation = "A bay is available only when its state is exactly 'free'. This is AC-2, and it also breaks AC-6.",
                    Confidence = 0.82
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Medium,
                    Classification = AiClassification.Likely,
                    Finding = "Because the count is wrong, a fully-reserved deck never reaches zero availability, so the FULL badge never shows.",
                    Evidence = "index.html — renderDecks(): full = avail === 0 (avail is inflated by the reserved bays)",
                    Recommendation = "Fixing the availability rule fixes this automatically — no separate change.",
                    Confidence = 0.79
                },
                new AiReviewFinding
                {
                    Severity = FindingSeverity.Low,
                    Classification = AiClassification.Suggestion,
                    Finding = "The plate field accepts up to 8 characters but does not show a format hint beyond the placeholder.",
                    Evidence = "index.html — <input id=\"assign-plate\" maxlength=\"8\">",
                    Recommendation = "Fine for the demo; a pattern attribute would be stricter.",
                    Confidence = 0.5
                }
            ],
            AiInteraction = Recorded("codereview.v1", 7300, 0.7),
            Notes = ["Every finding carries a classification: Confirmed / Likely / Possible / Suggestion (§18)."]
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
            AiInteraction = Recorded("codereview.v1", 7300, 0.7) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.Accepted,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-29T09:31:00Z"),
                    Reason = "Audit is clean. The AI flagged an availability-accounting bug (AC-2 / AC-6) — render it, see it, then have the AI close it."
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
        Caption = "The generated console, rendered and driven in a locked-down sandboxed iframe.",
        SimulatedThinkingMs = 1400,
        Payload = new StepPayload
        {
            Ui = new UiPreview
            {
                DocumentHtml = DeckDraftHtml,
                Checks = Checks,
                ReviewNotes =
                [
                    "The headline reads AVAILABLE 28 / 44 at 36% occupancy — but count the open bays: it is 20 at 55%.",
                    "Level 3 is fully reserved/occupied yet shows no FULL badge and its bays are still lit."
                ],
                Rendered = false
            },
            Acceptance = SpecAcceptance(),
            Notes =
            [
                "It runs — you can assign a plate, open a ticket, watch the dwell timers tick.",
                "But two self-checks fail (4 / 6): the availability count is inflated by the reserved bays, and no deck is flagged FULL.",
                "This does not merge. The AI must refine the code."
            ]
        }
    };

    private static JourneyStep Refine() => new()
    {
        Order = 10,
        Kind = JourneyStepKind.Refine,
        Title = "AI refinement",
        Caption = "The AI regenerates the console to fix the availability rule, then ForgeOps re-audits and re-renders it.",
        SimulatedThinkingMs = 3200,
        Payload = new StepPayload
        {
            Implementation = new GeneratedImplementation
            {
                Summary =
                    "Corrected availability: a bay counts as available only when its state is exactly 'free'. "
                    + "The occupancy strip and the per-deck FULL badge both follow from that one rule.",
                Rationale =
                    "One-line change in availOf() — from `state !== 'occupied'` to `state === 'free'`. "
                    + "renderDecks() and renderStrip() already derive everything from availOf(), so the FULL "
                    + "badge on Level 3 and the 55% occupancy now appear with no other edit.",
                Kind = ImplementationKind.WebComponent,
                Origin = ImplementationOrigin.ModelWithRepairs,
                RepairAttempts = 0,
                Files = [new GeneratedFile { Path = "index.html", Language = "html", Content = DeckFinalHtml }],
                UiChecks = Checks,
                ReviewNotes =
                [
                    "Availability should now read 20 / 44 at 55%, and Level 3 should be flagged FULL with its bays dimmed.",
                    "Assign / release / ticket behaviour is unchanged — verify a plate still parks instantly."
                ]
            },
            Audit = WebAuditPassed,
            Ui = new UiPreview
            {
                DocumentHtml = DeckFinalHtml,
                Checks = Checks,
                ReviewNotes =
                [
                    "AVAILABLE should read 20 / 44 · 55% occupancy.",
                    "Level 3 · Roof should show a red FULL badge and its bays should be dimmed."
                ],
                Rendered = false
            },
            Acceptance = SpecAcceptance(),
            Refinement = new RefinementRound
            {
                Round = 1,
                AddressedCriteria = ["AC-2", "AC-6"],
                Summary = "Availability now means 'free' only; occupancy meter and FULL badge corrected as a consequence.",
                AllCriteriaMet = true
            },
            AiInteraction = Recorded("webcomp.refine.v1", 18700, 0.83) with
            {
                Decision = new ForgeOps.Contracts.Ai.HumanDecision
                {
                    Kind = HumanDecisionKind.Accepted,
                    DecidedBy = "Sharath",
                    DecidedAt = DateTimeOffset.Parse("2026-08-29T09:47:00Z"),
                    Reason = "Numbers line up now (20 / 44, 55%), Level 3 shows FULL, assign/release still instant. Ship it."
                }
            },
            Notes =
            [
                "Round 1 — the AI regenerated addressing AC-2 and AC-6. All six self-checks pass (6 / 6).",
                "One rule changed; the meter and the FULL badge corrected themselves. Verify by eye above."
            ]
        }
    };

    private static JourneyStep Merge() => new()
    {
        Order = 11,
        Kind = JourneyStepKind.Merge,
        Title = "Merge",
        Caption = "The refined console is green and a human approved it. It ships.",
        SimulatedThinkingMs = 1000,
        Payload = new StepPayload
        {
            Gates =
            [
                Gate("Parse", GateStatus.Passed, 1),
                Gate("Self-contained", GateStatus.Passed, 1),
                Gate("Banned-pattern scan", GateStatus.Passed, 1),
                Gate("Behavioural checks", GateStatus.Passed, 1, ["6 / 6 in the sandboxed iframe after 1 refinement round"])
            ],
            PullRequest = new PullRequestSummary
            {
                Number = 151, Title = "Add parking-deck operator console", Branch = "feature/parking-deck",
                FilesChanged = 1, Additions = 291, Deletions = 3
            },
            Notes = ["PR #151 merged. The first attempt mis-modelled availability; the AI closed AC-2 and AC-6 in one refinement round, then a human approved."]
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
                new TelemetrySample { Metric = "forgeops_ai_request_duration p95", Value = "41.2 s", Detail = "webcomp.v3 via AI Bridge (qwen2.5-coder:14b)" },
                new TelemetrySample { Metric = "forgeops_forge_refinement_rounds", Value = "1", Detail = "to close AC-2 + AC-6" },
                new TelemetrySample { Metric = "forgeops_ui_checks_passed_ratio", Value = "6 / 6", Detail = "4 / 6 before refinement" },
                new TelemetrySample { Metric = "forgeops_generated_document_bytes", Value = "15.4 KB", Detail = "single self-contained file" },
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
                Score = 84,
                Components =
                [
                    new HealthComponent { Name = "Tests", Weight = 0.25, Score = 80 },
                    new HealthComponent { Name = "Architecture", Weight = 0.20, Score = 94 },
                    new HealthComponent { Name = "Security", Weight = 0.20, Score = 96 },
                    new HealthComponent { Name = "Code quality", Weight = 0.15, Score = 82 },
                    new HealthComponent { Name = "Observability", Weight = 0.10, Score = 64 },
                    new HealthComponent { Name = "Delivery", Weight = 0.05, Score = 84 },
                    new HealthComponent { Name = "Documentation", Weight = 0.05, Score = 62 }
                ],
                Reasons =
                [
                    new HealthReason { Kind = ReasonKind.Pass, Text = "6 / 6 behavioural checks passed in the sandbox (after 1 refinement round)" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "Component is fully self-contained — 0 external requests, one 15 KB file" },
                    new HealthReason { Kind = ReasonKind.Pass, Text = "Banned-pattern scan clean; renders under sandbox + default-src 'none'" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "The first attempt mis-modelled availability (AC-2); the self-checks caught it and the AI closed it" },
                    new HealthReason { Kind = ReasonKind.Warn, Text = "No visual regression baseline for the console yet" }
                ]
            }
        }
    };

    // ------------------------------------------------------------- shared

    private static readonly SpecificationDraft Spec = new()
    {
        Title = "Parking-deck operator console",
        Summary =
            "A single self-contained screen showing every bay across three decks as a live status grid, with "
            + "assign / release, per-ticket dwell time and a metered charge, using hard-coded state and no "
            + "external assets. Tariff: $3.50/hr, first 15 minutes free, $28.00 daily cap.",
        AcceptanceCriteria =
        [
            new AcceptanceCriterion { Id = "AC-1", Statement = "The screen renders all three decks as a grid of bays, each bay labelled and colour-coded by status (free / occupied / reserved / EV / accessible)." },
            new AcceptanceCriterion { Id = "AC-2", Statement = "A live occupancy meter shows how many bays are available now — a bay is available only if it is free, not reserved, occupied or charging." },
            new AcceptanceCriterion { Id = "AC-3", Statement = "Selecting a free bay and entering a licence plate marks that bay occupied and starts a ticket." },
            new AcceptanceCriterion { Id = "AC-4", Statement = "Releasing an occupied bay returns it to free, closes the ticket and adds its charge to shift revenue." },
            new AcceptanceCriterion { Id = "AC-5", Statement = "An active ticket shows dwell time and a running charge — free for the first 15 minutes, then $3.50 per hour, capped at $28 per day." },
            new AcceptanceCriterion { Id = "AC-6", Statement = "A deck with zero available bays is flagged FULL and its free-bay affordance is disabled." }
        ],
        OutOfScope = ["Live gate hardware / payment capture.", "Multi-day tickets and lost-ticket handling.", "Persistence — state resets on reload."],
        OpenQuestions = ["Should reserved bays be releasable by the attendant, or only by the reservation holder?"]
    };

    /// <summary>UI acceptance is human visual judgment (§2.1); the criteria are shown as a reviewer checklist.</summary>
    private static List<AcceptanceOutcome> SpecAcceptance() =>
        Spec.AcceptanceCriteria
            .Select(c => new AcceptanceOutcome { CriterionId = c.Id, Statement = c.Statement, Status = AcceptanceStatus.NotCovered })
            .ToList();

    private static readonly AuditReport WebAuditPassed = new()
    {
        Kind = ImplementationKind.WebComponent,
        Compiled = true,
        RepairAttempts = 0,
        Diagnostics = [],
        BannedApis = [],
        ArchitecturePassed = true,
        ArchitectureNotes = ["Self-contained document; inline styles/scripts only; no external resources."],
        Verdict = AuditVerdict.Passed
    };

    private static readonly IReadOnlyList<UiCheck> Checks =
    [
        new UiCheck
        {
            Title = "All three decks render as bay grids",
            Script = "return document.querySelectorAll('.deck').length === 3 && document.querySelectorAll('.bay').length === 44;"
        },
        new UiCheck
        {
            Title = "Every bay is colour-coded by status",
            Script = "return [].slice.call(document.querySelectorAll('.bay')).every(function(b){ return /is-(free|occupied|reserved)/.test(b.className); });"
        },
        new UiCheck
        {
            Title = "A live availability meter is shown",
            Script = "return !!document.querySelector('#meter i') && /\\d+\\s*\\/\\s*\\d+\\s*bays/.test(document.querySelector('.strip').textContent);"
        },
        new UiCheck
        {
            Title = "The availability count matches the actual free bays",
            Script = "return Number(document.getElementById('avail').getAttribute('data-avail')) === document.querySelectorAll('.bay.is-free').length;"
        },
        new UiCheck
        {
            Title = "A deck with no free bays is flagged FULL",
            Script = "return document.querySelector('.deck[data-deck=\"P3\"] .deck-full') !== null;"
        },
        new UiCheck
        {
            Title = "Assigning a plate to a free bay fills it",
            Script =
                "var f=document.querySelector('.bay.is-free'); if(!f) throw new Error('no free bay'); "
                + "var n=f.getAttribute('data-n'); f.click(); "
                + "if(document.getElementById('assign').hidden) throw new Error('assign panel did not open'); "
                + "document.getElementById('assign-plate').value='CHK-9'; "
                + "document.getElementById('assign-confirm').click(); "
                + "var x=document.querySelector('.bay[data-n=\"'+n+'\"]'); "
                + "return !!x && x.classList.contains('is-occupied') && /CHK-9/.test(x.textContent);"
        }
    ];

    // The model's first attempt — availability counts any bay that is not occupied, so reserved
    // bays are wrongly reported free (fails AC-2) and no deck ever reaches FULL (fails AC-6).
    private static readonly string DeckDraftHtml = DeckTemplate.Replace(
        "/*__AVAIL_IMPL__*/",
        "return d.bays.filter(function(b){ return b.state !== 'occupied'; }).length; // counts reserved as available");

    // The refined console — a bay is available only when its state is exactly 'free'.
    private static readonly string DeckFinalHtml = DeckTemplate.Replace(
        "/*__AVAIL_IMPL__*/",
        "return d.bays.filter(function(b){ return b.state === 'free'; }).length;");

    private static QualityGate Gate(string name, GateStatus status, int seconds, IReadOnlyList<string>? evidence = null) => new()
    {
        Name = name,
        Status = status,
        Duration = TimeSpan.FromSeconds(seconds),
        Evidence = evidence ?? [],
        Timestamp = DateTimeOffset.Parse("2026-08-29T09:20:00Z").AddSeconds(seconds)
    };

    private static AiInteractionRecord Recorded(string promptVersion, long latencyMs, double confidence) => new()
    {
        Id = $"demo-deck-{promptVersion}",
        Provider = "OllamaBridge (recorded)",
        Model = "qwen2.5-coder:14b",
        ModelVersion = "qwen2.5-coder:14b",
        PromptVersion = promptVersion,
        RequestedAt = DateTimeOffset.Parse("2026-08-29T09:00:00Z"),
        LatencyMs = latencyMs,
        RawResponse = string.Empty,
        Validation = AiValidationResult.Ok(),
        Confidence = confidence,
        Simulated = true
    };

    // ---------------------------------------------------------------------
    // The generated artefact. One self-contained document; the only thing that
    // differs between the first attempt and the refinement is the body of availOf(),
    // injected at /*__AVAIL_IMPL__*/.
    // ---------------------------------------------------------------------
    private const string DeckTemplate =
        """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>ParkDeck — Central Ramp</title>
        <style>
          :root{color-scheme:dark}
          *{box-sizing:border-box;margin:0}
          body{display:block;font:13px/1.5 ui-sans-serif,system-ui,"Segoe UI",Roboto,sans-serif;color:#c9d4e2;
               background:#0a0e15;
               background-image:linear-gradient(rgba(120,140,170,.05) 1px,transparent 1px),linear-gradient(90deg,rgba(120,140,170,.05) 1px,transparent 1px);
               background-size:23px 23px;min-height:100vh;padding:22px}
          .mono{font-family:ui-monospace,"SF Mono","JetBrains Mono",Consolas,monospace}
          .wrap{max-width:1120px;margin:0 auto;display:flex;flex-direction:column;gap:16px}

          .topbar{display:flex;justify-content:space-between;align-items:center;border-bottom:1px solid #1b2532;padding-bottom:13px}
          .brand{font-family:ui-monospace,Consolas,monospace;font-weight:700;letter-spacing:.16em;color:#eaf2ff;font-size:13px}
          .brand b{color:#37e3d2}
          .clock{font-family:ui-monospace,Consolas,monospace;color:#6f8096;letter-spacing:.1em;font-size:12px}
          .clock b{color:#9db0c6}

          .strip{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:12px}
          .stat{background:#0e1420;border:1px solid #1b2532;border-radius:10px;padding:12px 14px}
          .stat .k{font-family:ui-monospace,Consolas,monospace;font-size:9.5px;letter-spacing:.18em;color:#5d6b81;text-transform:uppercase}
          .stat .v{font-size:25px;font-weight:700;color:#eef4ff;letter-spacing:-.01em;margin-top:3px}
          .stat .v small{font-size:11px;font-weight:500;color:#748298}
          .meter{height:5px;border-radius:99px;background:#1b2532;overflow:hidden;margin-top:10px}
          .meter i{display:block;height:100%;width:0;background:linear-gradient(90deg,#37e3d2,#33e59f);transition:width .4s ease}
          .meter.hot i{background:linear-gradient(90deg,#ffb057,#ff5d7a)}

          .legend{display:flex;gap:15px;flex-wrap:wrap;font-family:ui-monospace,Consolas,monospace;font-size:10.5px;color:#7d8ca1;padding:2px 0}
          .legend span{display:flex;align-items:center;gap:6px}
          .dot{width:9px;height:9px;border-radius:3px;flex:0 0 auto}
          .d-free{background:#33e59f}.d-occ{background:#3c4a5d}.d-res{background:#a98cff}.d-ev{background:#26d4ee}.d-acc{background:#67a6ff}

          .decks{display:grid;grid-template-columns:repeat(auto-fit,minmax(290px,1fr));gap:14px}
          .deck{background:#0e1420;border:1px solid #1b2532;border-radius:12px;padding:14px}
          .deck.is-full{border-color:#4d2130}
          .deck-head{display:flex;justify-content:space-between;align-items:center;margin-bottom:11px}
          .deck-head .lvl{font-family:ui-monospace,Consolas,monospace;letter-spacing:.09em;color:#dce7f5;font-weight:700;font-size:12px}
          .deck-head .cnt{font-family:ui-monospace,Consolas,monospace;font-size:10.5px;color:#33e59f;letter-spacing:.06em}
          .deck-full{font-family:ui-monospace,Consolas,monospace;font-size:9.5px;font-weight:700;letter-spacing:.16em;
                     color:#ff6a86;border:1px solid #4d2130;background:rgba(255,106,134,.13);padding:3px 8px;border-radius:5px}
          .bays{display:grid;grid-template-columns:repeat(4,1fr);gap:7px}
          .bay{position:relative;aspect-ratio:1/1.06;border-radius:7px;border:1px solid #212e3d;background:#121a26;
               display:flex;flex-direction:column;justify-content:center;align-items:center;gap:3px;cursor:pointer;padding:5px;
               transition:transform .1s ease,border-color .1s ease}
          .bay:hover{transform:translateY(-1px);border-color:#37e3d2}
          .bay .id{position:absolute;top:3px;left:5px;font-family:ui-monospace,Consolas,monospace;font-size:8px;color:#556479;letter-spacing:.02em}
          .bay .ico{position:absolute;top:2px;right:4px;font-size:9px;line-height:1}
          .bay .tag{font-family:ui-monospace,Consolas,monospace;font-size:8.5px;letter-spacing:.12em}
          .bay.is-free{border-color:rgba(51,229,159,.4);background:rgba(51,229,159,.06)}
          .bay.is-free .tag{color:#33e59f}
          .bay.is-occupied{background:#18212f;border-color:#2a3849}
          .bay.is-occupied .plate{font-family:ui-monospace,Consolas,monospace;font-size:10.5px;font-weight:700;color:#eaf2fb;
               border:1px solid #384759;border-radius:3px;padding:1px 4px;background:#0c131c}
          .bay.is-occupied .t{font-family:ui-monospace,Consolas,monospace;font-size:8.5px;color:#7787a0}
          .bay.is-reserved{border-color:rgba(169,140,255,.42);background:rgba(169,140,255,.07);border-style:dashed}
          .bay.is-reserved .tag{color:#a98cff}
          .deck.is-full .bay.is-free{opacity:.35;pointer-events:none}

          .panel{background:#0e1420;border:1px solid #24313f;border-radius:10px;padding:14px;display:flex;flex-wrap:wrap;gap:14px;align-items:flex-end}
          .panel[hidden]{display:none}
          .panel h3{font-family:ui-monospace,Consolas,monospace;font-size:10.5px;letter-spacing:.16em;color:#7d8ca1;width:100%;text-transform:uppercase;margin-bottom:-2px}
          .panel label{font-family:ui-monospace,Consolas,monospace;font-size:9.5px;color:#5d6b81;display:block;margin-bottom:5px;letter-spacing:.06em;text-transform:uppercase}
          .panel input{background:#090e16;border:1px solid #2a3849;border-radius:6px;color:#eef4ff;padding:9px 11px;
               font-family:ui-monospace,Consolas,monospace;font-size:13px;letter-spacing:.12em;width:160px}
          .panel input:focus{outline:none;border-color:#37e3d2}
          .btn{font-family:ui-monospace,Consolas,monospace;font-size:10.5px;font-weight:700;letter-spacing:.1em;border-radius:6px;
               padding:9px 15px;cursor:pointer;border:1px solid #2a3849;background:#131c28;color:#c9d4e2;text-transform:uppercase}
          .btn:hover{border-color:#37e3d2}
          .btn.go{background:linear-gradient(180deg,#37e3d2,#23b7ae);color:#032420;border-color:transparent}
          .btn.warn{border-color:#4d2130;color:#ff8fa6}
          .tkt{display:flex;gap:22px;flex-wrap:wrap}
          .tkt .k{font-family:ui-monospace,Consolas,monospace;font-size:9px;letter-spacing:.16em;color:#5d6b81;text-transform:uppercase}
          .tkt .v{font-family:ui-monospace,Consolas,monospace;font-size:15px;color:#eef4ff;margin-top:3px}
          .tkt .v.free{color:#33e59f}
          .foot{font-family:ui-monospace,Consolas,monospace;font-size:10px;color:#556479;letter-spacing:.06em;text-align:right}
        </style>
        </head>
        <body>
        <div class="wrap">
          <div class="topbar">
            <div class="brand">&#9646; PARK<b>DECK</b> &nbsp;/&nbsp; CENTRAL RAMP</div>
            <div class="clock mono">SHIFT <b>DAY</b> &nbsp;&#183;&nbsp; <span id="clock">--:--:--</span></div>
          </div>

          <div class="strip">
            <div class="stat">
              <div class="k">Available now</div>
              <div class="v"><span class="occ" id="avail" data-avail="0">0</span> <small id="availOf">/ 0 bays</small></div>
              <div class="meter" id="meter"><i></i></div>
            </div>
            <div class="stat"><div class="k">Occupancy</div><div class="v" id="pct">0%</div></div>
            <div class="stat"><div class="k">Revenue &#183; shift</div><div class="v" id="rev">$0.00</div></div>
            <div class="stat"><div class="k">Turnovers</div><div class="v" id="turn">0</div></div>
          </div>

          <div class="legend">
            <span><i class="dot d-free"></i>FREE</span>
            <span><i class="dot d-occ"></i>OCCUPIED</span>
            <span><i class="dot d-res"></i>RESERVED</span>
            <span><i class="dot d-ev"></i>EV &#9889;</span>
            <span><i class="dot d-acc"></i>ACCESSIBLE &#9855;</span>
          </div>

          <div class="decks" id="decks"></div>

          <div class="panel" id="assign" hidden>
            <h3 id="assignTitle">Assign bay</h3>
            <div><label>Licence plate</label><input id="assign-plate" maxlength="8" placeholder="ABC-1234" autocomplete="off"></div>
            <button class="btn go" id="assign-confirm">Confirm</button>
            <button class="btn" id="assign-cancel">Cancel</button>
          </div>

          <div class="panel" id="ticket" hidden>
            <h3 id="ticketTitle">Ticket</h3>
            <div class="tkt">
              <div><div class="k">Plate</div><div class="v" id="tk-plate">&#8212;</div></div>
              <div><div class="k">Entered</div><div class="v" id="tk-in">&#8212;</div></div>
              <div><div class="k">Dwell</div><div class="v" id="tk-dwell">&#8212;</div></div>
              <div><div class="k">Charge</div><div class="v" id="tk-fee">&#8212;</div></div>
            </div>
            <button class="btn warn" id="ticket-release">Release</button>
            <button class="btn" id="ticket-close">Close</button>
          </div>

          <div class="foot">$3.50 / hr &#183; first 15 min free &#183; $28.00 daily cap &#183; hard-coded state, no network</div>
        </div>

        <script>
        (function(){
          "use strict";
          var RATE=3.5, GRACE=15, CAP=28;
          var ICON={std:"",ev:"⚡",acc:"♿"};

          function makeDeck(id,label,s){
            var bays=[];
            for(var i=1;i<=s.count;i++){
              var n=id+"-"+(i<10?"0":"")+i;
              var type = (s.ev&&s.ev.indexOf(i)>=0) ? "ev" : (s.acc&&s.acc.indexOf(i)>=0) ? "acc" : "std";
              var b={n:n,type:type,state:"free",plate:null,since:null};
              var oi = s.occ ? s.occ.indexOf(i) : -1;
              if(oi>=0){ b.state="occupied"; b.plate=s.plates[oi]; b.since=Date.now()-s.age[oi]*60000; }
              else if(s.res && s.res.indexOf(i)>=0){ b.state="reserved"; }
              bays.push(b);
            }
            return {id:id,label:label,bays:bays};
          }

          var decks=[
            makeDeck("P1","Level 1 / Street",{count:16,ev:[1,2],acc:[16],
              occ:[3,4,7,12],plates:["KLM-4417","TRX-902","H8N-556","9BQ-118"],age:[8,52,205,3],res:[9,10]}),
            makeDeck("P2","Level 2 / Mezzanine",{count:16,ev:[5,6],acc:[8],
              occ:[1,5,11,14],plates:["BZ-7781","MNO-330","PLZ-006","ARS-51"],age:[26,140,4,71],res:[15,16]}),
            makeDeck("P3","Level 3 / Roof",{count:12,ev:[1,2],acc:[6],
              occ:[3,4,5,7,8,9,10,11],plates:["RF-201","RF-118","RF-940","RF-077","RF-455","RF-312","RF-889","RF-624"],
              age:[12,33,90,5,61,7,148,44],res:[1,2,6,12]})
          ];

          var revenue=0, turnovers=0, openBay=null;

          function availOf(d){
            /*__AVAIL_IMPL__*/
          }
          function total(){ var t=0; decks.forEach(function(d){ t+=d.bays.length; }); return t; }
          function totalAvail(){ var t=0; decks.forEach(function(d){ t+=availOf(d); }); return t; }
          function find(n){ for(var i=0;i<decks.length;i++) for(var j=0;j<decks[i].bays.length;j++) if(decks[i].bays[j].n===n) return decks[i].bays[j]; return null; }
          function deckOf(n){ var id=n.split("-")[0]; for(var i=0;i<decks.length;i++) if(decks[i].id===id) return decks[i]; return null; }

          function money(x){ return "$"+x.toFixed(2); }
          function fee(sinceMs){
            var mins=Math.ceil((Date.now()-sinceMs)/60000);
            if(mins<=GRACE) return 0;
            return Math.min(((mins-GRACE)/60)*RATE, CAP);
          }
          function dwell(sinceMs){
            var s=Math.floor((Date.now()-sinceMs)/1000), h=Math.floor(s/3600), m=Math.floor((s%3600)/60);
            return h>0 ? h+"h "+m+"m" : m+"m";
          }
          function pad(x){ return (x<10?"0":"")+x; }
          function hhmm(ms){ var d=new Date(ms); return pad(d.getHours())+":"+pad(d.getMinutes()); }

          var decksEl=document.getElementById("decks");
          function renderDecks(){
            var html="";
            decks.forEach(function(d){
              var avail=availOf(d), full=avail===0;
              html+='<div class="deck'+(full?" is-full":"")+'" data-deck="'+d.id+'">';
              html+='<div class="deck-head"><span class="lvl">'+d.id+' &#183; '+d.label+'</span>';
              html+= full ? '<span class="deck-full">FULL</span>' : '<span class="cnt">'+avail+' free</span>';
              html+='</div><div class="bays">';
              d.bays.forEach(function(b){
                html+='<div class="bay is-'+b.state+'" data-n="'+b.n+'" title="'+b.n+'">';
                html+='<span class="id">'+b.n+'</span>';
                if(ICON[b.type]) html+='<span class="ico">'+ICON[b.type]+'</span>';
                if(b.state==="occupied") html+='<span class="plate" data-plate="'+b.plate+'">'+b.plate+'</span><span class="t">'+dwell(b.since)+'</span>';
                else if(b.state==="reserved") html+='<span class="tag">RESV</span>';
                else html+='<span class="tag">OPEN</span>';
                html+='</div>';
              });
              html+='</div></div>';
            });
            decksEl.innerHTML=html;
          }

          function renderStrip(){
            var T=total(), avail=totalAvail(), occ=T-avail, pct=Math.round(occ/T*100);
            var a=document.getElementById("avail");
            a.textContent=avail; a.setAttribute("data-avail",avail);
            document.getElementById("availOf").textContent="/ "+T+" bays";
            document.getElementById("pct").textContent=pct+"%";
            var m=document.getElementById("meter");
            m.firstElementChild.style.width=pct+"%";
            if(pct>=80) m.classList.add("hot"); else m.classList.remove("hot");
            document.getElementById("rev").textContent=money(revenue);
            document.getElementById("turn").textContent=turnovers;
          }
          function renderAll(){ renderDecks(); renderStrip(); }

          var assign=document.getElementById("assign"), plateInput=document.getElementById("assign-plate");
          var ticket=document.getElementById("ticket");

          function openAssign(b){
            assign.setAttribute("data-n",b.n);
            document.getElementById("assignTitle").textContent="Assign bay "+b.n;
            plateInput.value=""; ticket.hidden=true; assign.hidden=false; plateInput.focus();
          }
          function openTicket(b){
            openBay=b.n;
            document.getElementById("ticketTitle").textContent="Ticket / "+b.n;
            assign.hidden=true; ticket.hidden=false; refreshTicket();
          }
          function refreshTicket(){
            if(ticket.hidden||!openBay) return;
            var b=find(openBay);
            if(!b||b.state!=="occupied"){ ticket.hidden=true; openBay=null; return; }
            document.getElementById("tk-plate").textContent=b.plate;
            document.getElementById("tk-in").textContent=hhmm(b.since);
            document.getElementById("tk-dwell").textContent=dwell(b.since);
            var f=fee(b.since), fx=document.getElementById("tk-fee");
            if(f===0){ fx.textContent="$0.00 / grace"; fx.className="v free"; }
            else { fx.textContent=money(f); fx.className="v"; }
          }

          document.getElementById("assign-cancel").addEventListener("click",function(){ assign.hidden=true; });
          document.getElementById("assign-confirm").addEventListener("click",function(){
            var b=find(assign.getAttribute("data-n"));
            if(!b||b.state!=="free"){ assign.hidden=true; return; }
            var p=(plateInput.value||"").trim().toUpperCase().replace(/[^A-Z0-9-]/g,"");
            if(!p) p="TMP-"+Math.floor(1000+Math.random()*9000);
            b.state="occupied"; b.plate=p; b.since=Date.now();
            assign.hidden=true; renderAll();
          });
          document.getElementById("ticket-close").addEventListener("click",function(){ ticket.hidden=true; openBay=null; });
          document.getElementById("ticket-release").addEventListener("click",function(){
            var b=find(openBay);
            if(b&&b.state==="occupied"){ revenue+=fee(b.since); turnovers++; b.state="free"; b.plate=null; b.since=null; }
            ticket.hidden=true; openBay=null; renderAll();
          });

          decksEl.addEventListener("click",function(e){
            var el=e.target.closest ? e.target.closest(".bay") : null;
            if(!el) return;
            var b=find(el.getAttribute("data-n"));
            if(!b) return;
            if(b.state==="free"){ if(availOf(deckOf(b.n))>0) openAssign(b); }
            else if(b.state==="occupied"){ openTicket(b); }
          });

          function tick(){
            var d=new Date();
            document.getElementById("clock").textContent=pad(d.getHours())+":"+pad(d.getMinutes())+":"+pad(d.getSeconds());
            var live=document.querySelectorAll(".bay.is-occupied");
            for(var i=0;i<live.length;i++){
              var b=find(live[i].getAttribute("data-n"));
              var t=live[i].querySelector(".t");
              if(b&&t) t.textContent=dwell(b.since);
            }
            refreshTicket();
          }

          renderAll();
          tick();
          setInterval(tick,1000);
        })();
        </script>
        </body>
        </html>
        """;
}
