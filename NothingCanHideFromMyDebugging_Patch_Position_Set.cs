using BetterGrenadeHandling;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

[HarmonyPatch(typeof(Thing))]
[HarmonyPatch("Position", MethodType.Setter)]
public static class Thing_Position_Set_Patch
{

    //private static float counter1 = 0f;
    //private static float counter2 = 0f;
    static void Postfix(Thing __instance, IntVec3 value)
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

        //counter1 = counter1 + 1f;
        //if (counter1 % 100 == 0)
        //{
        //Log.Message($"Position Set Patch 1: {counter1}");
        //}

        AttackerToTargetBlacklist.TryRemoveByCollateral(__instance.thingIDNumber);

        Pawn[] grenadiers = GrenadiersOnWarmup.GetSnapshot();
        if (grenadiers.Length == 0) return;

        foreach (var grenadier in grenadiers)
        {
            if (__instance is null)
                return;

            //counter2 = counter2 + 1f;
            //if (counter2 % 1000 == 0)
            //{
                //Log.Message($"Position Set Patch 2 (loop): {counter2}");
            //}
            //var verbField = AccessTools.Field(typeof(Stance_Warmup), "verb");
            //Verb verb = verbField != null ? (Verb)verbField.GetValue(__instance) : null;
            //Pawn pawn = verb.CasterPawn;

            if (grenadier == null)
            {
                continue;
            }

            int grenadierID = grenadier.thingIDNumber;

            Debug.Log($"1 Grenadier: {grenadier.LabelShort}");

            //Stance pawn_stance = grenadier.stances.curStance;

            //if (!(pawn_stance is Stance_Warmup))
            //{
            //continue;
            //}

            Verb verb = grenadier.CurrentEffectiveVerb;

            if (verb == null)
            {
                continue;
            }

            Debug.Log($"2 Grenadier: {grenadier.LabelShort} - no verb");

            LocalTargetInfo target = grenadier.TargetCurrentlyAimingAt;

            if (target == null)
            {
                continue;
            }

            Debug.Log($"3 Grenadier: {grenadier.LabelShort} - no target");

            Thing target_thing = target.Thing;

            if (grenadier.CurJob == null || grenadier.CurJob.playerForced || grenadier.CurrentEffectiveVerb.IsMeleeAttack || target_thing == null)
            {
                AttackerToTargetBlacklist.TryRemove(grenadierID);
                continue;
            }

            Debug.Log($"4 Grenadier: {grenadier.LabelShort} - testing done");

            float blastradius = BGHUtils.GetCurrentBlastRadius(grenadier);
            if (blastradius == 0f)
            {
                AttackerToTargetBlacklist.TryRemove(grenadierID);
                continue;
            }

            Debug.Log($"5 Grenadier: {grenadier.LabelShort} - blast radius positive");

            if (blastradius == 1.1f)
            {
                blastradius = 2.9f; //doesn't work the same for molotovs, frag max radius instead
            }
            else
            {
                blastradius = blastradius + 1f; //just adding 1 does the trick
            }

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

            /*
            List<Thing> things_in_blast = new List<Thing>();
            things_in_blast = BGHUtils.GetThingsInTargetBlast(grenadier, target.Pawn, blastradius);
            if (things_in_blast.NullOrEmpty())
            {
                continue;
            }

            foreach (Thing collateral in things_in_blast)
            {
                if (!BGHUtils.CanIgnoreCollateral(grenadier, collateral, verb))
                {
                    grenadier.stances.SetStance(new Stance_Mobile());
                    break;
                }
            }
            */
        }

    }
}
