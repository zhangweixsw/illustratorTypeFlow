[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$IllustratorSdkRoot = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$artifacts = Join-Path $projectRoot 'artifacts'
$appOutput = Join-Path $artifacts 'app'

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

if (-not $SkipTests) {
    & $dotnet test (Join-Path $projectRoot 'tests\IllustratorTypeFlow.Tests\IllustratorTypeFlow.Tests.csproj') `
        -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

if (Test-Path -LiteralPath $appOutput) {
    $resolvedOutput = (Resolve-Path -LiteralPath $appOutput).Path
    $expectedOutput = [IO.Path]::GetFullPath($appOutput)
    $expectedArtifacts = [IO.Path]::GetFullPath($artifacts) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedOutput.Equals($expectedOutput, [StringComparison]::OrdinalIgnoreCase) -or
        -not $resolvedOutput.StartsWith($expectedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected output path: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

& $dotnet publish (Join-Path $projectRoot 'src\IllustratorTypeFlow.App\IllustratorTypeFlow.App.csproj') `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=false `
    -o $appOutput
if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }

Write-Host "Tray app: $appOutput"

if ($IllustratorSdkRoot) {
    & (Join-Path $PSScriptRoot 'build-plugin.ps1') `
        -IllustratorSdkRoot $IllustratorSdkRoot `
        -Configuration $Configuration
} else {
    Write-Warning 'Illustrator SDK path not supplied; native .aip build was skipped.'
}
