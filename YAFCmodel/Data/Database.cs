using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace YAFC.Model
{
    public static class Database
    {
        public static FactorioObject[] rootAccessible { get; internal set; }
        public static Item[] allSciencePacks { get; internal set; }
        public static Dictionary<string, FactorioObject> objectsByTypeName { get; internal set; }
        public static Dictionary<string, List<Fluid>> fluidVariants { get; internal set; }
        public static Goods voidEnergy { get; internal set; }
        public static Goods electricity { get; internal set; }
        public static Recipe electricityGeneration { get; internal set; }
        public static Goods heat { get; internal set; }
        public static Entity character { get; internal set; }
        public static Item[] allModules { get; internal set; }
        public static EntityBeacon[] allBeacons { get; internal set; }
        public static EntityBelt[] allBelts { get; internal set; }
        public static EntityInserter[] allInserters { get; internal set; }
        public static EntityAccumulator[] allAccumulators { get; internal set; }
        public static EntityContainer[] allContainers { get; internal set; }
        public static FactorioIdRange<FactorioObject> objects { get; internal set; }
        public static FactorioIdRange<Goods> goods { get; internal set; }
        public static FactorioIdRange<Special> specials { get; internal set; }
        public static FactorioIdRange<Item> items { get; internal set; }
        public static FactorioIdRange<Fluid> fluids { get; internal set; }
        public static FactorioIdRange<Recipe> recipes { get; internal set; }
        public static FactorioIdRange<Mechanics> mechanics { get; internal set; }
        public static FactorioIdRange<RecipeOrTechnology> recipesAndTechnologies { get; internal set; }
        public static FactorioIdRange<Technology> technologies { get; internal set; }
        public static FactorioIdRange<Entity> entities { get; internal set; }
        public static int constantCombinatorCapacity { get; internal set; } = 18;

        public static FactorioObject FindClosestVariant(string id)
        {
            string baseId;
            int temperature;
            var splitter = id.IndexOf("@", StringComparison.Ordinal);
            if (splitter >= 0)
            {
                baseId = id.Substring(0, splitter);
                int.TryParse(id.Substring(splitter+1), out temperature);
            }
            else
            {
                baseId = id;
                temperature = 0;
            }

            if (objectsByTypeName.TryGetValue(baseId, out var result))
                return result;
            if (fluidVariants.TryGetValue(baseId, out var variants))
            {
                var prev = variants[0];
                for (var i = 1; i < variants.Count; i++)
                {
                    var cur = variants[i];
                    if (cur.temperature >= temperature)
                        return cur.temperature - temperature > temperature - prev.temperature ? prev : cur;
                    prev = cur;
                }
                return prev;
            }

            return null;
        }
    }

    // The primary purpose of this wrapper is that because fast dependency algorithms operate on ints and int arrays instead of objects, so it makes sense to share data structures 
    public struct PackedList<T> : IReadOnlyList<T> where T : FactorioObject
    {
        private readonly FactorioId[] ids;
        public bool empty => ids == null || ids.Length == 0;
        public PackedList(IEnumerable<T> source)
        {
            ids = source == null ? Array.Empty<FactorioId>() : source.Select(x => x.id).ToArray();
        }

        public FactorioId[] raw => ids;
        public bool Contains(T obj) => obj != null && Array.IndexOf(ids, obj.id) >= 0;
        public T SingleOrNull() => ids.Length == 1 ? this[0] : null;
        public T this[int index] => Database.objects[ids[index]] as T;
        public int Count => ids.Length;
        
        public struct Enumerator : IEnumerator<T>
        {
            private readonly FactorioId[] arr;
            private int index;
            public Enumerator(FactorioId[] arr)
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
        public T this[FactorioId id] => all[(int)id-start];
        
        public Mapping<T, TValue> CreateMapping<TValue>() => new Mapping<T, TValue>(this);
        public Mapping<T, TOther, TValue> CreateMapping<TOther, TValue>(FactorioIdRange<TOther> other) where TOther : FactorioObject
            => new Mapping<T, TOther, TValue>(this, other);

        public Mapping<T, TValue> CreateMapping<TValue>(Func<T, TValue> mapFunc)
        {
            var map = CreateMapping<TValue>();
            foreach (var o in all)
                map[o] = mapFunc(o);
            return map;
        }
    }
    
    // Mapping[TKey, TValue] is almost like a dictionary where TKey is FactorioObject but it is an array wrapper and therefore very fast. This is preferable way to add custom properties to FactorioObjects
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

        public void Add(TKey key, TValue value) => this[key] = value;
        public bool ContainsKey(TKey key) => true;
        public bool Remove(TKey key) => throw new NotSupportedException();
        public bool TryGetValue(TKey key, out TValue value)
        {
            value = this[key];
            return true;
        }

        TValue IDictionary<TKey, TValue>.this[TKey key]
        {
            get => this[key];
            set => this[key] = value;
        }

        public Mapping<TKey, TOther> Remap<TOther>(Func<TKey, TValue, TOther> remap)
        {
            var remapped = source.CreateMapping<TOther>();
            foreach (var key in source.all)
                remapped[key] = remap(key, this[key]);
            return remapped;
        }

        public ref TValue this[TKey index] => ref data[(int)index.id - offset];
        public ref TValue this[FactorioId id] => ref data[(int)(id - offset)];
        //public ref TValue this[int id] => ref data[id];
        public void Clear() => Array.Clear(data, 0, data.Length);
        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
        public int Count => data.Length;
        public bool IsReadOnly => false;
        public TKey[] Keys => source.all;
        public TValue[] Values => data;
        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(KeyValuePair<TKey, TValue> item) => this[item.Key] = item.Value;
        public bool Contains(KeyValuePair<TKey, TValue> item) => EqualityComparer<TValue>.Default.Equals(this[item.Key], item.Value); 
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            for (var i = 0; i < data.Length; i++)
                array[i+arrayIndex] = new KeyValuePair<TKey, TValue>(source[i], data[i]);
        }
        
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private int index;
            private readonly TKey[] keys;
            private readonly TValue[] values;
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

    public readonly struct Mapping<TKey1, TKey2, TValue> where TKey1 : FactorioObject where TKey2 : FactorioObject
    {
        private readonly int offset1, offset2, count1;
        private readonly TValue[] data;
        internal Mapping(FactorioIdRange<TKey1> key1, FactorioIdRange<TKey2> key2)
        {
            offset1 = key1.start;
            offset2 = key2.start;
            count1 = key1.count;
            data = new TValue[count1 * key2.count];
        }
        public ref TValue this[TKey1 x, TKey2 y] => ref data[((int) x.id - offset1) * count1 + ((int) y.id - offset2)];

        public void CopyRow(TKey1 from, TKey1 to)
        {
            if (from == to)
                return; 
            var fromId = ((int)from.id - offset1) * count1;
            var toId = ((int)to.id - offset1) * count1;
            Array.Copy(data, fromId, data, toId, count1);
        }

        public ArraySegment<TValue> GetSlice(TKey1 row) => new ArraySegment<TValue>(data, ((int)row.id - offset1) * count1, count1);

        public FactorioId IndexToId(int index) => (FactorioId) (index + offset2);
    }
}