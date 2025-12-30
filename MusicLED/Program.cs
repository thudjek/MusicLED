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

// Bluetooth speaker management endpoints
var bluetoothHandler = app.Services.GetRequiredService<BluetoothHandler>();

app.MapPost("/bluetooth/scan/start", async () =>
{
    var started = await bluetoothHandler.StartScanningAsync();
    return started ? Results.Ok() : Results.BadRequest("Scanning already in progress");
});

app.MapPost("/bluetooth/scan/stop", async () =>
{
    await bluetoothHandler.StopScanningAsync();
    return Results.Ok();
});

app.MapGet("/bluetooth/devices", async () =>
{
    var groups = await bluetoothHandler.GetDeviceGroupsAsync();
    groups.IsScanning = bluetoothHandler.IsScanning;
    return Results.Ok(groups);
});

app.MapGet("/bluetooth/paired", async () =>
{
    var devices = await bluetoothHandler.GetPairedDevicesAsync();
    return Results.Ok(devices);
});

app.MapGet("/bluetooth/speaker-status", async () =>
{
    var status = await bluetoothHandler.GetOutputSpeakerStatusAsync();
    return Results.Ok(status);
});

app.MapPost("/bluetooth/connect", async ([FromBody] BluetoothConnectRequest request) =>
{
    var success = await bluetoothHandler.ConnectToSpeakerAsync(request.MacAddress);
    return success ? Results.Ok() : Results.BadRequest("Failed to connect to speaker");
});

app.MapPost("/bluetooth/disconnect", async () =>
{
    var success = await bluetoothHandler.DisconnectSpeakerAsync();
    return success ? Results.Ok() : Results.BadRequest("Failed to disconnect");
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
