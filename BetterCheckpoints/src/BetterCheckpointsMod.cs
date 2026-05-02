using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;

namespace BetterCheckpoints
{
    public sealed class BetterCheckpointsMod : UserMod2
    {
        public const string MOD_ID = "BetterCheckpoints";

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary(false);
            harmony.PatchAll();
        }
    }
}
