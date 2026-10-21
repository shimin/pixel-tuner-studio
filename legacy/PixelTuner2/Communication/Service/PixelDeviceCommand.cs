using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public enum PixelDeviceCommand
    {
        WHO_IS_IT = 0x53,
        INFO = 0x49,
        VERSION_BOOT = 0x56,
        VERSION_FW = 0x76,
        WRITE_IFLASH_MODE = 0x46,
        WRITE_IDATA_MODE = 0x66,
        WRITE_BLOCK = 0x42,
        END_WRITE_FLASH = 0x43,
        END_WRITE_DATA = 0x63,
        SWITCH_TO_BOOT = 0x62,
        WRITE_SETTINGS = 0x73,
        PLAYBACK_CONTROL = 0x50
    }
}
