namespace PixelTuner.Sync;

/// <summary>Command byte at offset 2 of the 8-byte playback packet.</summary>
public enum PlaybackCommand : byte
{
    Play = 0x02,
    Pause = 0x03,
    Stop = 0x04,
    Next = 0x05,
    Previous = 0x06,
}
