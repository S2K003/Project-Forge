using System.Diagnostics.Metrics;

namespace ForgeOps.AI;

/// <summary>
/// AI observability signals (ProjectForge.md §21, §7.2 — bridge reachability is itself
/// telemetry). Registered as a meter so any OpenTelemetry exporter can pick them up.
/// </summary>
public sealed class AiTelemetry : IDisposable
{
    public const string MeterName = "ForgeOps.AI";

    private readonly Meter _meter;
    private readonly Counter<long> _requests;
    private readonly Counter<long> _failures;
    private readonly Histogram<double> _duration;

    private int _bridgeUp;

    public AiTelemetry(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _requests = _meter.CreateCounter<long>("forgeops_ai_requests_total");
        _failures = _meter.CreateCounter<long>("forgeops_ai_request_failures_total");
        _duration = _meter.CreateHistogram<double>("forgeops_ai_request_duration", unit: "ms");

        _meter.CreateObservableGauge("forgeops_ai_bridge_up", () => _bridgeUp);
    }

    public void RecordRequest(string operation, long durationMs, bool success)
    {
        _requests.Add(1, new KeyValuePair<string, object?>("operation", operation));
        _duration.Record(durationMs, new KeyValuePair<string, object?>("operation", operation));
        if (!success)
        {
            _failures.Add(1, new KeyValuePair<string, object?>("operation", operation));
        }
    }

    public void SetBridgeUp(bool up) => Interlocked.Exchange(ref _bridgeUp, up ? 1 : 0);

    public void Dispose() => _meter.Dispose();
}
