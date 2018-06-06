using InMemoryCache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            int maxSize = 1000;

            var mc = new MemoryCache<int, TestComplexClass>(maxSize);

            var r = new Random(1);

            // Bombard the ConcurrentDictionary with 10000 competing AddOrUpdates
            Parallel.For(0, 10000, i =>
            {

                var index = r.Next(1, 2000);

                var newEntry = new TestComplexClass();

                mc.AddOrUpdate(i % index, newEntry);

                //Console.WriteLine($"Inserted GUID {index} {newEntry.RandomValue}");

                bool cacheHit = mc.TryGetValue(i % index, out TestComplexClass insertedValue);

                if (cacheHit == true)
                {
                    // Console.WriteLine($"[HIT] Inserted GUID {index} {insertedValue.RandomValue}");
                }
            });

            Console.WriteLine($"Cache stats: Inserts: {mc._inserts}, Updates: {mc._updates}, " +
                $"Evictions: {mc._evictions}, Misses: {mc._misses}, Total caches: {mc._inserts + mc._updates}, " +
                $"Total cache items: {mc._inserts - mc._evictions}"); // will match the number of items in the cache, ie, (mc._inserts - mc._evictions) == [the count of the internal cache]
            Console.ReadKey();
        }
    }

    public class TestComplexClass
    {
        public DateTime Now { get; set; }
        public Guid RandomValue { get; set; }

        public TestComplexClass()
        {
            Now = DateTime.Now;
            RandomValue = Guid.NewGuid();
        }
    }
}
