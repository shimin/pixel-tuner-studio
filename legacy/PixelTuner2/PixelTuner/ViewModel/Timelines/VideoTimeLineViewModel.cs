using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Common.Controls;

namespace PixelTuner.ViewModel.Timelines
{
    /// <summary>
    /// Класс, определяющий правила переноса элементов на таймлайн.
    /// </summary>
    public class VideoItemDragDropManager : ITimeLineManager
    {
        protected TimeLineViewModel _line;
        public VideoItemDragDropManager(TimeLineViewModel timeline)
        {
            _line = timeline;
        }

        public bool CanAddToTimeLine(ITimeLineMeta meta)
        {
            return meta is CanvasElementViewModel;
        }

        public bool CanAddToTimeLine(ITimeLineDataItem item, DateTime timePoint)
        {
            if (item is VideoThumbnailViewModel) return true;
            return false;
        }
    }

    public class VideoTimeLineViewModel : LightTimeLineViewModel
    {
        [XmlAttribute]
        public int LineHeight
        {
            get { return MetaData?.OrderedLeds.Count ?? 0; }
            set { }
        }

        #region ITimeLineManager Members

        ITimeLineManager _dragManager;
        public override ITimeLineManager DragManager
        {
            get
            {
                if (_dragManager == null) _dragManager = new VideoItemDragDropManager(this);
                return _dragManager;
            }
        }

        #endregion
    }
}
