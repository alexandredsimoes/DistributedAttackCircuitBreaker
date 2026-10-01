using StackExchange.Redis;

namespace DistributedAttackCircuitBreaker;

public sealed class RedisDistributedLock
{
    private readonly IDatabase _db;
    private readonly RedisKey _key;
    private readonly TimeSpan _lease;

    public RedisDistributedLock(IConnectionMultiplexer connection, RedisKey key, TimeSpan lease)
    {
        _db = connection.GetDatabase();
        _key = key;
        _lease = lease;
    }

    public async Task<Lease?> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = Guid.NewGuid().ToString("N");
        var acquired = (long)await _db.ScriptEvaluateAsync(
            RedisScripts.AcquireLock,
            [_key],
            [token, (long)_lease.TotalMilliseconds]).ConfigureAwait(false);

        return acquired == 1 ? new Lease(_db, _key, token, _lease) : null;
    }

    public sealed class Lease : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly RedisKey _key;
        private readonly string _token;
        private readonly TimeSpan _duration;
        private int _released;

        internal Lease(IDatabase db, RedisKey key, string token, TimeSpan duration)
        {
            _db = db; _key = key; _token = token; _duration = duration;
        }

        public async Task<bool> RenewAsync()
        {
            if (Volatile.Read(ref _released) != 0) return false;
            var result = (long)await _db.ScriptEvaluateAsync(
                RedisScripts.RenewLock,
                [_key],
                [_token, (long)_duration.TotalMilliseconds]).ConfigureAwait(false);
            return result == 1;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            await _db.ScriptEvaluateAsync(RedisScripts.ReleaseLock, [_key], [_token]).ConfigureAwait(false);
        }
    }
}
