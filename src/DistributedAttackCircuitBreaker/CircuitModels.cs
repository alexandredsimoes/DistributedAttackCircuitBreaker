namespace DistributedAttackCircuitBreaker;

public enum CircuitState
{
    Closed = 0,
    Open = 1,
    HalfOpen = 2
}

public sealed record CircuitOptions
{
    public string Name { get; set; } = "attack-protection";
    public int AttackThreshold { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int HalfOpenSuccessThreshold { get; set; } = 3;
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan EventRetention { get; set; } = TimeSpan.FromMinutes(5);
}

public sealed record AttackResult(
    bool Allowed,
    CircuitState State,
    long AttackCount,
    long NowMilliseconds);

public sealed record CircuitSnapshot(
    CircuitState State,
    long AttackCount,
    long? OpenedAtMilliseconds,
    long HalfOpenSuccesses);

public sealed class CircuitOpenException(string circuitName)
    : Exception($"Distributed circuit '{circuitName}' is open.");
