using System;
using System.Collections;
using System.Collections.Generic;

namespace YAFC.Model
{
    public class MigrationDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Action<TKey, TValue> add;
        public MigrationDictionary(Action<TKey, TValue> add) => this.add = add;
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => throw new NotSupportedException(); 
        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
        public void Add(KeyValuePair<TKey, TValue> item) => add(item.Key, item.Value);
        public void Clear() {}
        public bool Contains(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotSupportedException();

        public bool Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException(); 
        public int Count => throw new NotSupportedException(); 
        public bool IsReadOnly => throw new NotSupportedException();
        public void Add(TKey key, TValue value) => add(key, value);
        public bool ContainsKey(TKey key) => throw new NotSupportedException(); 
        public bool Remove(TKey key) => throw new NotSupportedException();
        public bool TryGetValue(TKey key, out TValue value) => throw new NotSupportedException();

        public TValue this[TKey key]
        {
            get => throw new NotSupportedException(); 
            set => throw new NotSupportedException(); 
        }

        public ICollection<TKey> Keys => throw new NotSupportedException(); 
        public ICollection<TValue> Values => throw new NotSupportedException();
    }
}