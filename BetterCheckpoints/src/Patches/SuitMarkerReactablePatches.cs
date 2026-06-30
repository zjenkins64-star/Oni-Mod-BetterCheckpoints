using System;
using System.Reflection;
using BetterCheckpoints.Options;
using HarmonyLib;
using UnityEngine;

namespace BetterCheckpoints.Patches
{
    // Per-dupe enforcement of the With Suit / Without Suit / Block mode
    // at the checkpoint, applied at the equip/unequip reactable level.
    // When a dupe is denied a transition here they walk past unchanged:
    // a bare dupe stays bare, a suited dupe stays suited.
    //
    // Pathfinder-level blocking for Block is handled by vanilla
    // AccessControl on the marker plus our LockerRestrictions mirror
    // onto adjacent locker cells. Direction permissions (left/right)
    // remain path-time enforced via the vanilla AccessControl component
    // we attach alongside this one.
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
            // Standard duplicants normally drop their suits at the dock
            // on return — vanilla checkpoint behaviour. Non-standard-
            // default duplicants (robots, plus Bionics when "Bionic
            // Duplicants" is set to Bypass) instead ignore the
            // checkpoint entirely — they never equip and never drop, so
            // a Bionic wearing an atmo suit walking back through keeps
            // it on.
            if (!useStandardDefaults)
            {
                __result = false;
                return;
            }

            // Per-dupe Block must also reject the unequip. Without this
            // check, a Block-set Standard dupe wearing a suit would drop
            // it at the dock when they path back — leaving the suit in
            // the locker (or on the ground if the rack is full) before
            // walking away. The pathfinder gate from vanilla AC + our
            // LockerRestrictions normally prevents the dupe from
            // reaching the marker in the first place, but if they're
            // already past it (e.g., the mode was just changed) this
            // ensures they keep the suit on instead of dropping it.
            var ac = marker.GetComponent<AccessControl>();
            if (ac != null)
            {
                var proxy = newReactor.GetComponent<MinionIdentity>()?.assignableProxy?.Get();
                if (proxy != null &&
                    ac.GetSetPermission(proxy) == AccessControl.Permission.Neither)
                {
                    __result = false;
                }
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
