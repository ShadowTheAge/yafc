using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace YAFC.Model
{
    public static class Database
    {
        public static FactorioObject[] rootAccessible { get; internal set; }
        public static FactorioObject[] defaultMilestones { get; internal set; }
        public static Dictionary<string, FactorioObject> objectsByTypeName { get; internal set; }
        public static Goods voidEnergy { get; internal set; }
        public static Goods electricity { get; internal set; }
        public static Entity character { get; internal set; }
        public static Item[] allModules { get; internal set; }

        public static FactorioIdRange<FactorioObject> objects { get; internal set; }
        public static FactorioIdRange<Goods> goods { get; internal set; }
        public static FactorioIdRange<Special> specials { get; internal set; }
        public static FactorioIdRange<Item> items { get; internal set; }
        public static FactorioIdRange<Fluid> fluids { get; internal set; }
        public static FactorioIdRange<Recipe> recipes { get; internal set; }
        public static FactorioIdRange<Recipe> recipesAndTechnologies { get; internal set; }
        public static FactorioIdRange<Technology> technologies { get; internal set; }
        public static FactorioIdRange<Entity> entities { get; internal set; }
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
        public T this[int index] => Database.objects[ids[index]] as T;
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
            public T Current => Database.objects[arr[index]] as T;
            object IEnumerator.Current => Current;

            public void Dispose() {}
        }

        public Enumerator GetEnumerator() => new Enumerator(ids);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(ids); 
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    
    // Since Factorio objects are sorted by type, objects of one type always occupy continuous range.
    // Because of that we can replace Dictionary<SomeFactorioObjectSubtype, T> with just plain array indexed with object id with range offset
    // This is mostly useful for analysis algorithms that needs a bunch of these constructs
    public class FactorioIdRange<T> where T : FactorioObject
    {
        internal readonly int start;
        public int count { get; }
        public T[] all { get; }

        internal FactorioIdRange(int start, int end, List<FactorioObject> source)
        {
            this.start = start;
            count = end-start;
            all = new T[count];
            for (var i = 0; i < count; i++)
                all[i] = source[i + start] as T;
        }

        public T this[int i] => all[i];
        
        public Mapping<T, TValue> CreateMapping<TValue>() => new Mapping<T, TValue>(this);
    }
    
    public readonly struct Mapping<TKey, TValue> : IDictionary<TKey, TValue> where TKey:FactorioObject
    {
        private readonly int offset;
        private readonly TValue[] data;
        private readonly FactorioIdRange<TKey> source;
        internal Mapping(FactorioIdRange<TKey> source)
        {
            this.source = source;
            data = new TValue[source.count];
            offset = source.start;
        }

        public void Add(TKey key, TValue value) => data[key.id - offset] = value;
        public bool ContainsKey(TKey key) => true;
        public bool Remove(TKey key) => throw new NotSupportedException();

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = this[key];
            return true;
        }

        public TValue this[TKey index]
        {
            get => data[index.id - offset];
            set => data[index.id - offset] = value;
        }
        
        public TValue this[int id]
        {
            get => data[id - offset];
            set => data[id - offset] = value;
        }
        
        public void Clear() => Array.Clear(data, 0, data.Length);
        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
        public int Count => data.Length;
        public bool IsReadOnly => false;
        public ICollection<TKey> Keys => source.all;
        public ICollection<TValue> Values => data;
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator(); 
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<TKey, TValue> item) => EqualityComparer<TValue>.Default.Equals(this[item.Key], item.Value); 
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            for (var i = 0; i < data.Length; i++)
                array[i+arrayIndex] = new KeyValuePair<TKey, TValue>(source[i], data[i]);
        }
        
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private int index;
            private TKey[] keys;
            private TValue[] values;
            internal Enumerator(Mapping<TKey, TValue> mapping)
            {
                index = -1;
                keys = mapping.source.all;
                values = mapping.data;
            }
            public bool MoveNext() => ++index < keys.Length;
            public void Reset() => index = -1;

            public KeyValuePair<TKey, TValue> Current => new KeyValuePair<TKey, TValue>(keys[index], values[index]);
            object IEnumerator.Current => Current;

            public void Dispose() {}
        }
    }
}