using System.Buffers.Binary;
using System.Text;

namespace PixelTuner.Devices;

/// <summary>Timeouts and block sizes PixelTuner 2 used.</summary>
public static class DeviceProtocol
{
    /// <summary>Data bytes per block of a firmware update, and of a show for E-pic poi and LightMaster.</summary>
    public const int SmallBlockSize = 60;

    /// <summary>Data bytes per block of a show for pixel devices, hoops and video modules.</summary>
    public const int LargeBlockSize = DevicePacket.MaxDataLength;

    /// <summary>Settings are sent in packets of at least this many bytes (the last one may be shorter).</summary>
    public const int SettingsBatchSize = 60;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan SettingsTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan SwitchToBootTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Writing to the device's flash memory can take long; PixelTuner 2 waited 30 s per block.</summary>
    public static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Waiting for the answer to "who is it" on a port that may not be a device at all.</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Longest wait for the data of one answer, however slowly it trickles in.</summary>
    public static readonly TimeSpan AnswerDataLimit = TimeSpan.FromSeconds(10);
}

/// <summary>Playback commands that an Illuminator relays to the props over radio.</summary>
public enum RadioPlayback : byte
{
    Play = 2,
    Pause = 3,
    Stop = 4,
    Seek = 5,
}

/// <summary>
/// A connected show device: Pixel Poi, Pixel Hoop, video module, E-pic poi, LightMaster or
/// Illuminator, over a cable or Wi-Fi. A port of PixelTuner 2's SerialDeviceBase and its
/// subclasses. Calls block until the device answers; one operation runs at a time.
/// </summary>
public sealed class ShowDevice : IDisposable
{
    private readonly IDeviceTransport _transport;
    private readonly Lock _gate = new();
    private int _disposed;

    private ShowDevice(IDeviceTransport transport, DeviceModel model)
    {
        _transport = transport;
        Model = model;
    }

    /// <summary>
    /// Asks who is on the other end and what state it is in. Throws when nothing answers as a
    /// device; the transport is then left to the caller.
    /// </summary>
    public static ShowDevice Connect(IDeviceTransport transport, TimeSpan? answerTimeout = null)
    {
        transport.ReadTimeout = answerTimeout ?? DeviceProtocol.DefaultTimeout;
        transport.WriteTimeout = answerTimeout ?? DeviceProtocol.DefaultTimeout;
        byte[] name = Session(transport, DevicePacket.Encode(DeviceCommand.WhoIsIt), TimeSpan.Zero);
        var device = new ShowDevice(transport, DeviceModel.FromId(Encoding.ASCII.GetString(name)));
        transport.ReadTimeout = DeviceProtocol.DefaultTimeout;
        transport.WriteTimeout = DeviceProtocol.DefaultTimeout;
        try
        {
            device.ReadInfo();
        }
        catch (DeviceProtocolException) when (device.Model.BootMode || !device.Model.IsSupported)
        {
            // A bootloader or a device we do not know may answer "info" differently; it is
            // still worth listing (a bootloader takes firmware).
        }
        return device;
    }

    public DeviceModel Model { get; }

    /// <summary>The latest answer to "info".</summary>
    public DeviceInfo Info { get; private set; } = new();

    public string ChannelId => _transport.ChannelId;

    /// <summary>A cable connection (a serial port), not Wi-Fi.</summary>
    public bool IsCable => _transport is not TcpDeviceTransport;

    /// <summary>An upload or another long operation is running.</summary>
    public bool IsBusy { get; private set; }

    public bool IsConnected => Volatile.Read(ref _disposed) == 0 && _transport.IsOpen;

    /// <summary>LightMaster needed a moment between the request and reading the answer.</summary>
    private TimeSpan WriteReadDelay =>
        Model.Family == DeviceFamily.LightMaster ? TimeSpan.FromMilliseconds(5) : TimeSpan.Zero;

    /// <summary>Asks the device for its state and updates <see cref="Info"/>.</summary>
    public DeviceInfo ReadInfo()
    {
        lock (_gate)
            return Info = DeviceInfo.Parse(Model, Session(DeviceCommand.Info));
    }

