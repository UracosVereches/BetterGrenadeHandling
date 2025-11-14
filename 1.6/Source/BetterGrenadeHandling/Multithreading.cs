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
        private static readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
        private static readonly Task worker;

        static Scapegoat()
        {
            // Background worker, runs forever
            worker = Task.Run(() =>
            {
                try
                {
                    foreach (var job in queue.GetConsumingEnumerable())
                    {
                        try
                        {
                            job();
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[Better Grenade Handling: Scapegoat] Job failed: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[Better Grenade Handling: Scapegoat] Background task failed: {ex}");
                }
            });
        }

        public static void Enqueue(Action job)
        {
            queue.Add(job);
        }
    }
}
