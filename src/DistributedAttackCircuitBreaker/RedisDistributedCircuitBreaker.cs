using System.Globalization;
using StackExchange.Redis;

namespace DistributedAttackCircuitBreaker;

public sealed class RedisDistributedCircuitBreaker
{
    private readonly IDatabase _db;
    private readonly CircuitOptions _options;

    private string Prefix => $"security:circuit:{_options.Name}";
    private RedisKey EventsKey => $"{Prefix}:events";
    private RedisKey StateKey => $"{Prefix}:state";
    private RedisKey OpenedAtKey => $"{Prefix}:opened-at";
    private RedisKey HalfSuccessKey => $"{Prefix}:half-success";
    public RedisKey LockKey => $"{Prefix}:leader-lock";

    public RedisDistributedCircuitBreaker(
        IConnectionMultiplexer connection,
        CircuitOptions options)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);
        _db = connection.GetDatabase();
        _options = options;
    }

    public async Task<AttackResult> RegisterAttackAsync(
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)_options.Window.TotalMilliseconds;
        var member = $"{now}:{Guid.NewGuid():N}:{source ?? "unknown"}";

        var result = (RedisResult[])await _db.ScriptEvaluateAsync(
            RedisScripts.RecordAttack,
            [EventsKey, StateKey, OpenedAtKey, HalfSuccessKey],
            [
                member,
                now,
                windowStart,
                _options.AttackThreshold,
                (long)_options.EventRetention.TotalMilliseconds,
                (long)_options.OpenDuration.TotalMilliseconds,
                _options.HalfOpenSuccessThreshold,
                0
            ]).ConfigureAwait(false);

        return new AttackResult(
            Allowed: result[0].ToString() != CircuitState.Open.ToString(),
            State: ParseState(result[0]),
            AttackCount: (long)result[1],
            NowMilliseconds: (long)result[2]);
    }

    public async Task<bool> IsRequestAllowedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await GetStateAsync().ConfigureAwait(false);
        return state != CircuitState.Open;
    }

    public async Task<CircuitState> TryTransitionToHalfOpenAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var result = (RedisResult[])await _db.ScriptEvaluateAsync(
            RedisScripts.TryHalfOpen,
            [StateKey, OpenedAtKey, HalfSuccessKey],
            [now, (long)_options.OpenDuration.TotalMilliseconds])
            .ConfigureAwait(false);

        return ParseState(result[0]);
    }

    public async Task RecordProbeSuccessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)_options.Window.TotalMilliseconds;
        var member = $"probe-success:{now}:{Guid.NewGuid():N}";

        await _db.ScriptEvaluateAsync(
            RedisScripts.RecordAttack,
            [EventsKey, StateKey, OpenedAtKey, HalfSuccessKey],
            [member, now, windowStart, _options.AttackThreshold,
             (long)_options.EventRetention.TotalMilliseconds,
             (long)_options.OpenDuration.TotalMilliseconds,
             _options.HalfOpenSuccessThreshold, 1])
            .ConfigureAwait(false);
    }

    public async Task RecordProbeFailureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)_options.Window.TotalMilliseconds;
        var member = $"probe-failure:{now}:{Guid.NewGuid():N}";

        await _db.ScriptEvaluateAsync(
            RedisScripts.RecordAttack,
            [EventsKey, StateKey, OpenedAtKey, HalfSuccessKey],
            [member, now, windowStart, _options.AttackThreshold,
             (long)_options.EventRetention.TotalMilliseconds,
             (long)_options.OpenDuration.TotalMilliseconds,
             _options.HalfOpenSuccessThreshold, 2])
            .ConfigureAwait(false);
    }

    public async Task<CircuitSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)_options.Window.TotalMilliseconds;

        var result = (RedisResult[])await _db.ScriptEvaluateAsync(
            RedisScripts.GetSnapshot,
            [StateKey, OpenedAtKey, HalfSuccessKey, EventsKey],
            [windowStart]).ConfigureAwait(false);

        long? opened = long.TryParse(result[1].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var openedValue)
            ? openedValue : null;

        return new CircuitSnapshot(
            ParseState(result[0]),
            (long)result[3],
            opened,
            (long)result[2]);
    }

    public async Task<CircuitState> GetStateAsync()
    {
        var value = await _db.StringGetAsync(StateKey).ConfigureAwait(false);
        return value.HasValue ? ParseState(value) : CircuitState.Closed;
    }

    public async Task ResetAsync()
    {
        await _db.KeyDeleteAsync([EventsKey, StateKey, OpenedAtKey, HalfSuccessKey])
            .ConfigureAwait(false);
    }

    private static CircuitState ParseState(RedisResult value) =>
        ParseState(value.ToString());

    private static CircuitState ParseState(RedisValue value) =>
        value.HasValue
            ? ParseState(value.ToString())
            : CircuitState.Closed;

    private static CircuitState ParseState(string? value) =>
        Enum.TryParse<CircuitState>(value, true, out var state)
            ? state
            : CircuitState.Closed;

    private static void Validate(CircuitOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Name)) throw new ArgumentException("Name is required.");
        if (options.AttackThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(options.AttackThreshold));
        if (options.Window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.Window));
        if (options.OpenDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.OpenDuration));
        if (options.HalfOpenSuccessThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(options.HalfOpenSuccessThreshold));
    }
}
