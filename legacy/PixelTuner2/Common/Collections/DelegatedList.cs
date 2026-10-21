using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace Common.Collections
{
    public class DelegatedIList <SourceT, TargetT> : IList
        where TargetT : class, IConvertible, new()
        where SourceT : class, IConvertible, new()
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
            return _listToDelegateTo.Add(Convert.ChangeType(value as TargetT, typeof(SourceT)));
        }

        public bool Contains(object value)
        {
            return _listToDelegateTo.Contains(Convert.ChangeType(value as TargetT, typeof(SourceT)));
        }

        public void Clear()
        {
            _listToDelegateTo.Clear();
        }

        public int IndexOf(object value)
        {
            return _listToDelegateTo.IndexOf(Convert.ChangeType(value as TargetT, typeof(SourceT)));
        }

        public void Insert(int index, object value)
        {
            _listToDelegateTo.Insert(index, Convert.ChangeType(value as TargetT, typeof(SourceT)));
        }

        public void Remove(object value)
        {
            _listToDelegateTo.Remove(Convert.ChangeType(value as TargetT, typeof(SourceT)));
        }

        public void RemoveAt(int index)
        {
            _listToDelegateTo.RemoveAt(index);
        }

        public object this[int index]
        {
            get 
            {
                return Convert.ChangeType(_listToDelegateTo[index] as SourceT, typeof(TargetT));
            }
            set 
            { 
                _listToDelegateTo[index] = Convert.ChangeType(value as TargetT, typeof(SourceT)); 
            }
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
