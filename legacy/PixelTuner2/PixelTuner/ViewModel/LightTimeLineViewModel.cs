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
using PixelTuner.Properties;


namespace PixelTuner.ViewModel
{
    /// <summary>
    /// Класс, определяющий правила переноса элементов на таймлайн.
    /// </summary>
    public class LightItemDragDropManager : ITimeLineManager
    {
        protected LightTimeLineViewModel _line;
        public LightItemDragDropManager(LightTimeLineViewModel timeline)
        {
            _line = timeline;
        }

        public bool CanAddToTimeLine(ITimeLineDataItem item, DateTime timePoint)
        {
            if (_line.Items.Count == 0) return true;
            if (item is SolidColorThumbnailViewModel || item is EffectThumbnailViewModel) return true;

            return false;
        }
    }
    
    public class LightTimeLineViewModel
        : TimeLineViewModel
    {
        public LightTimeLineViewModel() {
            _xmlItems = new DelegatedIList<TimeLineItemViewModel, TimeLineItem>(_items);
            RootViewModel.This.PropertyChanged += new PropertyChangedEventHandler(RootModel_PropertyChanged);
            this.PropertyChanged += new PropertyChangedEventHandler(TimeLineViewModel_PropertyChanged);
        }

        void TimeLineViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (MetaData == null)
            {
                if (!string.IsNullOrEmpty(MetaDataPath))
                {
                    CanvasElementViewModel m = null;
                    RootViewModel.This.CanvasModel.ElementsDictionary.TryGetValue(MetaDataPath, out m);
                    if (m != null) MetaData = m;
                }
            }
        }

        void RootModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "AnchorStep")
                RaisePropertyChanged("AnchorStep");
        }

        #region ITimeLineManager Members
        
        ITimeLineManager _dragManager;
        public ITimeLineManager DragManager
        {
            get
            {
                if (_dragManager == null) _dragManager = new LightItemDragDropManager(this);
                return _dragManager;
            }
        }

        #endregion
       
        #region Properties

        private bool _isSelectedToUpload = true;
        [XmlIgnore]
        public bool IsSelectedToUpload
        {
            get { return _isSelectedToUpload; }
            set { _isSelectedToUpload = value; RaisePropertyChanged("IsSelectedToUpload"); }
        }

        int _timelineHeightPx = 120;
        [XmlIgnore]
        public int TimelineHeightPx
        {
            get { return _timelineHeightPx; }
            set { _timelineHeightPx = value; RaisePropertyChanged("TimelineHeightPx"); }
        }

        int _timelinePreviewWidth = 120;
        [XmlIgnore]
        public int TimelinePreviewWidth
        {
            get { return _timelinePreviewWidth; }
            set { _timelinePreviewWidth = value; RaisePropertyChanged("TimelinePreviewWidth"); }
        }

        string _metaDataPath = null;
        [XmlAttribute]
        public string MetaDataPath
        {
            get { return _metaDataPath; }
            set { _metaDataPath = value; RaisePropertyChanged("MetaDataPath"); }
        }

        CanvasElementViewModel _metaData;
        [XmlIgnore]
        public CanvasElementViewModel MetaData
        {
            get { return _metaData; }
            set 
            {
                _metaData = value;
                var ce = _metaData as CanvasElementViewModel;
                if (ce != null)
                {
                    MetaDataPath = ce.Path;
                    PreviewTransform = new MatrixTransform(
                        ce.Transform.Value.M11,
                        ce.Transform.Value.M12,
                        ce.Transform.Value.M21,
                        ce.Transform.Value.M22,
                        -ce.Geometry.Bounds.Left,
                        -ce.Geometry.Bounds.Top);
                }
                RaisePropertyChanged("MetaData"); 
            }
        }

        MatrixTransform _previewTransform = null;
        [XmlIgnore]
        public MatrixTransform PreviewTransform
        {
            get { return _previewTransform; }
            set { _previewTransform = value; RaisePropertyChanged("PreviewTransform"); }
        }

        /// <summary>
        /// Step of rulers on timelines to link with items borders
        /// </summary>
        public int AnchorStep
        {
            get { return RootViewModel.This.AnchorStep; }
        }

        DelegatedIList<PixelTuner.ViewModel.TimeLineItemViewModel, TimeLineItem> _xmlItems;
        [XmlElement(ElementName = "Item", Type = typeof(TimeLineItem))]
        public DelegatedIList<PixelTuner.ViewModel.TimeLineItemViewModel, TimeLineItem> XmlItems
        {
            get
            {
                return _xmlItems;
            }
            set
            {
                _xmlItems = value;
            }
        }

        #endregion

        internal void TrimTimeSpaces()
        {
            // time borders correction
            ITimeLineDataItem prevItem = null;
            foreach (ITimeLineDataItem i in this.Items)
            {
                var ts = i.EndTime.Value - i.StartTime.Value;
                if (prevItem != null)
                {
                    i.StartTime = prevItem.EndTime;
                }
                else
                {
                    i.StartTime = DateTime.MinValue.AddYears(1);
                }
                // this should occur only when "apply to all" selected
                i.EndTime = i.StartTime.Value.AddTicks(ts.Ticks);
                prevItem = i;
            }
        }
        
        internal void InsertItems(ObservableCollection<ITimeLineDataItem> itemsToPaste, int insertIndex)
        {
            if (itemsToPaste.Count > 0)
            {
                DateTime? startTime = GetStartTimeByIndex(insertIndex);
                TimeSpan delta = startTime.Value - itemsToPaste.First().StartTime.Value;

                List<ITimeLineDataItem> copiedItems = new List<ITimeLineDataItem>();
                foreach (var i in itemsToPaste)
                {
                    var clone = (ITimeLineDataItem)i.Clone();
                    clone.StartTime = clone.StartTime + delta;
                    clone.EndTime = clone.EndTime + delta;
                    copiedItems.Add(clone);
                }

                var keyTimeEnd = copiedItems.Last().EndTime;
                var keyTimeStart = copiedItems.First().StartTime;
                if (Settings.Default.CutNextItems)
                {
                    List<ITimeLineDataItem> itemsToRemove = new List<ITimeLineDataItem>();
                    foreach (var i in Items)
                    {
                        if (i.EndTime < keyTimeEnd && i.StartTime >= keyTimeStart) itemsToRemove.Add(i);
                        else if (i.StartTime < keyTimeEnd && i.StartTime >= keyTimeStart) i.StartTime = keyTimeEnd;
                    }
                    itemsToRemove.ForEach(i => { (i as TimeLineItemViewModel).DisposeMe.Execute(null); });
                }
                else
                {
                    if (insertIndex < Items.Count && Items[insertIndex].StartTime < keyTimeEnd)
                    {
                        TimeSpan moveItemsDelta = keyTimeEnd.Value - Items[insertIndex].StartTime.Value;
                        for (int i = insertIndex; i < Items.Count; i++)
                        {
                            Items[i].StartTime = Items[i].StartTime + moveItemsDelta;
                            Items[i].EndTime = Items[i].EndTime + moveItemsDelta;
                        }
                    }
                }

                foreach (var i in copiedItems)
                {
                    Items.Insert(insertIndex, i);
                    insertIndex++;
                }
            }
        }

        private DateTime? GetStartTimeByIndex(int idx)
        {
            if (Items.Count == 0)
                return this.Start;
            else if (idx >= Items.Count)
                return Items.Last().EndTime;
            else if (idx == 0)
                return this.Start;
            else return Items[idx - 1].EndTime;
        }
    }
}
