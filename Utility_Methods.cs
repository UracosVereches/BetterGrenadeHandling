using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;


namespace BetterGrenadeHandling
{
    public static class BGHUtils
    {
        /*
        private static class IgnoreCollateralCache
        {
            private static readonly Dictionary<Pawn, dIgnoreCollateralCache

        }
        */

        /*
         * Ripped out directly from RimWorld.StunHandler.CanBeStunnedByDamage(DamageDef def) 
         * This method is very likely to change in future updates */
        // Precache
        private static DamageDef defEMP = DamageDefOf.EMP;
        private static bool CanBeStunnedByDamage(this Pawn pawn, DamageDef def)
        {
            if (!def.causeStun)
            {
                return false;
            }
            CompStunnable stunnableComp = pawn.TryGetComp<CompStunnable>();
            if (stunnableComp != null && !stunnableComp.CanBeStunnedByDamage(def))
            {
                return false;
            }
            if (pawn.Downed || pawn.Dead)
            {
                return false;
            }
            if (ModsConfig.AnomalyActive && pawn.health.hediffSet.HasHediff(HediffDefOf.AwokenCorpse))
            {
                return false;
            }
            if (def == DamageDefOf.Stun)
            {
                return true;
            }
            if (def == defEMP)
            {
                if (!pawn.RaceProps.IsFlesh)
                    return true;

                // Check if pawn has a shield belt and if it has any energy left
                if (pawn.HasWorkingShieldBelt())
                {
                    return true;
                }

                // Check if pawn has any stunnable hediffs (e.g. brain implants)
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    bool foundComp = hediff.TryGetComp<HediffComp_ReactOnDamage>(out HediffComp_ReactOnDamage comp);
                    if (foundComp && comp.Props.damageDefIncoming == defEMP)
                    {
                        return true;
                    }
                }
            }
            if (ModsConfig.BiotechActive && def == DamageDefOf.MechBandShockwave && pawn.RaceProps.IsMechanoid)
            {
                return true;
            }
            if (ModsConfig.AnomalyActive && def == DamageDefOf.NerveStun && !pawn.RaceProps.IsMechanoid)
            {
                return true;
            }
            return false;
        }

        /*
         * Ripped out directly from RimWorld.ITab_Pawn_Gear.TryDrawOverallArmor(ref float curY, float width, StatDef stat, string label
         * This method is very likely to change in future updates */
        private static float GetOverallArmorRating(this Pawn pawn, StatDef stat)
        {
            float num = 0f;
            float num2 = Mathf.Clamp01(pawn.GetStatValue(StatDefOf.ArmorRating_Heat) / 2f);
            List<BodyPartRecord> allParts = pawn.RaceProps.body.AllParts;
            List<Apparel> list = ((pawn.apparel != null) ? pawn.apparel.WornApparel : null);
            for (int i = 0; i < allParts.Count; i++)
            {
                float num3 = 1f - num2;
                if (list != null)
                {
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (list[j].def.apparel.CoversBodyPart(allParts[i]))
                        {
                            float num4 = Mathf.Clamp01(list[j].GetStatValue(StatDefOf.ArmorRating_Heat) / 2f);
                            num3 *= 1f - num4;
                        }
                    }
                }
                num += allParts[i].coverageAbs * (1f - num3);
            }
            num = Mathf.Clamp(num * 2f, 0f, 2f);
            num.ToStringPercent();

