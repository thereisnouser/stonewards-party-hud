# Developer setup

For contributors testing from source. Players should install the published version using a compatible Thunderstore manager.

## Prerequisites

- Stonewards (Windows x64, Unity Mono)
- BepInEx 5 installed in the game directory
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

This uses `<profile>/BepInEx/core/BepInEx.dll` for compilation and installs the current DLL in `<profile>/BepInEx/plugins/StonewardsPartyHUD/`, **not** in the Steam game directory. Launch through **Start modded** with that same profile. A missing profile BepInEx DLL is a setup error, not a reason to copy mod files into Steam.

When finished, delete only the development-plugin folder `<profile>/BepInEx/plugins/StonewardsPartyHUD/` and re-enable the published PartyHUD package. A published Thunderstore package version is immutable; do not upload a local development build as v0.1.0.

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

## Verified status

Previous source builds were compiled and installed successfully on Windows, and the user confirmed functioning party HUD, Hunter companion HP, original class icons and teammate outlines in live multiplayer. Subsequent menu-visibility fixes were also tested locally. The corrected ZIP was imported into a clean r2modman profile and Party HUD was confirmed visible in-game. Thunderstore's upload UI reported success for v0.1.0, and the user supplied the [public package listing](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/). Fresh installation **from the public catalog**, host/client edge cases and later game-version compatibility are still pending. For release tracking, see [Issue #4](https://github.com/thereisnouser/stonewards-party-hud/issues/4).
