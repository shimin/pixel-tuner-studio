using System.Buffers.Binary;

namespace PixelTuner.Sync;

/// <summary>
/// Minimal Art-Net reader: only ArtDmx (OpCode 0x5000) is needed to start, pause and
/// stop the show from a lighting desk. Byte order follows the Art-Net spec:
/// OpCode little endian, protocol version big endian, universe little endian,
/// length big endian. Vectors: legacy/EpicPlayer/epicplayer-mac/Tests/Fixtures/artnet-vectors.json.
/// </summary>
public static class ArtNet
{
    public const int Port = 6454;
    public const ushort OpDmx = 0x5000;
    public const ushort ProtocolVersion = 14;

    static ReadOnlySpan<byte> Id => "Art-Net\0"u8;
    const int HeaderLength = 18;

    public static byte[] EncodeDmx(ushort universe, byte sequence, byte physical, ReadOnlySpan<byte> data)
    {
        if (data.Length is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(data), "DMX data must be 1..512 bytes.");

        var packet = new byte[HeaderLength + data.Length];
        Id.CopyTo(packet);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), OpDmx);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(10), ProtocolVersion);
        packet[12] = sequence;
        packet[13] = physical;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(14), universe);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(16), (ushort)data.Length);
        data.CopyTo(packet.AsSpan(HeaderLength));
        return packet;
    }

    public static bool TryParseDmx(ReadOnlySpan<byte> packet, out ArtDmx dmx)
    {
        dmx = default;
        if (packet.Length < HeaderLength || !packet[..8].SequenceEqual(Id))
            return false;
        if (BinaryPrimitives.ReadUInt16LittleEndian(packet[8..]) != OpDmx)
            return false;

        var length = BinaryPrimitives.ReadUInt16BigEndian(packet[16..]);
        if (length is < 1 or > 512 || packet.Length < HeaderLength + length)
            return false;

        dmx = new ArtDmx(
            Universe: BinaryPrimitives.ReadUInt16LittleEndian(packet[14..]),
            Sequence: packet[12],
            Physical: packet[13],
            Data: packet.Slice(HeaderLength, length).ToArray());
        return true;
    }
}

public readonly record struct ArtDmx(ushort Universe, byte Sequence, byte Physical, byte[] Data)
{
    /// <summary>Channel value with 1-based numbering, as on a lighting desk.</summary>
    public byte? Channel(int channel) =>
        channel >= 1 && channel <= Data.Length ? Data[channel - 1] : null;
}

/// <summary>
/// Turns DMX channel values into playback commands. Reacts to a change of the value,
/// not to the value itself, so a held fader does not restart the track.
/// Defaults: 50 play, 100 pause, 150 stop.
/// </summary>
public sealed class DmxTrigger
{
    public byte PlayValue { get; init; } = 50;
    public byte PauseValue { get; init; } = 100;
    public byte StopValue { get; init; } = 150;

    byte? _last;

    public PlaybackCommand? OnValue(byte value)
    {
        if (_last == value)
            return null;
        _last = value;

        if (value == PlayValue) return PlaybackCommand.Play;
        if (value == PauseValue) return PlaybackCommand.Pause;
        if (value == StopValue) return PlaybackCommand.Stop;
        return null;
    }
}
