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
    //   bypass the existing checkpoint reactable patches entirely. A
    //   dupe approaching from the locker side just walks up to the
    //   rack, drops the suit, and walks away without ever crossing the
    //   marker.
    //
    //   We hook CreateChore() with a postfix that appends a Better
    //   Checkpoints precondition to both chores. The precondition
    //   resolves the locker's owning SuitMarker (via a static map we
    //   maintain on SetSuitMarker), checks whether that marker is one
    //   of our customised variants (has CheckpointAccessControl), and
    //   then short-circuits the chore for Bypass-Bionic and Block-set
    //   dupes.
    //
    // Safety note:
    //   Suppressing this chore means a Bypass-Bionic with an empty atmo
    //   suit and no air route can suffocate, since the vanilla
    //   emergency drop path no longer fires. The dropdown tooltip in
    //   the options dialog warns about this. Per-dupe Block has the
    //   same downstream consequence but is explicit player intent.
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

        [HarmonyPatch(typeof(SuitLocker), nameof(SuitLocker.SetSuitMarker))]
        [HarmonyPostfix]
        private static void SetSuitMarker_Postfix(SuitLocker __instance, SuitMarker suit_marker)
        {
            if (suit_marker == null)
            {
                LockerToMarker.Remove(__instance);
            }
            else
            {
                LockerToMarker[__instance] = suit_marker;
            }
        }

        // Belt-and-suspenders: drop the entry when the locker is
        // destroyed so the dictionary doesn't leak dead references.
        [HarmonyPatch(typeof(SuitLocker), "OnCleanUp")]
        [HarmonyPostfix]
        private static void OnCleanUp_Postfix(SuitLocker __instance)
        {
            LockerToMarker.Remove(__instance);
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

            // urgentChore and idleChore are private fields on the
            // ReturnSuitWorkable instance. They're freshly assigned by
            // the original method we just ran the postfix on, so they
            // exist for the lifetime of this workable.
            var urgent = AccessTools.Field(typeof(SuitLocker.ReturnSuitWorkable), "urgentChore")
                .GetValue(__instance) as Chore;
            var idle = AccessTools.Field(typeof(SuitLocker.ReturnSuitWorkable), "idleChore")
                .GetValue(__instance) as Chore;
            urgent?.AddPrecondition(BetterCheckpointsAccessPrecondition, locker);
            idle?.AddPrecondition(BetterCheckpointsAccessPrecondition, locker);
        }

        // Precondition body: return false to reject this dupe / chore
        // pair (chore won't be offered), true to let the chore proceed
        // through vanilla logic.
        //
        // Reject only when the locker is on a customised checkpoint
        // (has CheckpointAccessControl on its paired SuitMarker) AND
        // either:
        //   - the dupe is Bionic and the mod option is set to Bypass, or
        //   - the dupe's per-dupe row on the side screen is set to Block
        //     (vanilla AccessControl.Permission.Neither).
        //
        // Lockers paired with non-customised markers (Lead Suit, Jet
        // Suit) — and unpaired lockers — always allow the chore: the
        // mod's option only governs the suit types we customise.
        private static bool AllowChore(ref Chore.Precondition.Context context, object data)
        {
            var locker = data as SuitLocker;
            if (locker == null) return true;

            var marker = SuitLocker_SetSuitMarker_Patch.GetMarker(locker);
            if (marker == null) return true;

            var cac = marker.GetComponent<CheckpointAccessControl>();
            if (cac == null) return true; // non-customised variant, no Better Checkpoints rules apply

            var dupeGo = context.consumerState.gameObject;
            if (dupeGo == null) return true;

            // Bionic-in-Bypass: the chore is suppressed wholesale. This
            // is what makes Bionics in Bypass mode keep their atmo
            // suit on as the user requested.
            if (dupeGo.HasTag(GameTags.Minions.Models.Bionic) &&
                !BetterCheckpointsOptions.IsBionicsDefault)
            {
                return false;
            }

            // Per-dupe Block on the side screen sets vanilla
            // AccessControl.Permission.Neither for that dupe's proxy.
            // If the dupe is blocked from this checkpoint, they
            // shouldn't be using the lockers attached to it either.
            var ac = marker.GetComponent<AccessControl>();
            if (ac != null)
            {
                var identity = dupeGo.GetComponent<MinionIdentity>();
                var proxy = identity?.assignableProxy?.Get();
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
