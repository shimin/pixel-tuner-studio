using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Xml.Serialization;

namespace PixelTuner.ViewModel.Serialization
{
    public class TimeLineItemEffectParameters
    {
        #region Properties

        [XmlAttribute]
        public String ColorFirst { get; set; }
        
        [XmlAttribute]
        public String ColorLast { get; set; }

        [XmlAttribute]
        public int T1 { get; set; }

        [XmlIgnore]
        public bool T1Specified;

        [XmlAttribute]
        public int T2 { get; set; }

        [XmlIgnore]
        public bool T2Specified;

        #endregion
    }
}
