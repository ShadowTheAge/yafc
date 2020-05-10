using System;
using System.Collections;
using System.Collections.Generic;

namespace YAFC.Model
{
    // Simple set with array as backing storage with O(ln(n)) search, O(n) insertion and iteration
    public class SortedList<T> : ICollection<T>, IReadOnlyList<T>, IList<T>
    {
        private readonly IComparer<T> comparer;
        public SortedList(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        private int count;
        private int version;
        private T[] data = Array.Empty<T>();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Enumerator GetEnumerator() => new Enumerator(this);
        public struct Enumerator : IEnumerator<T>
        {
            private readonly SortedList<T> list;
            private int index;
            private int version;
            private T current;
            public Enumerator(SortedList<T> list)
            {
                this.list = list;
                version = list.version;
                index = -1;
                current = default;
            }

            public bool MoveNext()
            {
                if (list.version != version)
                    Throw();
                if (++index >= list.count)
                {
                    current = default;
                    return false;
                }

                current = list.data[index];
                return true;
            }

            private void Throw() => throw new InvalidOperationException("Collection was modified, enumeration cannot continue");

            public void Reset()
            {
                index = -1;
                version = list.version;
            }

            public T Current => current;
            object IEnumerator.Current => Current;
            public void Dispose() {}
        }


        public void Add(T item)
        {
            if (item == null)
                throw new NullReferenceException();
            var index = Array.BinarySearch(data, 0, count, item, comparer);
            if (index >= 0)
                return;
            index = ~index;
            if (count == data.Length)
                Array.Resize(ref data, Math.Max(data.Length*2, 4));
            if (index < count)
                Array.Copy(data, index, data, index+1, count-index);
            data[index] = item;
            ++version;
            ++count;
        }

        public void Clear()
        {
            Array.Clear(data, 0, count);
            ++version;
            count = 0;
        }

        public bool Contains(T item)
        {
            if (item == null)
                throw new NullReferenceException();
            return Array.BinarySearch(data, 0, count, item, comparer) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(data, 0, array, arrayIndex, count);
        }

        public bool Remove(T item)
        {
            if (item == null)
                throw new NullReferenceException();
            var index = Array.BinarySearch(data, 0, count, item, comparer);
            if (index < 0)
                return false;
            RemoveAt(index);
            return true;
        }

        public int Count => count;
        public bool IsReadOnly => false;

        public int IndexOf(T item)
        {
            if (item == null)
                throw new NullReferenceException();
            var index = Array.BinarySearch(data, 0, count, item, comparer);
            return index < 0 ? -1 : index;
        }

        public void Insert(int index, T item) => throw new NotSupportedException();

        public void RemoveAt(int index)
        {
            if (index < count-1)
                Array.Copy(data, index+1, data, index, count-index-1);
            ++version;
            --count;
        }

        public T this[int index]
        {
            get => ((uint) index) < count ? data[index] : throw new ArgumentOutOfRangeException(nameof(index));
            set => throw new NotSupportedException();
        }
    }
}