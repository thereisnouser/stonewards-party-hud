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
$changelog = Join-Path $repo 'CHANGELOG.md'
$license = Join-Path $repo 'LICENSE'
foreach ($required in @($manifest, $icon, $readme, $changelog, $license, $PluginDll)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required package file is missing: $required"
    }
}

$meta = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
if ($meta.version_number -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Thunderstore version_number must use X.Y.Z.'
}

# Reject mismatched manifests, stale builds and previous-release DLLs.
$source = Join-Path $repo 'src\PartyHudPlugin.cs'
$sourceText = Get-Content -LiteralPath $source -Raw
if ($sourceText -notmatch 'PluginVersion\s*=\s*"(\d+\.\d+\.\d+)"') {
    throw 'PluginVersion is missing from the Party HUD source.'
}
$sourceVersion = $Matches[1]
if ($sourceVersion -ne $meta.version_number) {
    throw "Source version $sourceVersion does not match Thunderstore manifest $($meta.version_number)."
}
# Every upload needs a changelog entry for exactly this package version.
$escapedVersion = [regex]::Escape($sourceVersion)
$changelogText = Get-Content -LiteralPath $changelog -Raw
if ($changelogText -notmatch "(?m)^##\s+$escapedVersion\s*$") {
    throw "CHANGELOG.md is missing an entry for version $sourceVersion."
}

# Check every C# file and the project definition, not only the plugin entry
# point. KingHealthReader.cs and future helpers are compiled into the DLL.
$buildInputs = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Filter '*.cs' -File)
$buildInputs += Get-Item -LiteralPath (Join-Path $repo 'StonewardsPartyHUD.csproj')
$latestInput = $buildInputs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
$builtDll = Get-Item -LiteralPath $PluginDll
if ($builtDll.LastWriteTimeUtc -lt $latestInput.LastWriteTimeUtc) {
    throw "Compiled DLL is older than $($latestInput.FullName). Rebuild before packaging."
}
# The public PluginVersion constant is embedded in .NET metadata as ASCII.
$dllMetadata = [System.Text.Encoding]::ASCII.GetString(
    [System.IO.File]::ReadAllBytes($PluginDll))
if (-not $dllMetadata.Contains($sourceVersion)) {
    throw "Compiled DLL does not contain plugin version $sourceVersion. Rebuild before packaging."
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
    @{ Source = $changelog; Entry = 'CHANGELOG.md' }
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
Write-Host "Plugin/manifest version: $sourceVersion"
Write-Host "Compiled DLL SHA256: $((Get-FileHash -LiteralPath $PluginDll -Algorithm SHA256).Hash)"
Write-Host 'Not published. Test the package in a fresh mod manager profile before release.'