    /// <summary>
    /// Reads the state unless an operation is running. Used by the connection monitor, which
    /// must not wait behind an upload. Returns false when the device was busy.
    /// </summary>
    public bool TryReadInfo()
    {
        if (!_gate.TryEnter()) return false;
        try
        {
            if (IsBusy) return false;
            Info = DeviceInfo.Parse(Model, Session(DeviceCommand.Info));
            return true;
        }
        finally
        {
            _gate.Exit();
        }
    }

    /// <summary>
    /// Writes a show (.bin) into the device's memory: announce the size, send it block by
    /// block (<see cref="DeviceModel.ShowBlockSize"/>), then close the transfer.
    /// </summary>
    /// <param name="progress">Share of the file sent, 0 to 1.</param>
    public void UploadShow(ReadOnlySpan<byte> show, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        RequireUploads();
        lock (_gate)
        {
            using var busy = Busy(DeviceProtocol.UploadTimeout);
            Span<byte> size = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(size, show.Length);
            byte[] answer = Session(DeviceCommand.WriteDataMode, size);
            if (answer.Length == 0 || answer[0] == 0x00)
                throw new DeviceProtocolException("The device refused the show (not enough memory?).");
            SendBlocks(show, Model.ShowBlockSize, progress, cancel);
            Session(DeviceCommand.EndWriteData);
            progress?.Report(1);
        }
    }

    /// <summary>
    /// Writes new firmware. The device restarts at the end without answering, so the
    /// connection is usually gone afterwards.
    /// </summary>
    public void UploadFirmware(ReadOnlySpan<byte> firmware, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        RequireSupported();
        lock (_gate)
        {
            using var busy = Busy(DeviceProtocol.UploadTimeout);
            Session(DeviceCommand.WriteFlashMode);
            SendBlocks(firmware, DeviceProtocol.SmallBlockSize, progress, cancel);
            _transport.Write(DevicePacket.Encode(DeviceCommand.EndWriteFlash));
            progress?.Report(1);
        }
    }

    /// <summary>Writes settings, several to a packet as PixelTuner 2 did.</summary>
    public void WriteSettings(IEnumerable<DeviceSetting> settings)
    {
        RequireSupported();
        lock (_gate)
        {
            using var busy = Busy(DeviceProtocol.SettingsTimeout);
            var batch = new List<byte>();
            foreach (var setting in settings)
            {
                batch.AddRange(setting.Encode());
                if (batch.Count < DeviceProtocol.SettingsBatchSize) continue;
                Session(DeviceCommand.WriteSettings, batch.ToArray());
                batch.Clear();
            }
            if (batch.Count > 0) Session(DeviceCommand.WriteSettings, batch.ToArray());
        }
    }

    /// <summary>
    /// Restarts the device into its bootloader, for a firmware update. The device may restart
    /// before it answers; that is not an error.
    /// </summary>
    public void SwitchToBoot()
    {
        RequireSupported();
        lock (_gate)
        {
            using var busy = Busy(DeviceProtocol.SwitchToBootTimeout);
            try { Session(DeviceCommand.SwitchToBoot); }
            catch (TimeoutException) { }
            catch (IOException) when (!_transport.IsOpen) { }
        }
    }

    /// <summary>Illuminator: sends a playback command for the props at the given show position.</summary>
    /// <returns>Whether the Illuminator confirmed it.</returns>
    public bool SendPlayback(RadioPlayback command, TimeSpan position)
    {
        if (Model.Family != DeviceFamily.Illuminator)
            throw new InvalidOperationException("Only an Illuminator relays playback commands.");
        Span<byte> data = stackalloc byte[5];
        data[0] = (byte)command;
        BinaryPrimitives.WriteUInt32LittleEndian(data[1..], (uint)position.TotalMilliseconds);
        lock (_gate)
        {
            byte[] answer = Session(DeviceCommand.PlaybackControl, data);
            return answer.Length > 0 && answer[0] == DevicePacket.Terminator;
        }
    }

