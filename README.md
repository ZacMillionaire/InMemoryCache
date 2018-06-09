# MemoryCache

Essentially a wrapper around a ConcurrentDictionary. A quick in memory cache for any arbitrary `<key,value>`.

The memory cache is bounded by a max capacity, with the oldest cached values being evicted first, based on when they were inserted, updated, or accessed.

Be careful when updating cached values outside of AddOrUpdate(key,value) **_will_** update the value within the cache, but **_will not_** update the age of the cached value.

For example,

```
// cache created at runtime: MemoryCache memCache = new MemoryCache<int,CacheValue>(10);

CacheValue v = new CacheValue("expensive data result");

memCache.AddOrUpdate(0,v);

// [some operations later that added 9 other values to the cache within the execution of this method]

v.Result = "Modified data result";
```

Within the cache, the value of `v` will contain "Modified data result", and the cache would hold the keys [0 .. 9] in order from last added to first.
If another cache insert were to occur, the modified value would be evicted first, as the cache was not updated via AddOrUpdate(), but instead its original reference value was changed.

This is an unlikely scenario, however care should be taken that values to be cached is the _last_ operation to be done, once any additional processing has been completed.

# Key Evictions

The cache tracks values inserted, if nothing but inserts are performed with unique keys, the eviction is first in last out. When updating or accessing values, the age of keys are updated as modified/accessed, so the oldest key is evicted.

The state of keys can be accessed at any time with `TKey[] MemoryCache.KeysByAge`, which will return an array of keys with the _oldest_ key (or next to be evicted on next unique insert) first. The youngest keys will be at the end of the array.

The size of this array is equal to the number of cached items, up to the defined maximum capacity.

# Usage

For the most part, using the MemoryCache class is similar to a ConcurrentDictionary when it comes to inserting, updating or deleting, exposing both `void AddOrUpdate(TKey,TValue)` and `bool TryGetValue(TKey, out TValue)`. 

If a key has been evicted (or does not exist) and `TryGetValue` is called on that key, `false` will be returned, and the value of `out TValue` will be null if `TValue` is a class, otherwise the value will be whatever `default(TValue)` would return.

Creating a new MemoryCache:
```
int cacheCapacity = 10; // Must be greater than 0
MemoryCache memCache = new MemoryCache<int, CacheValue>(cacheCapacity);
```

Adding/Updating a key:
```
memCache.AddOrUpdate(1, new CacheValue());
```

Retrieving a value:
```
bool cacheHit = memCache.TryGetValue(1, out CacheValue cachedValue);

// For early versions of C#
CacheValue cachedValue;
bool cacheHit = memCache.TryGetValue(1, out cachedValue);
```

# Tests

See the tests for more examples of usage.

For **Release** builds of MemoryCache, code coverage is 100%.

**Debug** builds have outputs and additional variables that are _not_ tested. So take care when building as **Debug** as some unexpected and uncaught exceptions may be raised.