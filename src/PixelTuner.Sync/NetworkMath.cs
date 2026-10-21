using System.Buffers.Binary;
using System.Net;

namespace PixelTuner.Sync;

/// <summary>IPv4 helpers for choosing where broadcasts go.</summary>
public static class NetworkMath
{
    /// <summary>Subnet broadcast address: <c>address | ~mask</c>.</summary>
    public static IPAddress Broadcast(IPAddress address, IPAddress netmask)
    {
        var a = ToUInt32(address);
        var m = ToUInt32(netmask);
        return FromUInt32(a | ~m);
    }

    /// <summary>
    /// Broadcast as PixelTuner 2 and EpicPlayer for Windows compute it: the last
    /// octet becomes 255 and the mask is ignored. Correct only for /24 networks.
    /// Kept for comparison, not for sending.
    /// </summary>
    public static IPAddress LegacyBroadcast(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return new IPAddress([b[0], b[1], b[2], 255]);
    }

    static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
            throw new ArgumentException("Only IPv4 addresses are supported.", nameof(address));
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    static IPAddress FromUInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return new IPAddress(bytes);
    }
}
