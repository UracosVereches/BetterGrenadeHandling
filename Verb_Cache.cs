using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace BetterGrenadeHandling
{
    // Very easy to implement cache utility to efficiently look up verb parameters.
    // Something the base game should definitely take advantage of
    public static class VerbCache
    {
        private static readonly ConcurrentDictionary<Thing, Verb> dThing_Verb = new ConcurrentDictionary<Thing, Verb>();
        private static readonly ConcurrentDictionary<Verb, float> dVerb_fBlastRadius = new ConcurrentDictionary<Verb, float>();
        private static readonly ConcurrentDictionary<Verb, float> dVerb_fRange = new ConcurrentDictionary<Verb, float>();
        private static readonly ConcurrentDictionary<Verb, bool> dVerb_bEMP = new ConcurrentDictionary<Verb, bool>();

        // For cleanup
        // Always remember to put new dictionaries in here
        public static void RemoveThingFromCache(Thing thing)
        {
            bool foundVerb = dThing_Verb.TryRemove(thing, out Verb verb);

            if (!foundVerb)
            {
                return;
            }

            dVerb_fBlastRadius.TryRemove(verb, out _);
            dVerb_fRange.TryRemove(verb, out _);
            dVerb_bEMP.TryRemove(verb, out _);
        }

        public static bool TryGetCurrentEffectiveVerb(Thing thing, out Verb verb)
        {
            bool verbFound = dThing_Verb.TryGetValue(thing, out verb);

            if (!verbFound || verb == null)
            {
                // Verb not found or null in dictionary, create new entry
                verb = (thing as IAttackTargetSearcher)?.CurrentEffectiveVerb; // IAttackTargetSearcher - turret support
                if (verb == null)
                {
                    return false;
                }

                dThing_Verb[thing] = verb;
                return verbFound;
            }

            return verbFound;
        }

        // Directly ripped out from Verse.VerbUtility.UsesExplosiveProjectiles(this Verb verb)
        public static float GetVerbBlastRadius(Verb verb)
        {
            bool found_radius = dVerb_fBlastRadius.TryGetValue(verb, out float radius);

            if (!found_radius)
            {
                //Verb blast radius not found in dictionary, create new entry
                ThingDef projectile = verb.GetProjectile();
                radius = 0f;
                if (projectile != null)
                {
                    radius = projectile.projectile.explosionRadius;
                }
                dVerb_fBlastRadius[verb] = radius;

                return radius;
            }

            return radius;
        }

        public static float GetVerbRange(Verb verb)
        {
            bool found_range = dVerb_fRange.TryGetValue(verb, out float range);

            if (!found_range)
            {
                //Verb range not found in dictionary, create new entry
                range = verb.EffectiveRange;
                dVerb_fRange[verb] = range;

                return range;
            }

            return range;
        }

        public static bool IsVerbEMP(Verb verb)
        {
            bool found_EMP = dVerb_bEMP.TryGetValue(verb, out bool isEMP);

            if (!found_EMP)
            {
                //Verb range not found in dictionary, create new entry
                isEMP = verb.IsEMP();
                dVerb_bEMP[verb] = isEMP;

                return isEMP;
            }

            return isEMP;
        }

        // Called for every dictionary
        // Remove stale entries when new weapon is equipped
        public static void Notify_VerbChanged(Pawn pawn)
        {
            RemoveThingFromCache(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker))]
    [HarmonyPatch(nameof(Pawn_EquipmentTracker.Notify_EquipmentAdded))]
    public static class Patch_NotifyEquipmentAdded
    {
        static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
        {
            try
            {
                VerbCache.Notify_VerbChanged(__instance.pawn);
            }
            catch (Exception ex)
            {
                Log.Error($"[Better Grenade Handling] Exception in Notify_EquipmentAdded postfix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "Kill",
    new Type[] { typeof(DamageInfo?), typeof(Hediff) })]
    public static class Pawn_Kill_Patch
    {
        static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            // Pawn is still alive
            if (!__instance.Dead)
            {
                return;
            }

            VerbCache.RemoveThingFromCache(__instance);
        }
    }

    [HarmonyPatch(typeof(WorldPawns), "PassToWorld",
    new Type[] { typeof(Pawn), typeof(PawnDiscardDecideMode) })]
    public static class WorldPawns_PassToWorld_Patch
    {
        static void Postfix(Pawn pawn, PawnDiscardDecideMode discardMode = PawnDiscardDecideMode.Decide)
        {
            VerbCache.RemoveThingFromCache(pawn);
        }
    }

    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("Destroy")]
    public static class VerbCache_Patch_Thing_Destroy_Prefix
    {
        static void Prefix(Thing __instance)
        {
            //if (!(__instance is Pawn pawn)) return;

            VerbCache.RemoveThingFromCache(__instance);
        }
    } 
}