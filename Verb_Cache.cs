using HarmonyLib;
using System;
using System.Collections.Concurrent;
using Verse;

namespace BetterGrenadeHandling
{
    public static class VerbCache
    {
        private static readonly ConcurrentDictionary<Pawn, Verb> PawnVerb = new ConcurrentDictionary<Pawn, Verb>();
        private static readonly ConcurrentDictionary<Verb, float> VerbRadius = new ConcurrentDictionary<Verb, float>();

        public static Verb GetCurrentEffectiveVerb(Pawn pawn)
        {
            bool found_verb = PawnVerb.TryGetValue(pawn, out Verb verb);

            if (!found_verb || verb == null)
            {
                // Verb not found or null in dictionary, create new entry
                verb = pawn.CurrentEffectiveVerb;
                PawnVerb[pawn] = verb;
                return verb;
            }

            return verb;
        }

        // Works both for PawnVerb and VerbRadius
        public static void Notify_VerbChanged(Pawn pawn)
        {
            // Create/replace entry when new weapon is equipped
            Verb verb = pawn.CurrentEffectiveVerb;
            PawnVerb[pawn] = verb;

            ThingDef projectile = verb.GetProjectile();
            float radius = 0f;
            if (projectile != null)
            {
                radius = projectile.projectile.explosionRadius;
            }
            VerbRadius[verb] = radius;
        }

        // Directly ripped out from Verse.VerbUtility.UsesExplosiveProjectiles(this Verb verb)
        public static float GetVerbBlastRadius(Verb verb)
        {
            bool found_radius = VerbRadius.TryGetValue(verb, out float radius);

            if (!found_radius)
            {
                //Blast radius not found in dictionary, create new entry
                ThingDef projectile = verb.GetProjectile();
                radius = 0f;
                if (projectile != null)
                {
                    radius = projectile.projectile.explosionRadius;
                }
                VerbRadius[verb] = radius;
                return radius;
            }

            return radius;
        }

        // TODO: remove stale pawn entries(dead, passed to world)
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
}
