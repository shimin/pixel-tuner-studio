using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Xml.Serialization;

using Common.Controls;
using Common.Collections;
using Common.ViewModel;
using PixelTuner.ViewModel.Serialization;
using System.Windows.Input;

namespace PixelTuner.ViewModel
{
    /// <summary>
    /// Класс, определяющий правила переноса элементов на таймлайн.
    /// </summary>
    public class ItemDragDropManager : ITimeLineManager
    {
        protected TimeLineViewModel _line;
        public ItemDragDropManager(TimeLineViewModel timeline)
        {
            _line = timeline;
        }

        public bool CanAddToTimeLine(ITimeLineDataItem item, DateTime timePoint)
        {
            return item is TimeLineItemViewModel;
        }
    }

    public class TimeLineViewModel : ViewModelBase
    {
        #region Properties

        FileViewModel _parent;
        [XmlIgnore]
        public FileViewModel Parent
        {
            get { return _parent; }
            set { _parent = value; }
        }

        Brush _darkLineColor = new SolidColorBrush(Color.FromArgb(255, 64, 64, 64));

        Brush _lightLineColor = new SolidColorBrush(Color.FromArgb(255, 70, 70, 70));

        Brush _backGround;
        [XmlIgnore]
        public Brush Background
        {
            get 
            {
                if (Parent.TimeLines.IndexOf(this) % 2 == 0) return _lightLineColor;
                else return _darkLineColor;
            }
            set { RaisePropertyChanged("Background"); }
        }

        private bool _isSelectedToUpload = true;
        [XmlIgnore]
        public bool IsSelectedToUpload
        {
            get { return _isSelectedToUpload; }
            set { _isSelectedToUpload = value; RaisePropertyChanged("IsSelectedToUpload"); }
        }

        [XmlIgnore]
        public virtual System.Windows.Visibility IsLineHeightVisible
        {
            get { return System.Windows.Visibility.Collapsed; }
            set { RaisePropertyChanged("IsLineHeightVisible"); }
        }

