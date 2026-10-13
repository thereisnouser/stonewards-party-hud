param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    # Optional r2modman/Gale profile directory. When set, builds against
    # the profile's BepInEx and installs there rather than in the Steam game.
    [string]$ProfileDir
)

$ErrorActionPreference = 'Stop'
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
$managed = Join-Path $GameDir 'StoneWards_Data\Managed'

$installRoot = $GameDir
if (-not [string]::IsNullOrWhiteSpace($ProfileDir)) {
    $installRoot = (Resolve-Path -LiteralPath $ProfileDir).Path
}

$bepinexCore = Join-Path $installRoot 'BepInEx\core'
$bepinex = Join-Path $bepinexCore 'BepInEx.dll'

if (-not (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) {
    throw "Game assemblies not found under $managed. Pass the folder containing StoneWards.exe as -GameDir."
}
if (-not (Test-Path -LiteralPath $bepinex -PathType Leaf)) {
    if ($ProfileDir) {
        throw "BepInEx.dll was not found in the selected mod-manager profile ($bepinex). Open the r2modman profile folder and check that BepInExPack is installed."
    }
    throw "BepInEx 5 is missing ($bepinex). To build using r2modman without modifying the Steam game, pass -ProfileDir with your r2modman profile folder."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is missing. Install the Microsoft .NET SDK (not just the runtime).'
}

$env:STONEWARDS_GAME_DIR = $GameDir
$project = Join-Path $PSScriptRoot 'StonewardsPartyHUD.csproj'
# Pass the BepInEx directory as a global MSBuild property. The reference
# source may be in r2modman even when the Steam game folder is clean.
& dotnet build $project --configuration Release "-p:BepInExCoreDir=$bepinexCore"
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no plugin was installed.' }

$built = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\StonewardsPartyHUD.dll'
$target = Join-Path $installRoot 'BepInEx\plugins\StonewardsPartyHUD'
New-Item -ItemType Directory -Force -Path $target | Out-Null
$installedDll = Join-Path $target 'StonewardsPartyHUD.dll'
Copy-Item -LiteralPath $built -Destination $installedDll -Force

$builtHash = (Get-FileHash -LiteralPath $built -Algorithm SHA256).Hash
$installedHash = (Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash
if ($builtHash -ne $installedHash) {
    throw 'The installed plugin hash does not match the DLL that was just built.'
}

$versionSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\PartyHudPlugin.cs') -Raw
$pluginVersion = 'unknown'
if ($versionSource -match 'PluginVersion\s*=\s*"([^"]+)"') {
    $pluginVersion = $Matches[1]
}
Write-Host "Built Party HUD source version: $pluginVersion"
Write-Host "Installed DLL: $installedDll" -ForegroundColor Green
Write-Host "SHA256: $installedHash"

if ($ProfileDir) {
    $pluginRoot = Join-Path $installRoot 'BepInEx\plugins'
    $otherCopies = @(Get-ChildItem -LiteralPath $pluginRoot -Filter 'StonewardsPartyHUD.dll' -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -ne $installedDll })
    if ($otherCopies.Count -gt 0) {
        Write-Warning ('Other copies of StonewardsPartyHUD.dll in this profile: ' +
            (($otherCopies | ForEach-Object { $_.FullName }) -join '; '))
    }
    $steamCopy = Join-Path $GameDir 'BepInEx\plugins\StonewardsPartyHUD\StonewardsPartyHUD.dll'
    if (Test-Path -LiteralPath $steamCopy -PathType Leaf) {
        Write-Warning "A manual Party HUD installation still exists in Steam: $steamCopy"
    }
    Write-Host 'Expected runtime log line: Party HUD v0.1.1 initialized. (in the ACTIVE profile BepInEx/LogOutput.log)'
}
if ($ProfileDir) {
    Write-Host 'Launch with Start modded using this profile. Disable the public PartyHUD package in this profile to prevent duplicate plugins.'
} else {
    Write-Host 'Launch the game. The HUD appears automatically when players join a session.'
}
