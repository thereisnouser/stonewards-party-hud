# Changelog

## 0.3.2

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
