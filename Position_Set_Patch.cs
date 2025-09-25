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

            //ignore wildlife
            if ((__instance as Pawn).IsAnimal && __instance.Faction == null)
                return;

            //AttackerToTargetBlacklist.TryRemoveByCollateral(__instance.thingIDNumber);
            //AttackerToTargetBlacklist.TryRemoveByTarget(__instance.thingIDNumber);

            //Cleanup stale entries in AttackBlacklist on grenadiers that are waiting for an attack
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

            foreach (var grenadier in GrenadiersOnWarmup.GetSnapshot())
            {
                if (grenadier == null)
                {
                    continue;
                }

                int grenadierID = grenadier.thingIDNumber;
                int instanceID = __instance.thingIDNumber;
                //Log.Message($"1 Grenadier: {grenadier.LabelShort}");

                //Remove target since it just moved and this is a stale entry
                AttackBlacklist.RemoveTarget(grenadierID, instanceID);

                Verb verb = grenadier.CurrentEffectiveVerb;

                if (verb == null)
                {
                    continue;
                }
                //Log.Message($"2 Grenadier: {grenadier.LabelShort} - no verb");

                LocalTargetInfo target = grenadier.TargetCurrentlyAimingAt;

                if (target == null)
                {
                    continue;
                }
                //Log.Message($"3 Grenadier: {grenadier.LabelShort} - no target");

                Thing target_thing = target.Thing;

                if (grenadier.CurJob == null || grenadier.CurJob.playerForced || grenadier.CurrentEffectiveVerb.IsMeleeAttack || target_thing == null)
                {
                    //AttackerToTargetBlacklist.TryRemove(grenadierID);
                    AttackBlacklist.RemoveAttacker(grenadierID);
                    //AttackerToTargetBlacklist.TryRemoveByTarget(instanceID);
                    continue;
                }
                //Log.Message($"4 Grenadier: {grenadier.LabelShort} - testing done");

                float blastradius = BGHUtils.GetCurrentBlastRadius(grenadier);
                if (blastradius == 0f)
                {
                    //AttackerToTargetBlacklist.TryRemove(grenadierID);
                    AttackBlacklist.RemoveAttacker(grenadierID);
                    //AttackerToTargetBlacklist.TryRemoveByTarget(instanceID);
                    continue;
                }
                //Log.Message($"5 Grenadier: {grenadier.LabelShort} - blast radius positive");

                if (blastradius == 1.1f) //1.1 - molotov radius(1x1 cross)
                {
                    blastradius = 2.9f; //doesn't work the same for molotovs, frag max radius instead
                }
                else
                {
                    blastradius = blastradius + 1f; //just adding 1 does the trick
                }

                //Log.Message($"Position Set position: {value}, Real Position: {__instance.Position}, Target cell: {target.Cell}, Target center vector: {target.CenterVector3}");

                //If grenadier is aiming at thing that just moved
                if (__instance == target_thing)
                {
                    List<Thing> things_in_blast = new List<Thing>();
                    things_in_blast = BGHUtils.GetThingsInTargetBlast(grenadier, target.Pawn, blastradius);
                    if (things_in_blast.NullOrEmpty())
                    {
                        //Log.Message($"null things in target's blast radius, removing {grenadier.LabelShort}");
                        //AttackerToTargetBlacklist.TryRemove(grenadierID);
                        AttackBlacklist.RemoveAttacker(grenadierID);
                        //AttackerToTargetBlacklist.TryRemoveByTarget(instanceID);
                        continue;
                    }

                    foreach (Thing collateral in things_in_blast)
                    {
                        if (!BGHUtils.CanIgnoreCollateral(grenadier, collateral, verb))
                        {
                            //Log.Message($"spotted some collaterals in target's radius, adding {grenadier.LabelShort}");
                            //AttackerToTargetBlacklist.Set(grenadierID, target_thing.thingIDNumber, __instance.thingIDNumber);
                            AttackBlacklist.AddTarget(grenadierID, instanceID);
                            grenadier.stances.SetStance(new Stance_Mobile());
                            break;
                        }
                    }
                    continue;
                }

                //Everything below - if instance(moved thing) is ally

                IntVec3 moved_thing_pos = value;

                //Log.Message($"GRENADIER: {grenadier.LabelShort}");

                if (moved_thing_pos.DistanceToSquared(target_thing.Position) >= (blastradius * blastradius))
                {
                    continue;
                }

                if (!BGHUtils.CanIgnoreCollateral(grenadier, __instance, verb))
                {
                    //Log.Message($"SETTING UP BLACKLIST: {target_thing.LabelShort} and collateral {__instance.LabelShort}");
                    //AttackerToTargetBlacklist.Set(grenadierID, target_thing.thingIDNumber, __instance.thingIDNumber);
                    AttackBlacklist.AddTarget(grenadierID, instanceID);
                    grenadier.stances.SetStance(new Stance_Mobile()); // cancel warmup
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