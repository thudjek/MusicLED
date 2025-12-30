using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MusicLED;

public class BluetoothHandler : IDisposable
{
    private readonly string SETUP_SCRIPT_PATH;
    private readonly string RESET_SCRIPT_PATH;

    private string currentBluetoothDevice = null;
    private string connectedSpeakerMac = null;
    private string connectedSpeakerName = null;
    private bool isScanning = false;
    private CancellationTokenSource _scanCts;
    private readonly Lock _scanLock = new();
    private readonly Dictionary<string, BluetoothDevice> _discoveredDevices = [];

    public BluetoothHandler()
    {
        SETUP_SCRIPT_PATH = Path.Combine(AppContext.BaseDirectory, "setup-bluetooth-speaker.sh");
        RESET_SCRIPT_PATH = Path.Combine(AppContext.BaseDirectory, "reset-bluetooth-speaker.sh");
        ChmodHelper.MakeFileExecutable(SETUP_SCRIPT_PATH);
        ChmodHelper.MakeFileExecutable(RESET_SCRIPT_PATH);
        RunSetupSpeakerScript();
    }

    #region Output Speaker Management (Pi connects TO speaker)

    public bool IsScanning => isScanning;

    public async Task<bool> StartScanningAsync()
    {
        lock (_scanLock)
        {
            if (isScanning)
                return false;

            isScanning = true;
            _scanCts = new CancellationTokenSource();
        }

        _discoveredDevices.Clear();

        _ = Task.Run(() => ContinuousScanAsync(_scanCts.Token));

        Console.WriteLine("Bluetooth scanning started");
        return true;
    }

    public async Task StopScanningAsync()
    {
        lock (_scanLock)
        {
            if (!isScanning)
                return;
        }

        _scanCts?.Cancel();
        await RunBluetoothCtlCommandAsync("scan off");

        lock (_scanLock)
        {
            isScanning = false;
        }

        Console.WriteLine("Bluetooth scanning stopped");
    }

    private async Task ContinuousScanAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunBluetoothCtlCommandAsync("scan on");

            while (!cancellationToken.IsCancellationRequested)
            {
                await RefreshDeviceListAsync();
                await Task.Delay(2000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping scan
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in continuous scan: {ex.Message}");
        }
        finally
        {
            await RunBluetoothCtlCommandAsync("scan off");
            lock (_scanLock)
            {
                isScanning = false;
            }
        }
    }

