using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeLineTool;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace PixelTuner
{
    public class TimeLineViewModel
        : ITimeLineManager, INotifyPropertyChanged
    {
        public TimeLineViewModel() {
            _items.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(_items_CollectionChanged);
        }

        void _items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (ITimeLineDataItem item in e.NewItems)
                    item.Parent = this;
            }
        }

        #region ITimeLineManager Members

        public bool CanAddToTimeLine(ITimeLineDataItem item)
        {
            return false;
        }

        #endregion
        
        DateTime? _start = DateTime.MinValue.AddHours(1);
        public DateTime? Start
        {
            get { return _start; }
            set
            {
                _start = value;
                RaisePropertyChanged("Start");
            }
        }

        ObservableCollection<ITimeLineDataItem> _items = new ObservableCollection<ITimeLineDataItem>();
        public ObservableCollection<ITimeLineDataItem> Items
        {
            get
            {
                if (_items == null)
                    _items = new ObservableCollection<ITimeLineDataItem>();
                //if (_items.Count == 0) _items.Add(new PixelTuner.ListViewStyle.ThumbnailFileItem());
                return _items;
            }
            set
            {
                _items = value;
                RaisePropertyChanged("Items");
            }
        }

        #region INotifyPropertyChanged Members

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
