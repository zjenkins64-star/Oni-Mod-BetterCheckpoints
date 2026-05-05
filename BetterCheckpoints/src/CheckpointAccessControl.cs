using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace BetterCheckpoints
{
    // Per-checkpoint, per-duplicant suit-state permissions ("With Suit" /
    // "Without Suit"). Direction permissions are handled by the vanilla
    // AccessControl component attached alongside this one.
    //
    // The defaults are hardcoded — every checkpoint starts in "With Suit
    // only" mode. Per-dupe overrides cascade onto these. The UI enforces
    // strict mutex (always exactly one of With/Without checked per dupe);
    // the data model still uses two booleans because per-dupe overrides
    // are stored sparsely.
    //
    // Suit-state enforcement happens at the equip/unequip reactable level
    // (see SuitMarkerReactablePatches), not in the pathfinder.
    [SerializationConfig(MemberSerialization.OptIn)]
    [AddComponentMenu("KMonoBehaviour/scripts/CheckpointAccessControl")]
    public class CheckpointAccessControl : KMonoBehaviour
    {
        // Defaults for Standard duplicants. Strict mutex: exactly one of
        // these is true. WithSuit=true is the natural default — the
        // dupe equips a suit on entry. The reactable patches treat the
        // unequip side as ALWAYS allowed for Standard duplicants, so
        // these flags only control the equip-on-entry transition.
        public const bool DefaultWithSuitAllowed = true;
        public const bool DefaultWithoutSuitAllowed = false;

        // Defaults for non-Standard duplicants (Bionic / Robots / future
        // models) — both false means the reactables block BOTH equip and
        // unequip, so the dupe ignores the checkpoint entirely: a bare
        // bionic walks past bare; a suited bionic (e.g. wearing an atmo
        // suit indefinitely because they don't breathe) walks past still
        // wearing it. The side screen hides these dupes; these defaults
        // are the only fallback the reactable patches consult for them.
        public const bool NonStandardDefaultWithSuitAllowed = false;
        public const bool NonStandardDefaultWithoutSuitAllowed = false;

        [Serialize] private List<KeyValuePair<int, bool>> withSuitOverrides = new List<KeyValuePair<int, bool>>();
        [Serialize] private List<KeyValuePair<int, bool>> withoutSuitOverrides = new List<KeyValuePair<int, bool>>();

        public static readonly int OnRulesChangedHash = Hash.SDBMLower("BetterCheckpoints.OnRulesChanged");

        // useStandardDefaults: caller-computed flag for which set of
        // hardcoded defaults to fall back to when no per-dupe override
        // exists. Standard dupes always pass true. Bionic dupes pass true
        // only when the "Bionic Duplicants" mod option is set to Default
        // (the option is consulted at the call sites, not here, so this
        // component stays UI-/option-agnostic).
        public bool GetWithSuitAllowed(int minionInstanceID, bool useStandardDefaults = true)
        {
            if (TryGetOverride(withSuitOverrides, minionInstanceID, out bool v)) return v;
            return useStandardDefaults ? DefaultWithSuitAllowed : NonStandardDefaultWithSuitAllowed;
        }

        public bool GetWithoutSuitAllowed(int minionInstanceID, bool useStandardDefaults = true)
        {
            if (TryGetOverride(withoutSuitOverrides, minionInstanceID, out bool v)) return v;
            return useStandardDefaults ? DefaultWithoutSuitAllowed : NonStandardDefaultWithoutSuitAllowed;
        }

        public void SetWithSuitOverride(int minionInstanceID, bool value)
        {
            SetOverride(withSuitOverrides, minionInstanceID, value);
            NotifyChanged();
        }

        public void SetWithoutSuitOverride(int minionInstanceID, bool value)
        {
            SetOverride(withoutSuitOverrides, minionInstanceID, value);
            NotifyChanged();
        }

        public void ClearWithSuitOverride(int minionInstanceID)
        {
            if (RemoveOverride(withSuitOverrides, minionInstanceID)) NotifyChanged();
        }

        public void ClearWithoutSuitOverride(int minionInstanceID)
        {
            if (RemoveOverride(withoutSuitOverrides, minionInstanceID)) NotifyChanged();
        }

        private void NotifyChanged()
        {
            Trigger(OnRulesChangedHash, this);
        }

        private static bool TryGetOverride(List<KeyValuePair<int, bool>> list, int id, out bool value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key == id) { value = list[i].Value; return true; }
            }
            value = default;
            return false;
        }

        private static void SetOverride(List<KeyValuePair<int, bool>> list, int id, bool value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key == id) { list[i] = new KeyValuePair<int, bool>(id, value); return; }
            }
            list.Add(new KeyValuePair<int, bool>(id, value));
        }

        private static bool RemoveOverride(List<KeyValuePair<int, bool>> list, int id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key == id) { list.RemoveAt(i); return true; }
            }
            return false;
        }
    }
}
