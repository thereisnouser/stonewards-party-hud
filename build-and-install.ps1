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
Copy-Item -LiteralPath $built -Destination (Join-Path $target 'StonewardsPartyHUD.dll') -Force
Write-Host "Installed Party HUD to $target" -ForegroundColor Green
if ($ProfileDir) {
    Write-Host 'Launch with Start modded using this profile. Disable the public PartyHUD package in this profile to prevent duplicate plugins.'
} else {
    Write-Host 'Launch the game. The HUD appears automatically when players join a session.'
}
