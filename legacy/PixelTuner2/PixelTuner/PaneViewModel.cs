using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

using Common.ViewModel;

namespace PixelTuner
{
    public class PaneViewModel : ViewModelBase
    {
        public PaneViewModel()
        { }


        #region Title

        private string _title = null;
        [XmlIgnore]
        public string Title
        {
            get { return _title; }
            set
            {
                if (_title != value)
                {
                    _title = value;
                    RaisePropertyChanged("Title");
                }
            }
        }

        #endregion

        public virtual Uri IconSource
        {
            get
            {
                return null;
            }
        }

        #region ContentId

        private string _contentId = null;
        [XmlIgnore]
        public string ContentId
        {
            get { return _contentId; }
            set
            {
                if (_contentId != value)
                {
                    _contentId = value;
                    RaisePropertyChanged("ContentId");
                }
            }
        }

        #endregion


    }
}
