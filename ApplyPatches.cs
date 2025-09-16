using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BetterGrenadeHandling
{
   public class BetterGrenadeHandling : Mod
    {
        public const string PACKAGE_ID = "UracosVereches.BetterGrenadeHandling";
        public const string PACKAGE_NAME = "Better Grenade Handling";

        public BetterGrenadeHandling(ModContentPack content) : base(content)
        {
            var harmony = new Harmony(PACKAGE_ID);
            harmony.PatchAll();

            Log.Message($"[{PACKAGE_NAME}] Loaded.");
        }
    }
}