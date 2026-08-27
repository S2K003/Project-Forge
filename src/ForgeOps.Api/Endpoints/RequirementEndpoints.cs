using ForgeOps.AI;
using ForgeOps.Contracts.Api;

namespace ForgeOps.Api.Endpoints;

/// <summary>
/// Live Mode requirement → specification (ProjectForge.md §32, Phase 3). Real AI Bridge
/// call to qwen3:8b through the gateway. Bridge-unreachable is surfaced distinctly from a
/// model/timeout failure (§45) so the frontend can route to the connection gate.
/// </summary>
public static class RequirementEndpoints
{
    public static IEndpointRouteBuilder MapRequirementEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/requirements/generate-specification", async (
            GenerateSpecificationRequest request,
            AiGateway gateway,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.RequirementText))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["requirementText"] = ["Requirement text is required."]
                });
            }

            try
            {
                var result = await gateway.GenerateSpecificationAsync(
                    request.RequirementText, request.ProjectName, cancellationToken);

                if (result.Draft is null)
                {
                    // AI answered, but the structured output failed deterministic validation (§9.2).
                    return Results.Problem(
                        title: "AI output rejected",
                        detail: "The model response did not pass specification validation. See the interaction record.",
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        extensions: new Dictionary<string, object?> { ["interaction"] = result.Interaction });
                }

                return Results.Ok(new GenerateSpecificationResponse
                {
                    Draft = result.Draft,
                    Interaction = result.Interaction
                });
            }
            catch (AiBridgeUnreachableException ex)
            {
                return Results.Problem(
                    title: "AI Bridge offline",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    extensions: new Dictionary<string, object?> { ["reason"] = "bridge-unreachable" });
            }
            catch (AiCircuitOpenException ex)
            {
                return Results.Problem(
                    title: "AI Bridge offline",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    extensions: new Dictionary<string, object?> { ["reason"] = "circuit-open" });
            }
            catch (AiModelException ex)
            {
                return Results.Problem(
                    title: "AI model error",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway,
                    extensions: new Dictionary<string, object?> { ["reason"] = "model-error" });
            }
        })
        .WithName("GenerateSpecification")
        .WithSummary("Generate a specification draft from a raw requirement using the AI Bridge.");

        return routes;
    }
}
