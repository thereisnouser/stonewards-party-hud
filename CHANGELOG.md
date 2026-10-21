# Changelog

## 0.4.2

### Added
- Rebuilt the host-only Build Inspector around an inventory-style view with separate **RINGS** and **TOMES** sections.
- Display upgrade icons, names, levels or stack counts, and cumulative stat modifiers for supported upgrade effects.
- Show unknown upgrades separately; resize and paginate the inspector to fit the available content.

### Improved
- Slightly enlarged modifier and level text (native font sizes 16 and 17) without changing normal HUD health text.

### Notes
- Unsupported or nonlinear upgrade effects show `—` instead of an unverified numeric value.
- The build inspector and connection ping remain host-only; teammates do not need this mod.
- Final multiplayer/inventory correctness and packaged Thunderstore installation still require gameplay verification.

## 0.4.1

### Changed
- Standardized the GitHub and Thunderstore descriptions for PartyHUD.
- Packaged a dedicated, player-focused Thunderstore README.
- No gameplay or configuration changes compared with 0.4.0.

## 0.4.0

### Added
- Built-in **Party HUD Settings** menu with **DISPLAY**, **LAYOUT**, and **HOTKEYS** tabs (F7 by default).
- In-game rebinding for all five shortcuts, including Ctrl/Shift/Alt combinations, duplicate-key warnings, and a restore-defaults action.
- Optional King health display when the current game exposes readable health data.

### Improved
- Compact SETTINGS and BUILDS action hints in the party header with updated shortcuts, active-state highlighting, and optional mouse clicks when the game cursor is already unlocked.
- Stonewards-style gray setting rows and sliders, numeric fields, and one-pixel position adjustment buttons.
- Contextual explanations for display options and immediate BepInEx config saving.
- HUD positioning, scaling, and preservation of saved offsets across resolution changes.
- Settings menu cursor and gameplay input handling, with better behavior around game menus.

### Notes
- The build inspector and connection ping display remain host-only.
- PartyHUD is client-side: teammates do not need the mod.
- King health detection depends on what the installed Stonewards version exposes.
