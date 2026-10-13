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

# Use explicitly normalized ZIP entry names. Compress-Archive on Windows
# can otherwise produce backslash-separated entries, which are not portable.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$zip = Join-Path $OutputDir ('PartyHUD-' + $meta.version_number + '.zip')
if (Test-Path -LiteralPath $zip) {
    throw "A package with this version already exists: $zip. Bump the version or remove the old local artifact first."
}

$files = @(
    @{ Source = $manifest; Entry = 'manifest.json' }
    @{ Source = $icon; Entry = 'icon.png' }
    @{ Source = $readme; Entry = 'README.md' }
    @{ Source = $license; Entry = 'LICENSE' }
    @{ Source = $PluginDll; Entry = 'plugins/StonewardsPartyHUD.dll' }
)

# Build to a temporary file, validate, then move into place.
$temporaryZip = $zip + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
$archive = $null
$check = $null
try {
    $archive = [System.IO.Compression.ZipFile]::Open(
        $temporaryZip, [System.IO.Compression.ZipArchiveMode]::Create)
    foreach ($file in $files) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file.Source, $file.Entry,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $archive.Dispose()
    $archive = $null

    $check = [System.IO.Compression.ZipFile]::OpenRead($temporaryZip)
    $expected = @($files | ForEach-Object { $_.Entry })
    $actual = @($check.Entries | ForEach-Object { $_.FullName })
    if (($actual.Count -ne $expected.Count) -or
        (@(Compare-Object -ReferenceObject $expected -DifferenceObject $actual).Count -gt 0)) {
        throw "Thunderstore ZIP entries differ from the expected package layout."
    }
    $check.Dispose()
    $check = $null

    Move-Item -LiteralPath $temporaryZip -Destination $zip
}
finally {
    if ($null -ne $check) { $check.Dispose() }
    if ($null -ne $archive) { $archive.Dispose() }
    if (Test-Path -LiteralPath $temporaryZip) {
        Remove-Item -LiteralPath $temporaryZip -Force
    }
}

Write-Host "Package created: $zip" -ForegroundColor Green
Write-Host 'Not published. Test the package in a fresh mod manager profile before release.'
