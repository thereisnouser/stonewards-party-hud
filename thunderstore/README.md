# Stonewards PartyHUD

**See your party's health, classes, and status at a glance.** PartyHUD adds a compact Stonewards-style overlay with teammate tracking, extra health information, and host-only build and ping tools.

## ✨ Features

- **Party status** — player names, class icons, current/max HP, and downed state.
- **Teammate tracking** — outlines and floating names, optionally visible through walls.
- **Extra health** — Hunter companion, defensive wall, and King health when available.
- **Host tools** — view teammates' rings, rarities, pick counts, and available ping measurements.
- **In-game settings** — customize displayed elements, panel size/position, and shortcuts.
- **Screen-aware layout** — automatic resolution scaling with adjustable offsets.

## 🎮 How to use

The HUD appears automatically when players join a session. Use these default shortcuts:

| Key | Action |
| --- | --- |
| `F7` | Open/close PartyHUD settings |
| `F8` | Open/close ring builds (host only) |
| `F9` | Next player in the build inspector |
| `PageUp` / `PageDown` | Previous / next ring page |

The **SETTINGS** and **BUILDS** labels in the HUD header can also be clicked when the game cursor is unlocked.

## ⚙️ Configuration

Press `F7` to access **DISPLAY**, **LAYOUT**, and **HOTKEYS**. Choose which elements appear, adjust the panel's position and scale, and rebind all five shortcuts (including Ctrl/Shift/Alt combinations). Settings are saved automatically.

Additional options are stored in `BepInEx/config/community.stonewards.partyhud.cfg` inside your active mod-manager profile.

## 📦 Installation

Install [PartyHUD](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/) using **r2modman** or another compatible Thunderstore mod manager, then launch Stonewards through **Start modded**. The required BepInExPack dependency is installed automatically by the mod manager.

## 🔧 Compatibility

- **Game:** Stonewards
- **Mod loader:** BepInEx 5
- **Multiplayer:** Client-side; other players do not need PartyHUD. Ring inspection and per-player connection latency are host-only.

## ⚠️ Known limitations

King HP is shown only when readable King health data is available in the installed game version. Ping is shown only when the host has a valid connection measurement; guests cannot inspect teammates' ring builds.

## 🔗 Links

[GitHub](https://github.com/thereisnouser/stonewards-party-hud) · [Report an issue](https://github.com/thereisnouser/stonewards-party-hud/issues) · [Changelog](https://github.com/thereisnouser/stonewards-party-hud/blob/main/CHANGELOG.md)

---

*Made by thereisnouser*
