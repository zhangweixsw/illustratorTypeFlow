[CmdletBinding()]
param(
    [string]$ArtifactsDirectory = '',
    [switch]$NoStartup
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = if ($ArtifactsDirectory) {
    (Resolve-Path -LiteralPath $ArtifactsDirectory).Path
} else {
    Join-Path $projectRoot 'artifacts'
}

$sourceApp = Join-Path $artifacts 'app'
$sourcePlugin = Join-Path $artifacts 'plugin'
$installRoot = Join-Path $env:LOCALAPPDATA 'IllustratorTypeFlow'
$installApp = Join-Path $installRoot 'app'
$installPlugin = Join-Path $installRoot 'plugin'

if (-not (Test-Path -LiteralPath (Join-Path $sourceApp 'IllustratorTypeFlow.exe'))) {
    throw "App artifact not found under $sourceApp. Run scripts\build.ps1 first."
}

New-Item -ItemType Directory -Force -Path $installApp | Out-Null
Copy-Item -Path (Join-Path $sourceApp '*') -Destination $installApp -Recurse -Force

if (Test-Path -LiteralPath (Join-Path $sourcePlugin 'IllustratorTypeFlow.aip')) {
    New-Item -ItemType Directory -Force -Path $installPlugin | Out-Null
    Copy-Item -Path (Join-Path $sourcePlugin '*') -Destination $installPlugin -Recurse -Force
} else {
    Write-Warning 'Native plugin artifact was not found; canvas text detection will be unavailable.'
}

$exe = Join-Path $installApp 'IllustratorTypeFlow.exe'
if (-not $NoStartup) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-Item -Path $runKey -Force | Out-Null
    Set-ItemProperty -Path $runKey -Name 'IllustratorTypeFlow' -Value "`"$exe`""
}

Start-Process -FilePath $exe

Write-Host "Installed to: $installRoot"
if (Test-Path -LiteralPath $installPlugin) {
    Write-Host ''
    Write-Host 'Illustrator one-time setup:'
    Write-Host '1. Open Preferences > Plug-ins & Scratch Disks.'
    Write-Host "2. Enable Additional Plug-ins Folder and choose: $installPlugin"
    Write-Host '3. Restart Illustrator.'
}

