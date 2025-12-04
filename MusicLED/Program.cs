using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MusicLED;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddSingleton<LEDController>();
builder.Services.AddSingleton<BluetoothHandler>();
builder.Services.AddSingleton<FFTAnalyzer>();
builder.Services.AddSingleton<AudioProcessor>();
builder.Services.AddSingleton<ModeManager>();

builder.Logging.ClearProviders();
var app = builder.Build();

app.UseStaticFiles();

var modeManager = app.Services.GetRequiredService<ModeManager>();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

app.MapGet("/status", () =>
{
    return Results.Ok(modeManager.CurrentMode);
});

app.MapPost("/mode", ([FromBody] SetModeRequest request) =>
{
    modeManager.SetMode(request);
    return Results.Ok();
});

var appTask = app.RunAsync("http://0.0.0.0:5000");
Console.WriteLine("Web API started on http://0.0.0.0:5000");
Console.WriteLine("Starting LED controller...");
Console.WriteLine("Press Ctrl+C to stop");

try
{
    await modeManager.RunAsync(lifetime.ApplicationStopping);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Shutdown requested.");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
finally
{
    Console.WriteLine("Shutting down...");
    await app.StopAsync();
    await appTask;
}
