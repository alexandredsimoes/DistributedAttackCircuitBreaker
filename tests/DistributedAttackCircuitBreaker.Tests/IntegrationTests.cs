using System.Linq;
using System.Threading.Tasks;
using DistributedAttackCircuitBreaker;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;
using Guid = System.Guid;
using TimeSpan = System.TimeSpan;

namespace DistributedAttackCircuitBreaker.Tests;

public sealed class IntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _connection = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private RedisDistributedCircuitBreaker Create(string name, int threshold = 10, int openSeconds = 30)
        => new(_connection, new CircuitOptions
        {
            Name = name,
            AttackThreshold = threshold,
            Window = TimeSpan.FromSeconds(60),
            OpenDuration = TimeSpan.FromSeconds(openSeconds),
            HalfOpenSuccessThreshold = 3
        });

    [Fact]
    public async Task Ten_attacks_open_the_circuit()
    {
        var circuit = Create(Guid.NewGuid().ToString("N"), threshold: 10);

        for (var i = 0; i < 9; i++)
        {
            var result = await circuit.RegisterAttackAsync("test");
            Assert.NotEqual(CircuitState.Open, result.State);
        }

        var tenth = await circuit.RegisterAttackAsync("test");

        Assert.Equal(CircuitState.Open, tenth.State);
        Assert.False(await circuit.IsRequestAllowedAsync());
    }

    [Fact]
    public async Task Two_instances_share_the_same_circuit_state()
    {
        var name = Guid.NewGuid().ToString("N");
        var first = Create(name, threshold: 10);
        var second = Create(name, threshold: 10);

        await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => first.RegisterAttackAsync("one")));
        await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => second.RegisterAttackAsync("two")));

        var snapshot = await first.GetSnapshotAsync();
        Assert.Equal(CircuitState.Open, snapshot.State);
        Assert.True(snapshot.AttackCount >= 10);
    }

    [Fact]
    public async Task Sliding_window_removes_old_events()
    {
        var circuit = new RedisDistributedCircuitBreaker(
            _connection,
            new CircuitOptions
            {
                Name = Guid.NewGuid().ToString("N"),
                AttackThreshold = 100,
                Window = TimeSpan.FromMilliseconds(100),
                EventRetention = TimeSpan.FromSeconds(2)
            });

        await circuit.RegisterAttackAsync();
        await Task.Delay(150);

        var snapshot = await circuit.GetSnapshotAsync();
        Assert.Equal(0, snapshot.AttackCount);
    }

    [Fact]
    public async Task Distributed_lock_allows_only_one_owner()
    {
        var name = Guid.NewGuid().ToString("N");
        var circuit = Create(name);
        var first = new RedisDistributedLock(_connection, circuit.LockKey, TimeSpan.FromSeconds(5));
        var second = new RedisDistributedLock(_connection, circuit.LockKey, TimeSpan.FromSeconds(5));

        var leases = await Task.WhenAll(
            first.TryAcquireAsync(),
            second.TryAcquireAsync());

        Assert.Equal(1, leases.Count(x => x is not null));

        foreach (var lease in leases.Where(x => x is not null))
            await lease!.DisposeAsync();
    }
}
