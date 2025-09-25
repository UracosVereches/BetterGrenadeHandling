using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using HarmonyLib;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace BetterGrenadeHandling
{
    public static class GrenadiersOnWarmup
    {
        public static readonly List<Pawn> GrenadiersOnWarmupList = new List<Pawn>();

        public static void Add(Pawn pawn)
        {
            if (GrenadiersOnWarmupList.Contains(pawn))
            {
                return;
            }
            GrenadiersOnWarmupList.Add(pawn);
        }

        public static void Remove(Pawn pawn)
        {
            //Log.Message($"Removing {pawn.LabelShort} from warmup list");
            GrenadiersOnWarmupList.Remove(pawn);
        }

        public static ReadOnlyCollection<Pawn> GetList()
        {
            lock (GrenadiersOnWarmupList)
            {
                return GrenadiersOnWarmupList.AsReadOnly();
            }
        }

        public static List<Pawn> GetSnapshot()
        {
            lock (GrenadiersOnWarmupList)
            {
                return new List<Pawn>(GrenadiersOnWarmupList);
            }
        }

        //TODO: cleanup every 20 add calls
        /*
        public static void Cleanup()
        {
            lock (sync)
            {
                var toremove = new List<Pawn>();
                foreach (var entry in GrenadiersOnWarmupList)
                {
                    if (e == null || e.pawn == null || e.pawn.Dead || e.pawn.Map == null) toRemove.Add(kv.Key)
                }
            }
        }
        */
    }

    public static class GrenadiersOnStandBy
    {
        private static readonly List<Pawn> GrenadiersOnStandByList = new List<Pawn>();

        public static void Add(Pawn pawn)
        {
            if (GrenadiersOnStandByList.Contains(pawn))
            {
                return;
            }
            GrenadiersOnStandByList.Add(pawn);
        }

        public static void Remove(Pawn pawn)
        {
            GrenadiersOnStandByList.Remove(pawn);
        }

        public static ReadOnlyCollection<Pawn> GetList()
        {
            lock (GrenadiersOnStandByList)
            {
                return GrenadiersOnStandByList.AsReadOnly();
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

                //GrenadiersOnStandBy - every pawn in stance mobile with explosives
                if (newStance is Stance_Mobile)
                {
                    Verb verb = VerbCache.GetCurrentEffectiveVerb(pawn);
                    float blastradius = VerbCache.GetVerbBlastRadius(verb);
                    if (blastradius == 0f)
                    {
                        return;
                    }

                    GrenadiersOnStandBy.Add(pawn);
                }
                else
                {
                    GrenadiersOnStandBy.Remove(pawn);
                }

                if (!(newStance is Stance_Warmup))
                {
                    GrenadiersOnWarmup.Remove(pawn);

                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Better Grenade Handling] Exception in SetStance: {ex}");
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