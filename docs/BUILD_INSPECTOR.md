# Build Inspector inventory view (PartyHUD 0.5.0)

The host-only Build Inspector shows **RINGS**, **TOMES** and (when needed) **UNKNOWN UPGRADES**. Each row is a single line containing an in-game icon, name, calculated modifier (when supported) and a level/stack count. The expanded panel is 540 logical pixels wide, uses 38-pixel item rows, adjusts its height to visible content and paginates longer inventories.

## Game data and limits

Implementation reads existing host-owned `FirstPersonController.RunPlayerData.rogueUpgrades` state and does not add network messages, gameplay patches or save mutations. The game's `RogueUpgradeData` contains an upgrade ID and rarity, not a ready-made aggregate modifier. The implementation groups repeated picks using the definition's category: `LEVELUP` to RINGS and `PICKUP` to TOMES.

For supported `UpgradeCharacterStatEffect` entries, cumulative item contribution is derived from saved picks and rarity multipliers. It is **not** the teammate's total player stat. Unsupported or nonlinear effects display `—` rather than using the local host's stat or a fabricated bonus. Unknown upgrade IDs remain visible for diagnosis. These findings were based on an October 2026 game assembly and need rechecking when Stonewards changes.

## Fonts and rendering

The modifier uses its own font style (native size 16) and the level/stack count uses a separate style (native size 17), leaving normal health values unchanged. The 540-unit window and single-line entries replace the old rarity/pick-list layout.

## Release checks

The user compiled the equivalent implementation locally as v0.4.2 with zero warnings/errors before it was renumbered for release. A fresh v0.5.0 DLL and package still require compilation and in-game testing.

- [ ] With host + teammate, confirm **BUILDS/F8** lists rings/tomes correctly and normal guests cannot inspect other players.
- [ ] Compare ring levels, tome stacks and calculated bonuses with the in-game inventory, including repeated different rarities and non-stackable tomes.
- [ ] Confirm special effects show `—` when no safe aggregate value is available, with unknown IDs clearly listed.
- [ ] Check 1-item, 8-item and long inventories; section headings and rows should paginate without clipping.
- [ ] Test 1920×1080 and 2560×1440 for modifier/level readability, icon overlap and window fit.
- [ ] Check teammate HP, King/wall durability, companion HP, outlines, ping, settings and hotkeys for regressions.
- [ ] Build a fresh v0.5.0 DLL and package via `tools/package-thunderstore.ps1`; inspect the ZIP before publishing.