            return num;
        }

        // Cache
        private struct CollateralCacheData
        {
            public int lastTick;
            public bool result;
        }
        private static readonly Dictionary<(Pawn, Pawn), CollateralCacheData> IgnoreCollateralCache = new Dictionary<(Pawn, Pawn), CollateralCacheData>();
        private static readonly int cacheUpdateInterval = 60;
        private static readonly int totalCacheAnnihilationInterval = 10000; // clear cache to avoid memory leaks
        private static int lastCacheAnnihilationTick = 0;

        /// <summary>
        /// Check if attacker can safely ignore collateral in target's blast radius
        /// </summary>\
        // Precache
        private static StatDef flammabiltiyDef = StatDefOf.Flammability;
        private static StatDef armorRatingHeat = StatDefOf.ArmorRating_Heat;
        private static DamageDef toxGasDef = DamageDefOf.ToxGas;
        private static StatDef toxEnvResistance = StatDefOf.ToxicEnvironmentResistance;
        private static FactionDef wastersDef = FactionDefOf.PirateWaster;
        public static bool CanIgnoreCollateral(this Pawn attacker, Pawn collateral, DamageDef damageDef, bool isIncendiary = false)
        {
            if (BGHConfig.AvoidFriendlyFire == false)
                return true;

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastCacheAnnihilationTick >= totalCacheAnnihilationInterval)
            {
                IgnoreCollateralCache.Clear();
                lastCacheAnnihilationTick = currentTick;
            }

            bool foundInCache = IgnoreCollateralCache.TryGetValue((attacker, collateral), out CollateralCacheData data);
            if (foundInCache)
            {
                if (currentTick - data.lastTick <= cacheUpdateInterval)
                {
                    return data.result;
                }
            }

            bool result = DoCollateralIgnoreCheck(attacker, collateral, damageDef, isIncendiary);

            CollateralCacheData newData = new CollateralCacheData();
            newData.lastTick = currentTick;
            newData.result = result;

            IgnoreCollateralCache.SetOrAdd((attacker, collateral), newData);

            return false;
        }

        private static bool DoCollateralIgnoreCheck(Pawn attacker, Pawn collateral, DamageDef damageDef, bool isIncendiary = false)
        {
            if (attacker == null || collateral == null || damageDef == null)
            {
                return false;
            }

            if (attacker.HostileTo(collateral))
            {
                return true;
            }

            // Ignore wildlife except for hostile animals(manhunters, ytakkin ability)
            if (collateral.IsAnimal && (collateral.Faction == null || collateral.InMentalState))
            {
                return true;
            }

            // Ignore friendly collateral if its flammability less than 10% or heat armor exceeds required threshold (>90%)
            // Although flammability and heat armor sound similar - they're not the same
            // Flammability - how likely you are to catch on fire
            // Heat armor - how resistant you are to burn when you catch on fire
            // You could wear a full devilstrand set, but you'd catch fire just as often as without it
            if (isIncendiary && (collateral.GetStatValue(flammabiltiyDef) <= BGHConfig.MinFlammabilityToIgnore || collateral.GetOverallArmorRating(armorRatingHeat) >= BGHConfig.MinHeatArmorToIgnore))
            {
                return true;
            }

            if (damageDef == toxGasDef)
            {
                // I find it reasonable for waster pirates to use toxic weapons no matter what, because that's their entire ideology
                // I kind of want to gatekeep that, so there is no config option
                if (attacker.Faction.def == wastersDef)
                {
                    return true;
                }

                if (collateral.GetStatValue(toxEnvResistance) >= BGHConfig.MinToxicResistanceToIgnore)
                {
                    return true;
                }

            }

            // Ignore if weapon causes stun damage and friendly collateral can't be stunned
            if (damageDef.causeStun && !collateral.CanBeStunnedByDamage(damageDef))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Expand blast radius by 1 cell
        /// </summary>
        public static float ExpandBlastRadius(float radius)
        {
            if (radius == 1.1f) // 1.1 - molotov radius(1x1 cross)
            {
                radius = 2.9f; // Doesn't work the same for molotovs, frag max radius instead
            }
            else
            {
                radius++; // Just adding 1 does the trick
            }

            return radius;
        }

        /// <summary>
        /// Get list of pawns in radius around pawn
        /// </summary>
        public static List<Pawn> GetPawnsInRadius(this Pawn pawn, float radius)
        {
            List<Pawn> pawnsList = new List<Pawn>();

            if (radius <= 0f)
            {
                return pawnsList;
            }

            Map map = pawn.Map;
            IntVec3 position = pawn.Position;
            int num = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = position + GenRadial.RadialPattern[i];
                if (!intVec.InBounds(map))
                {
                    continue;
                }

                Pawn firstpawn = intVec.GetFirstPawn(map);
                if (firstpawn == null || firstpawn == pawn)
                {
                    continue;
                }

                pawnsList.Add(firstpawn);
            }

            return pawnsList;
        }

        /// <summary>
        /// Force pawn to flee from explosion
        /// </summary>
        public static void ForceFleeFromExplosion(this Pawn pawn, IntVec3 explosionPos, float blastRadius)
        {
            if ((int)pawn.RaceProps.intelligence < 2)
            {
                return;
            }
            if (pawn.Downed && !pawn.health.CanCrawl)
            {
                return;
            }
            if ((float)(pawn.Position - explosionPos).LengthHorizontalSquared > 81f)
            {
                return;
            }
            if (!RCellFinder.TryFindDirectFleeDestination(explosionPos, blastRadius, pawn, out var result))
            {
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, result);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        /// <summary>
        /// Check if this target is not made of flesh and isn't EMP stunned already. If made of flesh - check if target has working shield-belt
        /// </summary>
        public static bool ShouldBeHitByEMP(this Thing target, Verb verb)
        {
            Pawn targetPawn = target as Pawn;

            return targetPawn?.RaceProps?.IsFlesh == false ? targetPawn?.stances?.stunner?.StunFromEMP == false : targetPawn.HasWorkingShieldBelt();
        }

        /// <summary>
        /// Check if this pawn has a shield belt with energy
        /// </summary>
        public static bool HasWorkingShieldBelt(this Pawn pawn)
        {
            if (pawn?.apparel != null)
            {
                for (int i = 0; i < pawn.apparel.WornApparelCount; i++)
                {
                    List<ThingComp> allComps = pawn.apparel.WornApparel[i].AllComps;
                    for (int j = 0; j < allComps.Count; j++)
                    {
                        if (allComps[j] is CompShield compShield)
                        {
                            if (compShield.Energy == 0)
                            {
                                return false;
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}