using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterCheckpoints.Patches
{
    // Per-dupe enforcement of the With Suit / Without Suit mode at the
    // checkpoint, applied at the equip/unequip reactable level (not in the
    // pathfinder). When a dupe is denied a transition here they walk past
    // unchanged: a bare dupe stays bare, a suited dupe stays suited.
    //
    // We deliberately do NOT block the pathfinder via Grid.HasSuit /
    // Grid.HasEmptyLocker. Doing so makes the SuitMarker cell appear
    // entirely impassable to the affected dupe, even when no transition
    // is needed (e.g. a bare dupe walking through to a breathable
    // destination), which is too aggressive. The mod controls only what
    // happens *at* the checkpoint, not whether the path through it is
    // valid — atmospheric survival is the player's responsibility.
    //
    // Direction permissions (left/right) remain path-time enforced via the
    // vanilla AccessControl component we attach alongside this one.
    internal static class ReactableReflection
    {
        public static readonly Type EquipType =
            AccessTools.Inner(typeof(SuitMarker), "EquipSuitReactable");

        public static readonly Type UnequipType =
            AccessTools.Inner(typeof(SuitMarker), "UnequipSuitReactable");

        public static readonly Type AbstractBaseType =
            AccessTools.Inner(typeof(SuitMarker), "SuitMarkerReactable");

        public static readonly FieldInfo SuitMarkerField =
            AccessTools.Field(AbstractBaseType, "suitMarker");

        public static SuitMarker GetSuitMarker(object reactableInstance)
        {
            return (SuitMarker)SuitMarkerField.GetValue(reactableInstance);
        }
    }

    [HarmonyPatch]
    internal static class EquipSuitReactable_InternalCanBegin_Patch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(ReactableReflection.EquipType, "InternalCanBegin");
        }

        private static void Postfix(object __instance, GameObject newReactor, ref bool __result)
        {
            if (!__result) return;
            var marker = ReactableReflection.GetSuitMarker(__instance);
            if (marker == null) return;
            var cac = marker.GetComponent<CheckpointAccessControl>();
            if (cac == null) return;
            var kpid = newReactor != null ? newReactor.GetComponent<KPrefabID>() : null;
            if (kpid == null) return;
            bool isStandard = newReactor.HasTag(GameTags.Minions.Models.Standard);
            // Equipping ends with the dupe wearing a suit — only allowed
            // when "With Suit" mode permits passing while suited. Non-
            // Standard dupes (bionic / robots) default to no-suit-needed,
            // so equip is always blocked for them.
            if (!cac.GetWithSuitAllowed(kpid.InstanceID, isStandard))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch]
    internal static class UnequipSuitReactable_InternalCanBegin_Patch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(ReactableReflection.UnequipType, "InternalCanBegin");
        }

        private static void Postfix(object __instance, GameObject newReactor, ref bool __result)
        {
            if (!__result) return;
            var marker = ReactableReflection.GetSuitMarker(__instance);
            if (marker == null) return;
            var cac = marker.GetComponent<CheckpointAccessControl>();
            if (cac == null) return;
            var kpid = newReactor != null ? newReactor.GetComponent<KPrefabID>() : null;
            if (kpid == null) return;
            bool isStandard = newReactor.HasTag(GameTags.Minions.Models.Standard);
            // Standard duplicants always drop their suits at the dock on
            // return, regardless of whether the side-screen is set to
            // "With Suit" or "Without Suit" mode — vanilla checkpoint
            // behaviour. The mode toggle only governs equip-on-entry.
            //
            // Non-Standard duplicants (bionic, robot) instead ignore the
            // checkpoint entirely — they never equip and never drop, so
            // a bionic wearing an atmo suit walking back through keeps
            // it on.
            if (!isStandard)
            {
                __result = false;
            }
        }
    }
}
