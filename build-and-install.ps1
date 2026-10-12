param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir
)

$ErrorActionPreference = 'Stop'
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
$managed = Join-Path $GameDir 'StoneWards_Data\Managed'
$bepinex = Join-Path $GameDir 'BepInEx\core\BepInEx.dll'

if (-not (Test-Path (Join-Path $managed 'Assembly-CSharp.dll'))) {
    throw "Game assemblies not found under $managed. Pass the folder containing StoneWards.exe as -GameDir."
}
if (-not (Test-Path $bepinex)) {
    throw "BepInEx 5 is missing ($bepinex). Install the Windows x64 Unity Mono build first and launch the game once."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is missing. Install the Microsoft .NET SDK (not just the runtime).'
}

$env:STONEWARDS_GAME_DIR = $GameDir
$project = Join-Path $PSScriptRoot 'StonewardsPartyHUD.csproj'
& dotnet build $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no plugin was installed.' }

$built = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\StonewardsPartyHUD.dll'
$target = Join-Path $GameDir 'BepInEx\plugins\StonewardsPartyHUD'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath $built -Destination (Join-Path $target 'StonewardsPartyHUD.dll') -Force
Write-Host "Installed Party HUD to $target" -ForegroundColor Green
Write-Host 'Launch the game. The HUD appears automatically when players join a session.'
