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
            {"\0\r", "PixelPoi (Very First Version)"}
        };

        public static string TryGetName(string id)
        {
            if (idNamePairs.ContainsKey(id)) return idNamePairs[id];
            else return "Unknown Device";
        }
    }
}
