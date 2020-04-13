using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FactorioData
{
    public static class Database
    {
        public static FactorioObject[] allObjects;
        public static FactorioObject[] rootAccessible;
        public static FactorioObject[] defaultMilestones;
        public static Goods[] allGoods; 
        public static Dictionary<(string, string), FactorioObject> objectsByTypeName;
        public static Goods voidEnergy;
    }

    // The primary purpose of this wrapper is that because fast dependency algorithms operate on ints and int arrays instead of objects, so it makes sense to share data structures 
    public struct PackedList<T> : IReadOnlyList<T> where T : FactorioObject
    {
        private readonly int[] ids;
        public bool empty => ids == null || ids.Length == 0;
        public PackedList(IEnumerable<T> source)
        {
            ids = source.Select(x => x.id).ToArray();
        }

        public int[] raw => ids;
        public bool Contains(T obj) => obj != null && Array.IndexOf(ids, obj.id) >= 0;
        public T SingleOrNull() => ids.Length == 1 ? this[0] : null;
        public T this[int index] => Database.allObjects[ids[index]] as T;
        public int Count => ids.Length;
        
        public struct Enumerator : IEnumerator<T>
        {
            private readonly int[] arr;
            private int index;
            public Enumerator(int[] arr)
            {
                this.arr = arr;
                index = -1;
            }

            public bool MoveNext()
            {
                return arr != null && ++index < arr.Length;
            }

            public void Reset() => index = -1;
            public T Current => Database.allObjects[arr[index]] as T;
            object IEnumerator.Current => Current;

            public void Dispose() {}
        }

        public Enumerator GetEnumerator() => new Enumerator(ids);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(ids); 
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}