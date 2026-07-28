[CmdletBinding()]
param(
    [switch]$NoStartup
)

$ErrorActionPreference = 'Stop'
$packageRoot = $PSScriptRoot
$sourceExe = Join-Path $packageRoot 'IllustratorTypeFlow.exe'
$installRoot = Join-Path $env:LOCALAPPDATA 'IllustratorTypeFlow'
$installApp = Join-Path $installRoot 'app'

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "The release package is incomplete. Missing: $sourceExe"
}

Get-Process -Name IllustratorTypeFlow -ErrorAction SilentlyContinue |
    Stop-Process -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $installApp | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $installApp -Force

$exe = Join-Path $installApp 'IllustratorTypeFlow.exe'
if (-not $NoStartup) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-Item -Path $runKey -Force | Out-Null
    Set-ItemProperty -Path $runKey -Name 'IllustratorTypeFlow' -Value "`"$exe`""
}

Start-Process -FilePath $exe
Write-Host ''
Write-Host "Installed to: $installRoot"
Write-Host 'The app icon is in the Windows notification area.'
Write-Host 'Help: https://mootop.top/docs/illustrator-typeflow/'
Write-Host ''
Write-Host 'mootop.top'
