using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using RimWorld;
using static UnityEngine.GraphicsBuffer;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

// Pretty much everything related to EMP tweaks can be found here
namespace BetterGrenadeHandling
{
    [HarmonyPatch(typeof(AttackTargetFinder), "BestAttackTarget")]
    public static class BestAttackTarget_Prefix
    {
        public static void Prefix(IAttackTargetSearcher searcher, TargetScanFlags flags, ref Predicate<Thing> validator
        )
        {
            Thing searcherThing = searcher as Thing;

            bool verbFound = VerbCache.TryGetCurrentEffectiveVerb(searcherThing, out Verb verb);
            if (!verbFound)
            {
                return;
            }

            if (!VerbCache.IsVerbEMP(verb))
            {
                return;
            }

            Predicate<Thing> origValidator = (Thing thing) => true; // return true by default
            // Save original validator if there's any
            if (validator != null)
            {
                origValidator = validator;
            }

            // Combine original and our validator
            Predicate<Thing> newValidator = (Thing target) => origValidator(target) && target.ShouldBeHitByEMP(verb);
            validator = newValidator;

            return;
        }
    }

    [HarmonyPatch(typeof(Verb), "CanHitTargetFrom",
    new Type[] { typeof(IntVec3), typeof(LocalTargetInfo)})]
    public static class CanHitTargetFrom_Patch
    {
        static void Postfix(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref bool __result)
        {
            Thing target = targ.Thing;

            if (target == null)
            {
                return;
            }

            if (__result)
            {
                if (!VerbCache.IsVerbEMP(__instance))
                {
                    return;
                }

                Pawn targetPawn = target as Pawn;
                if (targetPawn == null)
                    return;

                __result = target.ShouldBeHitByEMP(__instance);
            }
        }
    }

    // Enable EMP weapons to target everything in BestAttackTarget
    // Really hoped that I won't have to use transpilers
    [HarmonyPatch(typeof(AttackTargetFinder))]
    [HarmonyPatch(nameof(AttackTargetFinder.BestAttackTarget))]
    public static class AttackTargetFinder_BestAttackTarget_Patch
    {
        private static bool JustReturnFalse(Verb verb)
        {
            return false;
        }

        private static MethodInfo targetIsEMP = AccessTools.Method(typeof(VerbUtility), "IsEMP", new Type[] { typeof(Verb) });
        private static MethodInfo replacement = AccessTools.Method(typeof(AttackTargetFinder_BestAttackTarget_Patch), nameof(JustReturnFalse), new Type[] { typeof(Verb) });

        private static bool methodsFound = targetIsEMP != null && replacement != null;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions /*, ILGenerator generator*/)
        {
            if (!methodsFound)
                return instructions;

            try
            {

                if (!BGHConfig.EMPFix)
                {
                    return instructions;
                }

                var codes = new List<CodeInstruction>(instructions);

                int replacements = 0;
                for (int i = 0; i < codes.Count; i++)
                {
                    var ci = codes[i];
                    if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodInfo mi)
                    {
                        if (mi == targetIsEMP)
                        {
                            codes[i] = new CodeInstruction(OpCodes.Call, replacement)
                            {
                                labels = ci.labels,
                                blocks = ci.blocks
                            };
                            replacements++;
                        }
                    }
                }

                if (replacements == 0)
                {
                    Log.Warning("No occurrences of VerbUtility.IsEMP were replaced.");
                }

                return codes.AsEnumerable();
            }
            catch (Exception e)
            {
                Log.Error($"[Better Grenade Handling] Exception in AttackTargetFinder.BestAttackTarget transpiler: {e}");
            }

            return instructions;
        }
    }

    // Fix EMP weapon target searching to prioritize shield-belt enemies
    [HarmonyPatch(typeof(AttackTargetFinder), "GetShootingTargetScore")]
    public static class GetShootingTargetScorePatch
    {
        [HarmonyPostfix]
        public static void Postfix(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb, ref float __result)
        {
            try
            {
                if (!BGHConfig.EMPFix)
                {
                    return;
                }

                //Log.Message($"GetShootingTargetScore: {target} - {__result}");

                if (!VerbCache.IsVerbEMP(verb))
                {
                    return;
                }

                if ((target as Pawn).HasWorkingShieldBelt())
                {
                    __result = __result + 500f;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[Better Grenade Handling] Exception in AttackTargetFinder.GetShootingTargetScore: {e}");
            }
        }
    }

    // Prioritize targetting enemy groups with higher count
    [HarmonyPatch(typeof(AttackTargetFinder))]
    [HarmonyPatch("FriendlyFireBlastRadiusTargetScoreOffset")]
    public static class AttackTargetFinder_FriendlyFireBlastRadiusTargetScoreOffset_Patch
    {
        public static void Postfix(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb, ref float __result)
        {
            try
            {
                if (!BGHConfig.TargetScoreFix)
                {
                    return;
                }

                //Log.Message($"FriendlyFireBlastRadiusTargetScoreOffset (before - {__result}) : target {target}, searcher {searcher}, verb {verb} - {verb.verbProps.ai_AvoidFriendlyFireRadius}");

                if (!(searcher is Thing searcherThing))
                {
                    return;
                }

                float blastRadius = VerbCache.GetVerbBlastRadius(verb);
                if (blastRadius == 0f)
                {
                    return;
                }    

                Map map = target.Thing.Map;
                IntVec3 position = target.Thing.Position;

                int num = GenRadial.NumCellsInRadius(blastRadius);
                for (int i = 0; i < num; i++)
                {
                    IntVec3 intVec = position + GenRadial.RadialPattern[i];
                    if (!intVec.InBounds(map))
                    {
                        continue;
                    }

                    List<Thing> thingList = intVec.GetThingList(map);
                    for (int j = 0; j < thingList.Count; j++)
                    {
                        if (!(thingList[j] is IAttackTarget collateral) || thingList[j] == target)
                        {
                            continue;
                        }

                        if (searcherThing.HostileTo(collateral as Thing))
                        {
                            __result = __result + 20f; // 20 points for every enemy around target
                        }
                    }
                };

                //Log.Message($"score after: {__result}");
            }
            catch (Exception e)
            {
                Log.Error($"[Better Grenade Handling] Exception in AttackTargetFinder.FriendlyFireBlastRadiusTargetScoreOffset: {e}");
            }
        }
    }
}
