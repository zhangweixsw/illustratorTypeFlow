[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$KeepSettings
)

$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:LOCALAPPDATA 'IllustratorTypeFlow'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

Get-Process -Name IllustratorTypeFlow -ErrorAction SilentlyContinue |
    Stop-Process -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $runKey -Name 'IllustratorTypeFlow' -ErrorAction SilentlyContinue

if ($KeepSettings) {
    $appDirectory = Join-Path $installRoot 'app'
    if (Test-Path -LiteralPath $appDirectory) {
        $resolved = (Resolve-Path -LiteralPath $appDirectory).Path
        $expectedRoot = [IO.Path]::GetFullPath($installRoot) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected path: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
} elseif (Test-Path -LiteralPath $installRoot) {
    $resolvedRoot = (Resolve-Path -LiteralPath $installRoot).Path
    $expectedRoot = [IO.Path]::GetFullPath($installRoot)
    if (-not $resolvedRoot.Equals($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $resolvedRoot"
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}

Write-Host ''
Write-Host 'IllustratorTypeFlow was removed.'
Write-Host 'mootop.top'
