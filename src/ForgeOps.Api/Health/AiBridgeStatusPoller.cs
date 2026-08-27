using ForgeOps.AI;
using ForgeOps.AI.Ollama;
using ForgeOps.Contracts;

namespace ForgeOps.Api.Health;

/// <summary>Holds the most recent AI Bridge probe result for diagnostics surfaces.</summary>
public sealed class AiBridgeStatusCache
{
    private volatile AiBridgeStatus _status = AiBridgeStatus.Offline("Not yet probed.");

    public AiBridgeStatus Current => _status;

    public void Set(AiBridgeStatus status) => _status = status;
}

/// <summary>
/// Continuously tracks AI Bridge reachability as an observable signal (ProjectForge.md
/// §7.2). Feeds the <c>forgeops_ai_bridge_up</c> metric; the browser's own polling of
/// <c>/health/ai-bridge</c> (§9A.1) is separate and does a fresh probe per request.
/// </summary>
public sealed class AiBridgeStatusPoller : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly IAiBridgeProbe _probe;
    private readonly AiBridgeStatusCache _cache;
    private readonly AiTelemetry _telemetry;
    private readonly ILogger<AiBridgeStatusPoller> _logger;

    public AiBridgeStatusPoller(
        IAiBridgeProbe probe,
        AiBridgeStatusCache cache,
        AiTelemetry telemetry,
        ILogger<AiBridgeStatusPoller> logger)
    {
        _probe = probe;
        _cache = cache;
        _telemetry = telemetry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var status = await _probe.CheckAsync(stoppingToken).ConfigureAwait(false);
                _cache.Set(status);
                _telemetry.SetBridgeUp(status.Up);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI Bridge poll failed unexpectedly.");
                _telemetry.SetBridgeUp(false);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
