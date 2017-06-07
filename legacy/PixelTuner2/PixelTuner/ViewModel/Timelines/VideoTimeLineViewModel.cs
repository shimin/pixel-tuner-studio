using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using Common.Controls;

namespace PixelTuner.ViewModel
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

    public class VideoTimeLineViewModel : TimeLineViewModel
    {
        [XmlAttribute]
        public int LineHeight
        {
            get { return MetaData?.OrderedLeds.Count ?? 0; }
            set { }
        }

        private Color _lineColorTag = Color.FromRgb(0, 0, 255);
        [XmlIgnore]
        public Color LineColorTag
        {
            get { return _lineColorTag; }
            set { _lineColorTag = value; RaisePropertyChanged("LineColorTag"); }
        }

        [XmlAttribute]
        public string LineColorTagXml
        {
            get { return $"#{LineColorTag.A:X2}{LineColorTag.R:X2}{LineColorTag.G:X2}{LineColorTag.B:X2}"; }
            set
            {
                string c = value;
                byte a = byte.Parse(c.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
                byte r = byte.Parse(c.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(c.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(c.Substring(7, 2), System.Globalization.NumberStyles.HexNumber);
                LineColorTag = Color.FromArgb(a, r, g, b);
            }
        }


        #region SetLineColorTagCommand
        RelayCommand _setLineColorTagCommand = null;

        public ICommand SetLineColorTag
        {
            get
            {
                if (_setLineColorTagCommand == null)
                {
                    _setLineColorTagCommand = new RelayCommand((p) => OnSetLineColorTag(p));
                }

                return _setLineColorTagCommand;
            }
        }

        private void OnSetLineColorTag(object parameter)
        {
            var cd = new System.Windows.Forms.ColorDialog();
            var dr = cd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                Parent.PushUndo(true, this);
                LineColorTag = Color.FromRgb(cd.Color.R, cd.Color.G, cd.Color.B);
                IsChanged = true;
            }
        }

        #endregion

        public VideoTimeLineViewModel()
        {
            SetBackgrounds(Color.FromRgb(80, 60, 80), Color.FromRgb(70, 50, 70));
        }

        public override object Clone()
        {
            VideoTimeLineViewModel copyLine = (VideoTimeLineViewModel)base.Clone();
            copyLine.LineColorTag = LineColorTag;
            copyLine.MetaData = MetaData;
            return copyLine;
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
