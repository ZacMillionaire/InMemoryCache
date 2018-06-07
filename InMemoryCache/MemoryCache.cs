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
        private readonly ConcurrentDictionary<TKey, TValue> _cache;
        private readonly List<TKey> _lifetimeCache;

        private readonly object _cacheLock = new object();

        public int _inserts = 0;
        public int _updates = 0;
        public int _evictions = 0;
        public int _misses = 0;
        public int _refresh = 0;

        public MemoryCache(int maxCacheElements)
        {
            if (maxCacheElements == 0)
            {
                throw new ArgumentOutOfRangeException("maxCacheElements must be greater than 0");
            }

            _cacheSizeLimit = maxCacheElements;
            _cache = new ConcurrentDictionary<TKey, TValue>();
            _lifetimeCache = new List<TKey>();
        }

        public void AddOrUpdate(TKey key, TValue value)
        {
            lock (_cacheLock)
            {
                bool keyExists = _cache.ContainsKey(key);

                TValue evicted = default(TValue);

                if (keyExists == false && _cache.Count >= _cacheSizeLimit)
                {
                    var oldestByKeyList = _lifetimeCache[0];
                    _cache.TryRemove(oldestByKeyList, out evicted);
                    Console.WriteLine($"    [EVICT] Evicted oldest key : {oldestByKeyList}");
                    _lifetimeCache.Remove(oldestByKeyList);
                    _evictions++;
                }

                TValue cacheValue = value;

                if (keyExists == false)
                {
                    _inserts++;
                }

                _cache.AddOrUpdate(key, cacheValue, (k, existing) =>
                 {
                     // If we've seen the key before, its an update, so remove the old entry
                     // in our tracking list before we reinsert the key at the end
                     _lifetimeCache.Remove(key);
                     _updates++;
                     //existing.Update(value);
                     //Console.WriteLine($"    [UPDATE]");
                     return cacheValue;
                 });

                // Add the key to the new list, with the oldest keys eventually bubbling
                // to the top of the list.
                _lifetimeCache.Add(key);

                // debug output
                //Console.WriteLine($"    [CACHE] Cache size : {_cache.Count}/{_cacheSizeLimit}");
                if (evicted == null)
                {
                    Console.WriteLine($"    [OLDEST] Current oldest key : {_lifetimeCache[0]}");
                }
                else
                {
                    Console.WriteLine($"        [NEXT] Next oldest key : {_lifetimeCache[0]}");
                }
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (_cacheLock)
            {
                bool got = _cache.TryGetValue(key, out value);
                if (got == false)
                {
                    _misses++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[MISS] Cache miss on key {key}, key was previously evicted");
                    Console.ResetColor();
                }
                else
                {
                    _refresh++;
                    _lifetimeCache.Remove(key);
                    _lifetimeCache.Add(key);
                    Console.WriteLine($"    [REFRESHED] Refreshed key : {key}. Oldest is {_lifetimeCache[0]}");
                }
                return got;
            }
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