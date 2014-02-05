using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public static class PixelDeviceSpecs
    {
        private static Dictionary<string, string> idNamePairs;

        static PixelDeviceSpecs()
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary.Add("\0\r", "Pixel Poi (Very First Version)");
            dictionary.Add("STM32BOOT", "Pixel Poi 2 (Boot mode)");
            dictionary.Add("STM32FW", "Pixel Poi 2");
            dictionary.Add("PP2BOOT", "Pixel Poi 2 (Boot mode)");
            dictionary.Add("PP2FW", "Pixel Poi 2");
            dictionary.Add("UDMXBOOT", "uDmx master (Boot mode)");
            dictionary.Add("UDMXFW", "uDmx master");
            dictionary.Add("LRBOOT", "Light master (Boot mode)");
            dictionary.Add("LRFW", "Light master");
            idNamePairs = dictionary;
        }

        public static string TryGetName(string id)
        {
            if (!(string.IsNullOrEmpty(id) || !idNamePairs.ContainsKey(id)))
            {
                return idNamePairs[id];
            }
            return "Unknown Device";
        }
    }
}
