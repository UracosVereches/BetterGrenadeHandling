using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace BetterGrenadeHandling
{
    public static class AttackBlacklist
    {
        private static readonly Dictionary<int, HashSet<int>> AttackerRestrictedTargets = new Dictionary<int, HashSet<int>>();

        public static void AddTarget(int attacker, int target)
        {
            // Setup HashSet for attacker if there wasn't any
            if (!HasAttacker(attacker))
            {
                AttackerRestrictedTargets[attacker] = new HashSet<int>();
            }

            AttackerRestrictedTargets[attacker].Add(target);
        }

        public static void RemoveTarget(int attacker, int target)
        {
            if (!HasAttacker(attacker))
            {
                return;
            }

            AttackerRestrictedTargets[attacker].Remove(target);
        }

        public static void RemoveAttacker(int attacker)
        {
            if (!HasAttacker(attacker))
            {
                return;
            }

            AttackerRestrictedTargets.Remove(attacker);
        }

        public static bool HasAttackerAndTarget(int attacker, int target)
        {
            if (!HasAttacker(attacker))
            {
                return false;
            }

            return AttackerRestrictedTargets[attacker].Contains(target);
        }

        public static bool HasAttacker(int attacker)
        {
            return AttackerRestrictedTargets.ContainsKey(attacker);
        }

        public static HashSet<int> GetHashSet(int attacker)
        {
            if (!HasAttacker(attacker))
            {
                // Empty hashset if nothing was found
                return new HashSet<int>();
            }

            return AttackerRestrictedTargets[attacker];
        }
    }

    //honestly, it's just shit
    //transfer it to score system, bad targets get the least score
    //in TryStartCastOn check if current target is blacklisted
    //much better approach tbh
    [HarmonyPatch(typeof(AttackTargetsCache), "GetPotentialTargetsFor")]
    static class AttackTargetsCache_GetPotentialTargetsFor_Patch
    {
        static void Postfix(IAttackTargetSearcher th, ref List<IAttackTarget> __result)
        {
            try
            {
                Thing attacker = th.Thing;
                int attacker_id = attacker.thingIDNumber;

                if (AttackBlacklist.HasAttacker(attacker_id))
                {
                    HashSet<int> restrictedtargets = AttackBlacklist.GetHashSet(attacker_id);
                    __result.RemoveAll(t => restrictedtargets.Contains(t.Thing.thingIDNumber));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BGH] Exception in Stance_Warmup: {ex}");
            }
            
        }
    }
}