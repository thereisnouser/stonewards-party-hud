# Developer setup

This is for contributors testing from source. Players should use the mod manager once an official package is published.

## Prerequisites

- Stonewards (Windows x64, Unity Mono)
- BepInEx 5 installed in the game directory
- .NET SDK that supports `netstandard2.1`
- PowerShell 5.1+

Do not commit game assemblies or BepInEx binaries.

## Build and install locally

Close Stonewards, then run from the repository root:

```powershell
.\build-and-install.ps1 -GameDir "C:\path\to\steamapps\common\StoneWards"
```

This builds the plugin using the game's local assemblies, then copies it to `BepInEx/plugins/StonewardsPartyHUD/`.

## Package for Thunderstore

After a build has been tested in-game, package it with:

```powershell
.\tools\package-thunderstore.ps1
```

The script packages the compiled plugin, metadata, icon and README into `dist/`. It does **not** publish anything or create a release. Verify it on both host and remote client before uploading. Thunderstore publishing requires an authorized publisher account.

## Current risk

The initial source has not yet been compiled or tested with actual multiplayer sessions. Do not publish a user-facing release until that is resolved.
