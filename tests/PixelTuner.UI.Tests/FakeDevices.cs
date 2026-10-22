using System.Buffers.Binary;
using System.Text;
using PixelTuner.Devices;
using PixelTuner.UI.DevicesPanel;

namespace PixelTuner.UI.Tests;

/// <summary>A device in memory that answers the protocol like the firmware and keeps what is written to it.</summary>
internal sealed class FakeShowTransport(string name, byte[] info, string channel = "COM7") : IDeviceTransport
{
    private readonly Queue<byte> _output = new();

    public List<byte> Show { get; } = [];
    public int AnnouncedSize { get; private set; } = -1;
    public bool ShowClosed { get; private set; }
    public int Blocks { get; private set; }

    /// <summary>Answer to "write data mode": 0 means no room.</summary>
    public byte AcceptShow { get; set; } = 1;

    /// <summary>Called after each block is stored (to cancel an upload in the middle).</summary>
    public Action<int>? AfterBlock { get; set; }

    public bool Closed { get; private set; }
    public string ChannelId => channel;
    public bool IsOpen => !Closed;
    public TimeSpan ReadTimeout { get; set; }
    public TimeSpan WriteTimeout { get; set; }

    public void DiscardInput() => _output.Clear();

    public void Write(ReadOnlySpan<byte> data)
    {
        var request = data.ToArray();
        var payload = request.AsSpan(2, request[1]);
        switch ((DeviceCommand)request[0])
        {
            case DeviceCommand.WhoIsIt: Answer(request[0], Encoding.ASCII.GetBytes(name)); break;
            case DeviceCommand.Info: Answer(request[0], info); break;
            case DeviceCommand.WriteDataMode:
                AnnouncedSize = BinaryPrimitives.ReadInt32LittleEndian(payload);
                Answer(request[0], [AcceptShow]);
                break;
            case DeviceCommand.WriteBlock:
                Show.AddRange(payload.ToArray());
                Blocks++;
                Answer(request[0], [0xff]);
                AfterBlock?.Invoke(Blocks);
                break;
            case DeviceCommand.EndWriteData: ShowClosed = true; Answer(request[0], []); break;
            default: Answer(request[0], []); break;
        }
    }

    private void Answer(byte command, byte[] data)
    {
        _output.Enqueue(command);
        _output.Enqueue((byte)data.Length);
        foreach (byte b in data) _output.Enqueue(b);
        _output.Enqueue(DevicePacket.Terminator);
    }

    public int Read(Span<byte> buffer)
    {
        if (Closed) throw new IOException("closed");
        if (_output.Count == 0) throw new TimeoutException();
        int n = 0;
        while (n < buffer.Length && _output.Count > 0) buffer[n++] = _output.Dequeue();
        return n;
    }

    public void Dispose() => Closed = true;

    /// <summary>The "info" answer of a Pixel Poi 5 (family PixelPoi).</summary>
    public static byte[] PoiInfo(int leds, uint memory = 2_000_000, string tag = "left")
    {
        var info = new byte[60];
        info[0] = 5; info[1] = 3; info[2] = 17;
        BinaryPrimitives.WriteUInt16BigEndian(info.AsSpan(3), (ushort)leds);
        BinaryPrimitives.WriteUInt16BigEndian(info.AsSpan(5), 87);
        BinaryPrimitives.WriteUInt32BigEndian(info.AsSpan(7), memory);
        Encoding.ASCII.GetBytes(tag).CopyTo(info, 45);
        return info;
    }

    /// <summary>The "info" answer of a video module (the hoop layout: platform 2, LEDs, battery, memory, tag).</summary>
    public static byte[] VideoInfo(int leds, uint memory = 3_000_000, string tag = "")
    {
        var info = new byte[60];
        info[0] = 2; info[1] = 1; info[2] = 4;
        BinaryPrimitives.WriteUInt16BigEndian(info.AsSpan(3), (ushort)leds);
        BinaryPrimitives.WriteUInt32BigEndian(info.AsSpan(7), memory);
        Encoding.ASCII.GetBytes(tag).CopyTo(info, 45);
        return info;
    }

    /// <summary>The "info" answer of an E-pic poi (SimplePoiDevice): no LEDs in it, the battery and the memory.</summary>
    public static byte[] EpicInfo(uint memory = 2_000_000)
    {
        var info = new byte[60];
        info[0] = 2; info[1] = 1; info[2] = 2;
        BinaryPrimitives.WriteUInt32BigEndian(info.AsSpan(6), memory);
        return info;
    }

    /// <summary>The "info" answer of a LightMaster: 8 channels, 500 KB free.</summary>
    public static byte[] LightInfo()
    {
        var info = new byte[60];
        info[0] = 2; info[1] = 1; info[2] = 3; info[4] = 8;
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(6), 500);
        return info;
    }

    public static ShowDevice Connect(string id, byte[] info, out FakeShowTransport transport, string channel = "COM7")
    {
        transport = new FakeShowTransport(id, info, channel);
        return ShowDevice.Connect(transport);
    }
}

/// <summary>The devices "found" by a test: it adds and removes them by hand, on the calling thread.</summary>
internal sealed class FakeDeviceHub : IDeviceHub
{
    private readonly List<ShowDevice> _devices = [];

    public IReadOnlyList<ShowDevice> Devices => _devices;
    public bool IsSearching { get; private set; }

    public event EventHandler<ShowDevice>? DeviceFound;
    public event EventHandler<ShowDevice>? DeviceLost;

    public void StartSearching() => IsSearching = true;

    public Task StopSearchingAsync()
    {
        IsSearching = false;
        return Task.CompletedTask;
    }

    public void Plug(ShowDevice device)
    {
        _devices.Add(device);
        DeviceFound?.Invoke(this, device);
    }

    public void Unplug(ShowDevice device)
    {
        _devices.Remove(device);
        DeviceLost?.Invoke(this, device);
    }
}
