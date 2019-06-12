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

            //dictionary.Add("STM32BOOT", "Pixel Poi 2 (Boot mode)");
            //idClassPairs.Add("STM32BOOT", "Communication.Service.PixelDevice");
            //dictionary.Add("STM32FW", "Pixel Poi 2");
            //idClassPairs.Add("STM32FW", "Communication.Service.PixelDevice");

            dictionary.Add("STM32BOOT", "Pixel Poi 2 (Not supported)");
            idClassPairs.Add("STM32BOOT", "Communication.Service.GenericSerialDevice");
            dictionary.Add("STM32FW", "Pixel Poi 2 (Not supported)");
            idClassPairs.Add("STM32FW", "Communication.Service.GenericSerialDevice");

            dictionary.Add("E-PIC_FW", "Pixel Poi 3");
            idClassPairs.Add("E-PIC_FW", "Communication.Service.PixelDevice");
            dictionary.Add("E-PICBOOT", "Pixel Poi 3 (Boot mode)");
            idClassPairs.Add("E-PICBOOT", "Communication.Service.PixelDevice");

            dictionary.Add("E-PIC_v4FW", "Pixel Poi 4");
            idClassPairs.Add("E-PIC_v4FW", "Communication.Service.PixelDevice");
            dictionary.Add("E-PIC_v4BOOT", "Pixel Poi 4 (Boot mode)");
            idClassPairs.Add("E-PIC_v4BOOT", "Communication.Service.PixelDevice");

            dictionary.Add("E-PIC_v5FW", "Pixel Poi 5");
            idClassPairs.Add("E-PIC_v5FW", "Communication.Service.PixelDevice");
            dictionary.Add("E-PIC_v5BOOT", "Pixel Poi 5 (Boot mode)");
            idClassPairs.Add("E-PIC_v5BOOT", "Communication.Service.PixelDevice");

            dictionary.Add("E-PIC_v6FW", "Pixel Poi 6");
            idClassPairs.Add("E-PIC_v6FW", "Communication.Service.PixelDevice");
            dictionary.Add("E-PIC_v6BOOT", "Pixel Poi 6 (Boot mode)");
            idClassPairs.Add("E-PIC_v6BOOT", "Communication.Service.PixelDevice");

            dictionary.Add("EPIC_PHOOP_v1FW", "Pixel Hoop");
            idClassPairs.Add("EPIC_PHOOP_v1FW", "Communication.Service.PixelHoop");
            dictionary.Add("EPIC_PHOOP_v1BOOT", "Pixel Hoop (Boot mode)");
            idClassPairs.Add("EPIC_PHOOP_v1BOOT", "Communication.Service.PixelHoop");

            dictionary.Add("E-PIC_SFW", "E-pic poi");
            idClassPairs.Add("E-PIC_SFW", "Communication.Service.SimplePoiDevice");
            dictionary.Add("E-PIC_SBOOT", "E-pic poi (Boot mode)");
            dictionary.Add("E-PIC_SBO", "E-pic poi (Boot mode)");
            idClassPairs.Add("E-PIC_SBOOT", "Communication.Service.SimplePoiDevice");
            idClassPairs.Add("E-PIC_SBO", "Communication.Service.SimplePoiDevice");

            dictionary.Add("LRBOOT", "Light master (Boot mode)");
            idClassPairs.Add("LRBOOT", "Communication.Service.LightMasterDevice");
            dictionary.Add("LRFW", "Light master");
            idClassPairs.Add("LRFW", "Communication.Service.LightMasterDevice");

            dictionary.Add("LR_v3BOOT", "Light master (Boot mode)");
            idClassPairs.Add("LR_v3BOOT", "Communication.Service.LightMasterDevice");
            dictionary.Add("LR_v3FW", "Light master");
            idClassPairs.Add("LR_v3FW", "Communication.Service.LightMasterDevice");

            dictionary.Add("ILBOOT", "Illuminator (Boot mode)");
            idClassPairs.Add("ILBOOT", "Communication.Service.IlluminatorDevice");
            dictionary.Add("ILFW", "Illuminator");
            idClassPairs.Add("ILFW", "Communication.Service.IlluminatorDevice");

            dictionary.Add("PLR_v1BOOT", "Video module (Boot mode)");
            idClassPairs.Add("PLR_v1BOOT", "Communication.Service.VideoDevice");
            dictionary.Add("PLR_v1FW", "Video module");
            idClassPairs.Add("PLR_v1FW", "Communication.Service.VideoDevice");
            
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
