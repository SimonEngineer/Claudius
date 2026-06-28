using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Scheduling;

public class ModelCircuitBreakerTests
{
    private static ModelCircuitBreaker MakeBreaker(int threshold = 3, TimeSpan? cooldown = null) =>
        new(Options.Create(new SchedulerOptions
        {
            CircuitBreakerFailureThreshold = threshold,
            CircuitBreakerCooldown = cooldown ?? TimeSpan.FromMinutes(1),
        }));

    [Fact]
    public void IsOpen_ReturnsFalse_ForUnknownModel()
    {
        var breaker = MakeBreaker();
        Assert.False(breaker.IsOpen("some-model"));
    }

    [Fact]
    public void IsOpen_ReturnsFalse_BelowFailureThreshold()
    {
        var breaker = MakeBreaker(threshold: 3);
        breaker.RecordFailure("m");
        breaker.RecordFailure("m");

        Assert.False(breaker.IsOpen("m"));
    }

    [Fact]
    public void IsOpen_ReturnsTrue_OnceFailureThresholdReached()
    {
        var breaker = MakeBreaker(threshold: 3);
        breaker.RecordFailure("m");
        breaker.RecordFailure("m");
        breaker.RecordFailure("m");

        Assert.True(breaker.IsOpen("m"));
        Assert.NotNull(breaker.OpenUntil("m"));
    }

    [Fact]
    public void IsOpen_ReturnsFalse_AfterCooldownElapses()
    {
        var breaker = MakeBreaker(threshold: 1, cooldown: TimeSpan.FromMilliseconds(-1));
        breaker.RecordFailure("m");

        Assert.False(breaker.IsOpen("m"));
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCount_SoBreakerStaysClosed()
    {
        var breaker = MakeBreaker(threshold: 2);
        breaker.RecordFailure("m");
        breaker.RecordSuccess("m");
        breaker.RecordFailure("m");

        Assert.False(breaker.IsOpen("m"));
    }

    [Fact]
    public void Models_AreTrackedIndependently()
    {
        var breaker = MakeBreaker(threshold: 1);
        breaker.RecordFailure("model-a");

        Assert.True(breaker.IsOpen("model-a"));
        Assert.False(breaker.IsOpen("model-b"));
    }
}
