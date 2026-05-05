using System.Collections.Generic;
using PeterHan.PLib.Options;
using UnityEngine;

namespace BetterCheckpoints.Options
{
    // Settings persisted via PLib's POptions to
    // %DOCUMENTS%\Klei\OxygenNotIncluded\mods\config\BetterCheckpoints\config.json
    // (SharedConfigLocation = true so the choice survives mod
    // reinstalls / Steam updates that wipe the mod folder).
    [ConfigFile("config.json", IndentOutput: true, SharedConfigLocation: true)]
    public sealed class BetterCheckpointsOptions : IOptions
    {
        // Dropdown rendered by PLib's built-in SelectOneOptionsEntry.
        [Option(ModStrings.Options.BIONIC_HANDLING_TITLE,
                ModStrings.Options.BIONIC_HANDLING_TOOLTIP)]
        public BionicHandling BionicHandling { get; set; } = BionicHandling.Default;

        // Cached snapshot read on first access and refreshed whenever the
        // user closes the options dialog. Patches read this instead of
        // hitting disk each frame.
        private static BetterCheckpointsOptions cached;

        public static BetterCheckpointsOptions Current
        {
            get
            {
                if (cached == null)
                {
                    cached = POptions.ReadSettings<BetterCheckpointsOptions>()
                             ?? new BetterCheckpointsOptions();
                }
                return cached;
            }
        }

        public static bool IsBionicsDefault =>
            Current.BionicHandling == BionicHandling.Default;

        // PLib calls this on the live instance after the options dialog
        // saves. We compare the new value against the cached previous
        // value and force an ONI restart when it changed — this dodges
        // a known interaction with third-party mod-manager replacements
        // (Ony's Mod Manager) whose mod-list refresh coroutine NREs on
        // a stale GameObject reference shortly after our save. By the
        // time their coroutine ticks, the game process is already
        // restarting.
        public void OnOptionsChanged()
        {
            var previous = cached?.BionicHandling;
            cached = this;
            if (previous.HasValue && previous.Value != BionicHandling)
            {
                Debug.Log("[BetterCheckpoints] BionicHandling changed; restarting ONI.");
                App.instance.Restart();
            }
        }

        // Returning null lets PLib auto-scan [Option] attributes — the
        // standard path, which produces a working SelectOneOptionsEntry
        // for the enum dropdown. (Mixing auto-scan with manually-yielded
        // entries here doesn't work cleanly: the manual SelectOneOptionsEntry
        // didn't bind its dropdown widget, and TextBlockOptionsEntry's
        // row label hijacks the previous entry's title. The restart
        // notice lives in the dropdown's tooltip and the README instead.)
        public IEnumerable<IOptionsEntry> CreateOptions()
        {
            return null;
        }
    }
}
