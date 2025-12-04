namespace MusicLED;

public class ModeManager
{
    private readonly AudioProcessor _audioProcessor;
    private readonly LEDController _ledController;

    private readonly SetModeRequest _currentModeRequest = new();

    private CancellationTokenSource _modeCts = new();

    public SetModeRequest CurrentMode => _currentModeRequest;

    public ModeManager(AudioProcessor audioProcessor, LEDController ledController)
    {
        _audioProcessor = audioProcessor;
        _ledController = ledController;
    }

    public void SetMode(SetModeRequest request)
    {
        var previousMode = _currentModeRequest.Mode;
        _currentModeRequest.Mode = request.Mode;
        _currentModeRequest.FixedColors = request.FixedColors ?? [];
        _currentModeRequest.NumOfRunningLeds = request.NumOfRunningLeds;
        _currentModeRequest.RunningDelay = request.RunningDelay;
        _currentModeRequest.TransitionSteps = request.TransitionSteps;
        _currentModeRequest.TransitionDelay = request.TransitionDelay;

        _modeCts.Cancel();
        _modeCts = new CancellationTokenSource();

        if (previousMode == LedMode.Music)
        {
            _audioProcessor.StopAudioRecordProcess();
        }

        Console.WriteLine($"Mode changed: {previousMode} -> {request.Mode}");
    }

    public async Task RunAsync(CancellationToken appCancellationToken)
    {
        while (!appCancellationToken.IsCancellationRequested)
        {
            // Link app cancellation with mode cancellation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(appCancellationToken, _modeCts.Token);

            try
            {
                switch (_currentModeRequest.Mode)
                {
                    case LedMode.Off:
                        _ledController.Clear();
                        await WaitForModeChange(linkedCts.Token);
                        break;

                    case LedMode.Fixed:
                        _ledController.SetFixedColors(_currentModeRequest.FixedColors);
                        await WaitForModeChange(linkedCts.Token);
                        break;

                    case LedMode.Running:
                        _ledController.RunningAnimation(_currentModeRequest.NumOfRunningLeds, _currentModeRequest.RunningDelay, linkedCts.Token);
                        break;

                    case LedMode.Smooth:
                        _ledController.SmoothAnimation(_currentModeRequest.TransitionSteps, _currentModeRequest.TransitionDelay, linkedCts.Token);
                        break;

                    case LedMode.Music:
                        await _audioProcessor.MonitorAudio(linkedCts.Token);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task WaitForModeChange(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }
}
