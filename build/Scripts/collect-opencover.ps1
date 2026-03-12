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
$ensureStampScript = Join-Path $repoRoot "build\Scripts\EnsureRcaStamp.ps1"

$altCoverDir = Join-Path $repoRoot "build\Metrics\AltCover"
$coverageStorage = Join-Path $repoRoot "build\MetricsTemp\CoverageStorage.g.xml"
$coverageTimestampFile = Join-Path $repoRoot "build\MetricsTemp\Coverage.Timestamp.txt"
$reportJson = Join-Path $repoRoot "build\Metrics\Report\MetricsReport.g.json"
$reportHtml = Join-Path $repoRoot "build\Metrics\Report\MetricsReport.html"
$revitAddinsDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2026"

$unitTestProjects = @(
    @{ ProjectPath = "tests\Rca.Contracts.Tests\Rca.Contracts.Tests.csproj"; AssemblyName = "Rca.Contracts" },
    @{ ProjectPath = "tests\Rca.Core.Tests\Rca.Core.Tests.csproj"; AssemblyName = "Rca.Core" },
    @{ ProjectPath = "tests\Rca.Loader.Tests\Rca.Loader.Tests.csproj"; AssemblyName = "Rca.Loader" },
    @{ ProjectPath = "tests\Rca.Network.Tests\Rca.Network.Tests.csproj"; AssemblyName = "Rca.Network" },
    @{ ProjectPath = "tests\Rca.UI.Tests\Rca.UI.Tests.csproj"; AssemblyName = "Rca.UI" }
)

if (-not (Test-Path $solutionPath)) {
    throw "rca-plugin.sln was not found at '$solutionPath'."
}

if (-not (Test-Path $runtimeProjectPath)) {
    throw "Rca.Runtime.csproj was not found at '$runtimeProjectPath'."
}

if (-not (Test-Path $ensureStampScript)) {
    throw "EnsureRcaStamp.ps1 was not found at '$ensureStampScript'."
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
if (Test-Path $coverageTimestampFile) {
    Remove-Item $coverageTimestampFile -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $coverageStorage) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $reportJson) | Out-Null

Write-Host "Creating dedicated coverage timestamp..." -ForegroundColor Cyan
& powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $ensureStampScript -TargetPath $coverageTimestampFile -TtlSec 3600 -ForceStr "true" -TimestampPattern "yyyyMMdd_HHmmss"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to generate dedicated coverage timestamp."
}

$coverageTimestamp = (Get-Content $coverageTimestampFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($coverageTimestamp)) {
    throw "Dedicated coverage timestamp file '$coverageTimestampFile' is empty."
}

$runtimeDeployDir = Join-Path $revitAddinsDir $coverageTimestamp

$commonBuildProperties = @(
    "/p:AltCoverEnabled=true",
    "/p:CoverageVerbose=false",
    "/p:RcaTimestampFile=$coverageTimestampFile",
    "/p:RcaStickyStampSeconds=3600",
    "/p:RcaForceNewStamp=false"
)

Invoke-DotNet -Arguments (@(
    "build",
    $solutionPath,
    "/m:1"
) + $commonBuildProperties)

$runtimeSavedDir = Join-Path $runtimeDeployDir "__Saved"
$runtimeRecorderDll = Join-Path $runtimeDeployDir "AltCover.Recorder.g.dll"
$coverageTemplate = Join-Path $altCoverDir "CoverageTemplate.g.xml"

if (-not (Test-Path $runtimeDeployDir)) {
    throw "Runtime deploy directory was not created at '$runtimeDeployDir'."
}
if (-not (Test-Path $runtimeSavedDir)) {
    throw "AltCover backup directory was not created at '$runtimeSavedDir'. Instrumentation did not complete."
}
if (-not (Test-Path $runtimeRecorderDll)) {
    throw "AltCover recorder was not found at '$runtimeRecorderDll'. Instrumentation did not complete."
}
if (-not (Test-Path $coverageTemplate)) {
    throw "AltCover coverage template file was not created at '$coverageTemplate'."
}

foreach ($project in $unitTestProjects) {
    $testProjectDir = Split-Path -Parent $project.ProjectPath
    $testOutputDir = Join-Path $repoRoot (Join-Path $testProjectDir "bin\Debug\net8.0-windows")
    $testAssemblyPath = Join-Path $testOutputDir ($project.AssemblyName + ".dll")
    $deployAssemblyPath = Join-Path $runtimeDeployDir ($project.AssemblyName + ".dll")
    $testRecorderPath = Join-Path $testOutputDir "AltCover.Recorder.g.dll"

    if (-not (Test-Path $testAssemblyPath)) {
        throw "Test output assembly was not found at '$testAssemblyPath'."
    }
    if (-not (Test-Path $deployAssemblyPath)) {
        throw "Instrumented deploy assembly was not found at '$deployAssemblyPath'."
    }
    if (-not (Test-Path $testRecorderPath)) {
        throw "AltCover recorder was not copied to '$testRecorderPath'."
    }

    $testHash = (Get-FileHash -Path $testAssemblyPath -Algorithm SHA256).Hash
    $deployHash = (Get-FileHash -Path $deployAssemblyPath -Algorithm SHA256).Hash
    if ($testHash -ne $deployHash) {
        throw "Assembly '$($project.AssemblyName)' in test output '$testAssemblyPath' is not instrumented (hash mismatch vs '$deployAssemblyPath')."
    }
}

foreach ($project in $unitTestProjects) {
    $projectPath = Join-Path $repoRoot $project.ProjectPath
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

Invoke-DotNet -Arguments (@(
    "msbuild",
    $runtimeProjectPath,
    "/t:CollectCoverage",
    "/p:RuntimeDeployDir=$runtimeDeployDir"
) + $commonBuildProperties)

if (-not (Test-Path $coverageStorage)) {
    throw "Coverage file was not created at '$coverageStorage'."
}

[xml]$coverageXml = Get-Content -Path $coverageStorage
$visitedSequencePoints = [int]$coverageXml.CoverageSession.Summary.visitedSequencePoints
if ($visitedSequencePoints -le 0) {
    throw "Coverage file '$coverageStorage' was generated, but visitedSequencePoints is 0. Assemblies were not exercised by tests."
}

Write-Host "Coverage collection completed successfully. Coverage file: $coverageStorage" -ForegroundColor Green
