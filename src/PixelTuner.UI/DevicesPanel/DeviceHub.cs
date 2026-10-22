using PixelTuner.Devices;

namespace PixelTuner.UI.DevicesPanel;

/// <summary>
/// The devices found over USB and Wi-Fi, for the UI. The head of an app that can talk to devices sets <see cref="DeviceHub.Current"/>
/// (Studio does; a phone has no serial ports and Stage does not upload); the shared UI knows only this contract.
/// Events come on background threads.
/// </summary>
public interface IDeviceHub
{
    /// <summary>Connected devices; a snapshot.</summary>
    IReadOnlyList<ShowDevice> Devices { get; }

    bool IsSearching { get; }

    event EventHandler<ShowDevice>? DeviceFound;
    event EventHandler<ShowDevice>? DeviceLost;

    /// <summary>Starts looking for devices on the serial ports and listening for devices on Wi-Fi.</summary>
    void StartSearching();

    /// <summary>Stops looking and lets go of the devices that were found.</summary>
    Task StopSearchingAsync();
}

public static class DeviceHub
{
    /// <summary>Null where the app has no devices to talk to.</summary>
    public static IDeviceHub? Current { get; set; }
}

/// <summary>The hub on <see cref="DeviceManager"/>: a manager for as long as the search lasts.</summary>
public sealed class DeviceManagerHub(Func<DeviceManagerOptions>? options = null) : IDeviceHub
{
    private readonly Lock _gate = new();
    private DeviceManager? _manager;

    public IReadOnlyList<ShowDevice> Devices => _manager?.Devices ?? [];

    public bool IsSearching => _manager is not null;

    public event EventHandler<ShowDevice>? DeviceFound;
    public event EventHandler<ShowDevice>? DeviceLost;

    public void StartSearching()
    {
        lock (_gate)
        {
            if (_manager is not null) return;
            var manager = new DeviceManager(options?.Invoke());
            manager.DeviceFound += (_, device) => DeviceFound?.Invoke(this, device);
            manager.DeviceLost += (_, device) => DeviceLost?.Invoke(this, device);
            manager.Start();
            _manager = manager;
        }
    }

    public async Task StopSearchingAsync()
    {
        DeviceManager? manager;
        lock (_gate)
        {
            manager = _manager;
            _manager = null;
        }
        if (manager is null) return;
        var devices = manager.Devices;
        await manager.DisposeAsync();
        foreach (var device in devices) DeviceLost?.Invoke(this, device);
    }
}
