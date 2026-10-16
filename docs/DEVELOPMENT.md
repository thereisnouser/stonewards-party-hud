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

4. Examine `dist/PartyHUD-<version>.zip`. The archive root must contain `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`, and `plugins/StonewardsPartyHUD.dll` (plus `LICENSE`). The plugin must match the source that was tested; the packager checks the freshness of **all** project C# source files and validates the matching changelog entry.
5. Smoke-test the archive as a local mod in a fresh r2modman or Gale profile. Local ZIP imports may not automatically install dependencies, so add BepInExPack to the test profile explicitly.
6. Check the package description, version, icon, current game compatibility and licensing before publishing.
7. Upload the verified ZIP to the Stonewards community under the publisher's authorized Thunderstore team. Test a normal manager installation **from Thunderstore** after publishing, including dependency installation.

No automated CI build is configured because the project compiles against assemblies from the user's locally installed game. Do not commit proprietary binaries just to enable CI.

The packing script creates a ZIP only; it does not publish or tag releases. Thunderstore package versions cannot be overwritten: bump `packaging/manifest.json` for each subsequent upload.

## Current status and in-game menu test checklist

The v0.3.2 source contains a built-in **F7** settings menu with three tabs: **DISPLAY**, **LAYOUT**, and **HOTKEYS**. Its five shortcuts use BepInEx `KeyboardShortcut` values and are persisted in the active profile config. The user confirmed button functionality, supplied in-game screenshots and approved the latest native-style slider appearance. **A fresh installation of the packaged release and the full multiplayer checklist remain unverified.** Do not publish the new Thunderstore package based solely on visual approval.

Use a separate r2modman testing profile with its own BepInExPack and **disable the installed Thunderstore PartyHUD package in that profile**. Build with `-ProfileDir`, then launch that exact profile via **Start modded**. Never test by overwriting the regular playing profile.

### Required gameplay checks before the next release

- [ ] **Compilation and plugin load:** `dotnet build` succeeds, the script prints the expected DLL SHA256, and `BepInEx/LogOutput.log` contains `Party HUD v0.3.2 initialized.` in the test profile.
- [ ] **Menu lifecycle and input:** F7 opens and closes settings; the **X** button closes it; while the menu is open, moving/clicking the mouse and pressing WASD must not aim, attack or move the character. When it closes, gameplay controls and the cursor's previous state return immediately.
- [ ] **Other game screens:** when opening inventory, shop, character selection, upgrades, quests or loading screens, PartyHUD settings close or stay hidden, and the game keeps control of its own cursor.
- [ ] **DISPLAY:** each toggle immediately changes the corresponding live behavior and remains saved after restarting the same profile. Hover over both a label and its toggle; its description should appear in the settings footer without overlapping the last row.
- [ ] **LAYOUT:** sliders, one-pixel **− / +** controls, anchor buttons and resolution scaling behave correctly at 1920×1080 and another resolution. Verify both offset sliders can reach the visible screen edges (not capped at 460 px), the panel stays onscreen, and a resolution change alone does not overwrite saved offsets. Manual offsets take priority over the native player roster. Restart the profile to check persistence.
- [ ] **HOTKEYS:** rebind F7/F8/F9/PageUp/PageDown to distinct keys and Ctrl/Shift/Alt combinations, verify actions, then restore defaults. Escape cancels key capture without changing a binding; an already-used shortcut is rejected. The menu remains closable via **X** even after rebinding F7.
- [ ] **HUD header actions:** SETTINGS and BUILDS are compact one-line hints with a soft highlight and thin underline, still clickable with an unlocked cursor. The header should be 13 logical pixels shorter than the previous build, without hiding the player count, DOWN warning, or player/wall/King health. Check rebound shortcut labels at several resolutions, including long combinations. BUILDS shows HOST ONLY on clients and DISABLED when switched off. Hotkeys still work in locked-cursor gameplay; independently unlocked mouse clicks must not interfere with unrelated gameplay. Other settings and build-inspector window headers retain their original height.
- [ ] **KING HP:** in an active run, the KING row renders below WALL when the game provides current/max health; the SETTINGS / BUILDS actions remain in the header. Compare numeric values against the game's own King HUD before and after damage/healing, on both host and client; verify row disappears safely when the King object is absent (hub/scene transition), and toggling King health OFF removes only the King row. If the row is absent in a run, inspect the current Assembly-CSharp.dll King health component before publishing; the current runtime discovery is an unverified compatibility fallback.
- [ ] **Regression:** host-only ring inspector, player health, wall health, teammate outlines, nicknames, Hunter companion and ping still work in a two-player game, and the passive HUD does not intercept mouse input.
- [ ] **Release:** only after the gameplay checks succeed, build and inspect the matching ZIP and publish the tested version through the authorized Thunderstore account.

Legacy verification: earlier playtests established the v0.2.x baseline (HUD and scale/ping behavior). They do **not** establish that the new v0.3.2 in-game menu has been tested.
