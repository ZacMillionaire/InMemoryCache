using InMemoryCache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole {
    class Program {
        static void Main(string[] args) {
            int maxSize = 60;
            // Used to control the upper limit of key values, based on a modulus of i in the Parallel.For call
            // a value higher equal to maxSize means the cache will contain 0 .. maxSize inserts with no evictions,
            // a value less than maxSize will fill the cache with cacheBucketSize number of items with no evictions
            // any bucket size above maxSize will create evictions to simulate a competing cache
            int cacheBucketSize = 61;

            var mc = new MemoryCache<int, TestComplexClass>(maxSize);

            var r = new Random();

            // Bombard the ConcurrentDictionary with 10000 competing AddOrUpdates
            Parallel.For(0, 10000, i => {

                var newEntry = new TestComplexClass();

                mc.AddOrUpdate(i % cacheBucketSize, newEntry);

                //Console.WriteLine($"Inserted GUID {index} {newEntry.RandomValue}");

                bool cacheHit = mc.TryGetValue(i % cacheBucketSize, out TestComplexClass insertedValue);

                if(cacheHit == true) {
                    // Console.WriteLine($"[HIT] Inserted GUID {index} {insertedValue.RandomValue}");
                }
            });

#if DEBUG
            Console.WriteLine($"Cache stats: Inserts: {mc.Inserts}, Updates: {mc.Updates}, " +
                $"Evictions: {mc.Evictions}, Misses: {mc.Misses}, Total caches: {mc.Inserts + mc.Updates}, " +
                $"Refreshes: {mc.Refresh}, " +
                $"Total cache items: {mc.Inserts - mc.Evictions}"); // will match the number of items in the cache, ie, (mc._inserts - mc._evictions) == [the count of the internal cache]
#endif
            Console.ReadKey();
        }
    }

    public class TestComplexClass {
        public DateTime Now { get; set; }
        public Guid RandomValue { get; set; }

        public TestComplexClass() {
            Now = DateTime.Now;
            RandomValue = Guid.NewGuid();
        }
    }
}
