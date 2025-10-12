using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace BetterGrenadeHandling
{
    // Just a global HashSet containing both warmup and standby grenadiers, for quick O(1) lookups
    public static class GlobalGrenadierCache
    {
        private static readonly HashSet<Pawn> hGlobalGrenadierSet = new HashSet<Pawn>();

        public static void Add(Pawn pawn)
        {
            hGlobalGrenadierSet.Add(pawn);
        }

        public static void Remove(Pawn pawn)
        {
            hGlobalGrenadierSet.Remove(pawn);
        }

        public static bool IsGrenadier(this Pawn pawn)
        {
            return hGlobalGrenadierSet.TryGetValue(pawn, out _);
        }
    }

    public static class WarmupGrenadiers
    {
        public static readonly List<Pawn> WarmupGrenadiersList = new List<Pawn>();

        public static void Add(Pawn pawn)
        {
            if (WarmupGrenadiersList.Contains(pawn))
            {
                return;
            }
            WarmupGrenadiersList.Add(pawn);
        }

        public static void Remove(Pawn pawn)
        {
            WarmupGrenadiersList.Remove(pawn);
        }

        public static ReadOnlyCollection<Pawn> GetList()
        {
            lock (WarmupGrenadiersList)
            {
                return WarmupGrenadiersList.AsReadOnly();
            }
        }

        public static List<Pawn> GetSnapshot()
        {
            lock (WarmupGrenadiersList)
            {
                return new List<Pawn>(WarmupGrenadiersList);
            }
        }
    }

    public static class StandByGrenadiers
    {
        private static readonly List<Pawn> StandByGrenadiersList = new List<Pawn>();

        public static void Add(Pawn pawn)
        {
            if (StandByGrenadiersList.Contains(pawn))
            {
                return;
            }
            StandByGrenadiersList.Add(pawn);
        }

        public static void Remove(Pawn pawn)
        {
            StandByGrenadiersList.Remove(pawn);
        }

        public static ReadOnlyCollection<Pawn> GetList()
        {
            lock (StandByGrenadiersList)
            {
                return StandByGrenadiersList.AsReadOnly();
            }
        }

        public static List<Pawn> GetSnapshot()
        {
            lock (StandByGrenadiersList)
            {
                return new List<Pawn>(StandByGrenadiersList);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_StanceTracker), "SetStance")]
    public static class Pawn_StanceTracker_SetStance_Patch
    {
        static void Postfix(Pawn_StanceTracker __instance, Stance newStance)
        {
            try
            {
                Pawn pawn = __instance.pawn;

                if (pawn == null)
                {
                    return;
                }

                // Ffs, there is a strange edge case right after when cooldown wears off - newStance != curStance
                Stance stance = pawn.stances.curStance;

                Verb verb = VerbCache.GetCurrentEffectiveVerb(pawn);
                float blastradius = VerbCache.GetVerbBlastRadius(verb);

                if (blastradius == 0f)
                {
                    // Weapon has no blast radius, clean up lists
                    GlobalGrenadierCache.Remove(pawn);
                    StandByGrenadiers.Remove(pawn);
                    WarmupGrenadiers.Remove(pawn);
                    return;
                }

                if (stance is Stance_Warmup)
                {
                    GlobalGrenadierCache.Add(pawn);
                    WarmupGrenadiers.Add(pawn);
                    StandByGrenadiers.Remove(pawn);

                }
                else if (stance is Stance_Mobile)
                {
                    GlobalGrenadierCache.Add(pawn);
                    StandByGrenadiers.Add(pawn);
                    WarmupGrenadiers.Remove(pawn);
                }
                else
                {
                    // Neither Stance_Warmup or Stance_Mobile
                    GlobalGrenadierCache.Remove(pawn);
                    StandByGrenadiers.Remove(pawn);
                    WarmupGrenadiers.Remove(pawn);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Better Grenade Handling] Exception in SetStance: {ex}");
            }
        }
    }

    // Remove from lists when pawn is destroyed
    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("Destroy")]
    static class GrenadierTracker_Patch_Thing_Destroy_Prefix
    {
        static void Prefix(Thing __instance)
        {
            if (!(__instance is Pawn pawn)) return;

            GlobalGrenadierCache.Remove(pawn);
            StandByGrenadiers.Remove(pawn);
            WarmupGrenadiers.Remove(pawn);
        }
    }

    // Remove from lists when pawn is not in combat anymore
    [HarmonyPatch(typeof(Pawn_MindState), "MindStateTickInterval")]
    public static class MindStateTickInterval_Patch
    {
        // Postfix method that runs after the original method
        public static void Postfix(Pawn_MindState __instance, int delta)
        {
            Pawn pawn = __instance.pawn;

            // Not in combat anymore - remove
            if (!__instance.anyCloseHostilesRecently && GlobalGrenadierCache.IsGrenadier(pawn))
            {
                GlobalGrenadierCache.Remove(pawn);
                StandByGrenadiers.Remove(pawn);
                WarmupGrenadiers.Remove(pawn);
            }
        }
    }
}

/*
    Just let this serve as a prime example on how you should not code. Performance wise - this is fucking garbage.
    Get at least 15 pawns with grenades and your game will crawl.
    15 pawns * 60 ticks = and you are already running this trash 900 TIMES PER FRAME.
    I'm not even talking about the additional loops we have to rummage through each tick.
    The second i realized how sub-par this shit is i just moved the whole thing into position set method,
    effectively eliminating the issue. Event-based programming is superior to tick-based, performance improved DRASTICALLY
    */

/*
[HarmonyPatch(typeof(Stance_Warmup), "StanceTick")]
static class Stance_Warmup_StanceTick_Patch
{
    static void Postfix(Stance_Warmup __instance)
    {
        try
        {
            var verbField = AccessTools.Field(typeof(Stance_Warmup), "verb");
            Verb verb = verbField != null ? (Verb)verbField.GetValue(__instance) : null;
            Pawn pawn = verb.CasterPawn;

            if (pawn == null || verb == null)
            {
                return;
            }

            LocalTargetInfo target = pawn.TargetCurrentlyAimingAt;

            //Log.Message($"[Better Grenade Handling] Stance tick called: target {pawn.TargetCurrentlyAimingAt.Label} of {pawn.LabelShort}");

            if (pawn.CurJob.playerForced || pawn.CurrentEffectiveVerb.IsMeleeAttack || target == null)
            {
                return;
            }

            float blastradius = BGHUtils.GetCurrentBlastRadius(pawn);
            if (blastradius == 0f)
            {
                return;
            }

            List<Thing> things_in_blast = new List<Thing>();
            things_in_blast = BGHUtils.GetThingsInTargetBlast(pawn, target.Pawn, blastradius);
            if (things_in_blast.NullOrEmpty())
            {
                return;
            }

            foreach (Thing collateral in things_in_blast)
            {
                if (!BGHUtils.CanIgnoreCollateral(pawn, collateral, verb))
                {
                    pawn.stances.SetStance(new Stance_Mobile());
                    return;
                }
            }

            return;
        }
        catch (Exception ex)
        {
            Log.Error($"[Better Grenade Handling] Exception in Stance_Warmup: {ex}");
        }
    }
}
*/