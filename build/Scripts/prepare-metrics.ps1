[CmdletBinding()]
param()

# Runs MSBuild target that generates Roslyn and SARIF inputs for metricsreporter.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE"
    }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir ".." "..")).Path
$solutionPath = Join-Path $repoRoot "rca-plugin.sln"
$roslynToolDir = (Resolve-Path (Join-Path $repoRoot "build" "Resources" "metrics" "win-arm64")).Path

if (-not (Test-Path $solutionPath)) {
    throw "rca-plugin.sln was not found at '$solutionPath'."
}

if (-not (Test-Path $roslynToolDir)) {
    throw "Roslyn metrics tool directory was not found at '$roslynToolDir'."
}

$msbuildArgs = @(
    "msbuild",
    $solutionPath,
    "/t:Rebuild;GenerateSolutionMetrics",
    "/p:RoslynMetricsEnabled=true",
    "/p:SarifMetricsEnabled=true",
    "/p:RoslynMetricsToolDir=$roslynToolDir"
)

Invoke-DotNet -Arguments $msbuildArgs
