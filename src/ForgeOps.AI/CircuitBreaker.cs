namespace ForgeOps.AI;

/// <summary>
/// Minimal in-process circuit breaker for the AI Bridge (ProjectForge.md §7.2, §48 — no
/// dependency added where a small deterministic component does the job). Not distributed;
/// the free-tier API runs as a single instance.
/// </summary>
public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetAfter;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;
    private bool _open;

    public CircuitBreaker(int failureThreshold, TimeSpan resetAfter, TimeProvider? time = null)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
        _resetAfter = resetAfter;
        _time = time ?? TimeProvider.System;
    }

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                if (_open && _time.GetUtcNow() - _openedAt >= _resetAfter)
                {
                    // Half-open: allow the next call through as a trial.
                    _open = false;
                }

                return _open;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _open = false;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _failureThreshold)
            {
                _open = true;
                _openedAt = _time.GetUtcNow();
            }
        }
    }
}
