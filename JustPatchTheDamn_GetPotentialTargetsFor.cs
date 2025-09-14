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
    public static class AttackerToTargetBlacklist
    {
        public static readonly Dictionary<int, int> attacker_and_target = new Dictionary<int, int>();
        public static readonly Dictionary<int, int> collateral_and_attacker = new Dictionary<int, int>();

        //attacker_and_target[attacker][target]
        //collateral_and_attacker[collateral][attacker]

        private static int cleanup_counter = 0;
        public static void Set(int attacker, int target, int collateral)
        {
            cleanup_counter++;
            if (cleanup_counter >= 1000)
            {
                //Cleanup();
                cleanup_counter = 0;
            }
            if (attacker == null) return;
            if (collateral == null) return;
            if (target == null)
            {
                TryRemove(attacker);
                return;
            }
            attacker_and_target[attacker] = target;
            collateral_and_attacker[collateral] = attacker;
            //Log.Message(collateral_and_attacker[collateral].LabelShort);
        }

        public static bool TryRemove(int attacker)
        {
            //if (attacker == null) return false;
            return attacker_and_target.Remove(attacker);
        }

        public static void TryRemoveByCollateral(int collateral)
        {
            //Log.Message($"remove started on {collateral.LabelShort}");
            if (!collateral_and_attacker.ContainsKey(collateral))
            {
                //Log.Message($"no key found in collateral_and_attacker");
                return;
            }
            //Log.Message($"Target {collateral.LabelShort} was found in collateral_and_attacker list");
            bool didremove = attacker_and_target.Remove(collateral_and_attacker[collateral]);
            //Log.Message($"removed from Reverse: {didremove}");
            collateral_and_attacker.Remove(collateral);
            //Log.Message($"removed from Original dictionary: {didremove}");
        }

        public static bool TryGetTarget(int attacker, out int target)
        {
            //if (attacker == null) { target = null; return false; }
            return attacker_and_target.TryGetValue(attacker, out target);
        }

        public static KeyValuePair<int, int>[] GetSnapshot()
        {
            return attacker_and_target.ToArray();
        }

        /*
        public static void Cleanup()
        {
            var toRemoveAttackerAndTarget = new List<Pawn>();
            foreach (var kv in attacker_and_target)
            {
                var a = kv.Key;
                var t = kv.Value;
                if (a == null || a.Dead || a.Map == null || t == null || t.Dead || t.Map == null)
                    toRemoveAttackerAndTarget.Add(a);
            }
            foreach (var key in toRemoveAttackerAndTarget)
                attacker_and_target.Remove(key);

            var toRemoveCollateralAndAttacker = new List<Pawn>();
            foreach (var kv in collateral_and_attacker)
            {
                var a = kv.Key;
                var t = kv.Value;
                if (a == null || a.Dead || a.Map == null || t == null || t.Dead || t.Map == null)
                    toRemoveCollateralAndAttacker.Add(a);
            }
            foreach (var key in toRemoveCollateralAndAttacker)
                collateral_and_attacker.Remove(key);
        }
        */

        public static void Clear()
        {
            attacker_and_target.Clear();
            collateral_and_attacker.Clear();
        }
    }


    [HarmonyPatch(typeof(AttackTargetsCache), "GetPotentialTargetsFor")]
    static class AttackTargetsCache_GetPotentialTargetsFor_Patch
    {
        static void Postfix(IAttackTargetSearcher th, ref List<IAttackTarget> __result)
        {
            
            try
            {
                
                //if (th == null) return;
                //Verb verb = th.CurrentEffectiveVerb;
                //if (verb.IsMeleeAttack) return;
                //if (!verb.UsesExplosiveProjectiles()) return;
                Thing attacker = th.Thing;

                if (AttackerToTargetBlacklist.TryGetTarget(attacker.thingIDNumber, out int badTarget))
                {
                    //Log.Message($"{attacker.LabelShort} found in blacklist, target {badTarget.LabelShort}");
                    if (badTarget != null)
                    {
                        __result.RemoveAll(t => t == null || t.Thing.thingIDNumber == badTarget);
                    }
                }
                
                /*
                float blastradius = BGHUtils.GetCurrentBlastRadius(th);

                if (blastradius == 0f)
                {
                    return;
                }
                //Log.Message($"[BGH] Called for {th.Thing.LabelShort}");

                if (th == null)
                {
                    return;
                }

                if (th.CurrentEffectiveVerb.IsMeleeAttack)
                {
                    return;
                }

                float blastradius = BGHUtils.GetCurrentBlastRadius(th);

                if (blastradius == 0f)
                {
                    return;
                }

                var badtargets = new List<IAttackTarget>();
                badtargets = BGHUtils.GetBadTargetsInList(th, __result, blastradius);

                if (badtargets.Count > 0)
                {
                    __result.RemoveAll(t => t == null || badtargets.Contains(t));
                    //Log.Message($"[BGH] Filtered targets for {th.Thing.LabelShort}: removed {badtargets.Count}, remaining {__result.Count}");
                }
                */
            }
            catch (Exception ex)
            {
                Log.Error($"[BGH] Exception in Stance_Warmup: {ex}");
            }
            
        }
    }
}