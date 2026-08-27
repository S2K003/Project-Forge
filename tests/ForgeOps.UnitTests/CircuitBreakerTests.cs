using ForgeOps.AI;

namespace ForgeOps.UnitTests;

public sealed class CircuitBreakerTests
{
    [Fact]
    public void Opens_after_threshold_consecutive_failures()
    {
        var breaker = new CircuitBreaker(failureThreshold: 3, resetAfter: TimeSpan.FromSeconds(30));

        breaker.RecordFailure();
        breaker.RecordFailure();
        Assert.False(breaker.IsOpen);

        breaker.RecordFailure();
        Assert.True(breaker.IsOpen);
    }

    [Fact]
    public void Success_resets_the_failure_count()
    {
        var breaker = new CircuitBreaker(failureThreshold: 2, resetAfter: TimeSpan.FromSeconds(30));

        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure();

        Assert.False(breaker.IsOpen);
    }

    [Fact]
    public void Half_opens_after_the_reset_window()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var breaker = new CircuitBreaker(failureThreshold: 1, resetAfter: TimeSpan.FromSeconds(10), time);

        breaker.RecordFailure();
        Assert.True(breaker.IsOpen);

        time.Advance(TimeSpan.FromSeconds(11));
        Assert.False(breaker.IsOpen);
    }

    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
