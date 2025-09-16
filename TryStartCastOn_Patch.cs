using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace BetterGrenadeHandling
{
    [HarmonyPatch(typeof(Verb), "TryStartCastOn",
    new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Verb_TryStartCastOn_Patch
    {
        static bool Prefix(Verb __instance, LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack, bool canHitNonTargetPawns, bool preventFriendlyFire, bool nonInterruptingSelfCast, ref bool __result)
        {
            try
            {
                Verb verb = __instance;
                if (verb == null)
                {
                    return true;
                }

                Pawn caster_pawn = verb.CasterPawn;

                if (caster_pawn == null)
                {
                    return true;
                }

                //Log.Message($"1 {caster_pawn.LabelShort}");

                Pawn target_pawn = castTarg.Pawn;

                if (caster_pawn.CurJob == null || caster_pawn.CurJob.playerForced || verb.IsMeleeAttack || target_pawn == null)
                {
                    GrenadiersOnWarmup.Remove(caster_pawn);
                    return true;
                }

                //Log.Message($"2 {caster_pawn.LabelShort}");

                float blastradius = BGHUtils.GetCurrentBlastRadius(caster_pawn);

                if (blastradius == 0f)
                {
                    return true;
                }

                //Log.Message($"3 {caster_pawn.LabelShort}");

                //If blacklisted target somehow slips through (when target has moved in set position patch)
                if (AttackerToTargetBlacklist.TryGetTarget(caster_pawn.thingIDNumber, out int badTarget) && badTarget == target_pawn.thingIDNumber)
                {
                    Log.Message($"TRYSTARTCASTON {caster_pawn.LabelShort} found in blacklist, target {target_pawn.LabelShort}");
                    __result = false;
                    return false;
                }

                //filter out targets initially when drafted for example
                List<Thing> things_in_blast = new List<Thing>();
                things_in_blast = BGHUtils.GetThingsInTargetBlast(caster_pawn, target_pawn, blastradius);
                if (things_in_blast.NullOrEmpty())
                {
                    GrenadiersOnWarmup.Add(caster_pawn);
                    //Log.Message("null things in blast???");
                    return true;
                }

                //Log.Message($"4 {caster_pawn.LabelShort}");

                foreach (Thing collateral in things_in_blast)
                {
                    if (!BGHUtils.CanIgnoreCollateral(caster_pawn, collateral, verb))
                    {
                        AttackerToTargetBlacklist.Set(caster_pawn.thingIDNumber, target_pawn.thingIDNumber, collateral.thingIDNumber);
                        //bad practice, never do that, infinite loops ahead
                        //pawn.stances.SetStance(new Stance_Mobile());
                        //Log.Message($"attacker {caster_pawn.LabelShort}");
                        __result = false;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Better Grenade Handling] Exception in TryStartCastOn: {ex}");
            }
            //let the original method take over
            return true;
        }
    }
}
