using System.Diagnostics;
using System.Drawing;

namespace MusicLED;

public class AudioProcessor : IDisposable
{
    private Process _audioRecordProcess;

    private readonly BluetoothHandler _bluetoothHandler;
    private readonly FrequencyBands _frequencyBands;
    private readonly FFTAnalyzer _fftAnalyzer;
    private readonly LEDController _ledController;
    private readonly List<short> _sampleBuffer = new List<short>(8192);

    public AudioProcessor(
        BluetoothHandler bluetoothHandler,
        FFTAnalyzer fftAnalyzer,
        LEDController ledController)
    {
        _bluetoothHandler = bluetoothHandler;
        _fftAnalyzer = fftAnalyzer;
        _ledController = ledController;
        _frequencyBands = new FrequencyBands();
    }

    public async Task MonitorAudio(CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[8192];

            _ledController.SetSingleLED(0, Color.Blue);

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

        while (_sampleBuffer.Count >= 2048)
        {
            var samplesToAnalyze = _sampleBuffer.GetRange(0, 2048).ToArray();

            _fftAnalyzer.Analyze(samplesToAnalyze, _frequencyBands);

            _ledController.UpdateFromFrequencies(_frequencyBands);

            _sampleBuffer.RemoveRange(0, 1024);
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

            var startInfo = new ProcessStartInfo()
            {
                FileName = "parec",
                Arguments = $"--format=s16le --rate=48000 --channels=2 --latency-msec=20 --device={deviceName}",
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

    public void StopAudioRecordProcess()
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
        GC.SuppressFinalize(this);
        StopAudioRecordProcess();
    }
}