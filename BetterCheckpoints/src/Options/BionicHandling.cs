namespace BetterCheckpoints.Options
{
    // Persisted value for the "Bionic Duplicants" option in the mod's
    // POptions dialog. PLib renders it as a dropdown with the enum
    // value names ("Default" / "Bypass") as the visible labels.
    public enum BionicHandling
    {
        // Bionic dupes behave like Standard dupes at customised
        // checkpoints: equip on entry / drop on return, configurable per
        // dupe in the side screen. Default since v1.2.0.
        Default,

        // Bionic dupes ignore customised checkpoints entirely (pre-1.2
        // behaviour): no equip, no drop, no per-dupe row in the side
        // screen.
        Bypass,
    }
}
