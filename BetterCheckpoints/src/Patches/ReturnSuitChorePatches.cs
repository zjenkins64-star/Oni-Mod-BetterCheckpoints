using System.Collections.Generic;
using BetterCheckpoints.Options;
using HarmonyLib;
using UnityEngine;

namespace BetterCheckpoints.Patches
{
    // Suppresses the vanilla "return / recharge suit" chore for dupes
    // that, per Better Checkpoints' settings, should never be using a
    // particular customised checkpoint:
    //
    //   - Bionic duplicants when the mod option is set to Bypass.
    //   - Any duplicant whose per-dupe row on the side screen is set to
    //     Block (vanilla AccessControl.Permission.Neither).
    //
    // Why this patch exists at all:
    //   SuitLocker.ReturnSuitWorkable creates two WorkChores in its
    //   CreateChore() — an urgent one (suit empty / low on O2) and an
    //   idle one (dupe wearing a suit while idle). Both target the
    //   SuitLocker building directly, not the SuitMarker cell, so they
    //   bypass the existing checkpoint reactable patches entirely.
    //
    //   The SetSuitMarker postfix also mirrors the marker's AccessControl
    //   onto the locker's Grid cells via LockerRestrictions, so a Blocked
    //   dupe can't even pathfind through the locker cell to reach the
    //   far side of the checkpoint.
    [HarmonyPatch]
    internal static class SuitLocker_SetSuitMarker_Patch
    {
        // Maps a SuitLocker instance to its currently-paired SuitMarker
        // (null when the locker has no marker). Vanilla SuitLocker
        // stores only the SuitMarkerState enum, not the marker
        // reference, so we mirror the marker reference on every
        // SetSuitMarker call.
        private static readonly Dictionary<SuitLocker, SuitMarker> LockerToMarker
            = new Dictionary<SuitLocker, SuitMarker>();

        public static SuitMarker GetMarker(SuitLocker locker)
        {
            if (locker == null) return null;
            return LockerToMarker.TryGetValue(locker, out var marker) ? marker : null;
        }

        // Enumerate every locker currently paired with the given marker.
        // Used by the side-screen click handlers to propagate per-dupe
        // permission changes from the marker to the locker cells.
        public static void GetLockersForMarker(SuitMarker marker, List<SuitLocker> outLockers)
        {
            outLockers.Clear();
            if (marker == null) return;
            foreach (var kv in LockerToMarker)
            {
                if (kv.Value == marker) outLockers.Add(kv.Key);
            }
        }

        [HarmonyPatch(typeof(SuitLocker), nameof(SuitLocker.SetSuitMarker))]
        [HarmonyPostfix]
        private static void SetSuitMarker_Postfix(SuitLocker __instance, SuitMarker suit_marker)
        {
            if (suit_marker == null)
            {
                LockerToMarker.Remove(__instance);
                LockerRestrictions.Unregister(__instance);
                return;
            }

            LockerToMarker[__instance] = suit_marker;

            // Only mirror restrictions for customised variants (atmo +
            // oxygen mask). Lead Suit / Jet Suit lockers stay as vanilla.
            if (suit_marker.GetComponent<CheckpointAccessControl>() != null)
            {
                LockerRestrictions.Register(__instance);
                LockerRestrictions.SyncFromMarker(__instance, suit_marker);
            }
        }

        // Belt-and-suspenders: drop the entry when the locker is
        // destroyed so the dictionary doesn't leak dead references and
        // the Grid doesn't keep stale restrictions on the locker's
        // former cells.
        [HarmonyPatch(typeof(SuitLocker), "OnCleanUp")]
        [HarmonyPostfix]
        private static void OnCleanUp_Postfix(SuitLocker __instance)
        {
            LockerToMarker.Remove(__instance);
            LockerRestrictions.Unregister(__instance);
        }
    }

    [HarmonyPatch(typeof(SuitLocker.ReturnSuitWorkable), nameof(SuitLocker.ReturnSuitWorkable.CreateChore))]
    internal static class ReturnSuitWorkable_CreateChore_Patch
    {
        private static readonly Chore.Precondition BetterCheckpointsAccessPrecondition
            = new Chore.Precondition
            {
                id = "BetterCheckpoints.ReturnSuitAccess",
                description = "Better Checkpoints — locker is on a customised checkpoint that this duplicant is not allowed to use.",
                fn = AllowChore,
            };

        private static void Postfix(SuitLocker.ReturnSuitWorkable __instance)
        {
            var locker = __instance.GetComponent<SuitLocker>();
            if (locker == null) return;

            var urgent = AccessTools.Field(typeof(SuitLocker.ReturnSuitWorkable), "urgentChore")
                .GetValue(__instance) as Chore;
            var idle = AccessTools.Field(typeof(SuitLocker.ReturnSuitWorkable), "idleChore")
                .GetValue(__instance) as Chore;
            urgent?.AddPrecondition(BetterCheckpointsAccessPrecondition, locker);
            idle?.AddPrecondition(BetterCheckpointsAccessPrecondition, locker);
        }

        // Reject Bionic-in-Bypass and per-dupe Block at the chore level.
        // The Grid restriction mirror in LockerRestrictions already
        // prevents the dupe from pathing to the locker, but rejecting
        // the chore here too prevents vanilla from queuing useless work
        // and keeps the errand panel honest.
        private static bool AllowChore(ref Chore.Precondition.Context context, object data)
        {
            var locker = data as SuitLocker;
            if (locker == null) return true;

            var marker = SuitLocker_SetSuitMarker_Patch.GetMarker(locker);
            if (marker == null) return true;

            var cac = marker.GetComponent<CheckpointAccessControl>();
            if (cac == null) return true;

            var dupeGo = context.consumerState.gameObject;
            if (dupeGo == null) return true;

            if (dupeGo.HasTag(GameTags.Minions.Models.Bionic) &&
                !BetterCheckpointsOptions.IsBionicsDefault)
            {
                return false;
            }

            var ac = marker.GetComponent<AccessControl>();
            if (ac != null)
            {
                var proxy = dupeGo.GetComponent<MinionIdentity>()?.assignableProxy?.Get();
                if (proxy != null &&
                    ac.GetSetPermission(proxy) == AccessControl.Permission.Neither)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
