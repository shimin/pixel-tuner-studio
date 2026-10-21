using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace PixelTuner.Sync;

/// <summary>
/// Wire format of the show devices (UDP port 333).
/// Spec: legacy/EpicPlayer/epicplayer-mac/plans/protocol.md.
/// Byte vectors: legacy/EpicPlayer/epicplayer-mac/Tests/Fixtures/protocol-vectors.json.
/// </summary>
public static class ShowProtocol
{
    /// <summary>UDP port the devices listen on and answer from.</summary>
    public const int DevicePort = 333;

    /// <summary>TCP port announced in the TCP device lookup.</summary>
    public const int TcpLookupPort = 3331;

    public const int PlaybackPacketLength = 8;

    const byte Header0 = 0x50;
    const byte Header1 = 0x05;
    const byte Terminator = 0x0D;
    const byte LookupMarker = 0x49;
    const byte TcpAnnounceMarker = 0x4C;

    /// <summary>
    /// 8-byte playback packet: <c>50 05 cmd p0 p1 p2 p3 0D</c>, position in ms, little endian.
    /// A new array on every call: buffers are never shared between threads.
    /// </summary>
    public static byte[] EncodePlayback(PlaybackCommand command, uint positionMilliseconds)
    {
        var packet = new byte[PlaybackPacketLength];
        packet[0] = Header0;
        packet[1] = Header1;
        packet[2] = (byte)command;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(3, 4), positionMilliseconds);
        packet[7] = Terminator;
        return packet;
    }

    /// <summary>Position in ms for the packet, clamped to the UInt32 range.</summary>
    public static uint ToPacketPosition(TimeSpan position)
    {
        var ms = position.TotalMilliseconds;
        if (ms <= 0) return 0;
        if (ms >= uint.MaxValue) return uint.MaxValue;
        return (uint)ms;
    }

    /// <summary>UDP device lookup request: <c>49 00 0D</c>.</summary>
    public static byte[] EncodeLookupRequest() => [LookupMarker, 0x00, Terminator];

    /// <summary>
    /// TCP lookup announcement: <c>4C 06 a0 a1 a2 a3 portLo portHi 0D</c>.
    /// The device connects back to this address and port.
    /// </summary>
    public static byte[] EncodeTcpAnnouncement(IPAddress address, ushort port = TcpLookupPort)
    {
        var octets = address.GetAddressBytes();
        if (octets.Length != 4)
            throw new ArgumentException("Only IPv4 addresses are supported.", nameof(address));

        var packet = new byte[9];
        packet[0] = TcpAnnounceMarker;
        packet[1] = 0x06;
        octets.CopyTo(packet, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6, 2), port);
        packet[8] = Terminator;
        return packet;
    }

    /// <summary>
    /// Parses a reply to the lookup request. A device reply starts with 0x49 and is
    /// longer than 3 bytes; the device name ("tag") is 12 bytes at offset 47 in Windows-1251.
    /// </summary>
    public static bool TryParseDeviceReply(ReadOnlySpan<byte> data, out DeviceReply reply)
    {
        reply = default;
        if (data.Length <= 3 || data[0] != LookupMarker)
            return false;

        string? tag = null;
        const int tagOffset = 47, tagLength = 12;
        if (data.Length >= tagOffset + tagLength)
        {
            tag = Cp1251.GetString(data.Slice(tagOffset, tagLength)).TrimEnd('\0');
            if (tag.Length == 0) tag = null;
        }

        reply = new DeviceReply(tag, data.ToArray());
        return true;
    }

    static readonly Encoding Cp1251 = CreateCp1251();

    static Encoding CreateCp1251()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1251);
    }
}

/// <summary>A device that answered the lookup request.</summary>
public readonly record struct DeviceReply(string? Tag, byte[] RawData);
