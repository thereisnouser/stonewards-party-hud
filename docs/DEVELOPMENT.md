# Developer setup

For contributors testing from source. Players should install the published version using a compatible Thunderstore manager.

## Prerequisites

- Stonewards (Windows x64, Unity Mono)
- BepInEx 5 available either in the game directory or in the chosen r2modman/Gale profile
- .NET SDK supporting `netstandard2.1`
- PowerShell 5.1+

Do not commit or distribute Stonewards assemblies or BepInEx binaries.

## Build and install locally

Close Stonewards, open PowerShell in the repository root, and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 -GameDir "C:\path\to\Steam\steamapps\common\Stonewards"
```

The script builds against the locally installed game and BepInEx assemblies and installs the DLL only when compilation succeeds.

### Test a development build with r2modman

Use a **separate test profile** with BepInExPack installed. Temporarily disable the published PartyHUD package in that profile so that only one copy of the plugin can load. In r2modman, use **Settings → Locations → Browse profile folder** and copy the full folder path.

Close the game, then from the repository root run:

```powershell
git pull
powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 -GameDir "F:\Steam\steamapps\common\Stonewards" -ProfileDir "<full r2modman profile folder path>"
```

This uses `<profile>/BepInEx/core/BepInEx.dll` for compilation and installs the current DLL in `<profile>/BepInEx/plugins/StonewardsPartyHUD/`, **not** in the Steam game directory. Launch through **Start modded** with that same profile, or use Steam if you have configured its launch options to launch that exact profile. **When you switch r2modman profiles, update Steam launch options too**; otherwise Steam can silently load an older profile/mod build. A missing profile BepInEx DLL is a setup error, not a reason to copy mod files into Steam.

When finished, delete only the development-plugin folder `<profile>/BepInEx/plugins/StonewardsPartyHUD/` and re-enable the published PartyHUD package. A published Thunderstore package version is immutable; always package a newly built DLL with the matching version number.

## Prepare a Thunderstore release

1. Pull the intended commit and build/install it against the current game version.
2. Verify the mod in a multiplayer session, including menu visibility and the player roster.
3. Package the locally built DLL:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\package-thunderstore.ps1
   ```

4. Examine `dist/PartyHUD-<version>.zip`. The archive root must contain `manifest.json`, `icon.png`, `README.md`, and `plugins/StonewardsPartyHUD.dll` (plus `LICENSE`). The plugin must match the source that was tested.
5. Smoke-test the archive as a local mod in a fresh r2modman or Gale profile. Local ZIP imports may not automatically install dependencies, so add BepInExPack to the test profile explicitly.
6. Check the package description, version, icon, current game compatibility and licensing before publishing.
7. Upload the verified ZIP to the Stonewards community under the publisher's authorized Thunderstore team. Test a normal manager installation **from Thunderstore** after publishing, including dependency installation.

No automated CI build is configured because the project compiles against assemblies from the user's locally installed game. Do not commit proprietary binaries just to enable CI.

The packing script creates a ZIP only; it does not publish or tag releases. Thunderstore package versions cannot be overwritten: bump `packaging/manifest.json` for each subsequent upload.

## Current status and in-game menu test checklist

The v0.3.2 source now contains a built-in **F7** settings menu with three tabs: **DISPLAY**, **LAYOUT**, and **HOTKEYS**. Its five shortcuts use BepInEx `KeyboardShortcut` values and are persisted in the active profile config. The repository implementation is **not a verified gameplay release** until a local game build and interactive tests have passed; do not publish a new Thunderstore package based only on source inspection.

Use a separate r2modman testing profile with its own BepInExPack and **disable the installed Thunderstore PartyHUD package in that profile**. Build with `-ProfileDir`, then launch that exact profile via **Start modded**. Never test by overwriting the regular playing profile.

### Required gameplay checks before the next release

- [ ] **Compilation and plugin load:** `dotnet build` succeeds, the script prints the expected DLL SHA256, and `BepInEx/LogOutput.log` contains `Party HUD v0.3.2 initialized.` in the test profile.
- [ ] **Menu lifecycle:** F7 opens and closes settings; the **X** button closes it; cursor becomes usable inside settings and returns to the game's prior lock/visibility state when closing.
- [ ] **Other game screens:** when opening inventory, shop, character selection, upgrades, quests or loading screens, PartyHUD settings close or stay hidden, and the game keeps control of its own cursor.
- [ ] **DISPLAY:** each toggle immediately changes the corresponding live behavior and remains saved after restarting the same profile.
- [ ] **LAYOUT:** sliders, anchor buttons, resolution scaling and native-roster avoidance behave correctly at 1920×1080 and another resolution; values persist across restart.
- [ ] **HOTKEYS:** rebind F7/F8/F9/PageUp/PageDown to distinct keys and Ctrl/Shift/Alt combinations, verify actions, then restore defaults. Escape cancels key capture without changing a binding; an already-used shortcut is rejected. The menu remains closable via **X** even after rebinding F7.
- [ ] **Regression:** host-only ring inspector, player health, wall health, teammate outlines, nicknames, Hunter companion and ping still work in a two-player game, and the passive HUD does not intercept mouse input.
- [ ] **Release:** only after the gameplay checks succeed, build and inspect the matching ZIP and publish the tested version through the authorized Thunderstore account.

Legacy verification: earlier playtests established the v0.2.x baseline (HUD and scale/ping behavior). They do **not** establish that the new v0.3.2 in-game menu has been tested.
