using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using InMemoryCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests {
    [TestClass]
    public class SimpleCacheTests {

        /// <summary>
        /// What is a cache with 0 capcity?
        /// A miserable pile of wasted memory.
        /// But enough pointless comments, assert that a cache with a capcity of 0 throws an ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void CacheMustHaveNonZeroCapacity() {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new MemoryCache<int, SimpleTestClass>(0));
        }

        /// <summary>
        /// Asserts that no matter how many entries are added, the cache will not hold more than the capcity defined
        /// at cache creation time.
        /// </summary>
        [TestMethod]
        public void CacheDoesNotExceedCapacity() {

            int capacity = 10;
            int itemsToInsert = 100;

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);

            Parallel.For(0, itemsToInsert, i => {

                var newEntry = new SimpleTestClass();

                memcache.AddOrUpdate(i, newEntry);
            });

            Assert.AreEqual(10, memcache.CacheSize);
        }

        /// <summary>
        /// Inserts a key, then attempts to get the value.
        /// </summary>
        [TestMethod]
        public void Cache_AddOrUpdateOnce_And_TryGet_SimpleData() {
            int capacity = 1;

            var testValue = new SimpleTestClass();

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);
            memcache.AddOrUpdate(1, testValue);

            memcache.TryGetValue(1, out SimpleTestClass insertedValue);

            Assert.AreEqual(testValue, insertedValue);
        }

        /// <summary>
        /// Validates that updating a cache item after being inserted renews its lifetime and makes it the 'youngest' in the cache
        /// </summary>
        [TestMethod]
        public void Cache_AddOrUpdate_SameKey_ShouldRenewLifetime() {
            int capacity = 5;
            int itemsToInsert = 5;
            int keyStart = 0;
            int keyOfInterest = 1; // the specific key in the cache to test against

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);

            // Insert as many entries as needed
            ParallelAddOrUpdateHelper<int, SimpleTestClass>(itemsToInsert, memcache, () => { return keyStart++; }, () => { return new SimpleTestClass(); });

            // Assert that the first key added is _not_ the youngest item
            // This is a poor test as who knows what order ParallelHelper inserted them.
            var initalKeyAge = memcache.KeysByAge;
            Assert.AreNotEqual(capacity - 1, Array.IndexOf(initalKeyAge, keyOfInterest));

            // Update keyOfInterest to trigger a renew on lifetime
            memcache.AddOrUpdate(keyOfInterest, new SimpleTestClass());

            // Assert that the keyOfInterest is the youngest now
            var updatedKeyAge = memcache.KeysByAge;
            Assert.IsTrue(( capacity - 2 <= Array.IndexOf(updatedKeyAge, keyOfInterest) ));
        }

        /// <summary>
        /// Inserts a fixed number of items equal to the caches maximum capacity.
        /// A specified key is then accessed to update its lifetime, and then verified that the key is the youngest in the cache
        /// </summary>
        [TestMethod]
        public void Cache_TryGetValue_SameKey_ShouldRenewLifetime() {
            int capacity = 5;
            int itemsToInsert = 5;
            int keyStart = 0;
            int keyOfInterest = 1; // the specific key in the cache to test against

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);

            // Insert as many entries as needed
            ParallelAddOrUpdateHelper<int, SimpleTestClass>(itemsToInsert, memcache, () => { return keyStart++; }, () => { return new SimpleTestClass(); });

            // Assert that the first key added is _not_ the youngest item
            // This is a poor test as who knows what order ParallelHelper inserted them.
            var initalKeyAge = memcache.KeysByAge;
            Assert.AreNotEqual(capacity - 1, Array.IndexOf(initalKeyAge, keyOfInterest));

            // Update the keyOfInterest with a value we control
            var newValue = new SimpleTestClass();
            memcache.AddOrUpdate(keyOfInterest, newValue);

            // Get keyOfInterest to renew its lifetime
            memcache.TryGetValue(keyOfInterest, out SimpleTestClass cachedValue);

            // Assert that getting the value updated its lifetime, and that the value did not change
            var updatedKeyAge = memcache.KeysByAge;
            Assert.AreEqual(capacity - 1, Array.IndexOf(updatedKeyAge, keyOfInterest));
            Assert.AreEqual(newValue, cachedValue);
        }

        /// <summary>
        /// Test verifies that attempting to retrieve an expired cache item returns false, and a null value.
        /// </summary>
        [TestMethod]
        public void CacheMiss_Returns_False_And_ValueNull() {
            int capacity = 5;
            int itemsToInsert = 50; // Insert more values than capacity to ensure we get an eviction
            int keyStart = 0;
            int keyOfInterest = 5; // the specific key in the cache to test against

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);

#if DEBUG
            // Verify output of inserts.
            // As Parallel will try to do as much as possible at once, the oldest keys should always be from at least itemsToInsert - capacity .. itemsToInsert
            ParallelAddOrUpdateHelperWithPostAction<int, SimpleTestClass>(itemsToInsert, memcache, () => { return keyStart++; }, () => { return new SimpleTestClass(); },
                (i) => {
                    Console.WriteLine($"i:{i} | {String.Join(",", memcache.KeysByAge)}, inserted {keyStart}");
                });
            Console.WriteLine($"final cache ages:{String.Join(",", memcache.KeysByAge)}");
#else
            // Insert as many entries as needed
            ParallelAddOrUpdateHelper<int, SimpleTestClass>(itemsToInsert, memcache, () => { return keyStart++; }, () => { return new SimpleTestClass(); });
