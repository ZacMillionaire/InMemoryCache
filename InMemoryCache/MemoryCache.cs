using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using InMemoryCache.Interfaces;

namespace InMemoryCache {
    public class MemoryCache<TKey, TValue> : ICache<TKey, TValue> {
        private readonly int _cacheSizeLimit;
        private readonly ConcurrentDictionary<TKey, TValue> _cache;
        private readonly List<TKey> _lifetimeCache;

        private readonly object _cacheLock = new object();

#if DEBUG
        public int Inserts = 0;
        public int Updates = 0;
        public int Evictions = 0;
        public int Misses = 0;
        public int Refresh = 0;
#endif

        public int CacheSize => _cache.Count;
        public TKey[] KeysByAge
        {
            get
            {
                lock(_cacheLock) {
                    return _lifetimeCache.ToArray();
                }
            }
        }

        public MemoryCache(int maxCacheElements) {
            if(maxCacheElements == 0) {
                throw new ArgumentOutOfRangeException("maxCacheElements must be greater than 0");
            }

            _cacheSizeLimit = maxCacheElements;
            _cache = new ConcurrentDictionary<TKey, TValue>();
            _lifetimeCache = new List<TKey>();
        }

        /// <summary>
        /// Inserts a value to the cache with the given key.
        /// <para>
        /// If the key is new, it will be inserted. If the key has been seen before, the value is updated, and the keys lifetime is renewed.
        /// </para>
        /// <para>
        /// Each call to this will check if the capacity of the cache has been exceeded and will evict keys based on their insert or access time.
        /// Entries will only be evicted if the incoming key has not been seen before and room needs to be made.
        /// </para>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void AddOrUpdate(TKey key, TValue value) {

            // Start a lock. The cache itself is a ConcurrentDictionary (which could probably just be the cache itself if we didn't
            // need an eviction policy), which has its own locking mechanisms. However, we also need to manage key eviction, and
            // having threads competing against this could cause problems, so its safer to have a lock, just incase index lookups are delayed
            // or something more on the thread itself causes it to halt or abort, such as a higher priority thread from another process.
            lock(_cacheLock) {

                bool keyExists = _cache.ContainsKey(key);

                // Check if the key is new, and if we're at (or somehow exceeding) the max capacity of the cache
                if(keyExists == false && _cache.Count >= _cacheSizeLimit) {
                    // The cache lifetime is a simple list of keys ordered descending, so the oldest key is the first entry in the list.
                    // Remove the oldest cached item from the ConcurrentDictionary, then remove the key from the list, ready to have the
                    // new key added to both.
                    _cache.TryRemove(_lifetimeCache[0], out TValue evicted);
#if DEBUG
                    Console.WriteLine($"[EVICT] Evicted oldest key : {_lifetimeCache[0]}");
#endif
                    _lifetimeCache.Remove(_lifetimeCache[0]);
#if DEBUG
                    Console.WriteLine($"    [NEXT] Next oldest key : {_lifetimeCache[0]}");
                    // For stat tracking, increment the number of keys evicted
                    Evictions++;
#endif
                }

#if DEBUG
                // AddOrUpdate only triggers a delegate on update, so once again, for stat tracking,
                // bump the number of inserts
                if(keyExists == false) {
                    Inserts++;
                }
#endif

                _cache.AddOrUpdate(key, value, (k, existing) => {
                    // If we've seen the key before, its an update, so remove the old entry
                    // in our tracking list before we reinsert the key at the end
                    _lifetimeCache.Remove(key);
#if DEBUG
                    // More debug stat tracking
                    Updates++;
#endif
                    return value;
                });

                // Add the key to the new list, with the oldest keys eventually bubbling
                // to the top of the list.
                // We don't track by timestamp as several calls to this method could happen at once,
                // and using the default DateTime object would not give enough resolution,
                // and tracking unique keys by timestamp would cause other issues.
                // If we simply just have a List that we abuse as an array, we can have instant lookups
                // for evictions, as the first key will always be the oldest, and the last is always the newest.
                _lifetimeCache.Add(key);

#if DEBUG
                // debug output
                Console.WriteLine($"    [CACHE] Cache size : {_cache.Count}/{_cacheSizeLimit}");
                Console.WriteLine($"[OLDEST] Current oldest key : {_lifetimeCache[0]}");
#endif
            }
        }

        /// <summary>
        /// Gets the value at the given key. Returns true if the key was found.
        /// <para>
        /// On finding a value, the lifetime for the cached item is refreshed, moving it to the front
        /// as if it were an insert.
        /// </para>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(TKey key, out TValue value) {
            // Lock again, as a precaution. The ConcurrentDictionary would handle
            // a simultaneous read/update from AddOrUpdate, however, to safeguard returning a value
            // that may be immediately evicted, lock to prevent the _lifetimeCache list from being dirtied
            // by multiple competing threads.
            // Probably overkill and might even hamper performance. Curious to test this against some of my
            // agents to find out...
            lock(_cacheLock) {
                bool got = _cache.TryGetValue(key, out value);
                if(got == false) {
#if DEBUG
                    Misses++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[MISS] Cache miss on key {key}, key was previously evicted");
                    Console.ResetColor();
#endif
                } else {
                    _lifetimeCache.Remove(key);
                    _lifetimeCache.Add(key);
#if DEBUG
                    Refresh++;
                    Console.WriteLine($"    [REFRESHED] Refreshed key : {key}. Oldest is {_lifetimeCache[0]}");
#endif
                }
                return got;
            }
        }
    }
}