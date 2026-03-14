[CmdletBinding()]
param()

# Collects AltCover/OpenCover coverage for unit test projects used by rca-plugin metrics.

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
$repoRoot = (Resolve-Path (Join-Path (Join-Path $scriptDir "..") "..")).Path
$solutionPath = Join-Path $repoRoot "rca-plugin.sln"
$runtimeProjectPath = Join-Path $repoRoot "src\Rca.Runtime\Rca.Runtime.csproj"

$altCoverDir = Join-Path $repoRoot "build\Metrics\AltCover"
$coverageStorage = Join-Path $repoRoot "build\MetricsTemp\CoverageStorage.g.xml"
$reportJson = Join-Path $repoRoot "build\Metrics\Report\MetricsReport.g.json"
$reportHtml = Join-Path $repoRoot "build\Metrics\Report\MetricsReport.html"

$unitTestProjects = @(
    "tests\Rca.Contracts.Tests\Rca.Contracts.Tests.csproj",
    "tests\Rca.Core.Tests\Rca.Core.Tests.csproj",
    "tests\Rca.Loader.Tests\Rca.Loader.Tests.csproj",
    "tests\Rca.Network.Tests\Rca.Network.Tests.csproj",
    "tests\Rca.UI.Tests\Rca.UI.Tests.csproj"
)

if (-not (Test-Path $solutionPath)) {
    throw "rca-plugin.sln was not found at '$solutionPath'."
}

if (-not (Test-Path $runtimeProjectPath)) {
    throw "Rca.Runtime.csproj was not found at '$runtimeProjectPath'."
}

Write-Host "Cleaning previous coverage and report outputs..." -ForegroundColor Cyan
if (Test-Path $altCoverDir) {
    Remove-Item $altCoverDir -Recurse -Force -ErrorAction SilentlyContinue
}
if (Test-Path $coverageStorage) {
    Remove-Item $coverageStorage -Force -ErrorAction SilentlyContinue
}
if (Test-Path $reportJson) {
    Remove-Item $reportJson -Force -ErrorAction SilentlyContinue
}
if (Test-Path $reportHtml) {
    Remove-Item $reportHtml -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $coverageStorage) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $reportJson) | Out-Null

Invoke-DotNet -Arguments @(
    "build",
    $solutionPath,
    "/p:AltCoverEnabled=true",
    "/p:CoverageVerbose=false"
)

foreach ($project in $unitTestProjects) {
    $projectPath = Join-Path $repoRoot $project
    if (-not (Test-Path $projectPath)) {
        throw "Test project was not found at '$projectPath'."
    }

    Invoke-DotNet -Arguments @(
        "test",
        $projectPath,
        "--no-build",
        "/p:AltCoverEnabled=true"
    )
}

Invoke-DotNet -Arguments @(
    "msbuild",
    $runtimeProjectPath,
    "/t:CollectCoverage",
    "/p:AltCoverEnabled=true",
    "/p:CoverageVerbose=false"
)

if (-not (Test-Path $coverageStorage)) {
    throw "Coverage file was not created at '$coverageStorage'."
}

Write-Host "Coverage collection completed successfully. Coverage file: $coverageStorage" -ForegroundColor Green
