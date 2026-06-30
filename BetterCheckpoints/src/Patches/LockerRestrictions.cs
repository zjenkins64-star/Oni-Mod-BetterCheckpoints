using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterCheckpoints.Patches
{
    // Mirrors live AC.SetPermission and AC.SetDefaultPermission calls
    // onto paired lockers. Required because the side-screen has TWO
    // permission-change paths: per-dupe via our checkbox handlers (which
    // we could intercept directly), and per-group via the section-header
    // arrows (vanilla flow we don't touch). Patching the AC API catches
    // both, plus any future paths like copy-settings.
    [HarmonyPatch(typeof(AccessControl), nameof(AccessControl.SetPermission),
        new[] { typeof(MinionAssignablesProxy), typeof(AccessControl.Permission) })]
    internal static class AccessControl_SetPermissionProxy_Patch
    {
        private static void Postfix(AccessControl __instance, MinionAssignablesProxy key,
            AccessControl.Permission permission)
        {
            if (__instance == null || key == null) return;
            var marker = __instance.GetComponent<SuitMarker>();
            if (marker == null) return;
            if (marker.GetComponent<CheckpointAccessControl>() == null) return;

            var kpid = key.GetComponent<KPrefabID>();
            if (kpid == null) return;
            int id = kpid.InstanceID;

            var lockers = ScratchLockers;
            SuitLocker_SetSuitMarker_Patch.GetLockersForMarker(marker, lockers);
            for (int i = 0; i < lockers.Count; i++)
            {
                LockerRestrictions.SetForDupe(lockers[i], id, permission);
            }
        }

        private static readonly List<SuitLocker> ScratchLockers = new List<SuitLocker>();
    }

    // Forces the Standard and Bionic group defaults to a usable value on
    // every customised checkpoint marker once it spawns. Without an
    // entry in AccessControl.defaultPermissionByTag, the Grid restriction
    // mechanism treats the group as "no rule" — which combined with our
    // mirrored locker restrictions ends up rejecting any dupe in that
    // group who lacks a per-dupe entry. A Bionic in Bypass mode (no
    // per-dupe entry, no group entry) was being reported as Unreachable
    // because of this.
    //
    // Patches SuitMarker.OnSpawn instead of AccessControl.OnSpawn so
    // Harmony's name lookup hits a public method on a non-abstract type;
    // earlier attempts to patch the protected override on AccessControl
    // didn't take effect for reasons that aren't worth chasing.
    //
    // We only override a stale Permission.Neither — Both/GoLeft/GoRight
    // are preserved so the user's explicit section-arrow clicks survive
    // across reloads. Neither as a *group* default has no meaning in our
    // model (per-dupe Block is how denial is expressed), so flipping it
    // back to Both is a safe self-heal.
    [HarmonyPatch(typeof(SuitMarker), "OnSpawn")]
    internal static class SuitMarker_OnSpawn_SeedDefaults_Patch
    {
        private static void Postfix(SuitMarker __instance)
        {
            if (__instance == null) return;
            if (__instance.GetComponent<CheckpointAccessControl>() == null) return;
            var ac = __instance.GetComponent<AccessControl>();
            if (ac == null) return;

            HealNeitherToBoth(ac, GameTags.Minions.Models.Standard);
            HealNeitherToBoth(ac, GameTags.Minions.Models.Bionic);
        }

        private static void HealNeitherToBoth(AccessControl ac, Tag tag)
        {
            var current = ac.GetDefaultPermission(tag);
            if (current == AccessControl.Permission.Neither ||
                !DefaultsContains(ac, tag))
            {
                // Either explicitly Neither, or not in the dict at all
                // (returns Both via fallback but no Grid restriction
                // entry exists). Either way, write an explicit Both so
                // the Grid entry is created and propagated to lockers.
                ac.SetDefaultPermission(tag, AccessControl.Permission.Both);
            }
        }

        private static readonly FieldInfo DefaultPermissionByTagField =
            AccessTools.Field(typeof(AccessControl), "defaultPermissionByTag");

        private static bool DefaultsContains(AccessControl ac, Tag tag)
        {
            if (DefaultPermissionByTagField == null) return true;
            var list = DefaultPermissionByTagField.GetValue(ac)
                as List<KeyValuePair<Tag, AccessControl.Permission>>;
            if (list == null) return true;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key == tag) return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(AccessControl), nameof(AccessControl.SetDefaultPermission))]
    internal static class AccessControl_SetDefaultPermission_Patch
    {
        private static void Postfix(AccessControl __instance, Tag groupTag,
            AccessControl.Permission permission)
        {
            if (__instance == null) return;
            var marker = __instance.GetComponent<SuitMarker>();
            if (marker == null) return;
            if (marker.GetComponent<CheckpointAccessControl>() == null) return;

            int tagId = LockerRestrictions.PublicGetTagId(groupTag);

            var lockers = ScratchLockers;
            SuitLocker_SetSuitMarker_Patch.GetLockersForMarker(marker, lockers);
            for (int i = 0; i < lockers.Count; i++)
            {
                LockerRestrictions.SetForDupe(lockers[i], tagId, permission);
            }
        }

        private static readonly List<SuitLocker> ScratchLockers = new List<SuitLocker>();
    }

    // Mirrors a customised SuitMarker's per-dupe AccessControl restrictions
    // onto the paired SuitLocker's Grid cells.
    //
    // Why: vanilla AccessControl only registers Grid.Restriction on its own
    // building's PlacementCells. The paired locker sits on adjacent cells
    // that vanilla AC never touches. In layouts wider than 1 cell, a
    // Blocked dupe can pathfind around the marker through the locker's
    // foundation cell — the marker rejects them, but the locker cell is
    // unrestricted. Dupe walks past as if the checkpoint isn't there.
    //
    // Atmo Block "works" only for 1-cell-wide passages where the marker
    // is the sole path; in any wider layout it leaks the same way. The
    // oxygen-mask layout used by community reporters happens to be wider
    // and surfaced the bug.
    //
    // Approach: call Grid.RegisterRestriction / Grid.SetRestriction
    // directly on locker cells using the same proxy InstanceID and
    // direction encoding vanilla AccessControl uses. Pathfinder treats
    // those cells as restricted for the Blocked dupe and routes around.
    //
    // Crucially we do NOT attach a vanilla AccessControl component to the
    // locker — that would give the locker its own "Access Permissions"
    // side screen, separate from the marker's, that users could desync.
    // The marker remains the single source of truth; we just mirror its
    // permissions onto the locker's Grid cells.
    internal static class LockerRestrictions
    {
        private static readonly FieldInfo SavedPermissionsByIdField =
            AccessTools.Field(typeof(AccessControl), "savedPermissionsById");

        private static readonly FieldInfo DefaultPermissionByTagField =
            AccessTools.Field(typeof(AccessControl), "defaultPermissionByTag");

        // Vanilla GridRestrictionSerializer maps Tags (like
        // GameTags.Minions.Models.Bionic) to the int IDs used as
        // Grid.SetRestriction keys. Need this so our locker-cell
        // restrictions for group defaults use the same IDs vanilla
        // uses on the marker cells.
        private static readonly MethodInfo GetTagIdMethod =
            AccessTools.Method(
                AccessTools.TypeByName("GridRestrictionSerializer"),
                "GetTagId");

        private static readonly System.Reflection.PropertyInfo GridSerializerInstanceProperty =
            AccessTools.Property(
                AccessTools.TypeByName("GridRestrictionSerializer"),
                "Instance");

        public static int PublicGetTagId(Tag tag)
        {
            if (GetTagIdMethod == null || GridSerializerInstanceProperty == null) return 0;
            var instance = GridSerializerInstanceProperty.GetValue(null);
            return (int)GetTagIdMethod.Invoke(instance, new object[] { tag });
        }

        private static int GetTagId(Tag tag) => PublicGetTagId(tag);

        // Tracks which lockers we've registered with Grid so we don't
        // double-register on re-pairing and so we know to unregister on
        // teardown.
        private static readonly HashSet<SuitLocker> RegisteredLockers
            = new HashSet<SuitLocker>();

        public static void Register(SuitLocker locker)
        {
            if (locker == null || RegisteredLockers.Contains(locker)) return;
            var cells = GetCells(locker);
            if (cells == null) return;
            for (int i = 0; i < cells.Length; i++)
            {
                Grid.RegisterRestriction(cells[i], Grid.Restriction.Orientation.Vertical);
            }
            RegisteredLockers.Add(locker);

            // Seed Both defaults for the duplicant groups that can
            // legitimately use suit checkpoints. Without these, the
            // locker cell registered above defaults to "block any dupe
            // without an explicit allow entry" — so a Bionic in Bypass
            // (no per-dupe entry, no group entry on the locker) would
            // be unable to pathfind through the locker cell.
            //
            // Vanilla AccessControl only populates defaultPermissionByTag
            // when the user clicks a group arrow on the side screen, so
            // SyncFromMarker has nothing to copy from a freshly-attached
            // marker. We provide the baseline here ourselves; SyncFromMarker
            // and AC.SetDefaultPermission's postfix layer on top to honor
            // any explicit group changes the user makes.
            SetForDupe(locker, PublicGetTagId(GameTags.Minions.Models.Standard),
                AccessControl.Permission.Both);
            SetForDupe(locker, PublicGetTagId(GameTags.Minions.Models.Bionic),
                AccessControl.Permission.Both);
        }

        public static void Unregister(SuitLocker locker)
        {
            if (locker == null || !RegisteredLockers.Contains(locker)) return;
            var cells = GetCells(locker);
            if (cells != null)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    Grid.UnregisterRestriction(cells[i]);
                }
            }
            RegisteredLockers.Remove(locker);
        }

        // Apply one (proxy-id, permission) pair to the locker's cells.
        // Called both from the side-screen click path and from sync-on-load.
        public static void SetForDupe(SuitLocker locker, int proxyInstanceId,
            AccessControl.Permission permission)
        {
            if (locker == null) return;
            // Register on demand if not already; covers the case where
            // side-screen propagation reaches a locker before the
            // SetSuitMarker pairing has flowed through.
            if (!RegisteredLockers.Contains(locker)) Register(locker);

            var cells = GetCells(locker);
            if (cells == null) return;

            var directions = PermissionToDirections(permission);
            for (int i = 0; i < cells.Length; i++)
            {
                Grid.SetRestriction(cells[i], proxyInstanceId, directions);
            }
        }

        public static void ClearForDupe(SuitLocker locker, int proxyInstanceId)
        {
            if (locker == null || !RegisteredLockers.Contains(locker)) return;
            var cells = GetCells(locker);
            if (cells == null) return;
            for (int i = 0; i < cells.Length; i++)
            {
                Grid.ClearRestriction(cells[i], proxyInstanceId);
            }
        }

        // On load (or fresh pairing), copy every entry from the marker's
        // AccessControl into Grid restrictions on the locker's cells.
        // Must sync BOTH lists or pathfinder defaults misbehave:
        //
        //   - savedPermissionsById — per-dupe (proxy InstanceID → Permission)
        //   - defaultPermissionByTag — per-group (e.g. Bionic → Both)
        //
        // Without the per-group sync, a Bionic with no per-dupe entry
        // hits the registered locker cell with no matching rule and
        // pathfinder treats them as restricted — Bionic-in-Bypass would
        // become "Unreachable" at the oxygen-mask checkpoint even when
        // they're meant to ignore it entirely.
        public static void SyncFromMarker(SuitLocker locker, SuitMarker marker)
        {
            if (locker == null || marker == null) return;
            var ac = marker.GetComponent<AccessControl>();
            if (ac == null) return;

            if (SavedPermissionsByIdField != null)
            {
                var perDupe = SavedPermissionsByIdField.GetValue(ac)
                    as List<KeyValuePair<int, AccessControl.Permission>>;
                if (perDupe != null)
                {
                    for (int i = 0; i < perDupe.Count; i++)
                    {
                        var kv = perDupe[i];
                        SetForDupe(locker, kv.Key, kv.Value);
                    }
                }
            }

            if (DefaultPermissionByTagField != null)
            {
                var perTag = DefaultPermissionByTagField.GetValue(ac)
                    as List<KeyValuePair<Tag, AccessControl.Permission>>;
                if (perTag != null)
                {
                    for (int i = 0; i < perTag.Count; i++)
                    {
                        var kv = perTag[i];
                        SetForDupe(locker, GetTagId(kv.Key), kv.Value);
                    }
                }
            }
        }

        private static int[] GetCells(SuitLocker locker)
        {
            return locker.GetComponent<Building>()?.PlacementCells;
        }

        // Mirrors AccessControl.SetGridRestrictions's switch — same
        // encoding so vanilla pathfinder treats our restrictions
        // identically to vanilla AC's.
        private static Grid.Restriction.Directions PermissionToDirections(
            AccessControl.Permission permission)
        {
            switch (permission)
            {
                case AccessControl.Permission.GoLeft:
                    return Grid.Restriction.Directions.Right;
                case AccessControl.Permission.GoRight:
                    return Grid.Restriction.Directions.Left;
                case AccessControl.Permission.Neither:
                    return Grid.Restriction.Directions.Left | Grid.Restriction.Directions.Right;
                case AccessControl.Permission.Both:
                default:
                    return (Grid.Restriction.Directions)0;
            }
        }
    }
}
