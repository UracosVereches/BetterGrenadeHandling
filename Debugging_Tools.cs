using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace BetterGrenadeHandling
{
#if DEBUG
    [HarmonyPatch(typeof(MapInterface), "MapInterfaceUpdate")]
    public static class MapInterface_MapInterfaceUpdate_Postfix
    {
        public static void Postfix()
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return;

                List<Pawn> standby_grenadiers = new List<Pawn>(GrenadiersOnStandBy.GetList());
                List<Pawn> warmup_grenadiers = new List<Pawn>(GrenadiersOnWarmup.GetList());

                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    IntVec3 pos = pawn.Position;
                    bool draw_true_pos = true;
                    if (standby_grenadiers.Contains(pawn))
                    {
                        GenDraw.DrawRadiusRing(pos, 0.5f, Color.green);
                        draw_true_pos = false;
                    }

                    if (warmup_grenadiers.Contains(pawn))
                    {
                        GenDraw.DrawRadiusRing(pos, 0.5f, Color.red);
                        draw_true_pos = false;
                    }

                    // Show real position of a pawn
                    if (draw_true_pos)
                    {
                        GenDraw.DrawRadiusRing(pos, 0.5f, Color.gray);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[Better Grenade Handling] Exception at MapInterfaceUpdate patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(UIRoot))]
    [HarmonyPatch("UIRootOnGUI")]
    public static class UIRoot_UIRootOnGUI_Postfix
    {
        public static void Postfix(UIRoot __instance)
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return;

                List<Pawn> standby_grenadiers = new List<Pawn>(GrenadiersOnStandBy.GetList());
                List<Pawn> warmup_grenadiers = new List<Pawn>(GrenadiersOnWarmup.GetList());

                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (standby_grenadiers.Contains(pawn))
                    {
                        GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(pawn, 0f), "Standby", Color.green);
                    }

                    if (warmup_grenadiers.Contains(pawn))
                    {
                        GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(pawn, -0.4f), "Warmup", Color.red);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[Better Grenade Handling] Exception at MapInterfaceUpdate patch: {ex}");
            }
        }
    }
#endif
}
