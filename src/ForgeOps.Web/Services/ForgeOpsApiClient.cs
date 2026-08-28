using System.Net;
using System.Net.Http.Json;
using ForgeOps.Contracts;
using ForgeOps.Contracts.Api;
using ForgeOps.Contracts.Ai;
using ForgeOps.Contracts.Forge;

namespace ForgeOps.Web.Services;

/// <summary>Typed access to <c>ForgeOps.Api</c>. Used only in Live Mode.</summary>
public sealed class ForgeOpsApiClient
{
    private readonly HttpClient _http;

    public ForgeOpsApiClient(HttpClient http) => _http = http;

    /// <summary>
    /// Probe the AI Bridge (ProjectForge.md §9A.1). Returns a status either way — a 503
    /// carries an offline <see cref="AiBridgeStatus"/> body; a transport failure becomes
    /// a synthetic offline status so the caller never has to catch here.
    /// </summary>
    public async Task<AiBridgeStatus> GetBridgeHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("health/ai-bridge", cancellationToken);
            var status = await response.Content.ReadFromJsonAsync<AiBridgeStatus>(cancellationToken);
            return status ?? AiBridgeStatus.Offline("Empty response from /health/ai-bridge.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return AiBridgeStatus.Offline("API unreachable (free-tier host may be cold-starting).");
        }
    }

    public async Task<SpecificationCallResult> GenerateSpecificationAsync(
        string requirementText,
        string? projectName,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/requirements/generate-specification",
            new GenerateSpecificationRequest { RequirementText = requirementText, ProjectName = projectName },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<GenerateSpecificationResponse>(cancellationToken);
            return body is null
                ? SpecificationCallResult.Failure("model-error", "The API returned an empty response.")
                : SpecificationCallResult.Success(body);
        }

        // Distinguish "bridge offline" from a model error (§45) using the problem-details reason.
        var reason = "model-error";
        var detail = $"HTTP {(int)response.StatusCode}";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken);
            if (problem is not null)
            {
                reason = problem.Reason ?? reason;
                detail = problem.Detail ?? problem.Title ?? detail;
            }
        }
        catch
        {
            // keep defaults
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            reason = reason is "circuit-open" ? reason : "bridge-unreachable";
        }

        return SpecificationCallResult.Failure(reason, detail);
    }

    /// <summary>Generate + audit a candidate implementation (no execution).</summary>
    public Task<ForgeCallResult> ForgeGenerateAsync(
        string requirementText, SpecificationDraft spec, string? projectName,
        ImplementationKind? kind = null, CancellationToken ct = default) =>
        PostForgeAsync("api/forge/run",
            new ForgeRequest { RequirementText = requirementText, Specification = spec, ProjectName = projectName, Kind = kind, Execute = false }, ct);

    /// <summary>Audit + sandbox-run an already-generated implementation (no new AI call).</summary>
    public Task<ForgeCallResult> ForgeExecuteAsync(GeneratedImplementation implementation, CancellationToken ct = default) =>
        PostForgeAsync("api/forge/execute",
            new ExecuteImplementationRequest { Implementation = implementation }, ct);

    private async Task<ForgeCallResult> PostForgeAsync(string path, object body, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(path, body, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ForgeCallResult.Failure("bridge-unreachable", "API unreachable (free-tier host may be cold-starting).");
        }

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<ForgeResponse>(ct);
            return payload is null
                ? ForgeCallResult.Failure("model-error", "The API returned an empty response.")
                : ForgeCallResult.Success(payload);
        }

        var reason = "model-error";
        var detail = $"HTTP {(int)response.StatusCode}";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(ct);
            if (problem is not null)
            {
                reason = problem.Reason ?? reason;
                detail = problem.Detail ?? problem.Title ?? detail;
            }
        }
        catch { /* keep defaults */ }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable && reason == "model-error")
        {
            reason = "bridge-unreachable";
        }

        return ForgeCallResult.Failure(reason, detail);
    }

    private sealed record ApiProblem
    {
        public string? Title { get; init; }
        public string? Detail { get; init; }
        public string? Reason { get; init; }
    }
}

public sealed record ForgeCallResult
{
    public bool Ok { get; init; }
    public ForgeResponse? Response { get; init; }
    public string? FailureReason { get; init; }
    public string? FailureDetail { get; init; }

    public bool IsBridgeOffline => FailureReason is "bridge-unreachable" or "circuit-open";

    public static ForgeCallResult Success(ForgeResponse response) => new() { Ok = true, Response = response };
    public static ForgeCallResult Failure(string reason, string detail) =>
        new() { Ok = false, FailureReason = reason, FailureDetail = detail };
}

public sealed record SpecificationCallResult
{
    public bool Ok { get; init; }
    public GenerateSpecificationResponse? Response { get; init; }
    public string? FailureReason { get; init; }
    public string? FailureDetail { get; init; }

    /// <summary>True when the failure means the AI Bridge is offline — route to the connection gate.</summary>
    public bool IsBridgeOffline => FailureReason is "bridge-unreachable" or "circuit-open";

    public static SpecificationCallResult Success(GenerateSpecificationResponse response) =>
        new() { Ok = true, Response = response };

    public static SpecificationCallResult Failure(string reason, string detail) =>
        new() { Ok = false, FailureReason = reason, FailureDetail = detail };
}
