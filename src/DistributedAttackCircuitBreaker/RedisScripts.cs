namespace DistributedAttackCircuitBreaker;

internal static class RedisScripts
{
    // KEYS[1] events sorted set
    // KEYS[2] state key
    // KEYS[3] opened-at key
    // KEYS[4] half-open successes key
    // ARGV[1] event member
    // ARGV[2] now ms
    // ARGV[3] window start ms
    // ARGV[4] threshold
    // ARGV[5] event retention ms
    // ARGV[6] open duration ms
    // ARGV[7] half-open success threshold
    // ARGV[8] mode: 0=normal attack, 1=probe success, 2=probe failure
    public const string RecordAttack = """
local events = KEYS[1]
local stateKey = KEYS[2]
local openedAtKey = KEYS[3]
local halfSuccessKey = KEYS[4]

local member = ARGV[1]
local now = tonumber(ARGV[2])
local windowStart = tonumber(ARGV[3])
local threshold = tonumber(ARGV[4])
local retention = tonumber(ARGV[5])
local openDuration = tonumber(ARGV[6])
local halfThreshold = tonumber(ARGV[7])
local mode = tonumber(ARGV[8])

redis.call('ZADD', events, now, member)
redis.call('ZREMRANGEBYSCORE', events, '-inf', now - retention)

local count = redis.call('ZCOUNT', events, windowStart, '+inf')
local state = redis.call('GET', stateKey) or 'Closed'

if mode == 2 then
    state = 'Open'
    redis.call('SET', stateKey, state)
    redis.call('SET', openedAtKey, now)
    redis.call('DEL', halfSuccessKey)
elseif mode == 1 then
    if state == 'HalfOpen' then
        local successes = redis.call('INCR', halfSuccessKey)
        if successes >= halfThreshold then
            state = 'Closed'
            redis.call('SET', stateKey, state)
            redis.call('DEL', openedAtKey)
            redis.call('DEL', halfSuccessKey)
        end
    end
elseif state == 'Closed' and count >= threshold then
    state = 'Open'
    redis.call('SET', stateKey, state)
    redis.call('SET', openedAtKey, now)
    redis.call('DEL', halfSuccessKey)
end

return {state, count, now}
""";

    public const string TryHalfOpen = """
local stateKey = KEYS[1]
local openedAtKey = KEYS[2]
local halfSuccessKey = KEYS[3]

local now = tonumber(ARGV[1])
local openDuration = tonumber(ARGV[2])

local state = redis.call('GET', stateKey) or 'Closed'
local openedAt = tonumber(redis.call('GET', openedAtKey) or '0')

if state ~= 'Open' then
    return {state, 0}
end

if now - openedAt < openDuration then
    return {'Open', 0}
end

redis.call('SET', stateKey, 'HalfOpen')
redis.call('SET', halfSuccessKey, '0')
return {'HalfOpen', 1}
""";

    public const string GetSnapshot = """
local state = redis.call('GET', KEYS[1]) or 'Closed'
local opened = redis.call('GET', KEYS[2]) or ''
local successes = redis.call('GET', KEYS[3]) or '0'
local count = redis.call('ZCOUNT', KEYS[4], ARGV[1], '+inf')
return {state, opened, successes, count}
""";

    public const string AcquireLock = """
if redis.call('SET', KEYS[1], ARGV[1], 'NX', 'PX', ARGV[2]) then
    return 1
end
return 0
""";

    public const string RenewLock = """
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('PEXPIRE', KEYS[1], ARGV[2])
end
return 0
""";

    public const string ReleaseLock = """
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
end
return 0
""";
}
