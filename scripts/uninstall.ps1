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
    $pluginDirectory = Join-Path $installRoot 'plugin'
    foreach ($target in @($appDirectory, $pluginDirectory)) {
        if (Test-Path -LiteralPath $target) {
            $resolved = (Resolve-Path -LiteralPath $target).Path
            if (-not $resolved.StartsWith($installRoot, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove unexpected path: $resolved"
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
} elseif (Test-Path -LiteralPath $installRoot) {
    $resolvedRoot = (Resolve-Path -LiteralPath $installRoot).Path
    $expectedRoot = [IO.Path]::GetFullPath($installRoot)
    if (-not $resolvedRoot.Equals($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $resolvedRoot"
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}

Write-Host 'IllustratorTypeFlow was removed.'

