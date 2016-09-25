using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Xml.Serialization;
using Common.Controls;

namespace PixelTuner.ViewModel
{
    /// <summary>
    /// Класс, определяющий правила переноса элементов на таймлайн.
    /// </summary>
    public class EpicItemDragDropManager : ITimeLineManager
    {
        protected TimeLineViewModel _line;
        public EpicItemDragDropManager(TimeLineViewModel timeline)
        {
            _line = timeline;
        }

        public bool CanAddToTimeLine(ITimeLineMeta meta)
        {
            return false;
        }

        public bool CanAddToTimeLine(ITimeLineDataItem item, DateTime timePoint)
        {
            return item is GradientThumbnailViewModel;
        }
    }

    public class EpicPoiTimeLineViewModel : TimeLineViewModel
    {
        [XmlAttribute]
        public override int TimelineHeightPx
        {
            get { return 5; }
            set { }
        }

        public EpicPoiTimeLineViewModel()
        {
            SetBackgrounds(Color.FromRgb(80, 60, 60), Color.FromRgb(70, 50, 50));
        }

        #region ITimeLineManager Members

        ITimeLineManager _dragManager;
        public override ITimeLineManager DragManager
        {
            get
            {
                if (_dragManager == null) _dragManager = new EpicItemDragDropManager(this);
                return _dragManager;
            }
        }

        #endregion
        
    }
}
