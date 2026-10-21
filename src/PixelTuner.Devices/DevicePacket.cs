namespace PixelTuner.Devices;

/// <summary>Commands of the cable and Wi-Fi protocol, as PixelTuner 2 named them.</summary>
public enum DeviceCommand : byte
{
    WhoIsIt = 0x53,
    Info = 0x49,
    VersionBoot = 0x56,
    VersionFirmware = 0x76,
    WriteFlashMode = 0x46,
    WriteDataMode = 0x66,
    WriteBlock = 0x42,
    EndWriteFlash = 0x43,
    EndWriteData = 0x63,
    SwitchToBoot = 0x62,
    WriteSettings = 0x73,
    PlaybackControl = 0x50,
}

/// <summary>
/// One packet: command, data length, data, 0x0D. Requests and answers look the same; the
/// device answers with the command it was asked.
/// </summary>
public static class DevicePacket
{
    public const byte Terminator = 0x0d;

    /// <summary>Largest data block: what a show upload to pixel, hoop and video devices uses.</summary>
    public const int MaxDataLength = 250;

    public static byte[] Encode(DeviceCommand command, ReadOnlySpan<byte> data = default)
    {
        if (data.Length > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(data), "A packet holds at most 255 data bytes.");
        var packet = new byte[data.Length + 3];
        packet[0] = (byte)command;
        packet[1] = (byte)data.Length;
        data.CopyTo(packet.AsSpan(2));
        packet[^1] = Terminator;
        return packet;
    }
}

/// <summary>The device answered, but not as the protocol says.</summary>
public sealed class DeviceProtocolException(string message) : IOException(message);
