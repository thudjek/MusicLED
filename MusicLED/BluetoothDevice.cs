namespace MusicLED;

public class BluetoothDevice
{
    public string MacAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPaired { get; set; }
    public bool IsConnected { get; set; }
    public bool IsTrusted { get; set; }
}

public class BluetoothSpeakerStatus
{
    public bool IsConnected { get; set; }
    public string DeviceName { get; set; }
    public string MacAddress { get; set; }
}

public class BluetoothConnectRequest
{
    public string MacAddress { get; set; } = string.Empty;
}

public class BluetoothDeviceGroups
{
    public List<BluetoothDevice> Connected { get; set; } = new();
    public List<BluetoothDevice> Paired { get; set; } = new();
    public List<BluetoothDevice> Discovered { get; set; } = new();
    public bool IsScanning { get; set; }
}
