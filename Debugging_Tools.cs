using HarmonyLib;
using LudeonTK;
using UnityEngine;
using Verse;

namespace BetterGrenadeHandling
{
#if DEBUG
    public class GrenadierStrongHighlightComponent : MapComponent
    {
        public GrenadierStrongHighlightComponent(Map map) : base(map) { }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();

            // Warmup red
            foreach (Pawn pawn in GrenadiersOnWarmup.GetSnapshot())
            {
                if (!IsPawnValidForMap(pawn)) continue;
                DrawPawnStrongHighlight(pawn, Color.red);
            }

            // StandBy yellow
            foreach (Pawn pawn in GrenadiersOnStandBy.GetList())
            {
                if (!IsPawnValidForMap(pawn)) continue;
                DrawPawnStrongHighlight(pawn, Color.yellow);
            }
        }

        private bool IsPawnValidForMap(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && pawn.Map == map && !pawn.Dead;
        }

        private void DrawPawnStrongHighlight(Pawn pawn, Color color)
        {
            Vector3 centerWorld = pawn.DrawPos;
            if (pawn?.Graphic == null) return;

            float gx = pawn.Graphic.drawSize.x;
            float gz = pawn.Graphic.drawSize.y;

            float angle = pawn.Rotation.AsAngle;
            Quaternion rot = Quaternion.Euler(0f, angle, 0f);
            Vector3 rightOffset = rot * new Vector3(gx * 0.5f, 0f, 0f);
            Vector3 forwardOffset = rot * new Vector3(0f, 0f, gz * 0.5f);

            Vector3 screenCenter = Find.Camera.WorldToScreenPoint(centerWorld);
            if (screenCenter.z <= 0f) return;

            Vector3 screenRight = Find.Camera.WorldToScreenPoint(centerWorld + rightOffset);
            Vector3 screenForward = Find.Camera.WorldToScreenPoint(centerWorld + forwardOffset);

            float pixelWidth = Mathf.Abs(screenRight.x - screenCenter.x) * 2f;
            float pixelHeight = Mathf.Abs(screenForward.y - screenCenter.y) * 2f;

            float x = screenCenter.x - pixelWidth * 0.5f;
            float y = Screen.height - screenCenter.y - pixelHeight * 0.5f;
            Rect rect = new Rect(x, y, pixelWidth, pixelHeight);

            Rect scaled = UIScaling.AdjustRectToUIScaling(rect);
            Widgets.DrawStrongHighlight(scaled, color);
        }
    }

    [HarmonyPatch(typeof(Map))]
    [HarmonyPatch("FinalizeInit")]
    public static class Map_FinalizeInit_Patch
    {
        public static void Postfix(Map __instance)
        {
            if (__instance.GetComponent<GrenadierStrongHighlightComponent>() == null)
            {
                __instance.components.Add(new GrenadierStrongHighlightComponent(__instance));
            }
        }
    }
#endif
}
