using ForgeOps.AI;
using ForgeOps.AI.Ollama;
using ForgeOps.Contracts;

namespace ForgeOps.Api.Health;

/// <summary>Health surface (ProjectForge.md §8, §9A.1).</summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        // Liveness — deliberately lightweight (§8).
        routes.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Liveness")
            .ExcludeFromDescription();

        // Readiness — required config is present.
        routes.MapGet("/health/ready", (IConfiguration config) =>
        {
            var aiConfigured = !string.IsNullOrWhiteSpace(config["Ai:BaseUrl"]);
            return aiConfigured
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "not-ready", detail = "Ai:BaseUrl missing" }, statusCode: 503);
        }).WithName("Readiness");

        // AI Bridge — a fresh, bounded probe per request (§9A.1). Never a cached value.
        routes.MapGet("/health/ai-bridge", async (
            IAiBridgeProbe probe,
            AiBridgeStatusCache cache,
            AiTelemetry telemetry,
            CancellationToken cancellationToken) =>
        {
            var status = await probe.CheckAsync(cancellationToken);
            cache.Set(status);
            telemetry.SetBridgeUp(status.Up);
            return status.Up ? Results.Ok(status) : Results.Json(status, statusCode: 503);
        }).WithName("AiBridgeHealth");

        return routes;
    }
}
