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
using static UnityEngine.GraphicsBuffer;

namespace BetterGrenadeHandling
{
    public static class DangerPositionTracker
    {
        // All dangerous positions, projectiles can overlap each other. For cheap global lookup of danger in position
        private static readonly Dictionary<IntVec3, int> allDangerousPositions = new Dictionary<IntVec3, int>();
        // Dangerous positions per each projectile
        private static readonly ConcurrentDictionary<Projectile, HashSet<IntVec3>> dict = new ConcurrentDictionary<Projectile, HashSet<IntVec3>>();

        private static int cleanup_count = 0;

        public static void AddProjectile(Projectile projectile, HashSet<IntVec3> positions)
        {
            dict.TryAdd(projectile, positions);

            foreach (IntVec3 pos in positions)
            {
                AddDangerousPosition(pos);
            }

            cleanup_count++;
            if (cleanup_count > 100)
            {
                CleanUp();
            }
        }

        public static void RemoveProjectile(Projectile projectile)
        {
            dict.TryRemove(projectile, out HashSet<IntVec3> positionsToBeRemoved);

            foreach (IntVec3 pos in positionsToBeRemoved)
            {
                RemoveDangerousPosition(pos);
            }
        }

        public static void RemovePosition(Projectile projectile, IntVec3 pos)
        {
            if (dict.TryGetValue(projectile, out var hashset))
            {
                hashset.Remove(pos);
            }
        }

        public static bool IsDangerVisibleFor(IntVec3 pos, Pawn pawn)
        {
            foreach (var kvp in dict)
            {
                HashSet<IntVec3> hashset = kvp.Value;
                if (!hashset.Contains(pos))
                {
                    continue;
                }

                Projectile projectile = kvp.Key;
                DamageDef damageDef = projectile.DamageDef;
                HashSet<IntVec3> positions = kvp.Value;

                Pawn launcher = projectile.Launcher as Pawn;
                if (launcher == null)
                    continue;

                if (!BGHUtils.CanIgnoreCollateral(launcher, pawn, damageDef))
                {
                    return true;
                }
            }

            return false;
        }

        public static List<IntVec3> ObtainDangerousPositionsFor(Pawn pawn)
        {
            HashSet<IntVec3> obtained_positions = new HashSet<IntVec3>();
            foreach (var kvp in dict)
            {
                Projectile projectile = kvp.Key;
                DamageDef damageDef = projectile.DamageDef;
                HashSet<IntVec3> positions = kvp.Value;

                Pawn launcher = projectile.Launcher as Pawn;
                if (launcher == null)
                    continue;

                if (!BGHUtils.CanIgnoreCollateral(launcher, pawn, damageDef))
                {
                    obtained_positions.UnionWith(positions);
                }
            }

            return obtained_positions.ToList();
        }

        public static ConcurrentDictionary<Projectile, HashSet<IntVec3>> GetDictionary()
        {
            return new ConcurrentDictionary<Projectile, HashSet<IntVec3>>(dict);
        }

        public static void CleanUp()
        {
            foreach (var kvp in dict)
            {
                var value = kvp.Key;
                if (value == null || value.Destroyed)
                {
                    RemoveProjectile(kvp.Key);
                }
            }
        }

        // allDangerousPositions operations below
        public static bool IsPositionDangerous(IntVec3 pos)
        {
            return allDangerousPositions.TryGetValue(pos, out _);
        }

        public static void AddDangerousPosition(IntVec3 pos)
        {
            bool found = allDangerousPositions.TryGetValue(pos, out int count);

            if (found)
            {
                allDangerousPositions[pos] = count++;
            }
            else
            {
                // first occurence, set 1
                allDangerousPositions.Add(pos, 1);
            }
        }

        public static void RemoveDangerousPosition(IntVec3 pos)
        {
            if (!allDangerousPositions.TryGetValue(pos, out int count))
                return;

            if (count <= 1)
            {
                allDangerousPositions.Remove(pos);
            }
            else
            {
                allDangerousPositions[pos] = count--;
            }

        }
    }

    // Custom cell grid constructor for dangerous positions
    public class DangerousGrid : IPathGridCustomizer
    {
        public readonly Map map;
        private NativeArray<ushort> array;
        private List<IntVec3> previousDangerList;

        private static readonly Dictionary<Map, DangerousGrid> dangerousGridCache = new Dictionary<Map, DangerousGrid>();

        public DangerousGrid(Map map, NativeArray<ushort> array = default)
        {
            this.map = map;
            this.array = array.IsCreated ? array : new NativeArray<ushort>(map.cellIndices.NumGridCells, Allocator.Persistent);
            this.previousDangerList = new List<IntVec3>();
        }

        public static DangerousGrid CombineGrids(IPathGridCustomizer originalGrid, DangerousGrid dangerousGrid)
        {
            var origArray = originalGrid.GetOffsetGrid();
            var dangerArray = dangerousGrid.GetOffsetGrid();

            int len = Math.Max(dangerArray.Length, origArray.Length);
            var combined = new NativeArray<ushort>(len, Allocator.Persistent);

            int origLen = origArray.Length;
            int dangerLen = dangerArray.Length;

            for (int i = 0; i < len; i++)
            {
                ushort danger_short = (i < dangerLen) ? dangerArray[i] : (ushort)0;
                ushort orig_short = (i < origLen) ? origArray[i] : (ushort)0;
                combined[i] = (orig_short >= danger_short) ? orig_short : danger_short;
            }

            dangerousGrid.array = combined;

            return dangerousGrid;
        }

