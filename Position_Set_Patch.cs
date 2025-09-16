using BetterGrenadeHandling;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

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

            AttackerToTargetBlacklist.TryRemoveByCollateral(__instance.thingIDNumber);
            AttackerToTargetBlacklist.TryRemoveByTarget(__instance.thingIDNumber);

            Pawn[] grenadiers = GrenadiersOnWarmup.GetSnapshot();
            if (grenadiers.Length == 0) return;

            foreach (var grenadier in grenadiers)
            {
                if (__instance is null)
                    return;

                if (grenadier == null)
                {
                    continue;
                }

                int grenadierID = grenadier.thingIDNumber;
                //Log.Message($"1 Grenadier: {grenadier.LabelShort}");

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
                    AttackerToTargetBlacklist.TryRemove(grenadierID);
                    continue;
                }
                //Log.Message($"4 Grenadier: {grenadier.LabelShort} - testing done");

                float blastradius = BGHUtils.GetCurrentBlastRadius(grenadier);
                if (blastradius == 0f)
                {
                    AttackerToTargetBlacklist.TryRemove(grenadierID);
                    continue;
                }
                //Log.Message($"5 Grenadier: {grenadier.LabelShort} - blast radius positive");

                if (blastradius == 1.1f)
                {
                    blastradius = 2.9f; //doesn't work the same for molotovs, frag max radius instead
                }
                else
                {
                    blastradius = blastradius + 1f; //just adding 1 does the trick
                }

                //If grenadier's target is moved thing
                if (__instance == target_thing)
                {
                    List<Thing> things_in_blast = new List<Thing>();
                    things_in_blast = BGHUtils.GetThingsInTargetBlast(grenadier, target.Pawn, blastradius);
                    if (things_in_blast.NullOrEmpty())
                    {
                        //Log.Message($"null things in target's blast radius, removing {grenadier.LabelShort}");
                        AttackerToTargetBlacklist.TryRemove(grenadierID);
                        continue;
                    }

                    foreach (Thing collateral in things_in_blast)
                    {
                        if (!BGHUtils.CanIgnoreCollateral(grenadier, collateral, verb))
                        {
                            //Log.Message($"spotted some collaterals in target's radius, adding {grenadier.LabelShort}");
                            AttackerToTargetBlacklist.Set(grenadierID, target_thing.thingIDNumber, __instance.thingIDNumber);
                            grenadier.stances.SetStance(new Stance_Mobile());
                            break;
                        }
                    }
                    continue;
                }

                //Everything below - if instance is ally

                IntVec3 moved_thing_pos = value;

                //Log.Message($"GRENADIER: {grenadier.LabelShort}");

                if (moved_thing_pos.DistanceToSquared(target_thing.Position) >= (blastradius * blastradius))
                {
                    continue;
                }

                if (!BGHUtils.CanIgnoreCollateral(grenadier, __instance, verb))
                {
                    //Log.Message($"SETTING UP BLACKLIST: {target_thing.LabelShort} and collateral {__instance.LabelShort}");
                    AttackerToTargetBlacklist.Set(grenadierID, target_thing.thingIDNumber, __instance.thingIDNumber);
                    grenadier.stances.SetStance(new Stance_Mobile()); // cancel warmup
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[Better Grenade Handling] Exception in TryStartCastOn: {ex}");
        }

    }
}
