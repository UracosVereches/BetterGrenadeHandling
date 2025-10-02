using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using System.Collections.Concurrent;
using UnityEngine;
using RimWorld;
using static Verse.PathRequest;
using Unity.Collections;
using Verse.Noise;

namespace BetterGrenadeHandling
{
    public static class DangerPositionTracker
    {
        private static readonly ConcurrentDictionary<IntVec3, Projectile> dict = new ConcurrentDictionary<IntVec3, Projectile>();
        private static int cleanup_count = 0;

        public static void MarkPosition(IntVec3 pos, Projectile projectile)
        {
            dict.GetOrAdd(pos, projectile);

            cleanup_count++;
            if (cleanup_count > 100)
            {
                CleanUp();
            }
        }

        public static void RemovePosition(IntVec3 pos)
        {
            dict.TryRemove(pos, out _);
        }

        public static bool IsPositionDangerousFor(IntVec3 pos, Pawn pawn)
        {
            return dict.TryGetValue(pos, out _);
        }

        public static ConcurrentDictionary<IntVec3, Projectile> GetDictionary()
        {
            
            return new ConcurrentDictionary<IntVec3, Projectile>(dict);
        }

        public static void CleanUp()
        {
            foreach (var kvp in dict)
            {
                var value = kvp.Value;
                if (value == null || value.Destroyed)
                {
                    dict.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    // Custom grid constructor for dangerous positions
    public class DangerousGrid : IPathGridCustomizer
    {
        private readonly Map map;
        private NativeArray<ushort> offsets;
        private static readonly Dictionary<Map, DangerousGrid> dangerousGridCache = new Dictionary<Map, DangerousGrid>();

        public DangerousGrid(Map map)
        {
            this.map = map;
            offsets = new NativeArray<ushort>(map.cellIndices.NumGridCells, Allocator.Persistent);
            foreach (var kvp in DangerPositionTracker.GetDictionary())
            {
                IntVec3 pos = kvp.Key;

                if (pos.InBounds(map))
                {
                    int index = map.cellIndices.CellToIndex(pos);
                    offsets[index] = 9000; // 10000 - impassable
                }
            }
        }

        public static NativeArray<ushort> CombineWithCustomizer(IPathGridCustomizer original, DangerousGrid dangerousGrid)
        {
            var danger = dangerousGrid.GetOffsetGrid();
            var orig = original?.GetOffsetGrid();

            int len = Math.Max(danger.Length, orig?.Length ?? 0);
            var combined = new NativeArray<ushort>(len, Allocator.Persistent);

            int dangerLen = danger.Length;
            int origLen = orig?.Length ?? 0;

            for (int i = 0; i < len; i++)
            {
                ushort d = (i < dangerLen) ? danger[i] : (ushort)0;
                ushort o = (i < origLen) ? orig[i] : (ushort)0;
                combined[i] = (o >= d) ? o : d;
            }

            return combined;
        }

        public NativeArray<ushort> GetOffsetGrid()
        {
            // Reset everything
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = 0;
            }

            // Mark all dangerous cells
            foreach (var kvp in DangerPositionTracker.GetDictionary())
            {
                IntVec3 pos = kvp.Key;

                if (pos.InBounds(map))
                {
                    int index = map.cellIndices.CellToIndex(pos);
                    offsets[index] = 9000; // 10000 - impassable
                }
            }

            return offsets;
        }

        public static DangerousGrid GetForMap(Map map)
        {
            bool found = dangerousGridCache.TryGetValue(map, out DangerousGrid grid);

            if (!found)
            {
                // Do unfound logic here
            }

            return grid;
        }

        // Dispose manually later
        public void Dispose()
        {
            if (offsets.IsCreated)
                offsets.Dispose();
        }
    }

    [HarmonyPatch(typeof(PawnUtility))]
    [HarmonyPatch("KnownDangerAt")]
    public static class Patch_KnownDangerAt_Prefix
    {
        static bool Prefix(IntVec3 c, Map map, Pawn forPawn, ref bool __result)
        {
            __result = DangerPositionTracker.IsPositionDangerousFor(c, forPawn);
            if (__result)
            {
                return false;
            }

            return true;
        }
    }

    // Include our dangerous cell grid with custom costs
    [HarmonyPatch(typeof(PathRequest))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] {
    typeof(Map), typeof(IntVec3), typeof(LocalTargetInfo), typeof(IntVec3?),
    typeof(TraverseParms), typeof(PathFinderCostTuning), typeof(PathEndMode),
    typeof(Pawn), typeof(int), typeof(int), typeof(int), typeof(IPathGridCustomizer)
    })]
    public static class PathRequest_Constructor_Patch
    {
        static void Prefix(
            PathRequest __instance,
            Map map,
            IntVec3 start,
            LocalTargetInfo dest,
            IntVec3? exactDestination,
            TraverseParms traverseParms,
            PathFinderCostTuning tuning,
            PathEndMode peMode,
            Pawn pawn,
            int tickCreated,
            int tickStart,
            int tickDeadline,
            ref IPathGridCustomizer customizer)
        {
            customizer = DangerousGrid.GetForMap(map);
        }
    }

    // Mark dangerous positions for pawns to avoid when explosive is launched
    [HarmonyPatch(typeof(Projectile))]
    [HarmonyPatch("Launch")]
    [HarmonyPatch(new Type[] {
    typeof(Thing),
    typeof(Vector3),
    typeof(LocalTargetInfo),
    typeof(LocalTargetInfo),
    typeof(ProjectileHitFlags),
    typeof(bool),
    typeof(Thing),
    typeof(ThingDef)
    })]
    public static class Patch_Projectile_Launch_Postfix
    {
        static void Postfix(Projectile __instance, Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            if (!(__instance is Projectile_Explosive))
            {
                return;
            }

            float blastradius = __instance.def?.projectile?.explosionRadius ?? 0f;
            IntVec3 usedCell = usedTarget.Cell;

            DangerPositionTracker.MarkPosition(usedCell, __instance);
            foreach (IntVec3 pos in GenRadial.RadialCellsAround(usedCell, blastradius, true))
            {
                DangerPositionTracker.MarkPosition(pos, __instance);
            }
        }
    }

    // Unmark dangerous positions when explosive is destroyed
    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("Destroy")]
    static class Patch_Thing_Destroy_Postfix
    {
        static void Postfix(Thing __instance)
        {
            if (!(__instance is Projectile_Explosive explosive)) return;
            float blastradius = explosive.def?.projectile?.explosionRadius ?? 0f;
            IntVec3 usedCell = explosive.usedTarget.Cell;

            DangerPositionTracker.RemovePosition(usedCell);
            foreach (IntVec3 pos in GenRadial.RadialCellsAround(usedCell, blastradius, true))
            {
                DangerPositionTracker.RemovePosition(pos);
            }
        }
    }
}
