using ForgeOps.Contracts;

namespace ForgeOps.Web.Services;

public enum BridgeConnection
{
    Unknown = 0,
    Connected = 1,
    Disconnected = 2
}

/// <summary>
/// Live Mode connection monitoring (ProjectForge.md §9A.1). Polls <c>/health/ai-bridge</c>
/// on an interval and applies hysteresis: N consecutive failures before "down", N
/// consecutive successes before "restored" — so a single dropped packet never flaps the
/// connection gate. Plain polling, not SignalR (§38).
/// </summary>
public sealed class AiBridgeMonitor : IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(7);
    private const int Hysteresis = 2;

    private readonly ForgeOpsApiClient _api;
    private readonly ForgeOpsWebOptions _options;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _consecutiveOk;
    private int _consecutiveFail;

    public AiBridgeMonitor(ForgeOpsApiClient api, ForgeOpsWebOptions options)
    {
        _api = api;
        _options = options;
    }

    public BridgeConnection Connection { get; private set; } = BridgeConnection.Unknown;

    public AiBridgeStatus LastStatus { get; private set; } = AiBridgeStatus.Offline("Not yet checked.");

    public event Action? Changed;

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        if (!_options.HasApi)
        {
            Connection = BridgeConnection.Disconnected;
            LastStatus = AiBridgeStatus.Offline("No API endpoint is configured for this deployment.");
            Changed?.Invoke();
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync();
        try
        {
            if (_loop is not null)
            {
                await _loop;
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loop = null;
            _consecutiveOk = 0;
            _consecutiveFail = 0;
            Connection = BridgeConnection.Unknown;
        }
    }

    /// <summary>Force an immediate check (the connection gate's "Retry" action).</summary>
    public async Task CheckNowAsync()
    {
        var status = await _api.GetBridgeHealthAsync();
        Apply(status);
    }

    /// <summary>
    /// A live AI call just failed with a bridge-unreachable result. Drop straight to the
    /// gate without waiting for the poll to accumulate hysteresis (§9A.1).
    /// </summary>
    public void ForceDisconnected(string detail)
    {
        LastStatus = AiBridgeStatus.Offline(detail);
        Connection = BridgeConnection.Disconnected;
        _consecutiveFail = Hysteresis;
        _consecutiveOk = 0;
        Changed?.Invoke();
    }

    private async Task RunAsync(CancellationToken token)
    {
        // First check immediately so the gate resolves fast on load.
        await SafeCheck(token);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(token))
        {
            await SafeCheck(token);
        }
    }

    private async Task SafeCheck(CancellationToken token)
    {
        try
        {
            var status = await _api.GetBridgeHealthAsync(token);
            Apply(status);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Apply(AiBridgeStatus.Offline("Health check failed."));
        }
    }

    private void Apply(AiBridgeStatus status)
    {
        LastStatus = status;
        var previous = Connection;

        if (status.Up)
        {
            _consecutiveOk++;
            _consecutiveFail = 0;
            if (_consecutiveOk >= Hysteresis || previous == BridgeConnection.Unknown)
            {
                Connection = BridgeConnection.Connected;
            }
        }
        else
        {
            _consecutiveFail++;
            _consecutiveOk = 0;
            if (_consecutiveFail >= Hysteresis || previous == BridgeConnection.Unknown)
            {
                Connection = BridgeConnection.Disconnected;
            }
        }

        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
