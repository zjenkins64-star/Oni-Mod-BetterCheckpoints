using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace BetterCheckpoints
{
    // Per-checkpoint, per-duplicant suit-state permissions ("With Suit" /
    // "Without Suit"). Direction permissions are handled by the vanilla
    // AccessControl component attached alongside this one.
    //
    // Storage uses Dictionary<int, bool> keyed by KPrefabID.InstanceID,
    // matching vanilla AccessControl.permissions's shape — KSerialization
    // handles dictionaries of primitive key/value natively but does not
    // round-trip List<KeyValuePair<,>> reliably (the v1.2.0 shape, which
    // is why overrides were silently dropped on load).
    //
    // Field names were renamed `*OverridesByMinionId` so KSerialization
    // ignores any v1.2.0 save data tagged with the old List names rather
    // than attempting a type-mismatched deserialization. v1.2.0 overrides
    // are lost on upgrade, but they were never persisting anyway, so no
    // real data loss.
    //
    // The defaults are hardcoded — every checkpoint starts in "With Suit
    // only" mode. Per-dupe overrides cascade onto these. The UI enforces
    // a 3-way mutex (exactly one of With/Without/Block checked per dupe);
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

        [Serialize] private Dictionary<int, bool> withSuitOverridesByMinionId = new Dictionary<int, bool>();
        [Serialize] private Dictionary<int, bool> withoutSuitOverridesByMinionId = new Dictionary<int, bool>();

        public static readonly int OnRulesChangedHash = Hash.SDBMLower("BetterCheckpoints.OnRulesChanged");

        // useStandardDefaults: caller-computed flag for which set of
        // hardcoded defaults to fall back to when no per-dupe override
        // exists. Standard dupes always pass true. Bionic dupes pass true
        // only when the "Bionic Duplicants" mod option is set to Default
        // (the option is consulted at the call sites, not here, so this
        // component stays UI-/option-agnostic).
        public bool GetWithSuitAllowed(int minionInstanceID, bool useStandardDefaults = true)
        {
            if (withSuitOverridesByMinionId.TryGetValue(minionInstanceID, out bool v)) return v;
            return useStandardDefaults ? DefaultWithSuitAllowed : NonStandardDefaultWithSuitAllowed;
        }

        public bool GetWithoutSuitAllowed(int minionInstanceID, bool useStandardDefaults = true)
        {
            if (withoutSuitOverridesByMinionId.TryGetValue(minionInstanceID, out bool v)) return v;
            return useStandardDefaults ? DefaultWithoutSuitAllowed : NonStandardDefaultWithoutSuitAllowed;
        }

        public void SetWithSuitOverride(int minionInstanceID, bool value)
        {
            withSuitOverridesByMinionId[minionInstanceID] = value;
            NotifyChanged();
        }

        public void SetWithoutSuitOverride(int minionInstanceID, bool value)
        {
            withoutSuitOverridesByMinionId[minionInstanceID] = value;
            NotifyChanged();
        }

        public void ClearWithSuitOverride(int minionInstanceID)
        {
            if (withSuitOverridesByMinionId.Remove(minionInstanceID)) NotifyChanged();
        }

        public void ClearWithoutSuitOverride(int minionInstanceID)
        {
            if (withoutSuitOverridesByMinionId.Remove(minionInstanceID)) NotifyChanged();
        }

        private void NotifyChanged()
        {
            Trigger(OnRulesChangedHash, this);
        }
    }
}
