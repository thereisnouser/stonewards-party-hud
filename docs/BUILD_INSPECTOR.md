# Build Inspector inventory view (development branch)

## Scope

This work-in-progress branch keeps the existing client-side Party HUD and host-only Build Inspector, but replaces the old rarity-variant/pick-count presentation with **RINGS** and **TOMES** sections. Each compact row displays **sprite | name | cumulative item modifier | level**, without last-choice descriptions or rarity text. Repeated level-up upgrades display `LVL N`; repeated pickup upgrades display `xN`. Any unknown IDs appear under **UNKNOWN UPGRADES** instead of being silently misclassified.

The code continues to rely on **host-owned** `FirstPersonController.RunPlayerData.rogueUpgrades`, without network messages, Harmony patches or mutations of player data. The host-only restriction is unchanged.

## Game data investigated

Inspected the user-supplied `Assembly-CSharp.dll` dated **2026-10-13**. These observations refer to that precise game assembly; recheck after game patches:

- `RogueUpgradeData` has only `upgradeID` and `rarity`; it has no level or cumulative modifier field.
- `NetworkHelper.ServerSaveRogueUpgrade` appends **one** `RogueUpgradeData` per acquired improvement to `RunPlayerData.rogueUpgrades`.
- `RogueLikeUpgradeManager.ActiveRunUpgrade.Apply` increases `Level` for category `LEVELUP`. For `PICKUP`, it increases `StackCount` only if the definition is stackable.
- `RogueLikeUpgradeSO.UpgradeCategory` distinguishes `LEVELUP` and `PICKUP`. This branch uses `LEVELUP` for the requested **RINGS** section, and `PICKUP` for **TOMES**. Internal inventory names (for example, `GetTomes()`) are not the presentation contract and should be checked visually in game.
- `RogueLikeUpgradeSO.GetLocalizedDescriptionWithRarity(Rarity)` describes **one** rarity choice, so it is **not used** for the row's modifier. For `UpgradeCharacterStatEffect` upgrades, the inspector accumulates `effect.baseValue * GetBonusValuePerRarity(savedRarity)` across **all** saved picks, matching the native stat effect's additive application. It formats this bonus using the game's effect formatter. This represents the **item's contribution**, not the teammate's entire current stat (which may include other modifiers).
- Non-stat/nonlinear effects are deliberately shown as `—` until their aggregate calculation is verified. Calling `GetCurrentFormattedValue()` directly would report the **host's** stats instead of the inspected teammate's, and is intentionally avoided.

If the UI uses a different player-facing distinction between rings and tomes in this particular game build, correct the category-to-label mapping after checking a known two-player build.

## Local build and test

Use a dedicated **stonewards-party-hud** r2modman profile with BepInEx installed. Disable the Thunderstore-distributed PartyHUD package in the same test profile to avoid duplicate plugins. Do not overwrite your regular play profile.

```powershell
cd X:\projects\stonewards-party-hud
git fetch origin
git switch feat/build-inspector-inventory
powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards-party-hud"
```

Launch that profile with **Start modded**.

## Compact single-line layout (latest requested change)

The user requested a single line for every upgrade: **icon, name, modifier, level/stack count**, with a slightly wider panel and no reference to the last selected ring. The new layout uses a **540-unit panel**, **38-unit item rows** and section headings for `RINGS` and `TOMES`. Window height still follows visible content and paginates when necessary.

For normal character-stat ring effects, the numeric modifier is the calculated cumulative contribution of that **one upgrade** across all recorded acquisitions. Other effect types are shown as `—` rather than misleading last-pick bonuses. Do not assume the modifier is the final player stat or that special effects are linear.

## UI follow-up after October 20 in-game screenshots

The two user screenshots confirm that the prior development build rendered actual ring/tome entries, icon sprites and levels. They also exposed clipped modifier text, a category heading stranded at the end of the first page, and unused space for short builds.

The second UI iteration in this branch:

- Widens the Build Inspector to **446 logical pixels** and allocates two lines for the last acquired effect's description.
- Replaces the `LAST RARE/UNCOMMON` text prefix with a rarity-colored item stripe, keeping the effect wording prominent.
- Calculates a variable-height window for the visible page. On large enough screens, **four rings and four tome types appear together** rather than separating tomes onto page 2.
- Restricts the window to **780 logical pixels** (or the available on-screen height); long inventories still paginate, with section headings kept with their first item.
- Keeps the original HUD styling, host-only data read, hotkeys and player switching.

These source changes have **not yet been compiled or playtested**. In particular, verify that two-line localized descriptions render without clipping and that the enlarged panel fits at the display resolution.

## Gameplay verification before merging

- [ ] **Compact rows:** each upgrade has exactly one readable line (icon, name, modifier, level). Confirm no `LAST` label or last-choice arrows appear.
- [ ] **Aggregate modifier:** with 1, then 2+, picks of the same ring at known rarities, compare the increment shown by the mod with the game's actual cumulative ring effect. Check both percent-valued and non-percent-valued stats. `—` is expected for special effects not yet supported; report which ones matter.


- [ ] The source compiles successfully against the installed game's managed DLLs. A successful GitHub commit is not compilation evidence.
- [ ] The active BepInEx log says Party HUD v0.4.1 initialized.
- [ ] On a two-player session, the host can open **BUILDS** (HUD button or F8) and inspect the other player's rings/tomes; a non-host cannot inspect remote player builds.
- [ ] Test a known ring upgraded several times: the displayed level must match the game's inventory. Check repeated different rarities as well.
- [ ] Pick up both a stackable and a non-stackable tome and compare the list/count against the native inventory.
- [ ] Confirm whether in-game **RINGS/TOMES** wording matches `LEVELUP/PICKUP` in this version; if not, adjust only the classification/labels before release.
- [ ] Inspect long localized effect text and absent sprites/definitions: the panel must remain legible and keep the ID as fallback. Confirm multi-line bonuses are readable and not garbled or cropped.
- [ ] Verify the screenshot cases: one ring plus one tome should produce a compact window; four rings plus four tome types should fit on one page where the screen allows.
- [ ] Check pagination and player switching at 1920x1080 and 2560x1440, including builds with more rows than fit within the available vertical space.
- [ ] Confirm no regression to HP, King/wall durability, Hunter bear, outlines, ping, settings or input behavior.

**Not yet verified:** No local Stonewards game installation or game-referenced .NET build toolchain is available in the development environment. Merge/release only after the user has performed the in-game checks.