        private bool _isSelected = false;
        [XmlIgnore]
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; RaisePropertyChanged("IsSelected"); }
        }

        private bool _isChanged = false;
        [XmlIgnore]
        public bool IsChanged
        {
            get { return _isChanged; }
            set { _isChanged = value; if (Parent != null) Parent.IsDirty = true; }
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

        Double _defaultLength;
        [XmlIgnore]
        public Double DefaultTimeInterval
        {
            get { return _defaultLength = PixelTuner.Properties.Settings.Default.DefaultDuration; }
            set { _defaultLength = value; RaisePropertyChanged("DefaultTimeInterval"); }
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

        // for comparasions in TimeLineControl which all use 'DateTime.MinValue'
        DateTime? _start = DateTime.MinValue.AddYears(1);
        [XmlIgnore]
        public DateTime? Start
        {
            get { return _start; }
            set
            {
                _start = value;
                RaisePropertyChanged("Start");
            }
        }

        #region ITimeLineManager Members

        ITimeLineManager _dragManager;
        public virtual ITimeLineManager DragManager
        {
            get
            {
                if (_dragManager == null) _dragManager = new ItemDragDropManager(this);
                return _dragManager;
            }
        }

        #endregion
       
        protected ObservableCollection<ITimeLineDataItem> _items
            = new ObservableCollection<ITimeLineDataItem>();
        [XmlIgnore]
        public ObservableCollection<ITimeLineDataItem> Items
        {
            get
            {
                return _items;
            }
            set
            {
                _items = value;
                RaisePropertyChanged("Items");
            }
        }

        #endregion

        public TimeLineViewModel()
        {
            _xmlItems = new DelegatedIList<TimeLineItemViewModel, TimeLineItem>(_items);
            _items.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(_items_CollectionChanged);
        }

        void _items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Console.WriteLine(e.Action.ToString());
        }

        #region Commands

        #region MoveUpCommand
        RelayCommand _moveUpCommand = null;
        public ICommand MoveUp
        {
            get
            {
                if (_moveUpCommand == null)
                {
                    _moveUpCommand = new RelayCommand((p) => OnMoveUp(p), (p) => CanMoveUp(p));
                }

                return _moveUpCommand;
            }
        }

        private bool CanMoveUp(object parameter)
        {
            if (Parent != null)
            {
                int idx = Parent.TimeLines.IndexOf(this);
                return idx > 0;
            }
            return false;
        }

        private void OnMoveUp(object parameter)
        {
            if (Parent != null)
            {
                int old = Parent.TimeLines.IndexOf(this);
                Parent.TimeLines.Move(old, old - 1);
                Parent.TimeLines.All(arg => { arg.RaisePropertyChanged("Background"); return false; });
            }
        }

        #endregion

        #region MoveDownCommand

        RelayCommand _moveDownCommand = null;
        public ICommand MoveDown
        {
            get
            {
                if (_moveDownCommand == null)
                {
                    _moveDownCommand = new RelayCommand((p) => OnMoveDown(p), (p) => CanMoveDown(p));
                }

                return _moveDownCommand;
            }
        }

        private bool CanMoveDown(object parameter)
        {
            if (Parent != null)
            {
                int idx = Parent.TimeLines.IndexOf(this);
                return idx < Parent.TimeLines.Count - 1;
            }
            return false;
        }

        private void OnMoveDown(object parameter)
        {
            if (Parent != null)
            {
                int old = Parent.TimeLines.IndexOf(this);
                Parent.TimeLines.Move(old, old + 1);
                Parent.TimeLines.All(arg => { arg.RaisePropertyChanged("Background"); return false; });
            }
        }

        #endregion

        #region DeleteLineCommand
        RelayCommand _deleteLineCommand = null;
        public ICommand DeleteLine
        {
            get
            {
                if (_deleteLineCommand == null)
                {
                    _deleteLineCommand = new RelayCommand((p) => OnDeleteLine(p), (p) => CanDeleteLine(p));
                }

                return _deleteLineCommand;
            }
        }

        private bool CanDeleteLine(object parameter)
        {
            if (Parent != null)
            {
                return Parent.TimeLines.Count > 1;
            }
            return false;
        }

        private void OnDeleteLine(object parameter)
        {
            if (Parent != null)
                Parent.TimeLines.Remove(this);
        }

        #endregion

        #region CloneLineCommand
        RelayCommand _cloneLineCommand = null;
        public ICommand CloneLine
        {
            get
            {
                if (_cloneLineCommand == null)
                {
                    _cloneLineCommand = new RelayCommand((p) => OnCloneLine(p), (p) => CanCloneLine(p));
                }

                return _cloneLineCommand;
            }
        }

        private bool CanCloneLine(object parameter)
        {
            return Parent != null && Parent.TimeLines != null && this.Items != null && this.Items.Count > 0;
        }

        private void OnCloneLine(object parameter)
        {
            int idx = Parent.TimeLines.IndexOf(this);
            Parent.TimeLines.Insert(idx + 1, this.Clone());
        }

        private TimeLineViewModel Clone()
        {
            var timelineCopy = (TimeLineViewModel)Activator.CreateInstance(this.GetType());
            timelineCopy.DefaultTimeInterval = this.DefaultTimeInterval;
            timelineCopy.Start = this.Start;
            foreach (var item in this.Items)
                timelineCopy.Items.Add((ITimeLineDataItem)(item as TimeLineItemViewModel).Clone());
            return timelineCopy;
        }

        #endregion

        #endregion

        #region Serialization

        [XmlIgnore]
        public static List<Type> ExtraTypes
        {
            get
            {
                var result = new List<Type>()
                {
                    typeof(TimeLineItem),
                    typeof(TimeLineItemSolid),
                    typeof(TimeLineItemEffect),
                    typeof(TimeLineItemEffectParameters)
                };
                return result;
            }
        }

        DelegatedIList<TimeLineItemViewModel, TimeLineItem> _xmlItems;
        [XmlElement(ElementName = "Item", Type = typeof(TimeLineItem))]
        public DelegatedIList<TimeLineItemViewModel, TimeLineItem> XmlItems
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
        
        #region Methods

        public void SetBackgrounds(Color light, Color dark)
        {
            _lightLineColor = new SolidColorBrush(light);
            _darkLineColor = new SolidColorBrush(dark);
        }

        internal void InsertItems(ObservableCollection<TimeLineItemViewModel> itemsToPaste, int insertIndex)
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
                if (PixelTuner.Properties.Settings.Default.CutNextItems)
                {
                    CutItemsWithinInterval(keyTimeStart, keyTimeEnd);
                }
                else if (insertIndex < Items.Count)
                {
                    TimeSpan duration = keyTimeEnd.Value - Items[insertIndex].StartTime.Value;
                    MoveItemsAfterIndex(keyTimeStart, duration);
                }

                foreach (var i in copiedItems)
                {
                    Items.Insert(insertIndex, i);
                    insertIndex++;
                }
            }
        }

        /// <summary>
        /// Removes item if it's fully within time interval and adjusts only start time otherwise.
        /// </summary>
        /// <param name="start">Start of time interval</param>
        /// <param name="end">End of time interval</param>
        internal void CutItemsWithinInterval(DateTime? start, DateTime? end)
        {
            List<ITimeLineDataItem> itemsToRemove = new List<ITimeLineDataItem>();
            foreach (var i in Items)
            {
                if (i.EndTime <= end && i.StartTime >= start) itemsToRemove.Add(i);
                else if (i.StartTime < end && i.StartTime >= start) i.StartTime = end;
            }
            itemsToRemove.ForEach(i => { (i as TimeLineItemViewModel).DisposeMe.Execute(null); });
        }

        /// <summary>
        /// Moves all timeline items after start time point
        /// </summary>
        /// <param name="start">Start time point</param>
        /// <param name="moveInterval">Borders move interval</param>
        internal void MoveItemsAfterIndex(DateTime? start, TimeSpan moveInterval)
        {
            if (moveInterval.TotalMilliseconds > 0)
            {
                foreach (var i in Items)
                {
                    if (i.StartTime >= start)
                    {
                        i.StartTime = i.StartTime + moveInterval;
                        i.EndTime = i.EndTime + moveInterval;
                    }
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

        TimeLineItemViewModel _lastFoundItem = null;
        public TimeLineItemViewModel FindItemByTime(DateTime timePoint)
        {
            TimeLineItemViewModel result = null;
            if (_lastFoundItem != null)
            {
                if (_lastFoundItem.StartTime.Value <= timePoint && _lastFoundItem.EndTime.Value > timePoint)
                {
                    result = _lastFoundItem;
                }
                else
                {
                    int idx = Items.IndexOf(_lastFoundItem);
                    for (; idx < Items.Count; idx++)
                    {
                        if (timePoint >= Items[idx].StartTime.Value && timePoint < Items[idx].EndTime.Value)
                        {
                            result = _lastFoundItem = (TimeLineItemViewModel)Items[idx];
                            break;
                        }
                        else if (timePoint < Items[idx].StartTime.Value)
                        {
                            break;
                        }
                    }
                }
            }

            if (result == null)
            {
                // if still not found anything
                foreach (var i in Items)
                {
                    if (i.StartTime.Value <= timePoint && i.EndTime.Value > timePoint)
                    {
                        result = _lastFoundItem = (TimeLineItemViewModel)i;
                        break;
                    }
                    else if (timePoint < i.StartTime.Value)
                    {
                        break;
                    }
                }
            }
            return result;
        }

        #endregion
    }
}
