using HarmonyLib;
using Verse;

namespace InterstellarOdyssey
{
    public class InterstellarOdysseyMod : Mod
    {
        public static GalaxyWorldConfiguration PendingGalaxyConfig = GalaxyConfigUtility.CreateDefaultConfiguration();
        public static bool WorldGenGalaxyTabSelected;

        public InterstellarOdysseyMod(ModContentPack content) : base(content)
        {
            Harmony harmony = new Harmony("InterstellarOdyssey.GalaxyExpansion");
            harmony.PatchAll();
        }
    }
}
