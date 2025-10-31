<#
.SYNOPSIS
    Collects code coverage after running tests when EnableCodeMetrics is enabled.

.DESCRIPTION
    This script runs dotnet test and then collects coverage using AltCover.
    It requires EnableCodeMetrics to be set to true during build.

.PARAMETER SolutionPath
    Path to the solution file. Defaults to the solution in the script's parent directory.

.PARAMETER EnableCodeMetrics
    Enable code metrics collection. Defaults to true if not specified.

.EXAMPLE
    .\Invoke-CoverageCollection.ps1
    Runs tests and collects coverage using default settings.

.EXAMPLE
    .\Invoke-CoverageCollection.ps1 -SolutionPath ".\rca-plugin.sln"
    Runs tests and collects coverage for a specific solution.
#>
param(
    [string]$SolutionPath = "",
    [bool]$EnableCodeMetrics = $true
)

$ErrorActionPreference = "Stop"

# Determine solution path
if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $scriptDir = Split-Path -Parent $PSScriptRoot
    $SolutionPath = Join-Path $scriptDir "../rca-plugin.sln"
}

if (-not (Test-Path $SolutionPath)) {
    throw "Solution file not found at: $SolutionPath"
}

Write-Host "Running tests with coverage enabled..." -ForegroundColor Cyan

# Run tests
$testResult = & dotnet test $SolutionPath /p:EnableCodeMetrics=$EnableCodeMetrics

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Tests completed with errors, but proceeding with coverage collection..."
}

Write-Host "`nCollecting coverage data..." -ForegroundColor Cyan

# Collect coverage using MSBuild target
$runtimeProject = Join-Path (Split-Path -Parent $PSScriptRoot) "src\Rca.Runtime\Rca.Runtime.csproj"
$collectResult = & dotnet msbuild $runtimeProject /t:CollectCoverage /p:EnableCodeMetrics=$EnableCodeMetrics /v:minimal

if ($LASTEXITCODE -eq 0) {
    $coverageFile = Join-Path (Split-Path -Parent $PSScriptRoot) "build\Metrics\coverage.xml"
    if (Test-Path $coverageFile) {
        Write-Host "`nCoverage report generated successfully: $coverageFile" -ForegroundColor Green
    } else {
        Write-Warning "Coverage collection completed, but output file not found at: $coverageFile"
        Write-Warning "This may indicate that no code was executed during tests, or signal file was missing."
    }
} else {
    Write-Error "Coverage collection failed. Check the output above for details."
    exit $LASTEXITCODE
}

