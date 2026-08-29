using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;
using ForgeOps.Demo;
using ForgeOps.Forge;

namespace ForgeOps.UnitTests;

public sealed class RequirementClassifierTests
{
    [Theory]
    [InlineData("Customers should receive loyalty points after successful purchases.")]
    [InlineData("Add an API endpoint that returns the Nth Fibonacci number.")]
    [InlineData("The webhook handler must be idempotent for duplicate delivery.")]
    public void Logic_requirements_classify_as_csharp(string requirement) =>
        Assert.Equal(ImplementationKind.CSharpLogic, RequirementClassifier.Classify(requirement));

    [Theory]
    [InlineData("Show a customer's loyalty status as a compact card with points and tier.")]
    [InlineData("Build a dark-mode dashboard widget showing recent activity.")]
    [InlineData("Design a sign-in form with email and password fields and a submit button.")]
    [InlineData("Show a weekly workout plan as a card with 5 day rows and a progress count.")]
    [InlineData("A pricing table with three plans and a highlighted recommended tier.")]
    [InlineData("Build an operator dashboard for a multi-level parking structure — a responsive control-room screen. Show every bay as a live status grid with dark styling.")]
    public void Ui_requirements_classify_as_web_component(string requirement) =>
        Assert.Equal(ImplementationKind.WebComponent, RequirementClassifier.Classify(requirement));
}

public sealed class HtmlAuditorTests
{
    private const string Clean =
        "<!doctype html><html><head><style>body{background:#000}</style></head><body><div class='card'>Hi</div></body></html>";

    [Fact]
    public void Clean_self_contained_document_passes()
    {
        Assert.Empty(HtmlAuditor.Scan(Clean));
        Assert.True(HtmlAuditor.CheckStructure(Clean).Ok);
    }

    [Theory]
    [InlineData("<body><script>fetch('/x')</script></body>")]
    [InlineData("<body><script src='https://cdn.example.com/x.js'></script></body>")]
    [InlineData("<body><img src='https://example.com/a.png'></body>")]
    [InlineData("<body><script>eval('1+1')</script></body>")]
    [InlineData("<body><script>window.parent.location='x'</script></body>")]
    [InlineData("<body><script>localStorage.setItem('a','b')</script></body>")]
    [InlineData("<body><iframe src='x'></iframe></body>")]
    public void Dangerous_markup_is_flagged(string html) =>
        Assert.NotEmpty(HtmlAuditor.Scan(html));

    [Fact]
    public void Audit_of_a_clean_component_permits_rendering()
    {
        var report = GeneratedCodeAuditor.AuditWebComponent(Clean, 0);
        Assert.True(report.ExecutionAllowed);
        Assert.Equal(AuditVerdict.Passed, report.Verdict);
    }

    [Fact]
    public void Audit_of_a_component_with_a_banned_pattern_blocks_rendering()
    {
        var report = GeneratedCodeAuditor.AuditWebComponent("<body><script>fetch('/x')</script></body>", 0);
        Assert.False(report.ExecutionAllowed);
    }
}

public sealed class LoyaltyCardJourneyTests
{
    private static readonly JourneyDefinition Journey = LoyaltyCardJourney.Build();

    [Fact]
    public void Is_a_web_component_journey_covering_every_step_kind()
    {
        Assert.Equal(ImplementationKind.WebComponent, Journey.Kind);
        Assert.Equal(Enum.GetValues<JourneyStepKind>(), Journey.Steps.Select(s => s.Kind).ToArray());
    }

    [Fact]
    public void The_generated_component_passes_the_deterministic_audit()
    {
        var impl = Journey.Steps.Single(s => s.Kind == JourneyStepKind.Implementation).Payload.Implementation!;
        var html = impl.Files.Single(f => f.Language == "html").Content;
        var report = GeneratedCodeAuditor.AuditWebComponent(html, 0);
        Assert.True(report.ExecutionAllowed, string.Join("; ", report.BannedApis.Select(b => b.Api)));
    }

    [Fact]
    public void The_run_step_carries_a_renderable_component_with_checks()
    {
        var ui = Journey.Steps.Single(s => s.Kind == JourneyStepKind.AcceptanceRun).Payload.Ui!;
        Assert.False(string.IsNullOrWhiteSpace(ui.DocumentHtml));
        Assert.NotEmpty(ui.Checks);
        Assert.All(ui.Checks, c => Assert.False(string.IsNullOrWhiteSpace(c.Script)));
    }

