# Stonewards PartyHUD

**Keep your party's health, classes, and status in view while playing Stonewards.** PartyHUD adds a compact in-game overlay for teammate tracking, extra health indicators, and host-only build and latency information.

## ✨ Features

- **Party status** — player names, class icons, current and maximum HP, and downed status in one panel.
- **Teammate tracking** — optional character outlines and floating nicknames, with an option to show outlines through walls.
- **Extra health** — Hunter companion HP, defensive wall durability, and King HP when that information is available.
- **Build inspector** — host-only inventory-style lists of teammates' rings and tomes, including icons, levels or stack counts, and supported cumulative modifiers.
- **Connection information** — available player latency measurements shown to the host in milliseconds.
- **In-game customization** — display toggles, HUD placement and scaling, and rebindable keyboard shortcuts.

## 🎮 How to use

PartyHUD appears automatically when network players are present. The overlay is local to your game; teammates do not need to install the mod.

### Controls

| Default key | Action |
| --- | --- |
| `F7` | Open or close PartyHUD settings |
| `F8` | Open or close the build inspector (host only) |
| `F9` | Select the next player in the build inspector |
| `PageUp` | Show the previous build page |
| `PageDown` | Show the next build page |

The **SETTINGS** and **BUILDS** labels in the HUD header are clickable when the game cursor is already unlocked. Only the session host can inspect teammates' builds or see the host-side connection measurements. Unsupported upgrade modifiers display `—` rather than an estimated value.

## ⚙️ Configuration

Press `F7` to open the built-in settings menu:

- **DISPLAY** — toggle HUD elements, teammate outlines and names, and extra health indicators.
- **LAYOUT** — adjust HUD size, anchor, and position.
- **HOTKEYS** — rebind all five shortcuts, including Ctrl, Shift, and Alt combinations, or restore their defaults.

Changes are saved to your current BepInEx profile. Additional options are available in `BepInEx/config/community.stonewards.partyhud.cfg`.

| Setting | Default |
| --- | --- |
| Panel anchor | Top left |
| Left and top offsets | 8 px each |
| Automatic resolution scaling | Enabled |
| Companion, wall, and King health | Enabled when data is available |
| Teammate outlines and floating names | Enabled |
| Outlines through walls | Enabled |

## 📦 Installation

### Mod manager

Install [PartyHUD on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/) with **r2modman** or another compatible Thunderstore mod manager, then launch Stonewards through **Start modded**. The manager installs the declared **BepInExPack 5** dependency.

PartyHUD is installed client-side: only players who want its overlay need it. If testing a custom build, disable the published PartyHUD package in that profile to avoid loading duplicate plugins.

## 🛠️ Development

PartyHUD is a **BepInEx 5** plugin targeting **.NET Standard 2.1**. It reads locally available Stonewards and Mirror multiplayer data to render the HUD and uses the game's `QuickOutline` component for optional teammate outlines.

### Build from source

You need a local Stonewards installation, the **.NET SDK**, **PowerShell**, and **BepInEx 5** installed in the game directory or your selected mod-manager profile. Close the game, clone the repository, and run this from its root:

```powershell
$gameDir = "C:\path\to\Stonewards"
$profileDir = "C:\path\to\r2modman\Stonewards\profiles\stonewards-party-hud"

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

Replace the example paths with existing directories. Install BepInExPack in the test profile and disable its published PartyHUD package before installing the development DLL. For package creation and test checks, see [Developer setup](docs/DEVELOPMENT.md).

## 📄 License

Distributed under the [MIT License](LICENSE).

## 🔗 Links

[Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/) · [Report an issue](https://github.com/thereisnouser/stonewards-party-hud/issues) · [Changelog](CHANGELOG.md)

---

*Made by thereisnouser*
