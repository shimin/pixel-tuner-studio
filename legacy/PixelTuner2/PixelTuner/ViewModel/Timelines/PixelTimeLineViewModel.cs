using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using Common.Collections;
using Common.Controls;
using Common.ViewModel;
using PixelTuner.ViewModel.Serialization;

namespace PixelTuner.ViewModel
{
    /// <summary>
    /// Класс, определяющий правила переноса элементов на таймлайн.
    /// </summary>
    public class PixelItemDragDropManager : ITimeLineManager
    {
        protected TimeLineViewModel _line;
        public PixelItemDragDropManager(TimeLineViewModel timeline)
        {
            _line = timeline;
        }

        public bool CanAddToTimeLine(ITimeLineMeta meta)
        {
            return false;
        }

        public bool CanAddToTimeLine(ITimeLineDataItem item, DateTime timePoint)
        {
            var i = item as PixelThumbnailViewModel;

            bool matchingHeight = _line.Items.Count == 0;
            if (!matchingHeight && i != null) 
                matchingHeight = i.ActualHeight == ((PixelThumbnailViewModel)_line.Items.First()).ActualHeight;
            
            return i != null && matchingHeight;
        }
    }

    public class PixelTimeLineViewModel : TimeLineViewModel
    {
        public PixelTimeLineViewModel() {
            Items.CollectionChanged += (s, e) => { RaisePropertyChanged("LineHeight"); };
        }

        [XmlIgnore]
        public int LineHeight
        {
            get
            {
                int h = 0;
                if (Items.Count != 0) h = ((PixelThumbnailViewModel)Items.First()).ActualHeight;
                return h;
            }
            set
            {
                RaisePropertyChanged("LineHeight");
            }
        }
        
        #region ITimeLineManager Members

        ITimeLineManager _dragManager;
        public override ITimeLineManager DragManager
        {
            get
            {
                if (_dragManager == null) _dragManager = new PixelItemDragDropManager(this);
                return _dragManager;
            }
        }

        #endregion

        public override object Clone()
        {
            PixelTimeLineViewModel copyLine = (PixelTimeLineViewModel)base.Clone();
            copyLine.LineHeight = LineHeight;
            return copyLine;
        }
    }
}
