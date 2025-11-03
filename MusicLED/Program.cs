using MusicLED;

CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

var audioProcessor = new AudioProcessor();

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    _cancellationTokenSource.Cancel();
};

try
{
    audioProcessor.Init();

    await Task.Delay(1000);

    Console.WriteLine("Starting audio monitoring...");
    Console.WriteLine("Press Ctrl+C to stop");

    var monitorTask = audioProcessor.MonitorAudio(_cancellationTokenSource.Token);

    await Task.WhenAll(monitorTask);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Shutdown requested.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Unexpected error: {ex.Message}");
}
finally
{
    audioProcessor.Dispose();
    Console.WriteLine("Shutting down...");
}


