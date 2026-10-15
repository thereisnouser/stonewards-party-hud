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
- **Custom hotkeys:** rebind all four inspector actions using the BepInEx config file, including Ctrl/Shift/Alt combinations.
- **Fits your screen:** resolution-proportional scaling and configurable panel position.

**Client-side only:** teammates do not need to install the mod.

## Installation

Install [PartyHUD on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/) with [r2modman](https://thunderstore.io/c/stonewards/p/ebkr/r2modman/) or another compatible manager, then launch **modded**. Required dependencies are installed automatically.

## Configure hotkeys

After launching the mod once, open the r2modman **Config editor** and edit `community.stonewards.partyhud.cfg`, section `[Hotkeys]`. You can also edit the file directly in your active profile at `BepInEx/config/community.stonewards.partyhud.cfg`.

```ini
[Hotkeys]
ToggleBuildInspector = F8
NextBuildPlayer = F9
PreviousBuildPage = PageUp
NextBuildPage = PageDown
```

To use a key combination, place the main key first, followed by modifiers (for example, `ToggleBuildInspector = B + LeftControl`). Save and restart the game after changing the config file. The inspector is still **host-only**; other players do not need the mod.

If you have the optional **BepInEx ConfigurationManager**, it can also edit the bindings from inside the game. PartyHUD does not require ConfigurationManager.

## Links

[Issues & feedback](https://github.com/thereisnouser/stonewards-party-hud/issues) · [Developer setup](https://github.com/thereisnouser/stonewards-party-hud/blob/main/docs/DEVELOPMENT.md) · [MIT License](https://github.com/thereisnouser/stonewards-party-hud/blob/main/LICENSE)
