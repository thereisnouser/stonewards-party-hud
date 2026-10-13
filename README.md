# Stonewards Party HUD

A lightweight, client-side party overlay for **Stonewards**. See connected players' names, classes, current/max health, and downed state without searching the battlefield. Hunter companions appear as an indented health row under their owner when available. Remote teammate outlines and floating teammate nicknames are optional, with through-wall visibility configurable. The HUD uses the game's own class sprites when available, falling back to short class labels.

> **Status:** Public alpha v0.1.0; v0.1.1 improvements under test. Tested locally in Stonewards v0.2.3 multiplayer; feedback and bug reports are welcome.

**[Download on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/)**

## Installation

1. Install [r2modman](https://thunderstore.io/c/stonewards/p/ebkr/r2modman/) or another compatible Thunderstore manager and choose **Stonewards**.
2. Install **[PartyHUD](https://thunderstore.io/c/stonewards/p/thereisnouser/PartyHUD/)** from the mod browser.
3. Use **Start modded** to launch the game.

The package declares BepInExPack as a required dependency. **No .NET SDK, terminal or manual DLL copying is needed.**

## Display

The panel defaults to the upper-left corner, below the game's player-name list, and hides while the Shop, Character Selection, Missions, or Upgrade Offers menu is open (Inventory hiding is being tested for v0.1.1). Position (`Appearance.Anchor`, `TopOffset`, `BottomOffset`, `AvoidNativePlayerNames`), size (`Scale`, `ReadabilityScale`, `AutoScaleToResolution`) and Hunter companion display can be configured in `BepInEx/config/community.stonewards.partyhud.cfg`.

The upcoming v0.1.1 build also adds a compact **local-player-only** stamina and optional mana row (`ShowLocalResources`), since Stonewards does not synchronize teammates' current stamina or mana.

## Development

For local source builds and installation see [Developer setup](docs/DEVELOPMENT.md). The mod is intended to read locally available multiplayer data without modifying game state or sending network messages.

Track readiness and upcoming features in [GitHub Issues](https://github.com/thereisnouser/stonewards-party-hud/issues).

## License

MIT — see [LICENSE](LICENSE). Third-party and proprietary game binaries are not redistributed.
