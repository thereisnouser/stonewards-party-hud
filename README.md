# Stonewards Party HUD

A lightweight, **client-side** party HUD for **Stonewards**, styled to fit the game's pixel-art UI. See your team's status at a glance without searching the battlefield.

## Preview

![Illustration of the Party HUD with fictional player names](https://raw.githubusercontent.com/thereisnouser/stonewards-party-hud/main/packaging/icon.png)

*Illustrative preview — player names are fictional.*

## Features

- **Party status:** player names, class icons, current/max HP and downed state.
- **Teammate tracking:** optional outlines and floating nicknames, including through walls.
- **Extra health bars:** Hunter's bear companion and defensive wall health.
- **Host ping:** the host can see measured connection latency for teammates (in ms).
- **Host ring inspector:** inspect teammates' selected rings, rarities and pick counts. The default shortcuts are **F8** (open/close), **F9** (next player), **PageUp** (previous page) and **PageDown** (next page).
- **In-game settings:** press **F7** to open the built-in Stonewards-style settings menu. Configure visible HUD features, scaling, position, and all five hotkeys without installing another mod.
- **Discoverable HUD actions:** the bottom of the party panel shows **SETTINGS** and **BUILDS** with their currently assigned shortcuts (F7 / F8 by default). BUILDS is host-only and displays **HOST ONLY** on clients or **DISABLED** when turned off. The hints stay visible during normal gameplay without unlocking the cursor; when the game has already released the pointer, the buttons can also be clicked.
- **Custom hotkeys:** click a binding under **HOTKEYS** and press a new key or Ctrl/Shift/Alt combination; changes are saved automatically.
- **Fits your screen:** resolution-proportional scaling and configurable panel position.

**Client-side only:** teammates do not need to install the mod.

## Installation

Install [PartyHUD on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/) with [r2modman](https://thunderstore.io/c/stonewards/p/ebkr/r2modman/) or another compatible manager, then launch **modded**. Required dependencies are installed automatically.

## In-game settings and key bindings

Press **F7** while playing to open **PARTY HUD SETTINGS**. No additional mod is required.

- **DISPLAY:** toggle the panel, teammate outlines and nicknames, through-wall outlines, host ping, Hunter bear and wall HP, and host ring inspector.
- **LAYOUT:** adjust panel size, horizontal/vertical offsets measured in screen pixels, top/bottom anchoring, and resolution scaling. By default, the HUD sits **8 px from the top and left screen edges** (or 8 px from the bottom if bottom-anchored). Manual positioning always takes priority, even if the panel overlaps Stonewards' built-in player list.
- **HOTKEYS:** click an action, then press a key (or hold Ctrl/Shift/Alt while pressing it). Press **Esc** to cancel capturing. **RESTORE DEFAULT HOTKEYS** resets the five shortcuts. You can always click **X** to close settings.

Changes take effect immediately and are saved to the current BepInEx profile. Shortcut labels in the HUD reflect your choices. The build inspector remains **host-only**, but the settings menu is available to every PartyHUD user.

You can still edit shortcuts in r2modman's **Config editor** (`community.stonewards.partyhud.cfg`) or in `BepInEx/config/community.stonewards.partyhud.cfg` inside the active profile:

```ini
[Hotkeys]
ToggleSettings = F7
ToggleBuildInspector = F8
NextBuildPlayer = F9
PreviousBuildPage = PageUp
NextBuildPage = PageDown
```

For example, `ToggleSettings = B + LeftControl` uses **Ctrl+B**. Restart the game after changing the config file externally. The optional BepInEx ConfigurationManager can also manage hotkeys, but PartyHUD does not require it.

## Links

[Issues & feedback](https://github.com/thereisnouser/stonewards-party-hud/issues) · [Developer setup](https://github.com/thereisnouser/stonewards-party-hud/blob/main/docs/DEVELOPMENT.md) · [MIT License](https://github.com/thereisnouser/stonewards-party-hud/blob/main/LICENSE)
