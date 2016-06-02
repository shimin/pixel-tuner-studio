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
        protected TimeLineViewModel _line;
        public LightItemDragDropManager(TimeLineViewModel timeline)
        {
            _line = timeline;
        }

        public bool CanAddToTimeLine(ITimeLineMeta meta)
        {
            return meta is CanvasElementViewModel;
        }

        public bool CanAddToTimeLine(ITimeLineDataItem item, DateTime timePoint)
        {
            if (item is SolidColorThumbnailViewModel || item is EffectThumbnailViewModel) return true;
            return false;
        }
    }
    
    public class LightTimeLineViewModel
        : TimeLineViewModel
    {
        public LightTimeLineViewModel() {
            RootViewModel.This.PropertyChanged += new PropertyChangedEventHandler(RootModel_PropertyChanged);
            this.PropertyChanged += new PropertyChangedEventHandler(TimeLineViewModel_PropertyChanged);
            SetBackgrounds(Color.FromRgb(60, 60, 80), Color.FromRgb(50, 50, 70));
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
        public override ITimeLineManager DragManager
        {
            get
            {
                if (_dragManager == null) _dragManager = new LightItemDragDropManager(this);
                return _dragManager;
            }
        }

        #endregion
       
        #region Properties
        
        /// <summary>
        /// Step of rulers on timelines to link with items borders
        /// </summary>
        public int AnchorStep
        {
            get { return RootViewModel.This.AnchorStep; }
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

        public override object Clone()
        {
            return base.Clone();
        }
    }
}
