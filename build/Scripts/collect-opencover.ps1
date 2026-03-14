# collect-opencover.ps1
<#
.SYNOPSIS
Collects OpenCover metrics with AltCover instrumentation for metricsreporter.

.DESCRIPTION
Builds the solution with AltCover enabled, optionally synchronizes runtime in Revit,
runs test projects, and executes CollectCoverage to produce coverage storage and HTML output.
Use this script when real coverage data is required.
#>
[CmdletBinding()]
param(
    [switch]$SkipRevitReloadCheck,
    [switch]$AllowLoaderOutdated,
    [switch]$SkipIntegrationTests,
    [string]$CommandPipeName = "RCA_COMMAND_PIPE",
    [int]$PipeTimeoutMs = 5000
)

# Collects AltCover/OpenCover coverage for unit test projects used by rca-plugin metrics.

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
Sends a single JSON command to RCA named pipe and returns parsed response.

.PARAMETER PipeName
Named pipe identifier (default RCA_COMMAND_PIPE).

.PARAMETER Command
Pipe command name to send (for example, RELOAD_RUNTIME).

.PARAMETER Payload
Optional payload string sent with the command.

.PARAMETER TimeoutMs
Connection timeout in milliseconds.
#>
function Invoke-RcaPipeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PipeName,

        [Parameter(Mandatory = $true)]
        [string]$Command,

        [string]$Payload = "",

        [int]$TimeoutMs = 5000
    )

    $request = @{ Command = $Command; Payload = $Payload } | ConvertTo-Json -Compress
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
        ".",
        $PipeName,
        [System.IO.Pipes.PipeDirection]::InOut,
        [System.IO.Pipes.PipeOptions]::None
    )

    try {
        $pipe.Connect($TimeoutMs)
        if (-not $pipe.IsConnected) {
            return $null
        }

        $writer = New-Object System.IO.StreamWriter($pipe)
        $writer.AutoFlush = $true
        $reader = New-Object System.IO.StreamReader($pipe)

        $writer.WriteLine($request)
        $line = $reader.ReadLine()

        if ([string]::IsNullOrWhiteSpace($line)) {
            return $null
        }

        return $line | ConvertFrom-Json
    }
    finally {
        if ($null -ne $pipe) {
            $pipe.Dispose()
        }
    }
}

<#
.SYNOPSIS
Ensures the latest runtime build is loaded in running Revit before tests.

.DESCRIPTION
Calls RELOAD_RUNTIME through the RCA command pipe and handles loader/runtime states.
Throws when synchronization fails, unless loader-outdated state is explicitly allowed.

.PARAMETER PipeName
Named pipe used to reach the running RCA loader.

.PARAMETER TimeoutMs
Maximum wait time for pipe connection.

.PARAMETER AllowLoaderOutdatedState
If set, LOADER_RESTART_REQUIRED is treated as warning instead of fatal error.
#>
function Test-RevitRuntimeReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PipeName,

        [int]$TimeoutMs = 5000,

        [switch]$AllowLoaderOutdatedState
    )

    Write-Host "Ensuring latest runtime is loaded in Revit (pipe: $PipeName)..." -ForegroundColor Cyan

    $response = $null
    try {
        $response = Invoke-RcaPipeCommand -PipeName $PipeName -Command "RELOAD_RUNTIME" -TimeoutMs $TimeoutMs
    }
    catch {
        throw "Failed to communicate with Revit pipe '$PipeName': $($_.Exception.Message)"
    }

    if ($null -eq $response) {
        throw "RCA pipe '$PipeName' is not available. Start Revit with RCA plugin and retry."
    }

    if ($response.Status -ne "OK") {
        throw "RELOAD_RUNTIME failed: $($response.Message)"
    }

    switch ($response.Message) {
        "LOADER_RESTART_REQUIRED" {
            $restartMessage = "Loader changes detected. Restart Revit (or use Reload/Restart action in RCA), then rerun coverage collection."
            if ($AllowLoaderOutdatedState) {
                Write-Warning $restartMessage
            }
            else {
                throw $restartMessage
            }
        }
        "NO_ACTION_NEEDED" {
            Write-Host "Revit runtime is already up to date." -ForegroundColor Green
        }
        default {
            Write-Host "Revit response: $($response.Message)" -ForegroundColor Green
        }
    }
}

# --- Resolve repository paths and output locations ---
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path (Join-Path $scriptDir "..") "..")).Path
$solutionPath = Join-Path $repoRoot "rca-plugin.sln"
$runtimeProjectPath = Join-Path $repoRoot "src\Rca.Runtime\Rca.Runtime.csproj"

$altCoverDir = Join-Path $repoRoot "build\Metrics\AltCover"
$coverageStorage = Join-Path $repoRoot "build\MetricsTemp\CoverageStorage.g.xml"
$reportJson = Join-Path $repoRoot "build\Metrics\Report\MetricsReport.g.json"
$reportHtml = Join-Path $repoRoot "build\Metrics\Report\MetricsReport.html"

# --- Define test projects participating in coverage run ---
$unitTestProjects = @(
    "tests\Rca.Contracts.Tests\Rca.Contracts.Tests.csproj",
    "tests\Rca.Core.Tests\Rca.Core.Tests.csproj",
    "tests\Rca.Loader.Tests\Rca.Loader.Tests.csproj",
    "tests\Rca.Network.Tests\Rca.Network.Tests.csproj",
    "tests\Rca.UI.Tests\Rca.UI.Tests.csproj"
)

if (-not $SkipIntegrationTests) {
    $unitTestProjects += "tests\Rca.Integration.Revit.Tests\Rca.Integration.Revit.Tests.csproj"
}

if (-not (Test-Path $solutionPath)) {
    throw "rca-plugin.sln was not found at '$solutionPath'."
}

if (-not (Test-Path $runtimeProjectPath)) {
    throw "Rca.Runtime.csproj was not found at '$runtimeProjectPath'."
}

# --- Clean outputs from previous coverage run ---
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

# --- Build with AltCover instrumentation enabled ---
Invoke-DotNet -Arguments @(
    "build",
    $solutionPath,
    "/p:AltCoverEnabled=true",
    "/p:CoverageVerbose=false"
)

# --- Optionally synchronize runtime state in running Revit ---
if (-not $SkipRevitReloadCheck) {
    Test-RevitRuntimeReady -PipeName $CommandPipeName -TimeoutMs $PipeTimeoutMs -AllowLoaderOutdatedState:$AllowLoaderOutdated
}

# --- Execute test projects without rebuilding ---
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

# --- Aggregate coverage and regenerate HTML report ---
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