    [Fact]
    public void The_refine_step_regenerates_the_component_and_closes_ac2()
    {
        var refine = Journey.Steps.Single(s => s.Kind == JourneyStepKind.Refine).Payload;

        Assert.NotNull(refine.Refinement);
        Assert.True(refine.Refinement!.AllCriteriaMet);
        Assert.Contains("AC-2", refine.Refinement.AddressedCriteria);

        var ui = refine.Ui!;
        Assert.False(string.IsNullOrWhiteSpace(ui.DocumentHtml));
        Assert.Contains("progressbar", ui.DocumentHtml);

        var report = GeneratedCodeAuditor.AuditWebComponent(ui.DocumentHtml, 0);
        Assert.True(report.ExecutionAllowed);
    }

    [Fact]
    public void The_first_attempt_omits_the_tier_progress_bar()
    {
        var ui = Journey.Steps.Single(s => s.Kind == JourneyStepKind.AcceptanceRun).Payload.Ui!;
        Assert.DoesNotContain("progressbar", ui.DocumentHtml);
    }

    [Fact]
    public void Catalog_exposes_every_journey_and_defaults_to_the_parking_deck_console()
    {
        Assert.Contains(JourneyCatalog.All, j => j.Key == "parking-deck" && j.Kind == ImplementationKind.WebComponent);
        Assert.Contains(JourneyCatalog.All, j => j.Key == "loyalty-card" && j.Kind == ImplementationKind.WebComponent);
        Assert.Contains(JourneyCatalog.All, j => j.Key == "customerhub" && j.Kind == ImplementationKind.CSharpLogic);

        Assert.Equal("parking-deck", JourneyCatalog.DefaultKey);
        Assert.Equal(ImplementationKind.WebComponent, JourneyCatalog.Build(null).Kind);
        Assert.Equal("parking-deck", JourneyCatalog.Build(null).ProjectKey);
        Assert.Equal(ImplementationKind.WebComponent, JourneyCatalog.Build("loyalty-card").Kind);
        Assert.Equal(ImplementationKind.CSharpLogic, JourneyCatalog.Build("customerhub").Kind);
    }
}

public sealed class ParkingDeckJourneyTests
{
    private static readonly JourneyDefinition Journey = ParkingDeckJourney.Build();

    private static string Html(JourneyStepKind kind) =>
        Journey.Steps.Single(s => s.Kind == kind).Payload.Ui!.DocumentHtml;

    [Fact]
    public void Is_a_web_component_journey_covering_every_step_kind()
    {
        Assert.Equal(ImplementationKind.WebComponent, Journey.Kind);
        Assert.Equal(Enum.GetValues<JourneyStepKind>(), Journey.Steps.Select(s => s.Kind).ToArray());
    }

    [Fact]
    public void The_spec_has_six_testable_acceptance_criteria()
    {
        var spec = Journey.Steps.Single(s => s.Kind == JourneyStepKind.Specification).Payload.Specification!;
        Assert.Equal(6, spec.AcceptanceCriteria.Count);
    }

    [Fact]
    public void Both_generated_versions_pass_the_deterministic_audit()
    {
        foreach (var kind in new[] { JourneyStepKind.AcceptanceRun, JourneyStepKind.Refine })
        {
            var report = GeneratedCodeAuditor.AuditWebComponent(Html(kind), 0);
            Assert.True(report.ExecutionAllowed, string.Join("; ", report.BannedApis.Select(b => $"{b.Api} @ {b.Line}")));
        }
    }

    [Fact]
    public void The_run_step_carries_a_renderable_console_with_six_checks()
    {
        var ui = Journey.Steps.Single(s => s.Kind == JourneyStepKind.AcceptanceRun).Payload.Ui!;
        Assert.False(string.IsNullOrWhiteSpace(ui.DocumentHtml));
        Assert.Equal(6, ui.Checks.Count);
        Assert.All(ui.Checks, c => Assert.False(string.IsNullOrWhiteSpace(c.Script)));
    }

    [Fact]
    public void The_first_attempt_miscounts_availability_and_the_refinement_corrects_it()
    {
        var draft = Html(JourneyStepKind.AcceptanceRun);
        var final = Html(JourneyStepKind.Refine);

        // The only behavioural difference is the availability rule.
        Assert.Contains("state !== 'occupied'", draft);
        Assert.DoesNotContain("state !== 'occupied'", final);
        Assert.Contains("state === 'free'", final);

        var refine = Journey.Steps.Single(s => s.Kind == JourneyStepKind.Refine).Payload;
        Assert.NotNull(refine.Refinement);
        Assert.True(refine.Refinement!.AllCriteriaMet);
        Assert.Contains("AC-2", refine.Refinement.AddressedCriteria);
        Assert.Contains("AC-6", refine.Refinement.AddressedCriteria);
    }

    [Fact]
    public void Every_recorded_ai_interaction_is_flagged_simulated()
    {
        var interactions = Journey.Steps.Select(s => s.Payload.AiInteraction).Where(i => i is not null).ToArray();
        Assert.NotEmpty(interactions);
        Assert.All(interactions, i => Assert.True(i!.Simulated));
    }
}
