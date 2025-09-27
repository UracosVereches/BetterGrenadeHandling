using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace BetterGrenadeHandling
{
    public static class Scapegoat
    {
        private static readonly BlockingCollection<Func<object>> queue = new BlockingCollection<Func<object>>();
        private static readonly Task worker;

        static Scapegoat()
        {
            // Background worker, runs forever
            worker = Task.Run(() =>
            {
                foreach (var job in queue.GetConsumingEnumerable())
                {
                    try
                    {
                        job();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Better Grenade Handling] Scapegoat task failed: {ex}");
                    }
                }
            });
        }

        // Add work
        public static void Enqueue(Func<object> job)
        {
            queue.Add(job);
        }
    }
}
