using BetterCheckpoints.Options;
using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace BetterCheckpoints
{
    public sealed class BetterCheckpointsMod : UserMod2
    {
        public const string MOD_ID = "BetterCheckpoints";

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary(false);

            // Register the options dialog (Mods menu -> "Options" button
            // next to BetterCheckpoints). The BionicHandling enum is
            // rendered by PLib's built-in SelectOneOptionsEntry as a
            // dropdown.
            new POptions().RegisterOptions(this, typeof(BetterCheckpointsOptions));

            harmony.PatchAll();
        }
    }
}
