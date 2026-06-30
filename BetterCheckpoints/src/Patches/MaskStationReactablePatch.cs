using System;
using System.Reflection;
using BetterCheckpoints.Options;
using HarmonyLib;
using UnityEngine;

namespace BetterCheckpoints.Patches
{
    // Gates MaskStation.OxygenMaskReactable for any oxygen-mask setup
    // that uses a MaskStation (not the OxygenMaskLocker SuitLocker
    // variant). MaskStation is a separate building with its own
    // mask-creating reactable; if a save uses it instead of the locker,
    // the reactable becomes the only equip/unequip gate.
    //
    // For the common Marker+OxygenMaskLocker layout this patch is a
    // no-op (the reactable never fires). For Marker+MaskStation layouts
    // (rare in v1 vanilla, possibly more common after DLC additions),
    // it mirrors the With/Without/Block gating we apply elsewhere.
    [HarmonyPatch]
    internal static class OxygenMaskReactable_InternalCanBegin_Patch
    {
        private static readonly Type OxygenMaskReactableType =
            AccessTools.Inner(typeof(MaskStation), "OxygenMaskReactable");

        private static readonly FieldInfo MaskStationField =
            OxygenMaskReactableType != null
                ? AccessTools.Field(OxygenMaskReactableType, "maskStation")
                : null;

        private static bool Prepare() =>
            OxygenMaskReactableType != null && MaskStationField != null;

        private static MethodBase TargetMethod() =>
            AccessTools.Method(OxygenMaskReactableType, "InternalCanBegin");

        private static void Postfix(object __instance, GameObject new_reactor, ref bool __result)
        {
            if (!__result) return;
            var maskStation = MaskStationField.GetValue(__instance) as MaskStation;
            if (maskStation == null) return;

            var marker = FindAdjacentMarker(maskStation.gameObject);
            if (marker == null) return;

            var cac = marker.GetComponent<CheckpointAccessControl>();
            if (cac == null) return;

            var kpid = new_reactor != null ? new_reactor.GetComponent<KPrefabID>() : null;
            var identity = new_reactor != null ? new_reactor.GetComponent<MinionIdentity>() : null;
            var equipment = identity?.GetEquipment();
            if (kpid == null || identity == null || equipment == null) return;

            bool useStandardDefaults = ModelHelpers.UseStandardDefaults(new_reactor);
            bool slotEmpty = !equipment.IsSlotOccupied(Db.Get().AssignableSlots.Suit);

            if (slotEmpty)
            {
                if (!cac.GetWithSuitAllowed(kpid.InstanceID, useStandardDefaults))
                {
                    __result = false;
                    return;
                }
            }
            else
            {
                if (!useStandardDefaults)
                {
                    __result = false;
                    return;
                }
            }

            var ac = marker.GetComponent<AccessControl>();
            if (ac != null)
            {
                var proxy = identity.assignableProxy?.Get();
                if (proxy != null &&
                    ac.GetSetPermission(proxy) == AccessControl.Permission.Neither)
                {
                    __result = false;
                }
            }
        }

        // Scan the 3x3 around the MaskStation for a SuitMarker with our
        // CheckpointAccessControl component. Vanilla doesn't expose the
        // pairing as a field (unlike SuitLocker.SetSuitMarker), so we
        // discover it lazily. The reactable's InternalCanBegin only
        // fires on actual transition attempts, so the lookup is not on
        // a hot path.
        private static SuitMarker FindAdjacentMarker(GameObject maskStationGo)
        {
            if (maskStationGo == null) return null;
            int center = Grid.PosToCell(maskStationGo.transform.position);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int adj = Grid.OffsetCell(center, dx, dy);
                    if (!Grid.IsValidCell(adj)) continue;
                    var go = Grid.Objects[adj, (int)ObjectLayer.Building];
                    if (go == null) continue;
                    var marker = go.GetComponent<SuitMarker>();
                    if (marker != null && marker.GetComponent<CheckpointAccessControl>() != null)
                    {
                        return marker;
                    }
                }
            }
            return null;
        }
    }
}
