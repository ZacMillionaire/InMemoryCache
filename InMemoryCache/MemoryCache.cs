using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using InMemoryCache.Interfaces;

namespace InMemoryCache
{
    public class MemoryCache<TKey, TValue> : ICache<TKey, TValue>
    {
        private readonly int _cacheSizeLimit;
        private readonly ConcurrentDictionary<TKey, CacheItem<TValue>> _cache;
        private readonly List<TKey> _lifetimeCache;

        private readonly object _cacheLock = new object();

        public MemoryCache(int maxCacheElements)
        {
            if (maxCacheElements == 0)
            {
                throw new ArgumentOutOfRangeException("maxCacheElements must be greater than 0");
            }

            _cacheSizeLimit = maxCacheElements;
            _cache = new ConcurrentDictionary<TKey, CacheItem<TValue>>();
            _lifetimeCache = new List<TKey>();
        }

        public void AddOrUpdate(TKey key, TValue value)
        {
            lock (_cacheLock)
            {
                bool keyExists = _cache.ContainsKey(key);

                TKey evicted = default(TKey);
                if (keyExists == false && _cache.Count >= _cacheSizeLimit)
                {
                    //var oldest = _cache.OrderBy(x => x.Value._modified).First().Key;
                    var oldestByKeyList = _lifetimeCache.First();
                    evicted = oldestByKeyList;
                    _lifetimeCache.Remove(oldestByKeyList);
                    //var same = oldest.Equals(oldestByKeyList);

                    //if (same == false)
                    //{
                    //    throw new Exception("key mismatch");
                    //}
                }

                CacheItem<TValue> cacheValue = new CacheItem<TValue>(value);

                _cache.AddOrUpdate(key, cacheValue, (k, existing) =>
                 {
                     existing.Update(value);
                     return existing;
                 });

                // If this is the first time seein this key,
                // add the new key to the end of the eviction list
                // This ensures that old keys will bubble to the top.
                // I don't like the double add I'm doing here but eh, I'm just
                // getting it working for now
                if (keyExists == false)
                {
                    _lifetimeCache.Add(key);
                }
                else
                {
                    // Otherwise remove the old instance of the key and reinsert it
                    _lifetimeCache.Remove(key);
                    _lifetimeCache.Add(key);
                }

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"Cache size : {_cache.Count}/{_cacheSizeLimit}");
                if (evicted.Equals(default(TKey)) == true)
                {
                    Console.WriteLine($"Current oldest key : {_lifetimeCache.First()}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Evicted key:{ evicted}, Next oldest key : {_lifetimeCache.First()}");
                }
                Console.ResetColor();

            }

        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (_cacheLock)
            {
                bool got = _cache.TryGetValue(key, out CacheItem<TValue> v);
                value = v.Get();

                return got;
            }
            //throw new NotImplementedException();
        }
    }

    public class CacheItem<TValue>
    {
        private TValue _value;
        public DateTime _modified;
        private readonly object _valueLock = new object();

        public CacheItem(TValue value)
        {
            _value = value;
            _modified = DateTime.UtcNow;
        }

        public TValue Get()
        {
            lock (_valueLock)
            {
                return _value;
            }
        }

        public TValue Update(TValue newValue)
        {
            lock (_valueLock)
            {
                _value = newValue;
                _modified = DateTime.UtcNow;
            }

            return Get();
        }
    }
}