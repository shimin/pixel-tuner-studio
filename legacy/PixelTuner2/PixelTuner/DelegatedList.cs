using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using PixelTuner.Serialization;
using PixelTuner.ListViewStyle;

namespace PixelTuner
{
    public class DelegatedIList : IList
    {
        private readonly IList _listToDelegateTo;

        public DelegatedIList(IList listToDelegateTo)
        {
            _listToDelegateTo = listToDelegateTo;
        }

        public IEnumerator GetEnumerator()
        {
            return _listToDelegateTo.GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            _listToDelegateTo.CopyTo(array, index);
        }

        public int Count
        {
            get { return _listToDelegateTo.Count; }
        }
        public object SyncRoot
        {
            get { return _listToDelegateTo.SyncRoot; }
        }
        public bool IsSynchronized
        {
            get { return _listToDelegateTo.IsSynchronized; }
        }

        public int Add(object value)
        {
            return _listToDelegateTo.Add((ThumbnailFileItem)(value as TimeLineItem));
        }

        public bool Contains(object value)
        {
            return _listToDelegateTo.Contains((ThumbnailFileItem)(value as TimeLineItem));
        }

        public void Clear()
        {
            _listToDelegateTo.Clear();
        }

        public int IndexOf(object value)
        {
            return _listToDelegateTo.IndexOf((ThumbnailFileItem)(value as TimeLineItem));
        }

        public void Insert(int index, object value)
        {
            _listToDelegateTo.Insert(index, (ThumbnailFileItem)(value as TimeLineItem));
        }

        public void Remove(object value)
        {
            _listToDelegateTo.Remove((ThumbnailFileItem)(value as TimeLineItem));
        }

        public void RemoveAt(int index)
        {
            _listToDelegateTo.RemoveAt(index);
        }

        public object this[int index]
        {
            get { return (TimeLineItem)(_listToDelegateTo[index] as ThumbnailFileItem); }
            set { _listToDelegateTo[index] = (ThumbnailFileItem)(value as TimeLineItem); }
        }

        public bool IsReadOnly
        {
            get { return _listToDelegateTo.IsReadOnly; }
        }
        public bool IsFixedSize
        {
            get { return _listToDelegateTo.IsFixedSize; }
        }
    } 
}
