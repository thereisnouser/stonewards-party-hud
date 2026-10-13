# Stonewards Party HUD

A lightweight, client-side party overlay for **Stonewards**. See connected players' names, classes, current/max health, and downed state without searching the battlefield. Hunter companions appear as an indented health row under their owner when available. Remote teammate outlines are optional, with through-wall visibility configurable. The HUD uses the game's own class sprites when available, falling back to short class labels.

> **Status:** Alpha. Earlier versions have been tested locally in multiplayer; the latest UI changes still require in-game testing. Not yet published on Thunderstore.

## Installation (planned for the first tested release)

1. Install [r2modman](https://thunderstore.io/c/stonewards/p/ebkr/r2modman/) or another compatible Thunderstore manager and choose **Stonewards**.
2. Find **PartyHUD** in the mod browser and click **Download**.
3. Use **Start modded** to launch the game.

The manager will install the required BepInEx dependency automatically. **No .NET SDK, terminal or manual DLL copying will be needed.**

**The mod is not available in the manager yet.** These steps become applicable only after the first tested package is published.

## Display

The panel defaults to the upper-left corner to keep Stonewards' native Inventory and Emote prompts unobstructed. Position (`Appearance.Anchor`, `TopOffset`, `BottomOffset`), size (`Scale`) and Hunter companion display can be configured in `BepInEx/config/community.stonewards.partyhud.cfg`.

## Development

For local source builds and installation see [Developer setup](docs/DEVELOPMENT.md). The mod is intended to read locally available multiplayer data without modifying game state or sending network messages.

Track readiness and upcoming features in [GitHub Issues](https://github.com/thereisnouser/stonewards-party-hud/issues).

## License

MIT — see [LICENSE](LICENSE). Third-party and proprietary game binaries are not redistributed.
