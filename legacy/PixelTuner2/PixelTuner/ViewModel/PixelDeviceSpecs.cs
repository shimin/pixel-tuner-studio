using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.Service
{
    static class PixelDeviceSpecs
    {
        static Dictionary<string, string> idNamePairs = new Dictionary<string, string>
        {
            {"\0\r", "Pixel Poi (Very First Version)"},
            {"STM32BOOT", "Pixel Poi 2 (Boot mode)"},
            {"STM32FW", "Pixel Poi 2"}
        };

        public static string TryGetName(string id)
        {
            if (idNamePairs.ContainsKey(id)) return idNamePairs[id];
            else return "Unknown Device";
        }
    }
}
