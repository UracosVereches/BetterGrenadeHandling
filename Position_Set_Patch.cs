using BetterGrenadeHandling;
using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using Verse;

namespace BetterGrenadeHandling
{
    // For debugging tools
    #if DEBUG
    public static class Debug_ThingID_PositionCache
    {
        private static readonly ConcurrentDictionary<int, IntVec3> dict = new ConcurrentDictionary<int, IntVec3>();

        public static void SavePos(int thingID, IntVec3 pos)
        {
            dict[thingID] = pos;
        }

        public static IntVec3 GetPos(int thingID)
        {
            dict.TryGetValue(thingID, out IntVec3 pos);
            return pos;
        }
    }
    #endif

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

                #if DEBUG
                Debug_ThingID_PositionCache.SavePos(__instance.thingIDNumber, value);
                #endif

                Pawn moved_pawn = __instance as Pawn;
                int moved_pawnID = moved_pawn.thingIDNumber;
                IntVec3 moved_pawn_pos = value;

                // Iterate over idle grenadiers (Drafted || In combat && No target)
                // Cleanup stale entries in AttackBlacklist on grenadiers that are waiting for an attack
                foreach (var waiting_grenadier in StandByGrenadiers.GetList())
                {
                    if (waiting_grenadier == null)
                    {
                        continue;
                    }
                    int waiting_grenadierID = waiting_grenadier.thingIDNumber;

                    // Just remove grenadier from blacklist completely since we don't know the target
                    // Because waiting grenadiers don't have any yet
                    AttackBlacklist.RemoveAttacker(waiting_grenadierID);
                }

                // Iterate over every grenadier who is aiming right now
                foreach (var grenadier in WarmupGrenadiers.GetSnapshot())
                {
                    if (grenadier == null)
                    {
                        continue;
                    }

                    int grenadierID = grenadier.thingIDNumber;
                    Verb verb = VerbCache.GetCurrentEffectiveVerb(grenadier);
                    LocalTargetInfo target = grenadier.TargetCurrentlyAimingAt;

                    if (target == null)
                    {
                        continue;
                    }

                    Thing target_thing = target.Thing;

                    if (target_thing == null)
                    {
                        continue;
                    }

                    float verb_range = VerbCache.GetVerbRange(verb);

                    // If moved thing is outside of grenadier's weapon range - ignore
                    if (moved_pawn_pos.DistanceToSquared(grenadier.Position) >= (verb_range * verb_range))
                    {
                        continue;
                    }

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

                    // Assume that moved thing is ally. Compare ally's distance to grenadier's current target
                    if (moved_pawn_pos.DistanceToSquared(target_thing.Position) >= (expanded_blastradius * expanded_blastradius))
                    {
                        continue;
                    }

                    if (!BGHUtils.CanIgnoreCollateral(grenadier, moved_pawn, verb.GetDamageDef()))
                    {
                        AttackBlacklist.AddTarget(grenadierID, target_thing.thingIDNumber);
                        grenadier.stances.SetStance(new Stance_Mobile()); // Cancel warmup
                        continue;
                    }

                    // Assume that grenadier's current target is the pawn that just moved. Check for allies around the target.
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
                            if (!BGHUtils.CanIgnoreCollateral(grenadier, collateral, verb.GetDamageDef()))
                            {
                                AttackBlacklist.AddTarget(grenadierID, moved_pawnID);
                                grenadier.stances.SetStance(new Stance_Mobile()); // Cancel warmup
                                break;
                            }
                        }
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