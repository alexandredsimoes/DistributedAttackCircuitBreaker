using DistributedAttackCircuitBreaker;

var builder = WebApplication.CreateBuilder(args);

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

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
