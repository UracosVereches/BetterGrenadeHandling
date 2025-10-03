using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using Unity.Collections;

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

                List<Pawn> standby_grenadiers = new List<Pawn>(StandByGrenadiers.GetList());
                List<Pawn> warmup_grenadiers = new List<Pawn>(WarmupGrenadiers.GetList());

                // Show standby grenadiers
                foreach (Pawn pawn in standby_grenadiers)
                {
                    IntVec3 pos = pawn.Position;
                    GenDraw.DrawRadiusRing(pos, 0.5f, Color.green);

                    if (AttackBlacklist.HasAttacker(pawn.thingIDNumber))
                    {
                        foreach (var keyvalue in AttackBlacklist.GetDictionary(pawn.thingIDNumber))
                        {
                            int pawnID = keyvalue.Key;
                            IntVec3 targetpos = Debug_ThingID_PositionCache.GetPos(pawnID);
                            if (targetpos == IntVec3.Zero)
                            {
                                continue;
                            }
                            GenDraw.DrawLineBetween(pawn.Position.ToVector3Shifted(), targetpos.ToVector3Shifted(), SimpleColor.Green, 0.2f);
                        }
                    }
                }

                // Show warmup grenaiers
                foreach (Pawn pawn in warmup_grenadiers)
                {
                    IntVec3 pos = pawn.Position;

                    GenDraw.DrawRadiusRing(pos, 0.5f, Color.red);

                    if (AttackBlacklist.HasAttacker(pawn.thingIDNumber))
                    {
                        foreach (var keyvalue in AttackBlacklist.GetDictionary(pawn.thingIDNumber))
                        {
                            int pawnID = keyvalue.Key;
                            IntVec3 targetpos = Debug_ThingID_PositionCache.GetPos(pawnID);
                            if (targetpos == IntVec3.Zero)
                            {
                                continue;
                            }
                            GenDraw.DrawLineBetween(pawn.Position.ToVector3Shifted(), targetpos.ToVector3Shifted(), SimpleColor.Red, 0.2f);
                        }
                    }
                }

                // Show real positions of every pawn
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    IntVec3 pos = pawn.Position;
                    if (!(standby_grenadiers.Contains(pawn)) && !(warmup_grenadiers.Contains(pawn)))
                    {
                        GenDraw.DrawRadiusRing(pos, 0.5f, Color.gray);
                    }
                }

                // Show dangerous positions directly from tracker
                foreach (var kvp in DangerPositionTracker.GetDictionary())
                {
                    foreach (IntVec3 pos in kvp.Value)
                    {
                        GenDraw.DrawRadiusRing(pos, 0.5f, Color.yellow);
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

                List<Pawn> standby_grenadiers = new List<Pawn>(StandByGrenadiers.GetList());
                List<Pawn> warmup_grenadiers = new List<Pawn>(WarmupGrenadiers.GetList());

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

                    if (pawn.stances?.curStance != null)
                    {
                        GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(pawn, 0.4f), pawn.stances.curStance.ToString(), Color.yellow);
                    }
                }

                // Show dangerous positions from grid
                Map curmap = Find.CurrentMap;
                NativeArray<ushort> grid = DangerousGrid.GetForMap(curmap).GetOffsetGrid();
                CellIndices cellindices = curmap.cellIndices;
                for (int i = 0; i < grid.Length; i++)
                {
                    if (grid[i] == 9000)
                    {
                        IntVec3 pos = cellindices.IndexToCell(i);
                        Vector2 drawpos = GenMapUI.LabelDrawPosFor(pos);
                        GenMapUI.DrawThingLabel(drawpos, grid[i].ToString(), Color.red);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[Better Grenade Handling] Exception at UIRootOnGUI patch: {ex}");
            }
        }
    }
#endif
}
