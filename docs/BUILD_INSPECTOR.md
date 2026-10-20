# Build Inspector inventory view (development branch)

## Scope

This work-in-progress branch keeps the existing client-side Party HUD and host-only Build Inspector, but replaces the old rarity-variant/pick-count presentation with **RINGS** and **TOMES** sections. Rows show the game's localized title and sprite, saved rarity, and (if localization is available) the **last pick's** effect description. Repeated level-up upgrades display `LVL N`; repeated pickup upgrades display `xN`. Any unknown IDs appear under **UNKNOWN UPGRADES** instead of being silently misclassified.

The code continues to rely on **host-owned** `FirstPersonController.RunPlayerData.rogueUpgrades`, without network messages, Harmony patches or mutations of player data. The host-only restriction is unchanged.

## Game data investigated

Inspected the user-supplied `Assembly-CSharp.dll` dated **2026-10-13**. These observations refer to that precise game assembly; recheck after game patches:

- `RogueUpgradeData` has only `upgradeID` and `rarity`; it has no level or cumulative modifier field.
- `NetworkHelper.ServerSaveRogueUpgrade` appends **one** `RogueUpgradeData` per acquired improvement to `RunPlayerData.rogueUpgrades`.
- `RogueLikeUpgradeManager.ActiveRunUpgrade.Apply` increases `Level` for category `LEVELUP`. For `PICKUP`, it increases `StackCount` only if the definition is stackable.
- `RogueLikeUpgradeSO.UpgradeCategory` distinguishes `LEVELUP` and `PICKUP`. This branch uses `LEVELUP` for the requested **RINGS** section, and `PICKUP` for **TOMES**. Internal inventory names (for example, `GetTomes()`) are not the presentation contract and should be checked visually in game.
- `RogueLikeUpgradeSO.GetLocalizedDescriptionWithRarity(Rarity)` describes the effect for **one** rarity choice, not the accumulated player stat. The inspector therefore shows the most recent saved choice's rarity/description, not an invented total.

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
