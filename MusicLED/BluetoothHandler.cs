using System.Diagnostics;

namespace MusicLED;

public class BluetoothHandler
{
    private readonly string SETUP_SCRIPT_PATH;
    private readonly string RESET_SCRIPT_PATH;

    private string currentBluetoothDevice = null;

    public BluetoothHandler()
    {
        SETUP_SCRIPT_PATH = Path.Combine(AppContext.BaseDirectory, "setup-bluetooth-speaker.sh");
        RESET_SCRIPT_PATH = Path.Combine(AppContext.BaseDirectory, "reset-bluetooth-speaker.sh");
        MakeScriptExecutable(SETUP_SCRIPT_PATH);
        MakeScriptExecutable(RESET_SCRIPT_PATH);
    }

    public void RunSetupSpeakerScript()
    {
        try
        {
            if (!File.Exists(SETUP_SCRIPT_PATH))
            {
                throw new InvalidOperationException($"Setup script not found at: {SETUP_SCRIPT_PATH}");
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = SETUP_SCRIPT_PATH,
                WorkingDirectory = Path.GetDirectoryName(SETUP_SCRIPT_PATH),
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false
            });

            if (process != null)
            {
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("✅ Bluetooth speaker setup completed successfully");
                }
                else
                {
                    throw new InvalidOperationException($"Setup speaker script exited with code: {process.ExitCode}");
                }
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public void RunResetSpeakerScript()
    {
        try
        {
            if (!File.Exists(RESET_SCRIPT_PATH))
            {
                Console.WriteLine($"⚠️ Reset script not found at: {RESET_SCRIPT_PATH}");
                return;
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = RESET_SCRIPT_PATH,
                WorkingDirectory = Path.GetDirectoryName(RESET_SCRIPT_PATH),
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false
            });

            if (process != null)
            {
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("✅ Bluetooth settings reset successfully");
                }
                else
                {
                    Console.WriteLine($"⚠️ Reset script exited with code: {process.ExitCode}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error running reset script: {ex.Message}");
        }
    }

    public async Task<string> WaitForBluetoothDevice(CancellationToken cancellationToken)
    {
        Console.WriteLine("Waiting for Bluetooth audio device...");

        while (!cancellationToken.IsCancellationRequested)
        {
            var device = FindBluetoothSink();

            if (!string.IsNullOrEmpty(device))
            {
                Console.WriteLine($"Found bluetooth device: {device}");
                currentBluetoothDevice = device;
                // Return the node name directly for PipeWire recording
                return device;
            }

            await Task.Delay(1000, cancellationToken);
        }

        return null;
    }

    public bool IsBluetoothDeviceStillConnected()
    {
        if (string.IsNullOrEmpty(currentBluetoothDevice))
        {
            return false;
        }

        var currentDevice = FindBluetoothSink();

        return currentDevice == currentBluetoothDevice;
    }

    private static string FindBluetoothSink()
    {
        try
        {
            // First check if there's any Bluetooth audio stream active
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = "-c \"pactl list sink-inputs | grep -q 'api.bluez5' && echo 'found'\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var hasBluetoothStream = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(1000);

            if (hasBluetoothStream == "found")
            {
                // Find the RUNNING sink (the one actually playing audio)
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = "-c \"pactl list short sinks | grep RUNNING | head -1 | cut -f2\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var sinkName = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(1000);

                if (!string.IsNullOrWhiteSpace(sinkName))
                {
                    // Return the monitor of this sink
                    return $"{sinkName}.monitor";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding Bluetooth: {ex.Message}");
        }

        return null;
    }

    private static void MakeScriptExecutable(string scriptPath)
    {
        using var chmodProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/chmod",
            Arguments = $"+x {scriptPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });

        if (chmodProcess != null)
        {
            chmodProcess.WaitForExit();

            if (chmodProcess.ExitCode == 0)
            {
                Console.WriteLine($"Script {scriptPath} is now exectuable");
            }
            else
            {
                Console.WriteLine($"Chmod +x for {scriptPath} failed");
            }
        }
    }
}