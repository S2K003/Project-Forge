using ForgeOps.Demo;

namespace ForgeOps.Api.Endpoints;

/// <summary>
/// A single fast fixture endpoint so Live Mode can replay the identical CustomerHub
/// story against a seeded project (ProjectForge.md §30). Demo Mode does not depend on
/// this — the browser has the same definition compiled in (§9A.2).
/// </summary>
public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/demo/journey", () => Results.Ok(CustomerHubJourney.Build()))
            .WithName("DemoJourney")
            .WithSummary("The seeded CustomerHub journey definition.");

        return routes;
    }
}