#endif

            Console.WriteLine($"keyOfInterest: {keyOfInterest}, {String.Join(",", memcache.KeysByAge)}");
            // try get an expired cache item
            bool cacheHit = memcache.TryGetValue(keyOfInterest, out SimpleTestClass cachedValue);

            if(cacheHit == true) {
                Console.WriteLine($"keyOfInterest: {keyOfInterest}, {String.Join(",", memcache.KeysByAge)}");
                throw new Exception();
            }
            // Assert that getting the value updated its lifetime, and that the value did not change
            Assert.AreEqual(false, cacheHit);
            Assert.AreEqual(null, cachedValue);
        }

        /// <summary>
        /// This test can be seen as a bit of a performance test, where 10_000 values are entered, and the 2nd key
        /// is constantly retrieved. This should refresh the keys lifetime so that at the end of the 10_000 inserts,
        /// the 2nd key is still the youngest (or near enough)
        /// </summary>
        [TestMethod]
        public void Cache_MassInsert_ContinuousUpdateSingleKey() {
            int capacity = 50;
            int itemsToInsert = 10_000; // Insert more values than capacity to ensure we get an eviction
            int keyStart = 0;
            int keyOfInterest = 1; // the specific key in the cache to test against

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);

            // Insert as many entries as needed, constantly refreshing the 2nd cache item (index 1)
            ParallelAddOrUpdateHelperWithPostAction<int, SimpleTestClass>(
                itemsToInsert,
                memcache,
                () => { return keyStart++; },
                () => { return new SimpleTestClass(); },
                () => {
                    // After each add or update, refresh a key to ensure it stays the youngest
                    memcache.TryGetValue(keyOfInterest, out SimpleTestClass v);
                    //Trace.WriteLine($"{GC.GetTotalMemory(false) / ( 1024 * 1024 )}MB");
                });

            // Get the final cache lifetime, and check that our desired key remained the youngest entry due to cache hits
            // from TryGetValue.
            var cacheLifetimes = memcache.KeysByAge;
            Assert.AreEqual(capacity - 1, Array.IndexOf(cacheLifetimes, keyOfInterest));

        }

        /// <summary>
        /// Wrapper for Parallel.For
        /// <para>
        /// Calls AddOrUpdate on the given cache as many times as timesToRun, using the specified key and value generation functions.
        /// </para>
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="timesToRun"></param>
        /// <param name="cache"></param>
        /// <param name="keyFunction"></param>
        /// <param name="objectToInsertFunction"></param>
        private void ParallelAddOrUpdateHelper<TKey, TValue>(int timesToRun, MemoryCache<TKey, TValue> cache, Func<TKey> keyFunction, Func<TValue> objectToInsertFunction) {
            var tasks = new List<Task>(timesToRun);
            for(int i = 0; i < timesToRun; i++) {
                cache.AddOrUpdate(keyFunction(), objectToInsertFunction());
            }
            Task.WaitAll(tasks.ToArray());
        }

        /// <summary>
        /// Wrapper for Parallel.For
        /// <para>
        /// Calls AddOrUpdate on the given cache as many times as timesToRun, using the specified key and value generation functions.
        /// </para>
        /// <para>
        /// Additionally runs an action after the AddOrUpdate
        /// </para>
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="timesToRun"></param>
        /// <param name="cache"></param>
        /// <param name="keyFunction"></param>
        /// <param name="objectToInsertFunction"></param>
        /// <param name="postAction"></param>
        private void ParallelAddOrUpdateHelperWithPostAction<TKey, TValue>(int timesToRun,
            MemoryCache<TKey, TValue> cache,
            Func<TKey> keyFunction,
            Func<TValue> objectToInsertFunction,
            Action postAction
        ) {
            var tasks = new List<Task>(timesToRun);
            for(int i = 0; i < timesToRun; i++) {
                cache.AddOrUpdate(keyFunction(), objectToInsertFunction());
                postAction();
            }
            Task.WaitAll(tasks.ToArray());
        }

        /// <summary>
        /// This method was to used to track down an issue using Parallel.For in a test context.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="timesToRun"></param>
        /// <param name="cache"></param>
        /// <param name="keyFunction"></param>
        /// <param name="objectToInsertFunction"></param>
        /// <param name="postAction"></param>
        private void DEBUG_ParallelAddOrUpdateHelperWithPostAction<TKey, TValue>(int timesToRun,
            MemoryCache<TKey, TValue> cache,
            Func<TKey> keyFunction,
            Func<TValue> objectToInsertFunction,
            Action<int> postAction
        ) {

            // using Parallel.For "amusingly" enough caused the cache to occasionally be in a bad state when asserting in some cases,
            // discovered when trying to Assert that a cache miss _had_ happened, but the thread executing it wasn't reflective
            // of the actual state of the cache.
            // Technically the below code should be the same as the above, however there can be a state where the test will
            // reach the stage to attempt a cache miss, and get in at a moment where the Parallel.For hasn't finished but released a lock,
            // resulting in the AddOrUpdate's still running, while the test is completed.
            // Using Task.WaitAll ensures that everything _is_ finished.
            /*
              Parallel.For(0, timesToRun, i => {
                  cache.AddOrUpdate(keyFunction(), objectToInsertFunction());
                  postAction(i);
              });*/

            var tasks = new List<Task>(timesToRun);
            for(int i = 0; i < timesToRun; i++) {
                cache.AddOrUpdate(keyFunction(), objectToInsertFunction());
                postAction(i); // Run an action, passing the current iteration to it
            }
            Task.WaitAll(tasks.ToArray());

        }
    }

    public class SimpleTestClass {
        public Guid ImportantValue { get; set; }

        public SimpleTestClass() {
            ImportantValue = Guid.NewGuid();
        }
    }
}
