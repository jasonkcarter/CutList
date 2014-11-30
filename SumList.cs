using System.Collections;
using System.Collections.Generic;

namespace CutList
{
    public class SumList : IList<decimal>
    {
        private readonly List<decimal> _list = new List<decimal>();

        public int Count
        {
            get { return _list.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public decimal this[int index]
        {
            get { return _list[index]; }
            set { _list[index] = value; }
        }

        public decimal Sum { get; private set; }

        public void Add(decimal item)
        {
            Sum += item;
            _list.Add(item);
        }

        public void Clear()
        {
            Sum = 0M;
            _list.Clear();
        }

        public bool Contains(decimal item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(decimal[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<decimal> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int IndexOf(decimal item)
        {
            return _list.IndexOf(item);
        }

        public void Insert(int index, decimal item)
        {
            Sum += item;
            _list.Insert(index, item);
        }

        public bool Remove(decimal item)
        {
            bool success = _list.Remove(item);
            if (success)
            {
                Sum -= item;
            }
            return success;
        }

        public void RemoveAt(int index)
        {
            Sum -= _list[index];
            _list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _list).GetEnumerator();
        }
    }
}