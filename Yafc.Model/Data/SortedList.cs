using System;
using System.Collections;
using System.Collections.Generic;

namespace Yafc.Model {
    // Simple set with array as backing storage with O(ln(n)) search, O(n) insertion and iteration
    public class SortedList<T>(IComparer<T> comparer) : ICollection<T>, IReadOnlyList<T>, IList<T> {
        private readonly IComparer<T> comparer = comparer;
        private int version;
        private T[] data = [];
        IEnumerator<T> IEnumerable<T>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public Enumerator GetEnumerator() {
            return new Enumerator(this);
        }

        public struct Enumerator(SortedList<T> list) : IEnumerator<T> {
            private readonly SortedList<T> list = list;
            private int index = -1;
            private int version = list.version;

            public bool MoveNext() {
                if (list.version != version) {
                    Throw();
                }

                if (++index >= list.Count) {
                    Current = default;
                    return false;
                }

                Current = list.data[index];
                return true;
            }

            private void Throw() {
                throw new InvalidOperationException("Collection was modified, enumeration cannot continue");
            }

            public void Reset() {
                index = -1;
                version = list.version;
            }

            public T Current { get; private set; } = default;
            object IEnumerator.Current => Current;
            public void Dispose() { }
        }


        public void Add(T item) {
            if (item == null) {
                throw new NullReferenceException();
            }

            int index = Array.BinarySearch(data, 0, Count, item, comparer);
            if (index >= 0) {
                return;
            }

            index = ~index;
            if (Count == data.Length) {
                Array.Resize(ref data, Math.Max(data.Length * 2, 4));
            }

            if (index < Count) {
                Array.Copy(data, index, data, index + 1, Count - index);
            }

            data[index] = item;
            ++version;
            ++Count;
        }

        public void Clear() {
            Array.Clear(data, 0, Count);
            ++version;
            Count = 0;
        }

        public bool Contains(T item) {
            if (item == null) {
                throw new NullReferenceException();
            }

            return Array.BinarySearch(data, 0, Count, item, comparer) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex) {
            Array.Copy(data, 0, array, arrayIndex, Count);
        }

        public bool Remove(T item) {
            if (item == null) {
                throw new NullReferenceException();
            }

            int index = Array.BinarySearch(data, 0, Count, item, comparer);
            if (index < 0) {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public int Count { get; private set; }
        public bool IsReadOnly => false;

        public int IndexOf(T item) {
            if (item == null) {
                throw new NullReferenceException();
            }

            int index = Array.BinarySearch(data, 0, Count, item, comparer);
            return index < 0 ? -1 : index;
        }

        public void Insert(int index, T item) {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index) {
            if (index < Count - 1) {
                Array.Copy(data, index + 1, data, index, Count - index - 1);
            }

            ++version;
            --Count;
        }

        public T this[int index] {
            get => ((uint)index) < Count ? data[index] : throw new ArgumentOutOfRangeException(nameof(index));
            set => throw new NotSupportedException();
        }
    }
}
