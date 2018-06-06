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
            Parallel.For(0, 1000, i =>
            {

                var index = r.Next(0, 20);
                mc.AddOrUpdate(index, new TestComplexClass());

                mc.TryGetValue(index, out TestComplexClass insertedValue);

                Console.WriteLine(insertedValue.RandomValue);

            });

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
