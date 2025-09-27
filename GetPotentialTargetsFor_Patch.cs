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
        private static readonly ConcurrentDictionary<int, HashSet<int>> AttackerRestrictedTargets = new ConcurrentDictionary<int, HashSet<int>>();

        public static void AddTarget(int attacker, int target)
        {
            // Setup HashSet for attacker if there wasn't any
            if (!HasAttacker(attacker))
            {
                AttackerRestrictedTargets[attacker] = new HashSet<int>();
            }

            AttackerRestrictedTargets[attacker].Add(target);
        }

        public static bool RemoveTarget(int attacker, int target)
        {
            if (!HasAttacker(attacker))
            {
                return false;
            }

            return AttackerRestrictedTargets[attacker].Remove(target);
        }

        public static bool RemoveAttacker(int attacker)
        {
            if (!HasAttacker(attacker))
            {
                return false;
            }

            return AttackerRestrictedTargets.TryRemove(attacker, out _);
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