using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace PixelTuner.Drawing
{
    public abstract class ConfigurationItemBase
    {
        string _name;
        [XmlAttribute]
        public virtual string Name
        {
            get { return _name; }
            set
            {
                if (_name != value) _name = value;
            }
        }

        #region Конструктор
        public ConfigurationItemBase()
            : this("")
        { }

        public ConfigurationItemBase(string name)
        {
            _name = name;
        } 
        #endregion

        public abstract ConfigurationItemBase Clone();
    }
}
