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
using System.Windows;

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

        public bool CanAddToTimeLine(ITimeLineMeta meta)
        {
            return false;
        }

        public bool CanAddToTimeLine(ITimeLineDataItem item, DateTime timePoint)
        {
            return item is TimeLineItemViewModel;
        }
    }

    public class TimeLineViewModel
         : ViewModelBase
    {
        #region Properties

        private string _lineTag = "Timeline";
        [XmlAttribute]
        public string LineTag
        {
            get { return _lineTag; }
            set { _lineTag = value; RaisePropertyChanged("LineTag"); }
        }

        FileViewModel _parent;
        [XmlIgnore]
        public FileViewModel Parent
        {
            get { return _parent; }
            set
            {
                _parent = value;
                if (_parent != null)
                {
                    _parent.PropertyChanged -= _parent_PropertyChanged;
                    _parent.PropertyChanged += _parent_PropertyChanged;
                }
            }
        }

        Brush _darkLineColor = new SolidColorBrush(Color.FromArgb(255, 64, 64, 64));

        Brush _lightLineColor = new SolidColorBrush(Color.FromArgb(255, 70, 70, 70));

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
        public bool IsMetaDataSet
        {
            get
            {
                return MetaData != null;
            }
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

        private bool _lineTagEditionVisible;
        [XmlIgnore]
        public bool LineTagEditionVisible
        {
            get { return _lineTagEditionVisible; }
            set { _lineTagEditionVisible = value; RaisePropertyChanged("LineTagEditionVisible"); }
        }

        System.Windows.Thickness _toolsetMargin = new Thickness(-27, 0, 0, 0);
        [XmlIgnore]
        public Thickness ToolsetMargin
        {
            get
            {
                return _toolsetMargin;
            }

            set
            {
                _toolsetMargin = value; RaisePropertyChanged("ToolsetMargin");
            }
        }

        int _timelineHeightPx = 0;
        [XmlAttribute]
        public virtual int TimelineHeightPx
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
                    var transform = ce.Transform;
                    if (transform == null) transform = new MatrixTransform();
                    PreviewTransform = new MatrixTransform(
                        transform.Value.M11,
                        transform.Value.M12,
                        transform.Value.M21,
                        transform.Value.M22,
                        -ce.Geometry.Bounds.Left,
                        -ce.Geometry.Bounds.Top);
                }
                RaisePropertyChanged("MetaData");
                RaisePropertyChanged("IsMetaDataSet");
                RaisePropertyChanged("LineHeight");
                RaisePropertyChanged("PreviewVisible");
            }
        }

        public bool PreviewVisible
        {
            get { return MetaData != null; }
            set { }
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
       
        [XmlIgnore]
        public ObservableCollection<ITimeLineDataItem> Items
        {
            get;
            set;
        }

        #endregion

        public TimeLineViewModel()
        {
            Items = new ObservableCollection<ITimeLineDataItem>();
            Items.CollectionChanged += (sender, args) =>
            {
                /*if (Parent != null && Parent.SoundPlayer != null && Parent.SoundPlayer.ChannelLength > 0)
                {
                    if (args.NewItems != null && args.NewItems.Count > 0)
                    {
                        DateTime firstElementStart = (args.NewItems[0] as TimeLineItemViewModel).StartTime.Value;
                        DateTime currentTime = new DateTime(2, 1, 1).Add(Parent.SoundPlayer.CurrentTime);
                        var delta = currentTime - firstElementStart;
                        foreach (TimeLineItemViewModel i in args.NewItems)
                        {
                            i.StartTime = i.StartTime + delta;
                            i.EndTime = i.EndTime + delta;
                        }
                    }
                }
                for (int i = Items.Count - 1; i > 0 && args.NewItems != null; i--)
                {
                    if (Items[i].StartTime > Items[i - 1].EndTime)
                    {
                        var delta = Items[i - 1].EndTime - Items[i].StartTime;
                        Items[i - 1].EndTime = Items[i].StartTime;
                        Items[i - 1].StartTime = Items[i - 1].StartTime - delta;
                    }
                }*/
                Parent?.RaiseFileChanged();
            };
            XmlItems = new DelegatedIList<TimeLineItemViewModel, TimeLineItem>(Items);
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
                Parent.PushUndo(false);
                int old = Parent.TimeLines.IndexOf(this);
                Parent.TimeLines.Move(old, old - 1);
                Parent.TimeLines.All(arg => { arg.RaisePropertyChanged("Background"); return false; });
            }
        }

        #endregion
        #region ChangeLineTag
        RelayCommand _changeLineTagCommand = null;
        public ICommand ChangeLineTag
        {
            get
            {
                if (_changeLineTagCommand == null)
                {
                    _changeLineTagCommand = new RelayCommand((p) => OnChangeLineTag(p), (p) => CanChangeLineTag(p));
                }

                return _changeLineTagCommand;
            }
        }

        protected virtual bool CanChangeLineTag(object parameter)
        {
            return true;
        }

        protected virtual void OnChangeLineTag(object parameter)
        {
            LineTagEditionVisible = true;
        }

        #endregion
        
        #region HandleTagKeyDown
        RelayCommand _handleTagKeyDownCommand;
        public ICommand HandleTagKeyDown
        {
            get
            {
                if (_handleTagKeyDownCommand == null)
                {
                    _handleTagKeyDownCommand = new RelayCommand((p) => OnHandleTagKeyDown(p), (p) => CanHandleTagKeyDown(p));
                }

                return _handleTagKeyDownCommand;
            }
        }

        protected virtual bool CanHandleTagKeyDown(object parameter)
        {
            return true;
        }

        protected virtual void OnHandleTagKeyDown(object parameter)
        {
            LineTagEditionVisible = false;
            RaisePropertyChanged("LineTag");
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
                Parent.PushUndo(false);
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
            {
                Parent.PushUndo(false);
                Parent.TimeLines.Remove(this);
            }
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
            Parent.PushUndo(false);
            int idx = Parent.TimeLines.IndexOf(this);
            Parent.TimeLines.Insert(idx + 1, (TimeLineViewModel)Clone());
        }

        public override object Clone()
        {
            var timelineCopy = (TimeLineViewModel)base.Clone();
            timelineCopy.Items = new ObservableCollection<ITimeLineDataItem>();
            timelineCopy.XmlItems = new DelegatedIList<TimeLineItemViewModel, TimeLineItem>(timelineCopy.Items);
            timelineCopy.Parent = Parent;
            timelineCopy.Background = Background;
            timelineCopy.IsChanged = true;
            timelineCopy.IsLineHeightVisible = IsLineHeightVisible;
            timelineCopy.IsSelectedToUpload = IsSelectedToUpload;
            timelineCopy.MetaDataPath = MetaDataPath;
            timelineCopy.LineTag = LineTag;
            timelineCopy.PreviewTransform = PreviewTransform;
            timelineCopy.TimelineHeightPx = TimelineHeightPx;
            timelineCopy.TimelinePreviewWidth = TimelinePreviewWidth;
            timelineCopy.DefaultTimeInterval = DefaultTimeInterval;
            timelineCopy.MetaData = (CanvasElementViewModel)MetaData?.Clone();
            timelineCopy.ToolsetMargin = ToolsetMargin;
            timelineCopy.Start = Start;
            foreach (var item in Items)
                timelineCopy.Items.Add((ITimeLineDataItem)item.Clone());
            return timelineCopy;
        }

        #endregion

        #region PreviewChangesCommand
        RelayCommand _previewChangesCommand = null;
        public ICommand PreviewChanges
        {
            get
            {
                if (_previewChangesCommand == null)
                {
                    _previewChangesCommand = new RelayCommand((p) => OnPreviewChanges(p), (p) => CanPreviewChanges(p));
                }

                return _previewChangesCommand;
            }
        }

        private bool CanPreviewChanges(object parameter)
        {
            return Parent != null;
        }

        private void OnPreviewChanges(object parameter)
        {
            Parent.PushUndo(false);
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
                    typeof(TimeLineItemEffectParameters),
                    typeof(TimeLineItemVideo),
                    typeof(TimeLineItemGradient)
                };
                return result;
            }
        }
        
        [XmlElement(ElementName = "Item", Type = typeof(TimeLineItem))]
        public DelegatedIList<TimeLineItemViewModel, TimeLineItem> XmlItems
        {
            get;
            set;
        }

        #endregion

        #region Methods

        private void _parent_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "HorizontalScrollPosition")
            {
                ToolsetMargin = new Thickness(Parent.HorizontalScrollPosition - 27, 0, 0, 0);
            }
        }

        public void SetBackgrounds(Color light, Color dark)
        {
            _lightLineColor = new SolidColorBrush(light);
            _darkLineColor = new SolidColorBrush(dark);
        }

        internal void InsertItems(ObservableCollection<TimeLineItemViewModel> itemsToPaste, int insertIndex)
        {
            if (itemsToPaste.Count > 0)
            {
                Parent.PushUndo(false);
                DateTime? startTime = null;
                bool hasCursor = false;
                if (Parent.SoundPlayer != null && Parent.SoundPlayer.WaveformData != null)
                {
                    startTime = Start.Value.Add(Parent.SoundPlayer.CurrentTime);
                    insertIndex = GetInsertionIndexByTime(startTime);
                    hasCursor = true;
                }
                else
                {
                    startTime = GetStartTimeByIndex(insertIndex);
                }
                TimeSpan delta = startTime.Value - itemsToPaste.First().StartTime.Value.AddYears(1);

                List<ITimeLineDataItem> copiedItems = new List<ITimeLineDataItem>();
                foreach (var i in itemsToPaste)
                {
                    var clone = (ITimeLineDataItem)i.Clone();
                    clone.StartTime = clone.StartTime + delta;
                    clone.EndTime = clone.EndTime + delta;
                    if (clone.StartTime.Value.Year < 2) clone.StartTime = clone.StartTime.Value.AddYears(1);
                    if (clone.EndTime.Value.Year < 2) clone.EndTime = clone.EndTime.Value.AddYears(1);
                    copiedItems.Add(clone);
                }

                var keyTimeEnd = copiedItems.Last().EndTime;
                var keyTimeStart = copiedItems.First().StartTime;

                if (PixelTuner.Properties.Settings.Default.CutNextItems || hasCursor)
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

        private int GetInsertionIndexByTime(DateTime? timePoint)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (timePoint.Value <= Items[i].StartTime.Value) return i;
                else if (timePoint.Value > Items[i].StartTime.Value && timePoint.Value <= Items[i].EndTime.Value) return i+1;
                else if (Items.Count > i + 1 && timePoint.Value > Items[i].EndTime.Value && timePoint.Value < Items[i + 1].StartTime.Value) return i + 1;
            }
            return Items.Count;
        }

        /// <summary>
        /// Removes item if it's fully within time interval and adjusts only start time otherwise.
        /// </summary>
        /// <param name="start">Start of time interval</param>
        /// <param name="end">End of time interval</param>
        internal int CutItemsWithinInterval(DateTime? start, DateTime? end)
        {
            int removed = 0;
            List<ITimeLineDataItem> itemsToRemove = new List<ITimeLineDataItem>();
            TimeLineItemViewModel first = FindItemByTime(start.Value);
            if (first != null)
            {
                if (first.StartTime != start) first.EndTime = start;
                else removed += 1;
            }

            foreach (var i in Items)
            {
                if (i.EndTime <= end && i.StartTime >= start) itemsToRemove.Add(i);
                else if (i.StartTime < end && i.StartTime >= start) i.StartTime = end;
            }

            itemsToRemove.ForEach(i => { (i as TimeLineItemViewModel).DisposeMe.Execute(null); });
            return removed;
        }

        /// <summary>
        /// Moves all timeline items after start time point
        /// </summary>
        /// <param name="start">Start time point</param>
        /// <param name="moveInterval">Borders move interval</param>
        internal void MoveItemsAfterIndex(DateTime? start, TimeSpan moveInterval)
        {
            List<ITimeLineDataItem> itemsToRemove = new List<ITimeLineDataItem>();
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
                    if (idx >= 0)
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
