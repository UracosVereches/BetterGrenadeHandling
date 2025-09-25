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
            foreach (var waiting_grenadier in GrenadiersOnStandBy.GetList())
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
            foreach (var grenadier in GrenadiersOnWarmup.GetSnapshot())
            {
                if (grenadier == null)
                {
                    continue;
                }

                int grenadierID = grenadier.thingIDNumber;
                int instanceID = __instance.thingIDNumber;

                Verb verb = VerbCache.GetCurrentEffectiveVerb(grenadier);

                LocalTargetInfo target = grenadier.TargetCurrentlyAimingAt;

                if (target == null)
                {
                    continue;
                }

                Thing target_thing = target.Thing;

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

                if (blastradius == 1.1f) // 1.1 - molotov radius(1x1 cross)
                {
                    blastradius = 2.9f; // Doesn't work the same for molotovs, frag max radius instead
                }
                else
                {
                    blastradius = blastradius + 1f; // Just adding 1 does the trick
                }

                // If grenadier is aiming at a thing that just moved
                if (__instance == target_thing)
                {
                    List<Thing> things_in_blast = new List<Thing>();
                    things_in_blast = BGHUtils.GetThingsInTargetBlast(grenadier, target.Pawn, blastradius);
                    if (things_in_blast.NullOrEmpty())
                    {
                        AttackBlacklist.RemoveAttacker(grenadierID);
                        continue;
                    }

                    foreach (Thing collateral in things_in_blast)
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

                if (!BGHUtils.CanIgnoreCollateral(grenadier, __instance, verb))
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