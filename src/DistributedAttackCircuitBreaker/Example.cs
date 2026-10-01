/*
Example registration in Program.cs:

builder.Services.AddDistributedAttackCircuitBreaker(
    builder.Configuration.GetConnectionString("Redis")!,
    options =>
    {
        options.Name = "api-attack-protection";
        options.AttackThreshold = 10;
        options.Window = TimeSpan.FromSeconds(60);
        options.OpenDuration = TimeSpan.FromSeconds(30);
        options.HalfOpenSuccessThreshold = 3;
        options.LockDuration = TimeSpan.FromSeconds(10);
        options.CheckInterval = TimeSpan.FromSeconds(2);
    });

Example middleware/endpoint:

app.MapPost("/api/resource", async (
    RedisDistributedCircuitBreaker circuit,
    CancellationToken ct) =>
{
    var result = await circuit.RegisterAttackAsync("client-id", ct);
    if (!result.Allowed)
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);

    return Results.Ok();
});

IMPORTANT:
RegisterAttackAsync is intended for a security signal, not every ordinary request.
If your definition is "10 suspicious requests in 60 seconds", call it only when
an attack/suspicious request has been identified.
*/
