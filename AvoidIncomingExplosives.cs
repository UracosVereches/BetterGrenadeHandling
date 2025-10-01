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

    [HarmonyPatch(typeof(PawnUtility))]
    [HarmonyPatch("KnownDangerAt")]
    public static class Patch_KnownDangerAt_Prefix
    {
        // Prefix runs before the original method
        // Return 'true' to run original, 'false' to skip it
        static bool Prefix(IntVec3 c, Map map, Pawn forPawn, ref bool __result)
        {
            __result = DangerPositionTracker.IsPositionDangerousFor(c, forPawn);
            if (__result)
            {
                Log.Message($"big cockston at {c} for {forPawn.LabelShort}");
                return false;
            }

            return true;
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
