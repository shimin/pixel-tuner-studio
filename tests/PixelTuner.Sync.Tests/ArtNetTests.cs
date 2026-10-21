namespace PixelTuner.Sync.Tests;

public class ArtNetTests
{
    [Fact]
    public void ArtDmx_round_trips_reference_vectors()
    {
        foreach (var v in Vectors.Load("artnet-vectors.json").GetProperty("artDmx").EnumerateArray())
        {
            var universe = (ushort)v.GetProperty("universe").GetInt32();
            var sequence = (byte)v.GetProperty("sequence").GetInt32();
            var physical = (byte)v.GetProperty("physical").GetInt32();

            byte[] data = v.TryGetProperty("dmxLong", out _)
                ? new byte[v.GetProperty("dmxCount").GetInt32()]
                : v.GetProperty("dmxValues").EnumerateArray().Select(x => (byte)x.GetInt32()).ToArray();

            var packet = ArtNet.EncodeDmx(universe, sequence, physical, data);
            var hex = Vectors.ToHex(packet);
            if (v.TryGetProperty("expectedHex", out var full))
                Assert.Equal(full.GetString(), hex);
            else
                Assert.StartsWith(v.GetProperty("expectedPrefix").GetString()!, hex);

            Assert.True(ArtNet.TryParseDmx(packet, out var dmx));
            Assert.Equal(universe, dmx.Universe);
            Assert.Equal(sequence, dmx.Sequence);
            Assert.Equal(data, dmx.Data);
        }
    }

    [Fact]
    public void Channels_are_one_based()
    {
        Assert.True(ArtNet.TryParseDmx(ArtNet.EncodeDmx(1, 0, 0, [10, 20, 30]), out var dmx));
        Assert.Equal((byte)10, dmx.Channel(1));
        Assert.Equal((byte)30, dmx.Channel(3));
        Assert.Null(dmx.Channel(0));
        Assert.Null(dmx.Channel(4));
    }

    [Fact]
    public void Rejects_other_packets()
    {
        Assert.False(ArtNet.TryParseDmx("Art-Net\0"u8.ToArray(), out _));
        Assert.False(ArtNet.TryParseDmx([0x50, 0x05, 0x02, 0, 0, 0, 0, 0x0D], out _));
    }

    [Fact]
    public void Trigger_reacts_to_changes_only()
    {
        var thresholds = Vectors.Load("artnet-vectors.json").GetProperty("channelThresholds")[0];
        var trigger = new DmxTrigger
        {
            PlayValue = (byte)thresholds.GetProperty("start").GetInt32(),
            PauseValue = (byte)thresholds.GetProperty("pause").GetInt32(),
            StopValue = (byte)thresholds.GetProperty("stop").GetInt32(),
        };

        Assert.Equal(PlaybackCommand.Play, trigger.OnValue(50));
        Assert.Null(trigger.OnValue(50));
        Assert.Null(trigger.OnValue(0));
        Assert.Equal(PlaybackCommand.Pause, trigger.OnValue(100));
        Assert.Equal(PlaybackCommand.Stop, trigger.OnValue(150));
        Assert.Equal(PlaybackCommand.Play, trigger.OnValue(50));
    }
}
