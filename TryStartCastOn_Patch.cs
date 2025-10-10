using HarmonyLib;
using RimWorld;
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
                if (target_pawn == null)
                {
                    return true;
                }

                int grenadierID = caster_pawn.thingIDNumber;

                // If forced by player/Melee attack
                if (caster_pawn.CurJob == null || caster_pawn.CurJob.playerForced || verb.IsMeleeAttack)
                {
                    AttackBlacklist.RemoveAttacker(grenadierID);
                    return true;
                }

                int targetID = target_pawn.thingIDNumber;

                float blastradius = VerbCache.GetVerbBlastRadius(verb);
                if (blastradius == 0f)
                {
                    AttackBlacklist.RemoveAttacker(grenadierID);
                    return true;
                }

                // If target is blacklisted
                if (AttackBlacklist.HasAttackerAndTarget(grenadierID, targetID))
                {
                    __result = false;
                    return false;
                }

                // Filter out targets initially when drafted for example
                float expanded_blastradius = BGHUtils.ExpandBlastRadius(blastradius);

                List<Pawn> PawnsInBlastList = new List<Pawn>();
                PawnsInBlastList = target_pawn.GetPawnsInRadius(expanded_blastradius);

                if (PawnsInBlastList.NullOrEmpty())
                {
                    //WarmupGrenadiers.Add(caster_pawn);
                    return true;
                }

                foreach (Pawn collateral in PawnsInBlastList)
                {
                    if (!caster_pawn.CanIgnoreCollateral(collateral, verb.GetDamageDef(), verb.IsIncendiary_Ranged()))
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
