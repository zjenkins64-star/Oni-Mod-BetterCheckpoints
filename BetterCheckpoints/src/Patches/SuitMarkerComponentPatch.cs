using System.Collections.Generic;
using HarmonyLib;

namespace BetterCheckpoints.Patches
{
    // Attaches our per-dupe permission components to every SuitMarker on
    // spawn (works on existing saves too).
    //
    //   - Vanilla AccessControl is attached to ALL SuitMarker checkpoints
    //     (atmo, oxygen mask, lead suit, etc.) so the vanilla side screen
    //     with direction permissions and group sections shows up.
    //
    //   - CheckpointAccessControl is attached ONLY to the suit-types we
    //     specifically customise (atmo + oxygen mask). For other suit
    //     checkpoints (lead suit, future bionic-suit variants, etc.) we
    //     intentionally don't attach it, which means our injection /
    //     reactable patches no-op for those buildings and the user gets
    //     full vanilla behaviour: every dupe model that wears that suit
    //     equips on entry, drops on exit. The Bionic / Robot section
    //     headers stay visible because we only hide them for SuitMarkers
    //     that have CheckpointAccessControl.
    [HarmonyPatch(typeof(SuitMarker), "OnSpawn")]
    internal static class SuitMarker_OnSpawn_AttachAccessControl
    {
        // Locker tags on the SuitMarker that identify the checkpoint
        // types we customise. From SuitMarkerConfig / OxygenMaskMarkerConfig
        // in the decompiled source.
        private static readonly HashSet<string> CustomisedLockerTags = new HashSet<string>
        {
            "SuitLocker",
            "OxygenMaskLocker",
        };

        private static void Postfix(SuitMarker __instance)
        {
            var go = __instance.gameObject;

            var ac = go.AddOrGet<AccessControl>();
            ac.controlEnabled = true;

            if (IsCustomisedCheckpoint(__instance))
            {
                go.AddOrGet<CheckpointAccessControl>();
            }
        }

        private static bool IsCustomisedCheckpoint(SuitMarker marker)
        {
            var tags = marker?.LockerTags;
            if (tags == null || tags.Length == 0) return false;
            for (int i = 0; i < tags.Length; i++)
            {
                if (CustomisedLockerTags.Contains(tags[i].Name)) return true;
            }
            return false;
        }
    }
}
