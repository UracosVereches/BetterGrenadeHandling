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
            // Setup dictionary for attacker if there wasn't any
            var dict = AttackerRestrictedTargets.GetOrAdd(attacker, _ => new ConcurrentDictionary<int, byte>());
            dict.TryAdd(target, 0);
        }

        public static bool RemoveTarget(int attacker, int target)
        {
            if (AttackerRestrictedTargets.TryGetValue(attacker, out var dict))
            {
                return dict.TryRemove(target, out _);
            }
            return false;
        }

        public static bool RemoveAttacker(int attacker)
        {
            return AttackerRestrictedTargets.TryRemove(attacker, out _);
        }

        public static bool HasAttackerAndTarget(int attacker, int target)
        {
            return AttackerRestrictedTargets.TryGetValue(attacker, out var dict) && dict.TryGetValue(target, out _);
        }

        public static bool HasAttacker(int attacker)
        {
            return AttackerRestrictedTargets.TryGetValue(attacker, out _);
        }

        public static ConcurrentDictionary<int, byte> GetDictionary(int attacker)
        {
            return AttackerRestrictedTargets.GetOrAdd(attacker, _ => new ConcurrentDictionary<int, byte>());
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