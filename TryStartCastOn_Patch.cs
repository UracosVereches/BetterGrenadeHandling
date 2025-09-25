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

                Pawn target_pawn = castTarg.Pawn;

                if (caster_pawn.CurJob == null || caster_pawn.CurJob.playerForced || verb.IsMeleeAttack || target_pawn == null)
                {
                    GrenadiersOnWarmup.Remove(caster_pawn);
                    return true;
                }

                float blastradius = VerbCache.GetVerbBlastRadius(verb);

                if (blastradius == 0f)
                {
                    return true;
                }

                int grenadierID = caster_pawn.thingIDNumber;
                int targetID = target_pawn.thingIDNumber;

                // If blacklisted target somehow slips through (when target has moved in set position patch)
                if (AttackBlacklist.HasAttackerAndTarget(grenadierID, targetID))
                {
                    __result = false;
                    return false;
                }

                // Filter out targets initially when drafted for example
                List<Thing> things_in_blast = new List<Thing>();
                things_in_blast = BGHUtils.GetThingsInTargetBlast(caster_pawn, target_pawn, blastradius);
                if (things_in_blast.NullOrEmpty())
                {
                    GrenadiersOnWarmup.Add(caster_pawn);
                    return true;
                }

                foreach (Thing collateral in things_in_blast)
                {
                    if (!BGHUtils.CanIgnoreCollateral(caster_pawn, collateral, verb))
                    {
                        AttackBlacklist.AddTarget(grenadierID, targetID);
                        __result = false;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Better Grenade Handling] Exception in TryStartCastOn: {ex}");
            }
            // Let the original method take over
            return true;
        }
    }
}
