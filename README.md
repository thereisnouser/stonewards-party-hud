# Stonewards Party HUD

A lightweight, client-side multiplayer HUD for **Stonewards**. Shows every connected player's name, class, current/max health and downed state in one persistent panel.

> **Status:** 0.1.0-alpha (source preview). Not yet compiled or verified in-game. Compatibility with multiplayer sessions must be tested.

## Requirements

- Stonewards for Windows (Unity Mono build)
- BepInEx 5 (Windows x64), installed in the game directory
- .NET SDK with support for `netstandard2.1` and PowerShell (to build)

## Build and install

1. Install BepInEx 5 and launch/close the game once.
2. Close Stonewards and open PowerShell in this repository.
3. Run:

   ```powershell
   .\build-and-install.ps1 -GameDir "C:\path\to\Steam\steamapps\common\StoneWards"
   ```

4. Launch the game and join a session.

Uninstall: remove `BepInEx/plugins/StonewardsPartyHUD/`.

## Configuration

On first launch, BepInEx generates `BepInEx/config/community.stonewards.partyhud.cfg`. Available settings:

- `General.Enabled` — show or hide the HUD
- `Appearance.Scale` — scale the panel (0.65–1.8)
- `Appearance.LeftOffset` and `Appearance.BottomOffset` — panel position

Restart the game after changing settings.

## Project

The mod reads client-available game state without modifying gameplay or sending network messages. No game assemblies or third-party binaries are distributed.

For current progress, known limitations and next steps, see [GitHub Issues](https://github.com/thereisnouser/stonewards-party-hud/issues).

MIT-licensed. See [LICENSE](LICENSE).
