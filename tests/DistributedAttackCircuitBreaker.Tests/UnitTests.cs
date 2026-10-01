using System;
using DistributedAttackCircuitBreaker;
using Xunit;

namespace DistributedAttackCircuitBreaker.Tests;

public sealed class UnitTests
{
    [Fact]
    public void Defaults_are_suitable_for_ten_attacks_per_minute()
    {
        var options = new CircuitOptions();
        Assert.Equal(10, options.AttackThreshold);
        Assert.Equal(TimeSpan.FromSeconds(60), options.Window);
    }

    [Fact]
    public void State_enum_contains_expected_states()
    {
        Assert.Equal(0, (int)CircuitState.Closed);
        Assert.Equal(1, (int)CircuitState.Open);
        Assert.Equal(2, (int)CircuitState.HalfOpen);
    }
}