    private async Task RefreshDeviceListAsync()
    {
        try
        {
            var output = await RunBluetoothCtlCommandAsync("devices");
            var devices = ParseDeviceList(output, false);

            var pairedOutput = await RunBluetoothCtlCommandAsync("devices Paired");
            var pairedMacs = new HashSet<string>();
            foreach (var line in pairedOutput.Split('\n'))
            {
                var match = Regex.Match(line, @"Device\s+([0-9A-F:]{17})", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    pairedMacs.Add(match.Groups[1].Value.ToUpper());
                }
            }

            foreach (var device in devices)
            {
                device.IsPaired = pairedMacs.Contains(device.MacAddress.ToUpper());

                var info = await RunBluetoothCtlCommandAsync($"info {device.MacAddress}");
                device.IsConnected = info.Contains("Connected: yes");
                device.IsTrusted = info.Contains("Trusted: yes");

                lock (_scanLock)
                {
                    _discoveredDevices[device.MacAddress.ToUpper()] = device;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing device list: {ex.Message}");
        }
    }

    public async Task<BluetoothDeviceGroups> GetDeviceGroupsAsync()
    {
        var groups = new BluetoothDeviceGroups();

        try
        {
            // Get paired devices
            var pairedOutput = await RunBluetoothCtlCommandAsync("devices Paired");
            var pairedDevices = ParseDeviceList(pairedOutput, true);

            foreach (var device in pairedDevices)
            {
                var info = await RunBluetoothCtlCommandAsync($"info {device.MacAddress}");
                device.IsConnected = info.Contains("Connected: yes");
                device.IsTrusted = info.Contains("Trusted: yes");

                if (device.IsConnected)
                {
                    groups.Connected.Add(device);
                }
                else
                {
                    groups.Paired.Add(device);
                }
            }

            // Get discovered (not paired) devices
            lock (_scanLock)
            {
                foreach (var device in _discoveredDevices.Values)
                {
                    if (!device.IsPaired && !groups.Connected.Any(d => d.MacAddress == device.MacAddress))
                    {
                        groups.Discovered.Add(device);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting device groups: {ex.Message}");
        }

        return groups;
    }

    public async Task<List<BluetoothDevice>> GetPairedDevicesAsync()
    {
        var devices = new List<BluetoothDevice>();

        try
        {
            var output = await RunBluetoothCtlCommandAsync("devices Paired");
            devices = ParseDeviceList(output, true);

            // Get connection status for each device
            foreach (var device in devices)
            {
                var info = await RunBluetoothCtlCommandAsync($"info {device.MacAddress}");
                device.IsConnected = info.Contains("Connected: yes");
                device.IsTrusted = info.Contains("Trusted: yes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting paired devices: {ex.Message}");
        }

        return devices;
    }

    public async Task<bool> ConnectToSpeakerAsync(string macAddress)
    {
        try
        {
            // Stop scanning if in progress
            if (isScanning)
            {
                await StopScanningAsync();
            }

            Console.WriteLine($"Connecting to speaker: {macAddress}");

            // Check if already paired
            var info = await RunBluetoothCtlCommandAsync($"info {macAddress}");
            bool isPaired = info.Contains("Paired: yes");

            if (!isPaired)
            {
                // Pair with the device
                Console.WriteLine("Pairing...");
                var pairResult = await RunBluetoothCtlCommandAsync($"pair {macAddress}", 30000);
                if (pairResult.Contains("Failed") && !pairResult.Contains("Already exists"))
                {
                    Console.WriteLine($"Pairing failed: {pairResult}");
                    return false;
                }
            }

            // Trust the device for auto-reconnect
            Console.WriteLine("Trusting device...");
            await RunBluetoothCtlCommandAsync($"trust {macAddress}");

            // Connect to the device
            Console.WriteLine("Connecting...");
            var connectResult = await RunBluetoothCtlCommandAsync($"connect {macAddress}", 30000);
            if (connectResult.Contains("Failed"))
            {
                Console.WriteLine($"Connection failed: {connectResult}");
                return false;
            }

            // Wait for audio sink to be available
            await Task.Delay(2000);

            // Set the Bluetooth speaker as default sink
            var setSinkResult = await SetBluetoothSpeakerAsDefaultSinkAsync(macAddress);
            if (!setSinkResult)
            {
                Console.WriteLine("Warning: Could not set speaker as default sink");
            }

            // Store connected speaker info
            connectedSpeakerMac = macAddress;

            // Get device name
            info = await RunBluetoothCtlCommandAsync($"info {macAddress}");
            var nameMatch = Regex.Match(info, @"Name:\s*(.+)");
            connectedSpeakerName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : macAddress;

            Console.WriteLine($"Successfully connected to {connectedSpeakerName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to speaker: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DisconnectSpeakerAsync()
    {
        if (string.IsNullOrEmpty(connectedSpeakerMac))
        {
            return true;
        }

        try
        {
            Console.WriteLine($"Disconnecting from speaker: {connectedSpeakerMac}");
            await RunBluetoothCtlCommandAsync($"disconnect {connectedSpeakerMac}");

            connectedSpeakerMac = null;
            connectedSpeakerName = null;

            Console.WriteLine("Disconnected from speaker");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disconnecting: {ex.Message}");
            return false;
        }
    }

    public async Task<BluetoothSpeakerStatus> GetOutputSpeakerStatusAsync()
    {
        var status = new BluetoothSpeakerStatus();

        try
        {
            if (!string.IsNullOrEmpty(connectedSpeakerMac))
            {
                var info = await RunBluetoothCtlCommandAsync($"info {connectedSpeakerMac}");
                status.IsConnected = info.Contains("Connected: yes");
                status.MacAddress = connectedSpeakerMac;
                status.DeviceName = connectedSpeakerName;

                if (!status.IsConnected)
                {
                    connectedSpeakerMac = null;
                    connectedSpeakerName = null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting speaker status: {ex.Message}");
        }

        return status;
    }

    private static async Task<bool> SetBluetoothSpeakerAsDefaultSinkAsync(string macAddress)
    {
        try
        {
            // Convert MAC address format for PulseAudio (XX:XX:XX:XX:XX:XX -> XX_XX_XX_XX_XX_XX)
            var macForPulse = macAddress.Replace(":", "_");

            // Find the sink name for this Bluetooth device
            var sinksOutput = await RunBashCommandAsync("pactl list short sinks");
            var lines = sinksOutput.Split('\n');

            string sinkName = null;
            foreach (var line in lines)
            {
                if (line.Contains(macForPulse, StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("bluez", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 2)
                    {
                        sinkName = parts[1];
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(sinkName))
            {
                Console.WriteLine("Could not find Bluetooth sink");
                return false;
            }

            // Set as default sink
            await RunBashCommandAsync($"pactl set-default-sink {sinkName}");
            Console.WriteLine($"Set default sink to: {sinkName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting default sink: {ex.Message}");
            return false;
        }
    }

    private static List<BluetoothDevice> ParseDeviceList(string output, bool isPaired)
    {
        var devices = new List<BluetoothDevice>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Format: "Device XX:XX:XX:XX:XX:XX DeviceName"
            var match = Regex.Match(line, @"Device\s+([0-9A-F:]{17})\s+(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                devices.Add(new BluetoothDevice
                {
                    MacAddress = match.Groups[1].Value,
                    Name = match.Groups[2].Value.Trim(),
                    IsPaired = isPaired
                });
            }
        }

        return devices;
    }

    private static async Task<string> RunBluetoothCtlCommandAsync(string command, int timeoutMs = 5000)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"echo '{command}' | bluetoothctl\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeoutMs);
            await process.WaitForExitAsync(cts.Token);

            return output;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running bluetoothctl command '{command}': {ex.Message}");
            return string.Empty;
        }
    }

    private static async Task<string> RunBashCommandAsync(string command)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running bash command: {ex.Message}");
            return string.Empty;
        }
    }

    #endregion

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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        RunResetSpeakerScript();
    }
}