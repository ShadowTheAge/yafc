using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Yafc.Model {
    public static class Database {
        // null-forgiveness for all static properties here:
        public static FactorioObject[] rootAccessible { get; internal set; } = null!;
        public static Item[] allSciencePacks { get; internal set; } = null!;
        public static Dictionary<string, FactorioObject> objectsByTypeName { get; internal set; } = null!;
        public static Dictionary<string, List<Fluid>> fluidVariants { get; internal set; } = null!;
        public static Goods voidEnergy { get; internal set; } = null!;
        public static Goods electricity { get; internal set; } = null!;
        public static Recipe electricityGeneration { get; internal set; } = null!;
        public static Goods heat { get; internal set; } = null!;
        public static Entity? character { get; internal set; }
        public static EntityCrafter[] allCrafters { get; internal set; } = null!;
        public static Module[] allModules { get; internal set; } = null!;
        public static EntityBeacon[] allBeacons { get; internal set; } = null!;
        public static EntityBelt[] allBelts { get; internal set; } = null!;
        public static EntityInserter[] allInserters { get; internal set; } = null!;
        public static EntityAccumulator[] allAccumulators { get; internal set; } = null!;
        public static EntityContainer[] allContainers { get; internal set; } = null!;
        public static FactorioIdRange<FactorioObject> objects { get; internal set; } = null!;
        public static FactorioIdRange<Goods> goods { get; internal set; } = null!;
        public static FactorioIdRange<Special> specials { get; internal set; } = null!;
        public static FactorioIdRange<Item> items { get; internal set; } = null!;
        public static FactorioIdRange<Fluid> fluids { get; internal set; } = null!;
        public static FactorioIdRange<Recipe> recipes { get; internal set; } = null!;
        public static FactorioIdRange<Mechanics> mechanics { get; internal set; } = null!;
        public static FactorioIdRange<RecipeOrTechnology> recipesAndTechnologies { get; internal set; } = null!;
        public static FactorioIdRange<Technology> technologies { get; internal set; } = null!;
        public static FactorioIdRange<Entity> entities { get; internal set; } = null!;
        public static int constantCombinatorCapacity { get; internal set; } = 18;

        /// <summary>
        /// Returns the set of beacons filtered to only those that can accept at least one module.
        /// </summary>
        public static IEnumerable<EntityBeacon> usableBeacons => allBeacons.Where(b => allModules.Any(m => b.CanAcceptModule(m.moduleSpecification)));

        /// <summary>
        /// Fetches a module that can be used in this beacon, or <see langword="null"/> if no beacon was specified or no module could be found.
        /// </summary>
        /// <param name="beacon">The beacon to receive a module. If <see langword="null"/>, <paramref name="module"/> will be set to null and this method will return <see langword="false"/>.</param>
        /// <param name="module">A module that can be placed in that beacon, if such a module exists.</param>
        /// <returns><see langword="true"/> if a module could be found, or <see langword="false"/> if the supplied beacon does not accept any modules or was <see langword="null"/>.</returns>
        public static bool GetDefaultModuleFor(EntityBeacon? beacon, [NotNullWhen(true)] out Module? module) {
            module = allModules.FirstOrDefault(m => EntityWithModules.CanAcceptModule(m.moduleSpecification, beacon?.allowedEffects ?? AllowedEffects.None));
            return module != null;
        }

        public static FactorioObject? FindClosestVariant(string id) {
            string baseId;
            int temperature;
            int splitter = id.IndexOf('@');
            if (splitter >= 0) {
                baseId = id[..splitter];
                _ = int.TryParse(id[(splitter + 1)..], out temperature);
            }
            else {
                baseId = id;
                temperature = 0;
            }

            if (objectsByTypeName.TryGetValue(baseId, out var result)) {
                return result;
            }

            if (fluidVariants.TryGetValue(baseId, out var variants)) {
                var prev = variants[0];
                for (int i = 1; i < variants.Count; i++) {
                    var cur = variants[i];
                    if (cur.temperature >= temperature) {
                        return cur.temperature - temperature > temperature - prev.temperature ? prev : cur;
                    }

                    prev = cur;
                }
                return prev;
            }

            return null;
        }
    }

    // Since Factorio objects are sorted by type, objects of one type always occupy continuous range.
    // Because of that we can replace Dictionary<SomeFactorioObjectSubtype, T> with just plain array indexed with object id with range offset
    // This is mostly useful for analysis algorithms that needs a bunch of these constructs
    public class FactorioIdRange<T> where T : FactorioObject {
        internal readonly int start;
        public int count { get; }
        public T[] all { get; }

        public FactorioIdRange(int start, int end, List<FactorioObject> source) {
            this.start = start;
            count = end - start;
            all = new T[count];
            for (int i = 0; i < count; i++) {
                all[i] = (T)source[i + start];
            }
        }

        public T this[int i] => all[i];
        public T this[FactorioId id] => all[(int)id - start];

        public Mapping<T, TValue> CreateMapping<TValue>() {
            return new Mapping<T, TValue>(this);
        }

        public Mapping<T, TOther, TValue> CreateMapping<TOther, TValue>(FactorioIdRange<TOther> other) where TOther : FactorioObject {
            return new Mapping<T, TOther, TValue>(this, other);
        }

        public Mapping<T, TValue> CreateMapping<TValue>(Func<T, TValue> mapFunc) {
            var map = CreateMapping<TValue>();
            foreach (var o in all) {
                map[o] = mapFunc(o);
            }

            return map;
        }
    }

    // Mapping[TKey, TValue] is almost like a dictionary where TKey is FactorioObject but it is an array wrapper and therefore very fast. This is preferable way to add custom properties to FactorioObjects
    public readonly struct Mapping<TKey, TValue>(FactorioIdRange<TKey> source) : IDictionary<TKey, TValue> where TKey : FactorioObject {
        private readonly int offset = source.start;
        private readonly FactorioIdRange<TKey> source = source;

        public void Add(TKey key, TValue value) {
            this[key] = value;
        }

        public bool ContainsKey(TKey key) {
            return true;
        }

        public bool Remove(TKey key) {
            throw new NotSupportedException();
        }

        public bool TryGetValue(TKey key, out TValue value) {
            value = this[key];
            return true;
        }

        TValue IDictionary<TKey, TValue>.this[TKey key] {
            get => this[key];
            set => this[key] = value;
        }

        public Mapping<TKey, TOther> Remap<TOther>(Func<TKey, TValue, TOther> remap) {
            var remapped = source.CreateMapping<TOther>();
            foreach (var key in source.all) {
                remapped[key] = remap(key, this[key]);
            }

            return remapped;
        }

        public ref TValue this[TKey index] => ref Values[(int)index.id - offset];
        public ref TValue this[FactorioId id] => ref Values[(int)(id - offset)];
        //public ref TValue this[int id] => ref data[id];
        public void Clear() {
            Array.Clear(Values, 0, Values.Length);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) {
            return Remove(item.Key);
        }

        public int Count => Values.Length;
        public bool IsReadOnly => false;
        public TKey[] Keys => source.all;
        public TValue[] Values { get; } = new TValue[source.count];
        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
            return GetEnumerator();
        }

        public Enumerator GetEnumerator() {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item) {
            this[item.Key] = item.Value;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) {
            return EqualityComparer<TValue>.Default.Equals(this[item.Key], item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
            for (int i = 0; i < Values.Length; i++) {
                array[i + arrayIndex] = new KeyValuePair<TKey, TValue>(source[i], Values[i]);
            }
        }

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>> {
            private int index;
            private readonly TKey[] keys;
            private readonly TValue[] values;
            internal Enumerator(Mapping<TKey, TValue> mapping) {
                index = -1;
                keys = mapping.source.all;
                values = mapping.Values;
            }
            public bool MoveNext() {
                return ++index < keys.Length;
            }

            public void Reset() {
                index = -1;
            }

            public readonly KeyValuePair<TKey, TValue> Current => new KeyValuePair<TKey, TValue>(keys[index], values[index]);
            readonly object IEnumerator.Current => Current;
            public readonly void Dispose() { }
        }
    }

    public readonly struct Mapping<TKey1, TKey2, TValue> where TKey1 : FactorioObject where TKey2 : FactorioObject {
        private readonly int offset1, offset2, count1;
        private readonly TValue[] data;
        internal Mapping(FactorioIdRange<TKey1> key1, FactorioIdRange<TKey2> key2) {
            offset1 = key1.start;
            offset2 = key2.start;
            count1 = key1.count;
            data = new TValue[count1 * key2.count];
        }
        public ref TValue this[TKey1 x, TKey2 y] => ref data[(((int)x.id - offset1) * count1) + ((int)y.id - offset2)];

        public void CopyRow(TKey1 from, TKey1 to) {
            if (from == to) {
                return;
            }

            int fromId = ((int)from.id - offset1) * count1;
            int toId = ((int)to.id - offset1) * count1;
            Array.Copy(data, fromId, data, toId, count1);
        }

        public ArraySegment<TValue> GetSlice(TKey1 row) {
            return new ArraySegment<TValue>(data, ((int)row.id - offset1) * count1, count1);
        }

        public FactorioId IndexToId(int index) {
            return (FactorioId)(index + offset2);
        }
    }
}
