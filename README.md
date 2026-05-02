# Better Checkpoints

An [Oxygen Not Included](https://www.klei.com/games/oxygen-not-included) mod that adds **per-duplicant access permissions** to Atmo Suit and Oxygen Mask Checkpoints — directly inside the same Access Permissions side screen vanilla doors already use.

## What it does

For each Standard duplicant, the side screen on an Atmo Suit or Oxygen Mask Checkpoint now has three checkbox columns:

| Column | Behaviour |
| --- | --- |
| **With Suit** | Duplicant passes wearing a suit. Mutually exclusive with Without Suit. |
| **Without Suit** | Duplicant passes without a suit. Mutually exclusive with With Suit. |
| **Restrict use** | Duplicant is blocked from passing at all (pathfinder-level — they re-path automatically). |

Direction permissions, the dupe list, portraits, and group sections come straight from vanilla — nothing is rewritten.

The **Bionic** and **Robot** section headers are hidden on these checkpoints; non-Standard duplicants ignore atmo / oxygen-mask checkpoints by default (no suit equip on entry, no drop on exit), so they walk past in whatever state they're in.

**Lead Suit Checkpoints** (and any other vanilla `SuitMarker` variant) keep fully unmodified vanilla behaviour — Bionic header still visible, equip-on-entry / drop-on-exit reactables fire normally.

## Compatibility

- Vanilla and Spaced Out (DLC1).
- Save-safe — works on existing saves; no new game required.
- DLC3 (Bionic Booster Pack) and other DLCs: should work; non-Standard duplicants are handled explicitly.

## Installation

### Steam Workshop (recommended once published)

Search for **Better Checkpoints** in the in-game Mods browser, or use the Steam Workshop link (TBD).

### Manual install

1. Download `BetterCheckpoints-v1.1.0.zip` from the [`dist/`](dist/) folder of this repo (or the [Releases page](https://github.com/zjenkins64-star/Oni-Mod-BetterCheckpoints/releases) once a release is cut).
2. Extract the archive into your local mods folder so you end up with a `Local\BetterCheckpoints\` directory:
   - **Windows:** `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Local\`
   - **Mac:** `~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local/`
3. Launch ONI → Main Menu → **Mods** → enable **Better Checkpoints** → restart when prompted.

## Building from source

Requires Oxygen Not Included installed (the project auto-detects the game's `Managed` folder from common Steam paths) and the **.NET Framework 4.8** SDK.

```powershell
git clone https://github.com/zjenkins64-star/Oni-Mod-BetterCheckpoints.git
cd Oni-Mod-BetterCheckpoints/BetterCheckpoints
dotnet build
```

The post-build target installs the merged DLL straight into your local mods folder so it's ready to test. Override paths via env vars if needed:

- `ONI_INSTALL` — root of your ONI install (the folder containing `OxygenNotIncluded_Data` on Windows).
- `ONI_MODS_DIR` — folder containing your `Local` mods directory.

PLib is bundled into the output DLL via ILRepack, so the mod doesn't conflict with other mods that ship their own PLib copy.

## Credits

- [PLib](https://github.com/peterhaneve/ONIMods/tree/main/PLib) by Peter Han — UI helpers + library merging.
- [Lib.Harmony](https://github.com/pardeike/Harmony) by Andreas Pardeike — runtime patching.
