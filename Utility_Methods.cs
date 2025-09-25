using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using static UnityEngine.GraphicsBuffer;
using System.Security.Cryptography;
using System.Reflection.Emit;
using UnityEngine;


namespace BetterGrenadeHandling
{
    public static class BGHUtils
    {
        /*
         * Ripped out directly from RimWorld.StunHandler.CanBeStunnedByDamage(DamageDef def) 
         * TODO: cache. update it every 30 calls or so.
         * This method is very likely to change in future updates */
        public static bool CanBeStunnedByDamage(Thing thing, DamageDef def)
        {
            if (!def.causeStun)
            {
                return false;
            }
            // Can't figure it out
            //if (stunnableComp != null && !stunnableComp.CanBeStunnedByDamage(def))
            //{
                //return false;
            //}
            if (thing is Pawn pawn)
            {
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
                if (def == DamageDefOf.EMP && !pawn.RaceProps.IsFlesh)
                {
                    return true;
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
            return true;
        }

        /*
         * Ripped out directly from RimWorld.ITab_Pawn_Gear.TryDrawOverallArmor(ref float curY, float width, StatDef stat, string label)
         * TODO: Cache results in a list when entry for a pawn is null. If there is an entry, change the result on ApparelChanged only
         * This method is very likely to change in future updates */
        public static float GetOverallArmorRating(Pawn pawn, StatDef stat)
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

        /*
         * Check if attacker can safely ignore collateral in target's blast radius */
        public static bool CanIgnoreCollateral(Thing attacker, Thing collateral, Verb verb)
        {
            if (attacker == null || collateral == null || verb == null)
            {
                return false;
            }

            DamageDef def = verb.GetDamageDef();
            Pawn collateral_pawn = collateral as Pawn;

            if (attacker.HostileTo(collateral))
            {
                return true;
            }

            // Ignore collateral if its heat armor exceeds required threshold (>90%)
            if (verb.IsIncendiary_Ranged() && GetOverallArmorRating(collateral_pawn, StatDefOf.ArmorRating_Heat) > 0.9f)
            {
                return true;
            }

            if (def.causeStun && !BGHUtils.CanBeStunnedByDamage(collateral, def))
            {
                return true;
            }

            return false;
        }

        /*
         * Find bad targets with allies in potential blast radius
         * return them in a list */
        public static List<IAttackTarget> GetBadTargetsInList(IAttackTargetSearcher th, List<IAttackTarget> __result, float blastradius)
        {
            var badtargets = new List<IAttackTarget>();

            // Launchers = 1.1
            // Molotov = 1.1
            // Frag grenade = 1.9
            // Emp grenade = 3.5
            if (blastradius == 0f)
            {
                return badtargets;
            }

            foreach (IAttackTarget target in __result)
            {
                List<Thing> things_in_blast = new List<Thing>();

                things_in_blast = BGHUtils.GetThingsInTargetBlast(th, target, blastradius);

                if (things_in_blast.NullOrEmpty())
                {
                    continue;
                }

                foreach (var collateral in things_in_blast)
                {
                    Pawn pawn = th.Thing as Pawn;
                    if (!CanIgnoreCollateral(pawn, collateral, VerbCache.GetVerb(pawn)))
                    {
                        badtargets.Add(target);
                        break;
                    }

                }
            }
            return badtargets;
        }

        /*
         * Get list of things in blast radius of a single given target */
        public static List<Thing> GetThingsInTargetBlast(IAttackTargetSearcher th, IAttackTarget target, float blastradius)
        {
            List<Thing> things_in_blast = new List<Thing>();

            if (blastradius <= 0f)
            {
                return things_in_blast;
            }

            // TODO: code duplicate, move blastradius operations to its own util function
            // Include possible cells outside the actual radius too
            float blastradius_max = blastradius;
            if (blastradius == 1.1f)
            {
                blastradius_max = 2.9f; // Doesn't work the same for molotovs, frag max radius instead
            }
            else
            {
                blastradius_max = blastradius + 1f; // Just adding 1 does the trick
            }

            Map map = target.Thing.Map;
            IntVec3 position = target.Thing.Position;
            int num = GenRadial.NumCellsInRadius(blastradius_max);
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = position + GenRadial.RadialPattern[i];
                if (!intVec.InBounds(map))
                {
                    continue;
                }
                //bool flag = true;
                List<Thing> thingList = intVec.GetThingList(map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    
                    if (!(thingList[j] is IAttackTarget) || thingList[j] == target)
                    {
                        continue;
                    }
                    /*
                    if (flag)
                    {
                        if (!GenSight.LineOfSight(position, intVec, map, skipFirstCell: true))
                        {
                            break;
                        }
                        flag = false;
                    }
                    */
                    /*
                    if (!th.Thing.HostileTo(thingList[j]))
                    {
                        if (emp)
                        {
                            if (true)
                            {
                                
                            }
                            return false;
                        }
                        return true;
                    }
                    */
                    things_in_blast.Add(thingList[j]);
                }
            }

            return things_in_blast;
        }
    }
}