        public NativeArray<ushort> GetOffsetGrid()
        {
            return array;
        }

        public void ResetGrid()
        {
            for (int i = 0; i < this.array.Length; i++)
            {
                this.array[i] = 0;
            }
        }

        public void ResetPreviousDangerList()
        {
            CellIndices cellindices = map.cellIndices;
            foreach (IntVec3 pos in this.previousDangerList)
            {
                this.array[cellindices.CellToIndex(pos)] = 0;
            }
        }

        public static DangerousGrid GetForMap(Map map)
        {
            bool found = dangerousGridCache.TryGetValue(map, out DangerousGrid grid);

            if (!found)
            {
                DangerousGrid newgrid = new DangerousGrid(map);
                dangerousGridCache.Add(map, newgrid);
            }

            return grid;
        }

        public void UpdateForPawn(Pawn pawn)
        {
            List<IntVec3> dangerVecs = DangerPositionTracker.ObtainDangerousPositionsFor(pawn);

            this.ResetPreviousDangerList();

            CellIndices cellindices = map.cellIndices;
            foreach (IntVec3 pos in dangerVecs)
            {
                this.array[cellindices.CellToIndex(pos)] = 9000;
            }

            this.previousDangerList = dangerVecs;
        }

        // Dispose manually later
        public void Dispose()
        {
            if (this.array.IsCreated)
                this.array.Dispose();
        }
    }

    // Check if there is any danger in our path
    [HarmonyPatch(typeof(PawnUtility))]
    [HarmonyPatch("KnownDangerAt")]
    public static class Patch_KnownDangerAt_Prefix
    {
        static bool Prefix(IntVec3 c, Map map, Pawn forPawn, ref bool __result)
        {
            // Lookup if position has any danger in a hashset
            bool isPosDangerous = DangerPositionTracker.IsPositionDangerous(c);
            
            // If found danger - check if it's visible for a pawn
            if (isPosDangerous)
            {
                return DangerPositionTracker.IsDangerVisibleFor(c, forPawn);
            }

            return false;
        }
    }

    // Include our dangerous cell grid with custom costs when path is requested
    [HarmonyPatch(typeof(PathRequest))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] {
    typeof(Map), typeof(IntVec3), typeof(LocalTargetInfo), typeof(IntVec3?), typeof(TraverseParms), typeof(PathFinderCostTuning), typeof(PathEndMode),
    typeof(Pawn), typeof(int), typeof(int), typeof(int), typeof(IPathGridCustomizer)
    })]
    public static class PathRequest_Constructor_Patch
    {
        static void Prefix(
            PathRequest __instance, Map map, IntVec3 start, LocalTargetInfo dest, IntVec3? exactDestination, TraverseParms traverseParms, PathFinderCostTuning tuning,
            PathEndMode peMode, Pawn pawn, int tickCreated, int tickStart, int tickDeadline, ref IPathGridCustomizer customizer)
        {
            DangerousGrid dangerGrid = DangerousGrid.GetForMap(map);
            dangerGrid.UpdateForPawn(pawn);

            if (customizer != null)
            {
                // Mod compatibility: Combine existing custom cells with our dangerous grid
                customizer = DangerousGrid.CombineGrids(customizer, dangerGrid);
                return;
            }

            customizer = dangerGrid;
        }
    }

    // Mark dangerous positions for pawns to avoid when explosive is launched
    [HarmonyPatch(typeof(Projectile))]
    [HarmonyPatch("Launch")]
    [HarmonyPatch(new Type[] {
    typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)
    })]
    public static class Patch_Projectile_Launch_Postfix
    {
        static void Postfix(Projectile __instance, Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            if (!(__instance is Projectile_Explosive projectile))
            {
                return;
            }

            float blastradius = projectile.def?.projectile?.explosionRadius ?? 0f;
            IntVec3 usedCell = usedTarget.Cell;

            // Create a HashSet of dangerous positions with usedCell included initially
            HashSet<IntVec3> dangerousPositions = new HashSet<IntVec3>{usedCell};
            foreach (IntVec3 pos in GenRadial.RadialCellsAround(usedCell, blastradius, true))
            {
                dangerousPositions.Add(pos);
            }
            DangerPositionTracker.AddProjectile(projectile, dangerousPositions);
        }
    }

    // Remove dangerous positions when explosive is destroyed
    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("Destroy")]
    static class Patch_Thing_Destroy_Postfix
    {
        static void Postfix(Thing __instance)
        {
            if (!(__instance is Projectile_Explosive explosive)) return;
            float blastradius = explosive.def?.projectile?.explosionRadius ?? 0f;
            IntVec3 usedCell = explosive.usedTarget.Cell;

            DangerPositionTracker.RemoveProjectile(explosive);
        }
    }
}
