using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace PixelTuner.Devices;

/// <summary>
/// One device setting: id, value length, value (little endian), as PixelTuner 2's
/// DeviceFeature. The factory methods are the settings PixelTuner 2 wrote.
/// </summary>
public sealed record DeviceSetting(ushort Id, byte[] Value)
{
    public byte[] Encode()
    {
        var bytes = new byte[4 + Value.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, Id);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), checked((ushort)Value.Length));
        Value.CopyTo(bytes, 4);
        return bytes;
    }

    private static DeviceSetting UInt16(ushort id, ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return new DeviceSetting(id, bytes);
    }

    private static DeviceSetting Text(ushort id, string value, Encoding encoding) =>
        new(id, encoding.GetBytes(value + '\0'));

    // Menu behaviour (Pixel Poi, Pixel Hoop).
    public static DeviceSetting FadeDownMenuAfter(ushort value) => UInt16(1, value);
    public static DeviceSetting ShutdownAfterIdle(ushort value) => UInt16(2, value);
    public static DeviceSetting ExitMenuOnDoubleClick(bool value) => UInt16(3, (ushort)(value ? 1 : 0));

    /// <summary>Pixel Poi 4: the radio address of the device that leads the others.</summary>
    public static DeviceSetting MasterMac(byte[] mac) => new(4, mac);

    // Wi-Fi (video modules, Pixel Hoop, Pixel Poi, LightMaster).
    public static DeviceSetting WifiNetwork(string ssid) => Text(5, ssid, Encoding.ASCII);
    public static DeviceSetting WifiPassword(string password) => Text(6, password, Encoding.ASCII);
    public static DeviceSetting WifiAddress(IPAddress address) => new(7, address.GetAddressBytes());
    public static DeviceSetting WifiPort(ushort port) => UInt16(8, port);

    /// <summary>Name the device reports over the network, in Windows-1251, at most 11 characters.</summary>
    public static DeviceSetting Tag(string tag)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Text(11, tag, Encoding.GetEncoding(1251));
    }

    // LED strips (video modules, Pixel Hoop).
    public static DeviceSetting StripLengths(ushort first, ushort second)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, first);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), second);
        return new DeviceSetting(9, bytes);
    }

    /// <summary>LED chip type, 1-based as in PixelTuner 2's list.</summary>
    public static DeviceSetting LedType(ushort type) => UInt16(10, type);

    /// <summary>Colour order of the LEDs, 1-based as in PixelTuner 2's list (one byte).</summary>
    public static DeviceSetting LedOrder(byte order) => new(12, [order]);

    /// <summary>Pixel Poi 5–6, Pixel Hoop: this device leads all the others.</summary>
    public static DeviceSetting Leader(bool value) => UInt16(0x0d, (ushort)(value ? 1 : 0));
}
