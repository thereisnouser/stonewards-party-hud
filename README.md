# Stonewards PartyHUD

**Keep your party's health, classes, and status in view while playing Stonewards.** PartyHUD adds a compact, game-styled overlay with teammate tracking, extra health information, and host-only tools for checking builds and connection latency.

## ✨ Features

- **Party status** — player names, class icons, current/max HP, and downed state in one panel.
- **Teammate tracking** — optional outlines and floating names, including visibility through walls.
- **Extra health** — Hunter companion health, defensive wall durability, and King HP when the game exposes it.
- **Host tools** — inspect teammates' rings, rarities, and pick counts; see available connection RTT measurements in milliseconds.
- **In-game settings** — change display options, adjust HUD size and placement, and rebind shortcuts without leaving the game.
- **Adaptive layout** — resolution-aware scaling, configurable offsets, and a compact interface styled after Stonewards.

## 🎮 How to use

The party panel appears automatically when players are present in a session. It shows information available to your own game client; you do not need the other players to install PartyHUD.

### Controls

| Default key | Action |
| --- | --- |
| `F7` | Open or close PartyHUD settings |
| `F8` | Open or close the ring build inspector (host only) |
| `F9` | Select the next player in the build inspector |
| `PageUp` | Show the previous ring page |
| `PageDown` | Show the next ring page |

The HUD also includes **SETTINGS** and **BUILDS** shortcuts in its header. They can be clicked when the in-game cursor is already unlocked. The build inspector and per-player ping information are available only to the host.

## ⚙️ Configuration

Press `F7` to open the built-in settings menu:

- **DISPLAY** — enable or disable HUD elements, teammate outlines, health indicators, and other display options.
- **LAYOUT** — adjust panel placement, offsets, and size.
- **HOTKEYS** — rebind all five shortcuts, including Ctrl, Shift, and Alt combinations; restore the defaults when needed.

Settings are saved to the active BepInEx profile. Advanced options can also be edited in `BepInEx/config/community.stonewards.partyhud.cfg`.

Selected defaults:

| Option | Default |
| --- | --- |
| Panel anchor | Top left |
| Left and top offsets | 8 pixels each |
| Automatic resolution scaling | Enabled |
| Companion, wall, and King health | Enabled, when data is available |
| Teammate outlines and names | Enabled |
| Through-wall outlines | Enabled |

The HUD hides or yields to supported in-game menus so it does not obstruct normal menu interaction.

## 📦 Installation

Install [PartyHUD on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/) using **r2modman** or another compatible mod manager. The manager installs the declared BepInExPack dependency. Launch Stonewards using **Start modded**.

Only the player who wants the overlay needs to install it. If you are using a custom development build, disable the published package in that profile to avoid loading two copies of PartyHUD.

## 🔧 Compatibility

- **Game:** Stonewards
- **Mod loader:** BepInEx 5 (Thunderstore dependency: BepInExPack 5.4.2305)
- **Multiplayer:** Client-side installation; teammates do not need the mod. Build inspection and per-player RTT are host-only.

## ⚠️ Known limitations

- **King health** is displayed only when the installed game build exposes readable current/max King HP data.
- **Connection ping** uses measurements available to the host; a value is omitted if the game/network layer does not provide a valid sample.
- **Ring inspection** uses host-side run data, so ordinary clients cannot inspect other players' builds through this mod.

## 🛠️ Development

PartyHUD is a BepInEx 5 plugin targeting `.NET Standard 2.1`. It reads available Stonewards and Mirror multiplayer state to populate the overlay. It does not send additional gameplay network messages or edit player save files. Teammate outlines and the settings menu are drawn locally.

To build from source, you need Stonewards installed locally, BepInEx 5 in your chosen profile, the .NET SDK, and PowerShell. Clone the repository, open PowerShell in its root, then run:

```powershell
$gameDir = "C:\path\to\Stonewards"
$profileDir = "C:\path\to\r2modman\Stonewards\profiles\stonewards-party-hud"

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

Replace both example paths with existing directories. You can find the real profile path in r2modman under **Settings → Locations → Browse profile folder**. Close Stonewards before replacing its loaded DLL, and disable the Thunderstore PartyHUD package in your development profile.

For build verification, packaging instructions, and the gameplay test checklist, see [Developer setup](docs/DEVELOPMENT.md).

## 📄 License

Distributed under the [MIT License](LICENSE).

## 🔗 Links

[Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/) · [Report an issue](https://github.com/thereisnouser/stonewards-party-hud/issues) · [Changelog](CHANGELOG.md)

---

*Made by thereisnouser*
