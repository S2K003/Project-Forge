using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;

namespace ForgeOps.Demo;

/// <summary>The journeys ForgeOps can walk. Both work in Demo (recorded) and Live (real).</summary>
public static class JourneyCatalog
{
    public static readonly IReadOnlyList<JourneyOption> All =
    [
        new("loyalty-card", "Loyalty status card", "UI — generated HTML, audited, rendered live", ImplementationKind.WebComponent),
        new("customerhub", "Loyalty rules", "Backend logic — generated C#, compiled, executed", ImplementationKind.CSharpLogic),
    ];

    public static JourneyDefinition Build(string? key) => key switch
    {
        CustomerHubJourney.ProjectKey => CustomerHubJourney.Build(),
        _ => LoyaltyCardJourney.Build()
    };

    /// <summary>The UI development walkthrough is what Demo/Live Mode opens on by default.</summary>
    public static string DefaultKey => "loyalty-card";
}

public sealed record JourneyOption(string Key, string Name, string Blurb, ImplementationKind Kind);
