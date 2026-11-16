# Stonewards PartyHUD

**See your party's health, classes, and status at a glance.** PartyHUD adds a compact Stonewards-style overlay with teammate tracking, extra health indicators, and host-only build and ping tools.

## ✨ Features

- **Party status** — player names, class icons, current/max HP, and downed status.
- **Teammate tracking** — optional outlines and floating nicknames, with through-wall outlines.
- **Extra health** — Hunter companion, defensive wall, and King HP when available.
- **Build inspector** — host-only ring and tome lists with icons, levels or stack counts, and supported modifiers.
- **Host ping** — available connection latency measurements shown in milliseconds.
- **In-game settings** — adjust visible elements, HUD size and placement, and keyboard shortcuts.

## 🎮 How to use

PartyHUD appears automatically when network players are present. Default shortcuts:

| Key | Action |
| --- | --- |
| `F7` | Open/close PartyHUD settings |
| `F8` | Open/close build inspector (host only) |
| `F9` | Next player in the build inspector |
| `PageUp` / `PageDown` | Previous / next build page |

The **SETTINGS** and **BUILDS** labels in the HUD header are also clickable when the game cursor is unlocked. Build inspection and player latency are host-only. Unsupported upgrade modifiers display `—` instead of an estimate.

## ⚙️ Configuration

Press `F7` for the **DISPLAY**, **LAYOUT**, and **HOTKEYS** tabs. Toggle teammate outlines and names, adjust the HUD's position and scale, and rebind all five shortcuts (including Ctrl/Shift/Alt combinations). Changes are saved automatically.

Additional options are available in `BepInEx/config/community.stonewards.partyhud.cfg` inside your active mod-manager profile. Teammate outlines, names, through-wall outlines, and automatic resolution scaling are enabled by default.

## 📦 Installation

Install [PartyHUD](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/) using **r2modman** or another compatible Thunderstore mod manager, then launch Stonewards through **Start modded**. The manager installs the required **BepInExPack 5** dependency.

PartyHUD runs client-side; other players do not need to install it for your overlay to work.

## 🔗 Links

[GitHub](https://github.com/thereisnouser/stonewards-party-hud) · [Report an issue](https://github.com/thereisnouser/stonewards-party-hud/issues) · [Changelog](https://github.com/thereisnouser/stonewards-party-hud/blob/main/CHANGELOG.md)

---

*Made by thereisnouser*
