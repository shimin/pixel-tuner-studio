using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public static class SupportedDevices
    {
        private static Dictionary<string, string> idNamePairs;
        private static Dictionary<string, string> idClassPairs;

        static SupportedDevices()
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            idClassPairs = new Dictionary<string, string>();

            dictionary.Add("\0\r", "Pixel Poi (Very First Version)");

            dictionary.Add("STM32BOOT", "Pixel Poi 2 (Boot mode)");
            idClassPairs.Add("STM32BOOT", "Communication.Service.PixelDevice");
            dictionary.Add("STM32FW", "Pixel Poi 2");
            idClassPairs.Add("STM32FW", "Communication.Service.PixelDevice");
            
            dictionary.Add("E-PIC_FW", "Pixel Poi 3");
            idClassPairs.Add("E-PIC_FW", "Communication.Service.PixelDevice");
            dictionary.Add("E-PICBOOT", "Pixel Poi 3 (Boot mode)");
            idClassPairs.Add("E-PICBOOT", "Communication.Service.PixelDevice");

            dictionary.Add("E-PIC_v4FW", "Pixel Poi 4");
            idClassPairs.Add("E-PIC_v4FW", "Communication.Service.PixelDevice");
            dictionary.Add("E-PIC_v4BOOT", "Pixel Poi 4 (Boot mode)");
            idClassPairs.Add("E-PIC_v4BOOT", "Communication.Service.PixelDevice");

            dictionary.Add("E-PIC_SFW", "Simple poi");
            idClassPairs.Add("E-PIC_SFW", "Communication.Service.SimplePoiDevice");
            dictionary.Add("E-PIC_SBOOT", "Simple poi");
            idClassPairs.Add("E-PIC_SBOOT", "Communication.Service.SimplePoiDevice");
            
            dictionary.Add("LRBOOT", "Light master (Boot mode)");
            idClassPairs.Add("LRBOOT", "Communication.Service.LightMasterDevice");
            dictionary.Add("LRFW", "Light master");
            idClassPairs.Add("LRFW", "Communication.Service.LightMasterDevice");

            dictionary.Add("ILBOOT", "Illuminator (Boot mode)");
            idClassPairs.Add("ILBOOT", "Communication.Service.IlluminatorDevice");
            dictionary.Add("ILFW", "Illuminator");
            idClassPairs.Add("ILFW", "Communication.Service.IlluminatorDevice");

            dictionary.Add("VIDEOBOOT", "Video module (Boot mode)");
            idClassPairs.Add("VIDEOBOOT", "Communication.Service.VideoDevice");
            dictionary.Add("VIDEOFW", "Video module");
            idClassPairs.Add("VIDEOFW", "Communication.Service.VideoDevice");
            
            dictionary.Add("UDMXBOOT", "uDmx master (Boot mode)");  // unused
            dictionary.Add("UDMXFW", "uDmx master");                // unused
            dictionary.Add("PP2BOOT", "Pixel Poi 2 (Boot mode)");   // unused
            dictionary.Add("PP2FW", "Pixel Poi 2");                 // unused
            
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

        public static string TryGetType(string id)
        {
            if (!(string.IsNullOrEmpty(id) || !idClassPairs.ContainsKey(id)))
            {
                return idClassPairs[id];
            }

            return "Communication.Service.GenericSerialDevice";
        }
    }
}
