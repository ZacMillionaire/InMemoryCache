using System;
using System.Threading.Tasks;
using InMemoryCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests {
    [TestClass]
    public class SimpleKeyTests {
        [TestMethod]
        public void CacheMustHaveNonZeroCapacity() {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new MemoryCache<int, SimpleTestClass>(0));
        }

        [TestMethod]
        public void CacheDoesNotExceedCapacity() {

            int capacity = 10;
            int itemsToInsert = 100;

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);

            Parallel.For(0, itemsToInsert, i => {

                var newEntry = new SimpleTestClass();

                memcache.AddOrUpdate(i, newEntry);

                //Console.WriteLine($"Inserted GUID {index} {newEntry.RandomValue}");

                //memcache.TryGetValue(i, out SimpleTestClass insertedValue);
            });

            Assert.AreEqual(10, memcache.CacheSize);
        }

        [TestMethod]
        public void Cache_AddOrUpdateOnce_And_TryGet_SimpleData() {
            int capacity = 1;

            var testValue = new SimpleTestClass();

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);
            memcache.AddOrUpdate(1, testValue);

            memcache.TryGetValue(1, out SimpleTestClass insertedValue);

            Assert.AreEqual(testValue, insertedValue);
        }

        [TestMethod]
        public void Cache_AddOrUpdate_SameKey_ShouldUpdateCounter() {
            int capacity = 5;
            int itemsToInsert = 5;

            var memcache = new MemoryCache<int, SimpleTestClass>(capacity);

            int keyStart = 0;

            ParallelHelper<int, SimpleTestClass>(itemsToInsert, memcache, () => { return keyStart++; }, () => { return new SimpleTestClass(); });

            var initalKeyAge = memcache.KeysByAge;

            memcache.AddOrUpdate(1, new SimpleTestClass());

            var updatedKeyAge = memcache.KeysByAge;
        }

        private void ParallelHelper<TKey, TValue>(int timesToRun, MemoryCache<TKey, TValue> cache, Func<TKey> keyFunction, Func<TValue> objectToInsertFunction) {
            Parallel.For(0, timesToRun, i => {
                cache.AddOrUpdate(keyFunction(), objectToInsertFunction());
            });
        }
    }

    public class SimpleTestClass {
        public Guid ImportantValue { get; set; }

        public SimpleTestClass() {
            ImportantValue = Guid.NewGuid();
        }
    }
}
