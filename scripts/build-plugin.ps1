[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$IllustratorSdkRoot,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sdkRoot = (Resolve-Path -LiteralPath $IllustratorSdkRoot).Path
$pluginRoot = Join-Path $projectRoot 'plugin'
$buildRoot = Join-Path $projectRoot 'artifacts\plugin-build'
$outputRoot = Join-Path $projectRoot 'artifacts\plugin'

$cmakeCandidates = @(
    (Get-Command cmake.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

if (-not $cmakeCandidates) {
    throw 'CMake/Visual Studio 2022 C++ tools were not found.'
}
$cmake = $cmakeCandidates[0]

& $cmake -S $pluginRoot -B $buildRoot -A x64 "-DILLUSTRATOR_SDK_ROOT=$sdkRoot"
if ($LASTEXITCODE -ne 0) { throw 'CMake configure failed.' }
& $cmake --build $buildRoot --config $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$aip = Get-ChildItem $buildRoot -Recurse -Filter 'IllustratorTypeFlow.aip' |
    Select-Object -First 1
if (-not $aip) { throw 'Built AIP was not found.' }
Copy-Item -LiteralPath $aip.FullName -Destination $outputRoot -Force

$piplTool = Get-ChildItem $sdkRoot -Recurse -Filter 'create_pipl.py' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $piplTool) {
    throw 'Adobe create_pipl.py was not found in this SDK.'
}

$python = Get-Command python.exe -ErrorAction SilentlyContinue
if (-not $python) { throw 'Python is required by Adobe create_pipl.py.' }

Push-Location $outputRoot
try {
    $piplInput = Get-Content (Join-Path $pluginRoot 'plugin-pipl.json') -Raw
    & $python.Source $piplTool.FullName -input $piplInput
    if ($LASTEXITCODE -ne 0) { throw 'PiPL generation failed.' }
} finally {
    Pop-Location
}

if (-not (Test-Path (Join-Path $outputRoot 'plugin.pipl'))) {
    throw 'create_pipl.py completed but plugin.pipl was not produced.'
}

Write-Host "Illustrator plugin: $outputRoot"

