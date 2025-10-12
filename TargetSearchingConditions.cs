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

            __result = target.ShouldBeHitByEMP(__instance);
        }
    }
}