    public event EventHandler? Disconnected;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _transport.Dispose();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => $"{Model.DisplayName} ({ChannelId})";

    private void RequireSupported()
    {
        if (!Model.IsSupported)
            throw new NotSupportedException($"{Model.DisplayName} is not supported.");
    }

    private void RequireUploads()
    {
        RequireSupported();
        if (Model.BootMode)
            throw new InvalidOperationException("The device is in boot mode: only a firmware update is possible.");
        if (Model.Family == DeviceFamily.Illuminator)
            throw new InvalidOperationException("An Illuminator does not play shows.");
    }

    private void SendBlocks(ReadOnlySpan<byte> data, int blockSize, IProgress<double>? progress, CancellationToken cancel)
    {
        for (int offset = 0; offset < data.Length; offset += blockSize)
        {
            cancel.ThrowIfCancellationRequested();
            Session(DeviceCommand.WriteBlock, data.Slice(offset, Math.Min(blockSize, data.Length - offset)));
            progress?.Report((double)Math.Min(offset + blockSize, data.Length) / data.Length);
        }
    }

    private byte[] Session(DeviceCommand command, ReadOnlySpan<byte> data = default) =>
        Session(_transport, DevicePacket.Encode(command, data), WriteReadDelay);

    /// <summary>
    /// One request and its answer (SerialDeviceBase.DoSession): the answer starts with the
    /// request's command, then the data length and the data; the closing 0x0D is read but
    /// not required. A written block is confirmed with 0xFF.
    /// </summary>
    private static byte[] Session(IDeviceTransport transport, byte[] request, TimeSpan writeReadDelay)
    {
        transport.DiscardInput();
        transport.Write(request);
        if (writeReadDelay > TimeSpan.Zero) Thread.Sleep(writeReadDelay);

        Span<byte> header = stackalloc byte[2];
        ReadExactly(transport, header[..1], "Packet ID reading timeout.");
        if (header[0] != request[0])
            throw new DeviceProtocolException($"Wrong answer {header[0]:X2} on request {request[0]:X2}.");
        ReadExactly(transport, header[1..], "Packet length reading timeout.");

        var data = new byte[header[1]];
        var started = DateTime.UtcNow;
        int read = 0;
        while (read < data.Length)
        {
            if (DateTime.UtcNow - started > DeviceProtocol.AnswerDataLimit)
                throw new TimeoutException($"Incorrect packet length {read} (expected {data.Length}).");
            try { read += transport.Read(data.AsSpan(read)); }
            catch (TimeoutException) { throw new TimeoutException($"Incorrect packet length {read} (expected {data.Length})."); }
        }

        // PixelTuner 2 did not insist on the closing byte.
        try { transport.Read(header[..1]); }
        catch (TimeoutException) { }

        if (request[0] == (byte)DeviceCommand.WriteBlock && (data.Length == 0 || data[0] != 0xff))
            throw new DeviceProtocolException("Data block write error.");
        return data;
    }

    private static void ReadExactly(IDeviceTransport transport, Span<byte> buffer, string timeoutMessage)
    {
        try
        {
            while (!buffer.IsEmpty) buffer = buffer[transport.Read(buffer)..];
        }
        catch (TimeoutException e)
        {
            throw new TimeoutException(timeoutMessage, e);
        }
    }

    private BusyScope Busy(TimeSpan timeout)
    {
        IsBusy = true;
        _transport.ReadTimeout = timeout;
        _transport.WriteTimeout = timeout;
        return new BusyScope(this);
    }

    private readonly struct BusyScope(ShowDevice device) : IDisposable
    {
        public void Dispose()
        {
            device.IsBusy = false;
            if (!device.IsConnected) return;
            try
            {
                device._transport.ReadTimeout = DeviceProtocol.DefaultTimeout;
                device._transport.WriteTimeout = DeviceProtocol.DefaultTimeout;
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // The connection went away during the operation.
            }
        }
    }
}
