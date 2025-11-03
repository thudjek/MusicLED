using System.Diagnostics;

namespace MusicLED;

public class AudioProcessor : IDisposable
{
    private Process _audioRecordProcess;
    private bool _isDisposing = false;

    private readonly BluetoothHandler _bluetoothHandler;
    private readonly FrequencyBands _frequencyBands;
    private readonly FFTAnalyzer _fftAnalyzer;
    private readonly LEDController _ledController;
    private readonly BeatDetector _beatDetector;
    private readonly SustainDetector _sustainDetector;
    private readonly List<short> _sampleBuffer = new List<short>(4096);

    public AudioProcessor()
    {
        _bluetoothHandler = new BluetoothHandler();
        _frequencyBands = new FrequencyBands();
        _fftAnalyzer = new FFTAnalyzer(48000, 1024);  // Reduced FFT size from 2048 to 1024 for lower latency
        _ledController = new LEDController();
        _beatDetector = new BeatDetector();
        _sustainDetector = new SustainDetector();
    }

    public void Init()
    {
        _bluetoothHandler.RunSetupSpeakerScript();
    }

    public async Task MonitorAudio(CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[2048];  // Reduced from 4096 to lower latency

            while (!cancellationToken.IsCancellationRequested)
            {
                var bluetoothDevice = await _bluetoothHandler.WaitForBluetoothDevice(cancellationToken);

                if (string.IsNullOrEmpty(bluetoothDevice))
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                Console.WriteLine($"Starting audio capture from: {bluetoothDevice}");

                _audioRecordProcess = StartAudioRecordProcess(bluetoothDevice);

                if (_audioRecordProcess == null)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                while (!cancellationToken.IsCancellationRequested && !_audioRecordProcess.HasExited && _bluetoothHandler.IsBluetoothDeviceStillConnected())
                {
                    try
                    {
                        var bytesRead = await _audioRecordProcess.StandardOutput.BaseStream.ReadAsync(buffer, cancellationToken);

                        if (bytesRead > 0)
                        {
                            ProcessAudioData(buffer, bytesRead);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in audio monitoring: {ex.Message}");
        }
    }

    private void ProcessAudioData(byte[] buffer, int bytesRead)
    {
        ConvertBytesToSamples(buffer, bytesRead);

        while (_sampleBuffer.Count >= 1024)  // Reduced from 2048
        {
            var samplesToAnalyze = _sampleBuffer.GetRange(0, 1024).ToArray();

            _fftAnalyzer.Analyze(samplesToAnalyze, _frequencyBands);

            bool isBeat = _beatDetector.DetectBeat(_frequencyBands.SmoothedBass);
            bool isSustained = _sustainDetector.IsSustained(_frequencyBands.SmoothedMid);

            _ledController.UpdateFromFrequencies(_frequencyBands, isBeat, isSustained);

            _sampleBuffer.RemoveRange(0, 512);  // Remove half for 50% overlap
        }
    }

    private void ConvertBytesToSamples(byte[] buffer, int bytesRead)
    {
        for (int i = 0; i < bytesRead - 3; i += 4)
        {
            short left = (short)(buffer[i] | (buffer[i + 1] << 8));
            short right = (short)(buffer[i + 2] | (buffer[i + 3] << 8));
            short mono = (short)((left + right) / 2);
            _sampleBuffer.Add(mono);
        }
    }

    private Process StartAudioRecordProcess(string deviceName)
    {
        try
        {
            StopAudioRecordProcess();

            // Use parec for monitoring sinks - it's designed for this and won't interfere with playback
            var startInfo = new ProcessStartInfo()
            {
                FileName = "parec",
                Arguments = $"--format=s16le --rate=48000 --channels=2 --device={deviceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process() { StartInfo = startInfo };
            process.Start();

            Thread.Sleep(100);

            if (process.HasExited)
            {
                Console.WriteLine($"❌ Failed to start parec for device: {deviceName}");
                return null;
            }

            Console.WriteLine($"✅ Started parec monitoring device: {deviceName}");
            return process;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error starting audio recording: {ex.Message}");
            return null;
        }
    }

    private void StopAudioRecordProcess()
    {
        if (_audioRecordProcess != null)
        {
            try
            {
                if (!_audioRecordProcess.HasExited)
                {
                    _audioRecordProcess.Kill();
                    _audioRecordProcess.WaitForExit(1000);
                }
                _audioRecordProcess.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping audio recording: {ex.Message}");
            }
            finally
            {
                _audioRecordProcess = null;
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposing)
        {
            return;
        }

        _isDisposing = true;
        GC.SuppressFinalize(this);

        StopAudioRecordProcess();
        _bluetoothHandler.RunResetSpeakerScript();
        _ledController.Dispose();
    }
}