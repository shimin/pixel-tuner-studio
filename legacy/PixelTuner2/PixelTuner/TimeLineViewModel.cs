using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeLineTool;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;

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

        FileViewModel _parent;
        internal FileViewModel Parent
        {
            get { return _parent; }
            set { _parent = value; }
        }

        Brush _backGround;
        public Brush Background
        {
            get { return _backGround; }
            set { _backGround = value; RaisePropertyChanged("Background"); }
        }

        // for comparasions in TimeLineControl wich all use 'DateTime.MinValue'
        DateTime? _start = DateTime.MinValue.AddYears(DateTime.Now.Year);
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
                Parent.TimeLines.Move(old, old-1);
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

        #endregion
    }
}
