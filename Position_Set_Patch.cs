using BetterGrenadeHandling;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BetterGrenadeHandling
{
[HarmonyPatch(typeof(Thing))]
[HarmonyPatch("Position", MethodType.Setter)]
public static class Thing_Position_Set_Patch
{
    static void Postfix(Thing __instance, IntVec3 value)
    {
        try
        {
            if (__instance is null)
                return;

            if (!(__instance is Pawn))
                return;

            if (__instance.Map is null)
                return;

            if (__instance.Map.mapPawns is null)
                return;

            if (__instance.Map.mapPawns.AllPawnsSpawned is null)
                return;

            // Ignore wildlife
            if ((__instance as Pawn).IsAnimal && __instance.Faction == null)
                return;

            // Iterate over idle grenadiers (Drafted || In combat && No target)
            // Cleanup stale entries in AttackBlacklist on grenadiers that are waiting for an attack
            foreach (var waiting_grenadier in StandByGrenadiers.GetList())
            {
                if (waiting_grenadier == null)
                {
                    continue;
                }
                int waiting_grenadierID = waiting_grenadier.thingIDNumber;
                int moved_thingID = __instance.thingIDNumber;

                AttackBlacklist.RemoveTarget(waiting_grenadierID, moved_thingID);
            }

            // Iterate over every grenadier who is aiming right now
            foreach (var grenadier in WarmupGrenadiers.GetSnapshot())
            {
                if (grenadier == null)
                {
                    continue;
                }

                Pawn moved_pawn = __instance as Pawn;

                int grenadierID = grenadier.thingIDNumber;
                int instanceID = moved_pawn.thingIDNumber;

                Verb verb = VerbCache.GetCurrentEffectiveVerb(grenadier);

                LocalTargetInfo target = grenadier.TargetCurrentlyAimingAt;

                if (target == null)
                {
                    continue;
                }

                Thing target_thing = target.Thing;

                // If melee or attack was forced by player
                if (grenadier.CurJob == null || grenadier.CurJob.playerForced || verb.IsMeleeAttack || target_thing == null)
                {
                    AttackBlacklist.RemoveAttacker(grenadierID);
                    continue;
                }

                float blastradius = VerbCache.GetVerbBlastRadius(verb);
                if (blastradius == 0f)
                {
                    AttackBlacklist.RemoveAttacker(grenadierID);
                    continue;
                }

                float expanded_blastradius = BGHUtils.ExpandBlastRadius(blastradius);

                // If grenadier is aiming at a thing that just moved
                if (moved_pawn == target_thing)
                {
                    List<Pawn> PawnsInBlastList = new List<Pawn>();
                    PawnsInBlastList = BGHUtils.GetPawnsInRadius(target.Pawn, expanded_blastradius);
                    if (PawnsInBlastList.NullOrEmpty())
                    {
                        AttackBlacklist.RemoveAttacker(grenadierID);
                        continue;
                    }

                    foreach (Pawn collateral in PawnsInBlastList)
                    {
                        if (!BGHUtils.CanIgnoreCollateral(grenadier, collateral, verb))
                        {
                            AttackBlacklist.AddTarget(grenadierID, instanceID);
                            grenadier.stances.SetStance(new Stance_Mobile()); // Cancel warmup
                            break;
                        }
                    }
                    continue;
                }

                // If moved thing is ally
                IntVec3 moved_thing_pos = value;
                if (moved_thing_pos.DistanceToSquared(target_thing.Position) >= (blastradius * blastradius))
                {
                    continue;
                }

                if (!BGHUtils.CanIgnoreCollateral(grenadier, moved_pawn, verb))
                {
                    AttackBlacklist.AddTarget(grenadierID, instanceID);
                    grenadier.stances.SetStance(new Stance_Mobile()); // Cancel warmup
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[Better Grenade Handling] Exception in Position Set: {ex}");
        }

    }
}
}