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
    public void Catalog_exposes_both_journeys()
    {
        Assert.Contains(JourneyCatalog.All, j => j.Key == "customerhub" && j.Kind == ImplementationKind.CSharpLogic);
        Assert.Contains(JourneyCatalog.All, j => j.Key == "loyalty-card" && j.Kind == ImplementationKind.WebComponent);
    }
}
