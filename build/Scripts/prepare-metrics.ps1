# prepare-metrics.ps1
<#
.SYNOPSIS
Prepares Roslyn and SARIF metrics artifacts for metricsreporter.

.DESCRIPTION
Rebuilds the solution and runs GenerateSolutionMetrics.
Does not run tests and does not collect real OpenCover coverage.
Creates a minimal OpenCover placeholder so metricsreporter input validation can pass.
Use collect-opencover.ps1 for full coverage collection.
#>
[CmdletBinding()]
param()

# Runs MSBuild target that generates Roslyn and SARIF inputs for metricsreporter.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

<#
.SYNOPSIS
Executes a dotnet command and throws on non-zero exit code.

.PARAMETER Arguments
Argument list passed to the dotnet executable.
#>
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

<#
.SYNOPSIS
Ensures an OpenCover file exists when coverage was not collected.

.DESCRIPTION
Writes a minimal valid CoverageSession XML file only if the target file is missing.
This keeps metricsreporter input validation stable for Roslyn/SARIF-only runs.

.PARAMETER FilePath
Absolute path to CoverageStorage.g.xml.
#>
function Initialize-OpenCoverPlaceholder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    if (Test-Path $FilePath) {
        return
    }

    $placeholderXml = @"
<?xml version="1.0" encoding="utf-8"?>
<CoverageSession>
  <Summary numSequencePoints="0" visitedSequencePoints="0" numBranchPoints="0" visitedBranchPoints="0" sequenceCoverage="0" branchCoverage="0" maxCyclomaticComplexity="0" minCyclomaticComplexity="0" visitedClasses="0" numClasses="0" visitedMethods="0" numMethods="0" />
  <Modules />
</CoverageSession>
"@

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $FilePath) | Out-Null
    Set-Content -LiteralPath $FilePath -Value $placeholderXml -Encoding UTF8
    Write-Host "Created placeholder OpenCover report: $FilePath" -ForegroundColor DarkYellow
}

# --- Resolve repository and tooling paths ---
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path (Join-Path $scriptDir "..") "..")).Path
$solutionPath = Join-Path $repoRoot "rca-plugin.sln"
$roslynToolDir = (Resolve-Path (Join-Path (Join-Path (Join-Path (Join-Path $repoRoot "build") "Resources") "metrics") "win-arm64")).Path
$coverageStoragePath = Join-Path $repoRoot "build\MetricsTemp\CoverageStorage.g.xml"

# --- Validate required inputs ---
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

# --- Generate Roslyn and SARIF metrics artifacts ---
Invoke-DotNet -Arguments $msbuildArgs

# --- Ensure OpenCover placeholder for downstream validation ---
Initialize-OpenCoverPlaceholder -FilePath $coverageStoragePath
