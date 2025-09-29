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
        //This dictionary contains: ATTACKER -> [ANOTHER DICTIONARY OF RESTRICTED TARGETS]
        private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, byte>> AttackerRestrictedTargets = new ConcurrentDictionary<int, ConcurrentDictionary<int, byte>>();

        public static void AddTarget(int attacker, int target)
        {
            // Setup HashSet for attacker if there wasn't any
            if (!HasAttacker(attacker))
            {
                AttackerRestrictedTargets[attacker] = new ConcurrentDictionary<int, byte>();
            }

            AttackerRestrictedTargets[attacker].TryAdd(target, 0);
        }

        public static bool RemoveTarget(int attacker, int target)
        {
            if (!HasAttacker(attacker))
            {
                return false;
            }

            return AttackerRestrictedTargets[attacker].TryRemove(target, out _);
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

            return AttackerRestrictedTargets[attacker].ContainsKey(target);
        }

        public static bool HasAttacker(int attacker)
        {
            return AttackerRestrictedTargets.ContainsKey(attacker);
        }

        public static ConcurrentDictionary<int, byte> GetDictionary(int attacker)
        {
            if (!HasAttacker(attacker))
            {
                // Empty hashset if nothing was found
                return new ConcurrentDictionary<int, byte>();
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
                    ConcurrentDictionary<int, byte> restrictedtargets = AttackBlacklist.GetDictionary(attacker_id);
                    __result.RemoveAll(t => restrictedtargets.ContainsKey(t.Thing.thingIDNumber));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BGH] Exception in Stance_Warmup: {ex}");
            }
        }
    }
}