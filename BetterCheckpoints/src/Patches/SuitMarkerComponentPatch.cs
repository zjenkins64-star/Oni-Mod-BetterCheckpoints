using HarmonyLib;
using UnityEngine;

namespace BetterCheckpoints.Patches
{
    // Attaches our per-dupe permission components to SuitMarker building
    // prefabs at config time, NOT in OnSpawn. This is critical for save
    // persistence:
    //
    //   ONI's load sequence instantiates a building from its prefab,
    //   then runs KSerialization to populate [Serialize] fields on
    //   components present on the new instance, then fires OnSpawn.
    //   Components added in an OnSpawn postfix arrive after the
    //   deserialization pass — they exist for the runtime session but
    //   are born with default state every load, dropping any saved
    //   per-dupe overrides on the floor.
    //
    //   Patching DoPostConfigureComplete on each *MarkerConfig adds the
    //   components to the PREFAB itself, so every cloned instance has
    //   them from creation, well before KSerialization runs. Save data
    //   for AccessControl (vanilla) and CheckpointAccessControl (this
    //   mod) now finds a target component to deserialize into.
    //
    //   - SuitMarker (atmo) and OxygenMaskMarker get BOTH AccessControl
    //     (direction permissions) and CheckpointAccessControl (per-dupe
    //     With Suit / Without Suit overrides).
    //   - LeadSuit and JetSuit get AccessControl ONLY — vanilla side
    //     screen with full direction + group sections, our injection
    //     and reactable patches no-op (no CheckpointAccessControl).
    //
    //   Vanilla SuitMarker has no AccessControl on the prefab; we add
    //   it to bring per-group / per-dupe direction permissions to all
    //   variants.
    internal static class SuitMarkerAttachment
    {
        public static void Attach(GameObject go, bool withCheckpointAccessControl)
        {
            var ac = go.AddOrGet<AccessControl>();
            ac.controlEnabled = true;
            if (withCheckpointAccessControl)
            {
                go.AddOrGet<CheckpointAccessControl>();
            }
        }
    }

    [HarmonyPatch(typeof(SuitMarkerConfig), nameof(SuitMarkerConfig.DoPostConfigureComplete))]
    internal static class SuitMarkerConfig_DoPostConfigureComplete_Patch
    {
        private static void Postfix(GameObject go) =>
            SuitMarkerAttachment.Attach(go, withCheckpointAccessControl: true);
    }

    [HarmonyPatch(typeof(OxygenMaskMarkerConfig), nameof(OxygenMaskMarkerConfig.DoPostConfigureComplete))]
    internal static class OxygenMaskMarkerConfig_DoPostConfigureComplete_Patch
    {
        private static void Postfix(GameObject go) =>
            SuitMarkerAttachment.Attach(go, withCheckpointAccessControl: true);
    }

    [HarmonyPatch(typeof(LeadSuitMarkerConfig), nameof(LeadSuitMarkerConfig.DoPostConfigureComplete))]
    internal static class LeadSuitMarkerConfig_DoPostConfigureComplete_Patch
    {
        private static void Postfix(GameObject go) =>
            SuitMarkerAttachment.Attach(go, withCheckpointAccessControl: false);
    }

    [HarmonyPatch(typeof(JetSuitMarkerConfig), nameof(JetSuitMarkerConfig.DoPostConfigureComplete))]
    internal static class JetSuitMarkerConfig_DoPostConfigureComplete_Patch
    {
        private static void Postfix(GameObject go) =>
            SuitMarkerAttachment.Attach(go, withCheckpointAccessControl: false);
    }
}
