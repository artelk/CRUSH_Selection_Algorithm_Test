using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Selection_Algorithm_Test;

namespace CRUSH_Selection_Algorithm_Test
{
    public class Obj
    {
        private static uint idCounter;
        public readonly uint Id = ++idCounter;
    }

    public class Item
    {
        private static uint idCounter;
        public readonly uint Id = ++idCounter;
        public readonly HashSet<Obj> Objects = new HashSet<Obj>();
        public int Weight;
        public double w;
        public bool disabled;
    }

    public class Bucket
    {
        public readonly List<Item> Items;
        public int ObjectCount;

        public Bucket(Func<Bucket, Obj, uint, Item> alg, IEnumerable<int> weights)
        {
            this.alg = alg;
            Items = weights.Select(w => new Item { Weight = w }).ToList();
            Update();
        }

        private void Update()
        {
            double sumweight = Items.Select(_ => (long)_.Weight).Sum();

            foreach (var item in Items)
                item.w = item.Weight / sumweight;
        }

        public void AddObject(Obj obj)
        {
            var item = Choose(obj);
            item.Objects.Add(obj);
            ++ObjectCount;
        }

        public void AddObjects(int count)
        {
            for (int i = 0; i < count; i++)
                AddObject(new Obj());
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
                    var dest = Choose(obj);
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

        private readonly Func<Bucket, Obj, uint, Item> alg;

        public Item Choose(Obj obj, uint r = 0)
        {
            return alg(this, obj, r);
        }
    }

    class Program
    {
        private static Item Choose_Straw2(Bucket bucket, Obj obj, uint r)
        {
            Item selectedItem = null;
            long high_draw = long.MinValue;
            foreach (var item in bucket.Items)
            {
                int w = item.Weight;
                if (item.Weight == 0 || item.disabled)
                    continue;
                uint u = Hash.Calculate(obj.Id, item.Id, r);
                u &= 0xffff;

                long ln = Ln.Get(u) - 0x1000000000000L;

                long draw = ln/w;

                if (draw > high_draw)
                {
                    selectedItem = item;
                    high_draw = draw;
                }
            }
            return selectedItem;
        }

        private static Item Choose_Straw2Plus(Bucket bucket, Obj obj, uint r)
        {
            Item selectedItem = null;
            long min = long.MaxValue;
            foreach (var item in bucket.Items)
            {
                if (item.Weight == 0 || item.disabled)
                    continue;

                uint rnd1 = Hash.Calculate(obj.Id, item.Id, r);
                uint rnd2 = Hash.Calculate(rnd1 ^ 0xa5a5a5a5);

                uint s = (rnd1 & 0x00ff00ff) + ((rnd1 >> 8) & 0x00ff00ff) +
                         (rnd2 & 0x00ff00ff) + ((rnd2 >> 8) & 0x00ff00ff);

                const int center = 4*byte.MaxValue/2;
                int sum1 = (int)(s & 0xffff) - center;
                int sum2 = (int)(s >> 16) - center;
                long rnd = sum1 * sum1 + sum2 * sum2; //approx exponential
                rnd <<= 42;
                rnd /= item.Weight;
                if (rnd < min)
                {
                    min = rnd;
                    selectedItem = item;
                }
            }

            return selectedItem;
        }

        private static void Test()
        {
            const int N = 1000 * 1000;
            Func<Bucket, Obj, uint, Item> alg;
            if (true)
                alg = Choose_Straw2Plus;
            else
                alg = Choose_Straw2;

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var random = new Random();
            var weights = Enumerable.Range(1, 10).Select(i => i);
            var bucket = new Bucket(alg, weights);

            bucket.AddObjects(N);
            ShowObjectDistribution(bucket);

            Console.WriteLine("======= Rebalance =======");
            var item = bucket.Items.First(_ => _.Weight > 0);
            item.Weight = 0;
            int expected, moved;
            bucket.Rebalance(out expected, out moved);
            ShowObjectDistribution(bucket);
            Console.WriteLine("Expected: {0}, Moved: {1}\n", expected, moved);

            Console.WriteLine("======= Overloaded. Add more objects. =======");
            item = bucket.Items.First(_ => _.Weight > 0);
            item.disabled = true;//when overloaded or failed
            bucket.AddObjects(N);//add more items before rebalance
            ShowObjectDistribution(bucket);

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
            for (int i = 0; i < 20; i++)
            {
                bucket.Items.Add(new Item { Weight = 1 });
                bucket.Rebalance(out expected, out moved);
                ShowObjectDistribution(bucket);
                Console.WriteLine("Expected: {0}, Moved: {1}\n", expected, moved);
            }
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
            Console.WriteLine();
        }

        static void Main()
        {
            Test();

            Console.WriteLine("Done...");
            Console.ReadKey();
        }
    }
}
