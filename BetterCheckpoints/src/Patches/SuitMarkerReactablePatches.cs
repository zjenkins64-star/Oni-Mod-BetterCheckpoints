using System;
using System.Reflection;
using BetterCheckpoints.Options;
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
            bool useStandardDefaults = ModelHelpers.UseStandardDefaults(newReactor);
            // Equipping ends with the dupe wearing a suit — only allowed
            // when "With Suit" mode permits passing while suited. Non-
            // Standard dupes default to no-suit-needed, so equip is
            // blocked for them by the hardcoded false default. When the
            // "Bionic Duplicants" mod option is set to Default, Bionic
            // dupes count as standard for default purposes and equip on
            // entry too.
            if (!cac.GetWithSuitAllowed(kpid.InstanceID, useStandardDefaults))
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
            bool useStandardDefaults = ModelHelpers.UseStandardDefaults(newReactor);
            // Standard duplicants always drop their suits at the dock on
            // return, regardless of whether the side-screen is set to
            // "With Suit" or "Without Suit" mode — vanilla checkpoint
            // behaviour. The mode toggle only governs equip-on-entry.
            //
            // Non-standard-default duplicants (robots, plus Bionics when
            // "Bionic Duplicants" is set to Bypass) instead ignore the
            // checkpoint entirely — they never equip and never drop, so
            // a bionic wearing an atmo suit walking back through keeps
            // it on.
            if (!useStandardDefaults)
            {
                __result = false;
            }
        }
    }

    // Centralised "should this dupe be treated like a Standard dupe at
    // customised checkpoints?" check, consulting the BionicHandling mod
    // option for Bionic dupes. Used by the reactable patches and the
    // side-screen patch so they stay in sync.
    internal static class ModelHelpers
    {
        public static bool UseStandardDefaults(GameObject dupe)
        {
            if (dupe == null) return false;
            if (dupe.HasTag(GameTags.Minions.Models.Standard)) return true;
            if (dupe.HasTag(GameTags.Minions.Models.Bionic) &&
                BetterCheckpointsOptions.IsBionicsDefault)
            {
                return true;
            }
            return false;
        }
    }
}
