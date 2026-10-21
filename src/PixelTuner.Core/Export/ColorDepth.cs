namespace PixelTuner.Core.Export;

/// <summary>Bits per pixel stored in a device .bin. Values match PixelTuner 2 settings.</summary>
public enum ColorDepth
{
    /// <summary>4 bits per channel.</summary>
    Bit12 = 0,

    /// <summary>8 bits per channel. The PixelTuner 2 default and what current devices use.</summary>
    Bit24 = 1,

    /// <summary>12 bits per channel through the LED calibration tables, 2 bytes each.</summary>
    Bit36 = 2,
}
