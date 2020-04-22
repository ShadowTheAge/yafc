using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YAFC.UI
{    
    public abstract class ImGuiCache<T, TKey> : IDisposable where T:ImGuiCache<T, TKey> where TKey : IEquatable<TKey>
    {
        private static readonly T Constructor = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        public class Cache
        {
            private readonly Dictionary<TKey, T> activeCached = new Dictionary<TKey, T>();
            private readonly HashSet<TKey> unused = new HashSet<TKey>();
            
            public T GetCached(TKey key)
            {
                if (activeCached.TryGetValue(key, out var value))
                {
                    unused.Remove(key);
                    return value;
                }

                return activeCached[key] = Constructor.CreateForKey(key);
            }

            public void PurgeUnused()
            {
                foreach (var key in unused)
                {
                    if (activeCached.Remove(key, out var value))
                        value.Dispose();
                }
                unused.Clear();
                unused.UnionWith(activeCached.Keys);
            }
        }


        protected abstract T CreateForKey(TKey key);
        public abstract void Dispose();
    }
}