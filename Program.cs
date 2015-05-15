using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace CRUSH_Selection_Algorithm_Test
{
    public class Obj
    {
        public readonly Guid Id = Guid.NewGuid();
        private readonly static MD5 md5 = MD5.Create();

        //TODO: Change to CRC32 etc.
        public byte[] GetPseudoRandom(int m)
        {
            var buffer = Id.ToByteArray(); //16 bytes

            var ms = BitConverter.GetBytes(m); //4 bytes
            for (int i = 0; i < 4; i++)
                buffer[i] ^= ms[i];

            return md5.ComputeHash(buffer); //16 bytes in 16 bytes out
        }
    }

    public class Item
    {
        public readonly HashSet<Obj> Objects = new HashSet<Obj>();
        public int Weight;
        public double w;
        public bool disabled;
    }

    public class Bucket
    {
        public readonly List<Item> Items;
        public int ObjectCount;

        public Bucket(IEnumerable<int> weights)
        {
            Items = weights.Select(w => new Item { Weight = w }).ToList();
            Update();
        }

        private void Update()
        {
            double sumweight = Items.Select(_ => (long)_.Weight).Sum();

            foreach (var item in Items)
                item.w = item.Weight / sumweight;
        }

        public void AddObject(Obj obj, Dictionary<int, int> iterationStat)
        {
            int iters;
            var item = Select(obj, out iters);
            AddToIterationStat(iterationStat, iters);
            item.Objects.Add(obj);
            ++ObjectCount;
        }

        private void AddToIterationStat(Dictionary<int, int> iterationStat, int iters)
        {
            if (iterationStat.ContainsKey(iters))
                ++iterationStat[iters];
            else
                iterationStat[iters] = 1;
        }

        public void AddObjects(int count, Dictionary<int, int> iterationStat)
        {
            for (int i = 0; i < count; i++)
                AddObject(new Obj(), iterationStat);
        }

        public void Rebalance(out int expected, out int moved)
        {
            Update();
            expected = (int)Items.Sum(item => Math.Abs(item.w * ObjectCount - item.Objects.Count));
            expected /= 2;
            moved = 0;
            foreach (var item in Items)
            {
                foreach (var obj in item.Objects.ToList())
                {
                    int iters;
                    var dest = Select(obj, out iters);
                    if (dest != item)
                    {
                        //Move
                        item.Objects.Remove(obj);
                        dest.Objects.Add(obj);
                        ++moved;
                    }
                }
            }
        }

        public Item Select(Obj obj, out int iters)
        {
            iters = 0;
            while (true)
            {
                ++iters;
                Item selectedItem = null;
                long min = long.MaxValue;
                for (int index = 0; index < Items.Count; index++)
                {
                    var item = Items[index];
                    if (item.Weight == 0)
                        continue;
                    var k = index + 1;
                    byte[] rnd1 = obj.GetPseudoRandom(iters * k);
                    byte[] rnd2 = obj.GetPseudoRandom(2 * iters + rnd1[0] * k);

                    int sum1 = 2;//avarage sbyte is -0.5; move the center to 0
                    for (int i = 0; i < 4; i++)
                        sum1 += (sbyte)rnd1[i];

                    int sum2 = 2;
                    for (int i = 0; i < 4; i++)
                        sum2 += (sbyte)rnd2[i];

                    long rnd = sum1 * sum1 + sum2 * sum2; //approx exponential
                    rnd <<= 30;
                    rnd /= item.Weight;
                    if (rnd < min)
                    {
                        min = rnd;
                        selectedItem = item;
                    }
                }

                if (!selectedItem.disabled)
                    return selectedItem;
            }
        }
    }

    class Program
    {
        private static void Test()
        {
            const int N = 100 * 1000;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var random = new Random();
            var weights = Enumerable.Range(0, 10).Select(_ => random.Next(1, 3));
            var bucket = new Bucket(weights);
            var iterationStat = new Dictionary<int, int>();

            bucket.AddObjects(N, iterationStat);
            Show(bucket, iterationStat);

            Console.WriteLine("======= Rebalance =======");
            var item = bucket.Items.First(_ => _.Weight > 0);
            item.Weight = item.Weight > 0 ? 0 : 1;
            int expected, moved;
            bucket.Rebalance(out expected, out moved);
            ShowObjectDistribution(bucket);
            Console.WriteLine("Expected: {0}, Moved: {1}\n", expected, moved);

            Console.WriteLine("======= Overloaded. Add more objects. =======");
            item = bucket.Items.First(_ => _.Weight > 0);
            item.disabled = true;//when overloaded or failed
            iterationStat.Clear();
            bucket.AddObjects(N, iterationStat);//add more items before rebalance
            Show(bucket, iterationStat);

            Console.WriteLine("======= Rebalance =======");
            item.Weight = 0;
            bucket.Rebalance(out expected, out moved);
            ShowObjectDistribution(bucket);
            Console.WriteLine("Expected: {0}, Moved: {1}\n", expected, moved);

            Console.WriteLine("======= Replace item and rebalance =======");
            item.disabled = false;
            item.Weight = 1;
            bucket.Rebalance(out expected, out moved);
            ShowObjectDistribution(bucket);
            Console.WriteLine("Expected: {0}, Moved: {1}\n", expected, moved);

            Console.WriteLine("======= Remove item and rebalance =======");
            item.Weight = 0;
            bucket.Rebalance(out expected, out moved);
            ShowObjectDistribution(bucket);
            Console.WriteLine("Expected: {0}, Moved: {1}\n", expected, moved);

            Console.WriteLine("======= Add new item and rebalance =======");
            bucket.Items.Add(new Item { Weight = 1 });
            bucket.Rebalance(out expected, out moved);
            ShowObjectDistribution(bucket);
            Console.WriteLine("Expected: {0}, Moved: {1}\n", expected, moved);

            Console.WriteLine("======= Add more items (one by one) and rebalance =======");
            for (int i = 0; i < 10; i++)
            {
                bucket.Items.Add(new Item { Weight = 1 });
                bucket.Rebalance(out expected, out moved);
                Console.WriteLine("Expected: {0}, Moved: {1}\n", expected, moved);
            }
        }

        private static void Show(Bucket bucket, Dictionary<int, int> iterationStat)
        {
            ShowIterationStatistics(iterationStat);
            ShowObjectDistribution(bucket);
            Console.WriteLine();
        }

        private static void ShowObjectDistribution(Bucket bucket)
        {
            Console.WriteLine("--- Object Distribution: ---");
            Console.WriteLine(" Weigth, w, p, (diff from expected)%, count");

            foreach (var item in bucket.Items)
            {
                var expected = item.w * bucket.ObjectCount;
                var actual = item.Objects.Count;
                var diff = actual - expected;
                if ((int)Math.Round(diff) == 0)
                    diff = 0;
                else
                    diff = (100 * diff) / expected;
                Console.WriteLine("{3}Weight:{0}, w:{1:0.0000}, diff:{2:0.00}%, count:{4}",
                                  item.Weight, item.w, diff, item.disabled ? "*" : " ", item.Objects.Count);
            }
        }

        private static void ShowIterationStatistics(Dictionary<int, int> iterationStat)
        {
            Console.WriteLine("--- Iteration Statistics: ---");
            Console.WriteLine("Iteration Count: Number of Appearances");
            foreach (var iters in iterationStat.Keys.OrderBy(_ => _))
                Console.WriteLine("{0:00}: {1}", iters, iterationStat[iters]);
        }

        static void Main()
        {
            Test();

            Console.WriteLine("Done...");
            Console.ReadKey();
        }
    }
}
