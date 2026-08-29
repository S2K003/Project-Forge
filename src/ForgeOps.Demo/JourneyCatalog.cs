using ForgeOps.Contracts.Forge;
using ForgeOps.Contracts.Journey;

namespace ForgeOps.Demo;

/// <summary>The journeys ForgeOps can walk. All three work in Demo (recorded) and Live (real).</summary>
public static class JourneyCatalog
{
    public static readonly IReadOnlyList<JourneyOption> All =
    [
        new("parking-deck", "Parking-deck console", "UI — a full operator screen, generated and rendered", ImplementationKind.WebComponent),
        new("loyalty-card", "Loyalty status card", "UI — a compact card, generated and rendered", ImplementationKind.WebComponent),
        new("customerhub", "Loyalty rules", "Backend logic — generated C#, compiled, executed", ImplementationKind.CSharpLogic),
    ];

    public static JourneyDefinition Build(string? key) => key switch
    {
        CustomerHubJourney.ProjectKey => CustomerHubJourney.Build(),
        LoyaltyCardJourney.ProjectKey => LoyaltyCardJourney.Build(),
        _ => ParkingDeckJourney.Build()
    };

    /// <summary>The parking-deck console is the walkthrough Demo/Live Mode opens on by default.</summary>
    public static string DefaultKey => ParkingDeckJourney.ProjectKey;
}

public sealed record JourneyOption(string Key, string Name, string Blurb, ImplementationKind Kind);
