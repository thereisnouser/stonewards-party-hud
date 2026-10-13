# Prepares a Thunderstore ZIP from a locally compiled and verified DLL.
# This script does not upload or publish the package.
param(
    [string]$PluginDll,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $PluginDll) { $PluginDll = Join-Path $repo 'bin\Release\netstandard2.1\StonewardsPartyHUD.dll' }
if (-not $OutputDir) { $OutputDir = Join-Path $repo 'dist' }

$manifest = Join-Path $repo 'packaging\manifest.json'
$icon = Join-Path $repo 'packaging\icon.png'
$readme = Join-Path $repo 'README.md'
$license = Join-Path $repo 'LICENSE'
foreach ($required in @($manifest, $icon, $readme, $license, $PluginDll)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required package file is missing: $required"
    }
}

$meta = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
if ($meta.version_number -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Thunderstore version_number must use X.Y.Z.'
}

# The Thunderstore icon must be a 256x256 PNG.
$image = $null
try {
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($icon)
    if ($image.Width -ne 256 -or $image.Height -ne 256) {
        throw 'Thunderstore icon.png must be exactly 256x256.'
    }
}
finally {
    if ($null -ne $image) { $image.Dispose() }
}

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ('stonewards-party-hud-' + [Guid]::NewGuid().ToString('N'))
try {
    $pluginDir = Join-Path $stage 'plugins'
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    Copy-Item -LiteralPath $PluginDll -Destination (Join-Path $pluginDir 'StonewardsPartyHUD.dll')
    Copy-Item -LiteralPath $manifest -Destination (Join-Path $stage 'manifest.json')
    Copy-Item -LiteralPath $icon -Destination (Join-Path $stage 'icon.png')
    Copy-Item -LiteralPath $readme -Destination (Join-Path $stage 'README.md')
    Copy-Item -LiteralPath $license -Destination (Join-Path $stage 'LICENSE')

    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    $zip = Join-Path $OutputDir ('PartyHUD-' + $meta.version_number + '.zip')
    if (Test-Path -LiteralPath $zip) {
        throw "A package with this version already exists: $zip. Bump the version or remove the old local artifact first."
    }

    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Write-Host "Package created: $zip" -ForegroundColor Green
    Write-Host 'Not published. Test the compiled plugin and inspect the ZIP before release.'
}
finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}
