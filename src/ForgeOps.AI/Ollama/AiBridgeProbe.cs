using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ForgeOps.Contracts;
using Microsoft.Extensions.Options;

namespace ForgeOps.AI.Ollama;

/// <summary>
/// Lightweight liveness probe for the AI Bridge (ProjectForge.md §9A.1). Has its own
/// short timeout so a hung bridge cannot hang <c>/health/ai-bridge</c>. Reflects a live
/// view, never a cached value.
/// </summary>
public interface IAiBridgeProbe
{
    Task<AiBridgeStatus> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed class OllamaBridgeProbe : IAiBridgeProbe
{
    public const string HttpClientName = "ollama-bridge-probe";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;

    public OllamaBridgeProbe(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<AiBridgeStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ProbeTimeoutSeconds)));

            var tags = await client.GetFromJsonAsync<OllamaTagsResponse>("api/tags", timeout.Token)
                .ConfigureAwait(false);
            stopwatch.Stop();

            var model = tags?.Models?
                .FirstOrDefault(m => string.Equals(m.Name, _options.Model, StringComparison.OrdinalIgnoreCase))?.Name
                ?? tags?.Models?.FirstOrDefault()?.Name;

            var modelPresent = tags?.Models?.Any(m =>
                string.Equals(m.Name, _options.Model, StringComparison.OrdinalIgnoreCase)) ?? false;

            return new AiBridgeStatus
            {
                Up = true,
                Model = model,
                CheckedAt = DateTimeOffset.UtcNow,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Detail = modelPresent
                    ? $"Bridge reachable; model {_options.Model} loaded."
                    : $"Bridge reachable, but model {_options.Model} is not pulled."
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return AiBridgeStatus.Offline($"AI Bridge did not respond within {_options.ProbeTimeoutSeconds}s.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            stopwatch.Stop();
            return AiBridgeStatus.Offline("AI Bridge is offline (developer machine or tunnel unreachable).");
        }
    }

    private sealed record OllamaTagsResponse
    {
        [JsonPropertyName("models")] public List<OllamaModel>? Models { get; init; }
    }

    private sealed record OllamaModel
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
    }
}
