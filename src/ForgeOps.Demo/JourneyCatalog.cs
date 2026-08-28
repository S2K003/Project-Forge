using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;

namespace ForgeOps.Demo;

/// <summary>The journeys ForgeOps can walk. Both work in Demo (recorded) and Live (real).</summary>
public static class JourneyCatalog
{
    public static readonly IReadOnlyList<JourneyOption> All =
    [
        new("customerhub", "Loyalty rules", "Backend logic — generated C#, compiled, executed", ImplementationKind.CSharpLogic),
        new("loyalty-card", "Loyalty status card", "UI — generated HTML, audited, rendered", ImplementationKind.WebComponent),
    ];

    public static JourneyDefinition Build(string? key) => key switch
    {
        LoyaltyCardJourney.ProjectKey => LoyaltyCardJourney.Build(),
        _ => CustomerHubJourney.Build()
    };

    public static string DefaultKey => "customerhub";
}

public sealed record JourneyOption(string Key, string Name, string Blurb, ImplementationKind Kind);
