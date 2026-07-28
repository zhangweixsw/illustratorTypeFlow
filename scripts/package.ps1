[CmdletBinding()]
param(
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactsRoot = Join-Path $projectRoot 'artifacts'
$sourceExe = Join-Path $artifactsRoot 'app\IllustratorTypeFlow.exe'
$releaseName = "IllustratorTypeFlow-v$Version-Windows-x64"
$releaseRoot = Join-Path $artifactsRoot $releaseName
$zipPath = Join-Path $artifactsRoot "$releaseName.zip"

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw 'The app artifact is missing. Run scripts\build.ps1 first.'
}

if (Test-Path -LiteralPath $releaseRoot) {
    $resolved = (Resolve-Path -LiteralPath $releaseRoot).Path
    $expectedArtifacts = [IO.Path]::GetFullPath($artifactsRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($expectedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected path: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $releaseRoot | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'release-install.ps1') `
    -Destination (Join-Path $releaseRoot 'INSTALL.ps1')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'release-uninstall.ps1') `
    -Destination (Join-Path $releaseRoot 'UNINSTALL.ps1')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\HELP.md') `
    -Destination (Join-Path $releaseRoot 'HELP.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\DEVELOPMENT-NOTES.md') `
    -Destination (Join-Path $releaseRoot 'DEVELOPMENT-NOTES.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $releaseRoot

$releaseNotes = @"
IllustratorTypeFlow v$Version

- Windows 10/11 x64
- Adobe Illustrator 2026
- Free to use
- Self-contained single-file app; .NET installation is not required
- Administrator access is not required

Install: run INSTALL.ps1 from this folder
Help: https://mootop.top/docs/illustrator-typeflow/

This free package contains the tray app. It uses focus detection and canvas
interaction heuristics to switch the IME state. The optional native Illustrator
plugin requires a separate Illustrator SDK build and is not included here.

mootop.top
"@
Set-Content -LiteralPath (Join-Path $releaseRoot 'RELEASE-NOTES.txt') `
    -Value $releaseNotes -Encoding ascii

Compress-Archive -Path (Join-Path $releaseRoot '*') -DestinationPath $zipPath `
    -CompressionLevel Optimal

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
$hashLine = "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zipPath))"
Set-Content -LiteralPath (Join-Path $artifactsRoot 'SHA256SUMS.txt') `
    -Value $hashLine -Encoding ascii

Write-Host "Release: $zipPath"
Write-Host "SHA-256: $($hash.Hash.ToLowerInvariant())"
