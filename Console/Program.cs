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
            int maxSize = 10;

            var mc = new MemoryCache<int, TestComplexClass>(maxSize);

            var r = new Random(1);

            // Bombard the ConcurrentDictionary with 10000 competing AddOrUpdates
            Parallel.For(0, 100, i =>
            {

                var index = r.Next(0, 20);

                var newEntry = new TestComplexClass();

                mc.AddOrUpdate(index, newEntry);

                Console.WriteLine($"Inserted GUID {index} {newEntry.RandomValue}");

                bool cacheHit = mc.TryGetValue(index, out TestComplexClass insertedValue);

                if (cacheHit == true)
                {
                    Console.WriteLine($"[HIT] Inserted GUID {index} {insertedValue.RandomValue}");
                }
            });

            Console.WriteLine("end");
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
