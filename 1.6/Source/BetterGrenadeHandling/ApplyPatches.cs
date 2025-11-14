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

            string consoleMsg = $"[{PACKAGE_NAME}] Loaded v{base.Content?.ModMetaData?.ModVersion}.";

            #if DEBUG
            consoleMsg = consoleMsg + " Debug build.";
            #endif

            Log.Message(consoleMsg);
        }
    }
}