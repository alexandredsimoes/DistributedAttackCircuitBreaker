using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace DistributedAttackCircuitBreaker;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedAttackCircuitBreaker(
        this IServiceCollection services,
        string redisConnectionString,
        Action<CircuitOptions>? configure = null)
    {
        var options = new CircuitOptions();
        configure?.Invoke(options);

        var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton(options);
        services.AddSingleton(sp => new RedisDistributedCircuitBreaker(
            sp.GetRequiredService<IConnectionMultiplexer>(), options));
        services.AddSingleton(sp => new RedisDistributedLock(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<RedisDistributedCircuitBreaker>().LockKey,
            options.LockDuration));
        services.AddHostedService<CircuitLeaderBackgroundService>();
        return services;
    }
}
