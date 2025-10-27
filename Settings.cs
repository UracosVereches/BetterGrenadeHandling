using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace BetterGrenadeHandling
{
    public class BGHConfig : ModSettings
    {
        public static bool AvoidFriendlyFire = true;
        public static float MinFlammabilityToIgnore = 0.1f;
        public static float MinHeatArmorToIgnore = 0.9f;
        public static float MinToxicResistanceToIgnore = 0.8f;
        public static bool AvoidExplosives = true;
        public static bool FleeExplosives = true;
        public static bool AvoidShells = true;
        public static bool FleeShells = true;
        public static bool EMPFix = true;
        public static bool TargetScoreFix = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref AvoidFriendlyFire, "AvoidFriendlyFire", true);
            Scribe_Values.Look(ref MinFlammabilityToIgnore, "MinFlammabilityToIgnore", 0.1f);
            Scribe_Values.Look(ref MinHeatArmorToIgnore, "MinHeatArmorToIgnore", 0.9f);
            Scribe_Values.Look(ref MinToxicResistanceToIgnore, "MinToxicResistanceToIgnore", 0.8f);
            Scribe_Values.Look(ref AvoidExplosives, "AvoidExplosives", true);
            Scribe_Values.Look(ref FleeExplosives, "FleeExplosives", true);
            Scribe_Values.Look(ref AvoidShells, "AvoidShells", true);
            Scribe_Values.Look(ref FleeShells, "FleeShells", true);
            Scribe_Values.Look(ref EMPFix, "EMPFix", true);
            Scribe_Values.Look(ref TargetScoreFix, "TargetScoreFix", true);
            base.ExposeData();
        }
    }

    public class BetterGrenadeHandlingMod : Mod
    {
        BGHConfig settings;

        public BetterGrenadeHandlingMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<BGHConfig>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled("BGH_AvoidFriendlyFire".Translate(), ref BGHConfig.AvoidFriendlyFire, "BGH_AvoidFriendlyFire_Tip".Translate());

            listingStandard.Label("BGH_IgnoreConditions".Translate());
            listingStandard.GapLine(12);

            ref float flammabilityConfig = ref BGHConfig.MinFlammabilityToIgnore;
            listingStandard.Label($"BGH_MinFlammabilityToIgnore".Translate() + $": {(Math.Round(flammabilityConfig, 3)) * 100}%", maxHeight: -1, (TipSignal)"BGH_MinFlammabilityToIgnore_Tip".Translate());
            flammabilityConfig = listingStandard.Slider(flammabilityConfig, 0f, 1f);

            ref float heatArmorConfig = ref BGHConfig.MinHeatArmorToIgnore;
            listingStandard.Label($"BGH_MinHeatArmorToIgnore".Translate() + $": {(Math.Round(heatArmorConfig, 3)) * 100}%", maxHeight: -1, (TipSignal)"BGH_MinHeatArmorToIgnore_Tip".Translate());
            heatArmorConfig = listingStandard.Slider(heatArmorConfig, 0f, 2f);

            ref float toxArmorConfig = ref BGHConfig.MinToxicResistanceToIgnore;
            listingStandard.Label($"BGH_MinToxicResistanceToIgnore".Translate() + $": {(Math.Round(toxArmorConfig, 3)) * 100}%", maxHeight: -1, (TipSignal)"BGH_MinToxicResistanceToIgnore_Tip".Translate());
            toxArmorConfig = listingStandard.Slider(toxArmorConfig, 0f, 1f);
            listingStandard.GapLine(1);
            listingStandard.Gap(12);

            listingStandard.CheckboxLabeled("BGH_AvoidExplosives".Translate(), ref BGHConfig.AvoidExplosives, "BGH_AvoidExplosives_Tip".Translate());
            listingStandard.CheckboxLabeled("BGH_FleeExplosives".Translate(), ref BGHConfig.FleeExplosives, "BGH_FleeExplosives_Tip".Translate());
            listingStandard.Gap(12);

            listingStandard.CheckboxLabeled("BGH_AvoidShells".Translate(), ref BGHConfig.AvoidShells, "BGH_AvoidShells_Tip".Translate());
            listingStandard.CheckboxLabeled("BGH_FleeShells".Translate(), ref BGHConfig.FleeShells, "BGH_FleeShells_Tip".Translate());
            listingStandard.Gap(12);

            listingStandard.CheckboxLabeled("BGH_EMPFix".Translate(), ref BGHConfig.EMPFix, "BGH_EMPFix_Tip".Translate());
            listingStandard.CheckboxLabeled("BGH_TargetScoreFix".Translate(), ref BGHConfig.TargetScoreFix, "BGH_TargetScoreFix_Tip".Translate());
            listingStandard.Gap(12);

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "BetterGrenadeHandling";
        }
    }
}
