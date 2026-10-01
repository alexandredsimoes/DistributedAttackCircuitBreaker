using Microsoft.Extensions.Hosting;

namespace DistributedAttackCircuitBreaker;

public sealed class CircuitLeaderBackgroundService : BackgroundService
{
    private readonly RedisDistributedCircuitBreaker _circuit;
    private readonly RedisDistributedLock _lock;
    private readonly CircuitOptions _options;

    public CircuitLeaderBackgroundService(
        RedisDistributedCircuitBreaker circuit,
        RedisDistributedLock distributedLock,
        CircuitOptions options)
    {
        _circuit = circuit;
        _lock = distributedLock;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await CheckOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    public async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        await using var lease = await _lock.TryAcquireAsync(cancellationToken).ConfigureAwait(false);
        if (lease is null) return;

        var state = await _circuit.GetStateAsync().ConfigureAwait(false);
        if (state != CircuitState.Open) return;

        // Apenas o líder executa esta transição.
        await _circuit.TryTransitionToHalfOpenAsync(cancellationToken).ConfigureAwait(false);
    }
}
