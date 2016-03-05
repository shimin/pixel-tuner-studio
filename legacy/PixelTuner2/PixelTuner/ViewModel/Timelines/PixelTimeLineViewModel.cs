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

        public bool CanAddToTimeLine(ITimeLineDataItem item, DateTime timePoint)
        {
            return item is PixelThumbnailViewModel;
        }
    }

    public class PixelTimeLineViewModel : TimeLineViewModel
    {
        public PixelTimeLineViewModel()
        {
            
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
    }
}
