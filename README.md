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

The **Robot** section header is hidden on these checkpoints; robots ignore atmo / oxygen-mask checkpoints by default and walk past in whatever state they're in.

The **Bionic** section is configurable via the in-game options menu (**Mods → Better Checkpoints → Options**):

| Setting | Behaviour |
| --- | --- |
| **Default** *(default since v1.2)* | Bionic dupes behave like Standard — they appear in the side screen, equip on entry, and drop on return. |
| **Bypass** | Bionic dupes ignore the checkpoint entirely (pre-1.2 behaviour). The Bionic section is hidden. |

**Lead Suit Checkpoints** (and any other vanilla `SuitMarker` variant) keep fully unmodified vanilla behaviour — Bionic header still visible, equip-on-entry / drop-on-exit reactables fire normally.

## Compatibility

- Vanilla and Spaced Out (DLC1).
- Save-safe — works on existing saves; no new game required.
- DLC3 (Bionic Booster Pack) and other DLCs: should work; non-Standard duplicants are handled explicitly.

### Known mod conflict: Ony's Mod Manager

Saving the **Better Checkpoints** options dialog with **Ony's Mod Manager** active can trigger a `NullReferenceException` in `Ony.OxygenNotIncluded.ModManager.ModVisualBuilder.SortButtons` — Ony's mod-list refresh coroutine retains a destroyed `GameObject` reference. To work around this, Better Checkpoints **automatically restarts ONI** as soon as you change the Bionic Duplicants dropdown and click OK; the restart preempts Ony's coroutine before it can NRE. The dialog shows an inline notice explaining this.

The underlying bug is in Ony's mod, not in PLib or Better Checkpoints — please report to that mod's author if you encounter it elsewhere.

## Installation

### Steam Workshop (recommended once published)

Search for **Better Checkpoints** in the in-game Mods browser, or use the Steam Workshop link (TBD).

### Manual install

1. Download `BetterCheckpoints-vX.Y.Z.zip` from the [Releases page](https://github.com/zjenkins64-star/Oni-Mod-BetterCheckpoints/releases/latest).
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
