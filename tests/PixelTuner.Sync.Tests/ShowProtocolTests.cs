using System.Net;

namespace PixelTuner.Sync.Tests;

public class ShowProtocolTests
{
    static PlaybackCommand Parse(string name) => name switch
    {
        "play" => PlaybackCommand.Play,
        "pause" => PlaybackCommand.Pause,
        "stop" => PlaybackCommand.Stop,
        "next" or "nextTrack" => PlaybackCommand.Next,
        "previous" or "previousTrack" => PlaybackCommand.Previous,
        _ => throw new ArgumentException(name),
    };

    [Fact]
    public void Playback_packets_match_reference_vectors()
    {
        var vectors = Vectors.Load("protocol-vectors.json").GetProperty("playbackCommand");
        Assert.True(vectors.GetArrayLength() > 0);
        foreach (var v in vectors.EnumerateArray())
        {
            var packet = ShowProtocol.EncodePlayback(
                Parse(v.GetProperty("command").GetString()!),
                v.GetProperty("positionMilliseconds").GetUInt32());
            Assert.Equal(v.GetProperty("expected").GetString(), Vectors.ToHex(packet));
        }
    }

    [Fact]
    public void Lookup_request_matches_reference_vector()
    {
        var v = Vectors.Load("protocol-vectors.json").GetProperty("udpQuery")[0];
        Assert.Equal(v.GetProperty("expected").GetString(), Vectors.ToHex(ShowProtocol.EncodeLookupRequest()));
    }

    [Fact]
    public void Tcp_announcements_match_reference_vectors()
    {
        foreach (var v in Vectors.Load("protocol-vectors.json").GetProperty("tcpAnnouncement").EnumerateArray())
        {
            var packet = ShowProtocol.EncodeTcpAnnouncement(IPAddress.Parse(v.GetProperty("address").GetString()!));
            Assert.Equal(v.GetProperty("expected").GetString(), Vectors.ToHex(packet));
        }
    }

    [Fact]
    public void Device_replies_match_reference_vectors()
    {
        foreach (var v in Vectors.Load("artnet-vectors.json").GetProperty("deviceResponse").EnumerateArray())
        {
            var ok = ShowProtocol.TryParseDeviceReply(Vectors.Hex(v.GetProperty("hex").GetString()!), out var reply);
            Assert.Equal(v.GetProperty("isDevice").GetBoolean(), ok);
            var tag = v.GetProperty("tag");
            Assert.Equal(tag.ValueKind == System.Text.Json.JsonValueKind.Null ? null : tag.GetString(), ok ? reply.Tag : null);
        }
    }

    [Fact]
    public void Device_tag_is_decoded_as_windows_1251()
    {
        var data = new byte[59];
        data[0] = 0x49;
        // "Обруч" in Windows-1251.
        byte[] name = [0xCE, 0xE1, 0xF0, 0xF3, 0xF7];
        name.CopyTo(data, 47);
        Assert.True(ShowProtocol.TryParseDeviceReply(data, out var reply));
        Assert.Equal("Обруч", reply.Tag);
    }

    [Theory]
    [InlineData(-5, 0u)]
    [InlineData(0, 0u)]
    [InlineData(1234.9, 1234u)]
    public void Position_is_clamped_to_uint(double ms, uint expected) =>
        Assert.Equal(expected, ShowProtocol.ToPacketPosition(TimeSpan.FromMilliseconds(ms)));

    [Theory]
    [InlineData("192.168.88.17", "255.255.255.0", "192.168.88.255")]
    [InlineData("192.168.1.20", "255.255.0.0", "192.168.255.255")]
    [InlineData("10.0.0.5", "255.255.255.252", "10.0.0.7")]
    public void Broadcast_uses_the_netmask(string ip, string mask, string expected) =>
        Assert.Equal(IPAddress.Parse(expected), NetworkMath.Broadcast(IPAddress.Parse(ip), IPAddress.Parse(mask)));

    [Fact]
    public void Legacy_broadcast_replaces_last_octet() =>
        Assert.Equal(IPAddress.Parse("192.168.1.255"), NetworkMath.LegacyBroadcast(IPAddress.Parse("192.168.1.20")));
}
