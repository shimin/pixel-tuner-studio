using System.Collections.Immutable;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;

namespace PixelTuner.Devices;

/// <summary>
/// Finds show devices and keeps the list current, as PixelTuner 2's DeviceHelper and its
/// network panel did:
/// <list type="bullet">
/// <item>serial ports are scanned twice a second; a new port is asked "who is it", and a
/// port that does not answer as a device is not asked again until it disappears;</item>
/// <item>devices on Wi-Fi connect to TCP port 3331 once the app announced itself
/// (PixelTuner.Sync sends the announcement);</item>
/// <item>every connected device is asked for its state once a second, unless it is busy;
/// a device that stops answering is dropped.</item>
/// </list>
/// Events come on background threads.
/// </summary>
public sealed class DeviceManager : IAsyncDisposable
{
    /// <summary>Port devices connect to over Wi-Fi.</summary>
    public const int NetworkPort = 3331;

    private readonly DeviceManagerOptions _options;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _sync = new();
    private readonly HashSet<string> _probing = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ignoredPorts = new(StringComparer.Ordinal);
    private ImmutableList<ShowDevice> _devices = [];
    private readonly List<Task> _loops = [];
    private TcpListener? _listener;

    public DeviceManager(DeviceManagerOptions? options = null) => _options = options ?? new DeviceManagerOptions();

    /// <summary>Connected devices; a snapshot, safe to read from any thread.</summary>
    public IReadOnlyList<ShowDevice> Devices => _devices;

    public event EventHandler<ShowDevice>? DeviceFound;
    public event EventHandler<ShowDevice>? DeviceLost;

    /// <summary>The TCP port devices connect to, once listening (useful when the options asked for 0).</summary>
    public int? ListeningPort => (_listener?.LocalEndpoint as IPEndPoint)?.Port;

    public void Start()
    {
        if (_loops.Count > 0) throw new InvalidOperationException("The device manager is already running.");
        if (_options.ScanSerialPorts) _loops.Add(Task.Run(() => ScanLoopAsync(_stop.Token)));
        if (_options.ListenPort is { } port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _loops.Add(Task.Run(() => AcceptLoopAsync(_listener, _stop.Token)));
        }
        _loops.Add(Task.Run(() => MonitorLoopAsync(_stop.Token)));
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener?.Stop();
        try { await Task.WhenAll(_loops); }
        catch (OperationCanceledException) { }
        foreach (var device in _devices) device.Dispose();
        _devices = [];
        _stop.Dispose();
    }

    private async Task ScanLoopAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { ScanPorts(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Listing ports can fail while a USB device comes or goes; try again next time.
            }
            try { await Task.Delay(_options.ScanInterval, stop); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void ScanPorts()
    {
        var present = _options.ListPorts().ToHashSet(StringComparer.Ordinal);
        lock (_sync)
        {
            // A port that disappeared is probed again when it comes back (device plugged in again).
            _ignoredPorts.RemoveWhere(p => !present.Contains(p));
        }
        foreach (var device in _devices.Where(d => d.IsCable && !present.Contains(d.ChannelId)))
            Lose(device);

        foreach (string port in present)
        {
            lock (_sync)
            {
                if (_ignoredPorts.Contains(port) || _probing.Contains(port)
                    || _devices.Any(d => d.ChannelId == port)) continue;
                _probing.Add(port);
            }
            _ = Task.Run(() => Probe(port));
        }
    }

    private void Probe(string port)
    {
        try
        {
            IDeviceTransport transport;
            try { transport = _options.OpenPort(port); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                return;   // busy or gone: tried again on the next scan
            }
            if (TryConnect(transport) is { } device) Add(device);
            else lock (_sync) _ignoredPorts.Add(port);
        }
        finally
        {
            lock (_sync) _probing.Remove(port);
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            Socket socket;
            try { socket = await listener.AcceptSocketAsync(stop); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { continue; }
            _ = Task.Run(() =>
            {
                if (TryConnect(new TcpDeviceTransport(socket)) is { } device) Add(device);
            }, stop);
        }
    }

    /// <summary>Asks who is there; closes the transport when it is not a device.</summary>
    private ShowDevice? TryConnect(IDeviceTransport transport)
    {
        try
        {
            return ShowDevice.Connect(transport, _options.ProbeTimeout);
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            transport.Dispose();
            return null;
        }
    }

    private async Task MonitorLoopAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await Task.Delay(_options.MonitorInterval, stop); }
            catch (OperationCanceledException) { return; }
            foreach (var device in _devices)
            {
                if (stop.IsCancellationRequested) return;
                try
                {
                    if (!device.IsConnected) Lose(device);
                    else device.TryReadInfo();
                }
                catch (Exception e) when (e is TimeoutException or IOException or InvalidOperationException or ObjectDisposedException)
                {
                    Lose(device);
                }
            }
        }
    }

    private void Add(ShowDevice device)
    {
        lock (_sync)
        {
            if (_stop.IsCancellationRequested)
            {
                device.Dispose();
                return;
            }
            _devices = _devices.Add(device);
        }
        DeviceFound?.Invoke(this, device);
    }

    private void Lose(ShowDevice device)
    {
        lock (_sync)
        {
            if (!_devices.Contains(device)) return;
            _devices = _devices.Remove(device);
        }
        device.Dispose();
        DeviceLost?.Invoke(this, device);
    }
}

public sealed class DeviceManagerOptions
{
    /// <summary>Look for devices on serial ports (USB cable).</summary>
    public bool ScanSerialPorts { get; init; } = true;

    /// <summary>TCP port for devices on Wi-Fi; null to not listen, 0 for any free port (tests).</summary>
    public int? ListenPort { get; init; } = DeviceManager.NetworkPort;

    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MonitorInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How long a new port or connection has to answer "who is it".</summary>
    public TimeSpan ProbeTimeout { get; init; } = DeviceProtocol.ProbeTimeout;

    /// <summary>Serial ports to look at. On macOS only the call-out devices (/dev/cu.*), which open without waiting for a carrier.</summary>
    public Func<IEnumerable<string>> ListPorts { get; init; } = DefaultPorts;

    public Func<string, IDeviceTransport> OpenPort { get; init; } = port => new SerialPortTransport(port);

    private static IEnumerable<string> DefaultPorts()
    {
        var ports = SerialPort.GetPortNames();
        return OperatingSystem.IsMacOS() ? ports.Where(p => p.StartsWith("/dev/cu.", StringComparison.Ordinal)) : ports;
    }
}
