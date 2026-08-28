using ForgeOps.AI;
using ForgeOps.Contracts.Api;
using ForgeOps.Contracts.Forge;
using ForgeOps.Forge;

namespace ForgeOps.Api.Endpoints;

/// <summary>
/// Live Mode: generate a candidate implementation from an approved specification, audit it
/// deterministically, and — if the audit allows and the runner is enabled — execute the
/// tests in the sandbox (ProjectForge.md §2, §10). The AI never decides to ship.
/// </summary>
public static class ForgeEndpoints
{
    public static IEndpointRouteBuilder MapForgeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/forge/run", async (
            ForgeRequest request,
            CodeGenerator generator,
            ForgePipeline pipeline,
            CancellationToken cancellationToken) =>
        {
            if (request.Specification.AcceptanceCriteria.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["specification"] = ["An approved specification with acceptance criteria is required."]
                });
            }

            try
            {
                var kind = request.Kind
                    ?? RequirementClassifier.Classify(request.RequirementText, request.Specification.Summary);

                var generation = kind == ImplementationKind.WebComponent
                    ? await generator.GenerateWebComponentAsync(request.RequirementText, request.Specification, cancellationToken)
                    : await generator.GenerateAsync(request.RequirementText, request.Specification,
                        maxRepairAttempts: 3, allowReferenceFallback: true, cancellationToken);

                var forge = await pipeline.RunAsync(generation.Implementation, request.Execute, cancellationToken);

                var result = forge with { Interaction = generation.Interaction };

                return Results.Ok(new ForgeResponse
                {
                    Result = result,
                    RunnerDisabled = !pipeline.RunnerAvailable
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
        .WithName("ForgeRun")
        .WithSummary("Generate, audit and sandbox-run an implementation for an approved specification.");

        // Execute an already-generated implementation (no new AI call) — used after a human
        // approves running the exact code they reviewed.
        routes.MapPost("/api/forge/execute", async (
            ExecuteImplementationRequest request,
            ForgePipeline pipeline,
            CancellationToken cancellationToken) =>
        {
            if (request.Implementation.Files.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["implementation"] = ["No implementation files were supplied."]
                });
            }

            var forge = await pipeline.RunAsync(request.Implementation, execute: true, cancellationToken);
            return Results.Ok(new ForgeResponse
            {
                Result = forge,
                RunnerDisabled = !pipeline.RunnerAvailable
            });
        })
        .WithName("ForgeExecute")
        .WithSummary("Audit and sandbox-run an already-generated implementation.");

        return routes;
    }
}
