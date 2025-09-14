using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BetterGrenadeHandling
{
    //public static void DrawRadiusRing(IntVec3 center, float radius)
    //{
    //    DrawRadiusRing(center, radius, Color.white);
    //}
    [HarmonyPatch(typeof(GenDraw))]
    [HarmonyPatch("DrawRadiusRing", new Type[] { typeof(IntVec3), typeof(float), typeof(Color), typeof(Func<IntVec3, bool>) })]
    static class DrawRadiusRing_Patch
    {
        static void Prefix(IntVec3 center, ref float radius, Color color, Func<IntVec3, bool> predicate = null)
        {
            //launchers = 1.1
            //molotov = 1.1
            //frag grenade = 1.9
            //emp grenade = 3.5
            //radius = 2.1f;
            //Log.Message($"This bitch got called. Center: {center}, radius: {radius}, color: {color}");
        }
    }
    
}
