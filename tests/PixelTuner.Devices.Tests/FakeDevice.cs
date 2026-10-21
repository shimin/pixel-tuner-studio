using System.Buffers.Binary;
using System.Text;

namespace PixelTuner.Devices.Tests;

/// <summary>
/// A device in memory that answers the protocol the way the firmware does, and records
/// every request. Requests and answers are synchronous, so no threads are needed.
/// </summary>
internal sealed class FakeDevice(string name, byte[] info, string channel = "COM7") : IDeviceTransport
{
    private readonly Queue<byte> _output = new();

    public List<byte[]> Requests { get; } = [];
    public List<byte> Show { get; } = [];
    public List<byte> Firmware { get; } = [];
    public List<byte[]> Settings { get; } = [];
    public int AnnouncedSize { get; private set; } = -1;
    public bool ShowClosed { get; private set; }
    public bool FirmwareClosed { get; private set; }

    /// <summary>Answer to "write data mode": 0 means no room.</summary>
    public byte AcceptShow { get; set; } = 1;

    /// <summary>Block number (0-based) the device rejects.</summary>
    public int? RejectBlock { get; set; }

    /// <summary>Answer with another command than asked.</summary>
    public byte? WrongAnswer { get; set; }

    /// <summary>Leave out the closing 0x0D of answers.</summary>
    public bool NoTerminator { get; set; }

    /// <summary>Stop answering, as an unplugged cable.</summary>
    public bool Silent { get; set; }

    public bool Closed { get; private set; }

    public string ChannelId => channel;
    public bool IsOpen => !Closed;
    public TimeSpan ReadTimeout { get; set; }
    public TimeSpan WriteTimeout { get; set; }
    public List<TimeSpan> TimeoutsDuringUpload { get; } = [];

    public void DiscardInput() => _output.Clear();

    public void Write(ReadOnlySpan<byte> data)
    {
        if (Closed) throw new IOException("closed");
        var request = data.ToArray();
        Requests.Add(request);
        Assert.Equal(request[1] + 3, request.Length);
        Assert.Equal(DevicePacket.Terminator, request[^1]);
        var payload = request.AsSpan(2, request[1]);
        if (Silent) return;

        switch ((DeviceCommand)request[0])
        {
            case DeviceCommand.WhoIsIt: Answer(request[0], Encoding.ASCII.GetBytes(name)); break;
            case DeviceCommand.Info: Answer(request[0], info); break;
            case DeviceCommand.WriteDataMode:
                AnnouncedSize = BinaryPrimitives.ReadInt32LittleEndian(payload);
                Answer(request[0], [AcceptShow]);
                break;
            case DeviceCommand.WriteBlock:
                int block = Requests.Count(r => r[0] == (byte)DeviceCommand.WriteBlock) - 1;
                if (block == RejectBlock) { Answer(request[0], [0x00]); break; }
                TimeoutsDuringUpload.Add(ReadTimeout);
                (Requests.Any(r => r[0] == (byte)DeviceCommand.WriteFlashMode) ? Firmware : Show).AddRange(payload.ToArray());
                Answer(request[0], [0xff]);
                break;
            case DeviceCommand.EndWriteData: ShowClosed = true; Answer(request[0], []); break;
            case DeviceCommand.WriteFlashMode: Answer(request[0], []); break;
            case DeviceCommand.EndWriteFlash: FirmwareClosed = true; break;          // restarts, no answer
            case DeviceCommand.WriteSettings: Settings.Add(payload.ToArray()); Answer(request[0], []); break;
            case DeviceCommand.SwitchToBoot: break;                                  // restarts, no answer
            case DeviceCommand.PlaybackControl: Answer(request[0], [0x0d]); break;
            default: throw new InvalidOperationException($"Unexpected command {request[0]:X2}.");
        }
    }

    private void Answer(byte command, byte[] data)
    {
        _output.Enqueue(WrongAnswer ?? command);
        _output.Enqueue((byte)data.Length);
        foreach (byte b in data) _output.Enqueue(b);
        if (!NoTerminator) _output.Enqueue(DevicePacket.Terminator);
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

    /// <summary>The "info" answer of PixelTuner 2's VirtualDevice: platform 2, 24 LEDs, tag "spin".</summary>
    public static readonly byte[] SampleInfo =
    [
        0x02, 0x04, 0x02, 0x00, 0x18, 0x00, 0x00, 0x00, 0x39, 0xFF, 0x98, 0x6C, 0x75, 0x63, 0x6B, 0x79, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x74, 0x65, 0x68, 0x69, 0x62, 0x6F, 0x6F, 0x6D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0,
        0xA8, 0x58, 0xCB, 0x4D, 0x01, 0x73, 0x70, 0x69, 0x6E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>A Pixel Poi 5 on platform 5 with 150 LEDs, tag "Шоу 1" in Windows-1251.</summary>
    public static byte[] PixelPoi5Info()
    {
        var info = new byte[60];
        info[0] = 5; info[1] = 3; info[2] = 17;
        BinaryPrimitives.WriteUInt16BigEndian(info.AsSpan(3), 150);
        BinaryPrimitives.WriteUInt16BigEndian(info.AsSpan(5), 87);
        BinaryPrimitives.WriteUInt32BigEndian(info.AsSpan(7), 2_000_000);
        for (int i = 0; i < 8; i++) { info[11 + i] = (byte)(0xA0 + i); info[22 + i] = (byte)(0xB0 + i); }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding.GetEncoding(1251).GetBytes("Шоу 1").CopyTo(info, 45);
        return info;
    }
}
