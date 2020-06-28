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

        protected override bool CanFlipLine(object parameter)
        {
            return this.Items != null && this.Items.Count > 0;
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
            copyLine.LineColorTag = LineColorTag;
            return copyLine;
        }
    }
}
