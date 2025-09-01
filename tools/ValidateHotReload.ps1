#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Validates the RCA Hot Reload system setup and configuration.

.DESCRIPTION
    This script checks that all components of the hot reload system are properly configured:
    - Projects exist and can build
    - .addin files are correct
    - Build targets are properly configured
    - Documentation is up to date

.EXAMPLE
    .\ValidateHotReload.ps1
    # Runs full validation
    
.EXAMPLE
    .\ValidateHotReload.ps1 -QuickCheck
    # Runs basic checks only
#>

param(
    [switch]$QuickCheck = $false
)

Write-Host "🔍 RCA Hot Reload System Validation" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$ErrorCount = 0
$WarningCount = 0

function Test-Condition {
    param(
        [string]$Name,
        [bool]$Condition,
        [string]$ErrorMessage = "",
        [bool]$IsWarning = $false
    )
    
    if ($Condition) {
        Write-Host "✅ $Name" -ForegroundColor Green
        return $true
    } else {
        if ($IsWarning) {
            Write-Host "⚠️  $Name" -ForegroundColor Yellow
            if ($ErrorMessage) { Write-Host "   $ErrorMessage" -ForegroundColor Yellow }
            $script:WarningCount++
        } else {
            Write-Host "❌ $Name" -ForegroundColor Red
            if ($ErrorMessage) { Write-Host "   $ErrorMessage" -ForegroundColor Red }
            $script:ErrorCount++
        }
        return $false
    }
}

# Basic project structure checks
Write-Host "📁 Project Structure" -ForegroundColor Yellow
Write-Host "-------------------" -ForegroundColor Yellow

Test-Condition "Rca.Loader.Contracts project exists" (Test-Path "src/Rca.Loader.Contracts/Rca.Loader.Contracts.csproj")
Test-Condition "Rca.Loader project exists" (Test-Path "src/Rca.Loader/Rca.Loader.csproj")
Test-Condition "Rca.Runtime project exists" (Test-Path "src/Rca.Runtime/Rca.Runtime.csproj")
Test-Condition "Loader .addin file exists" (Test-Path "src/Rca.Loader/Rca.Loader.addin")
Test-Condition "PowerShell reload script exists" (Test-Path "tools/SendReload.ps1")
Test-Condition "Hot reload documentation exists" (Test-Path "DEV_HOT_RELOAD.md")

Write-Host ""

# Build system checks
Write-Host "🛠️  Build System" -ForegroundColor Yellow
Write-Host "--------------" -ForegroundColor Yellow

# Check for ILRepack package reference
$runtimeCsproj = Get-Content "src/Rca.Runtime/Rca.Runtime.csproj" -ErrorAction SilentlyContinue
Test-Condition "ILRepack package reference exists" (($runtimeCsproj -join " ") -match "ILRepack\.Lib\.MSBuild\.Task")

# Check for HotReloadStaging target
Test-Condition "Hot reload staging target exists" (($runtimeCsproj -join " ") -match "HotReloadStaging")

# Check for project references
Test-Condition "Runtime references Core project" (($runtimeCsproj -join " ") -match "Rca\.Core")
Test-Condition "Runtime references UI project" (($runtimeCsproj -join " ") -match "Rca\.UI")
Test-Condition "Runtime references Loader.Contracts" (($runtimeCsproj -join " ") -match "Rca\.Loader\.Contracts")

Write-Host ""

if (-not $QuickCheck) {
    # Interface implementation checks
    Write-Host "🔌 Interface Implementation" -ForegroundColor Yellow
    Write-Host "--------------------------" -ForegroundColor Yellow
    
    # Check if RcaRuntime implements IPluginRuntime
    $runtimeCs = Get-Content "src/Rca.Runtime/RcaRuntime.cs" -ErrorAction SilentlyContinue
    Test-Condition "RcaRuntime implements IPluginRuntime" (($runtimeCs -join " ") -match ": IPluginRuntime")
    Test-Condition "RcaRuntime has Version property" (($runtimeCs -join " ") -match "string Version")
    Test-Condition "RcaRuntime has Initialize method" (($runtimeCs -join " ") -match "void Initialize\(")
    Test-Condition "RcaRuntime has Shutdown method" (($runtimeCs -join " ") -match "void Shutdown\(")
    Test-Condition "RcaRuntime has OnLoaded method" (($runtimeCs -join " ") -match "void OnLoaded\(")
    
    Write-Host ""
    
    # Loader implementation checks
    Write-Host "⚡ Loader Implementation" -ForegroundColor Yellow
    Write-Host "----------------------" -ForegroundColor Yellow
    
    $loaderCs = Get-Content "src/Rca.Loader/LoaderApp.cs" -ErrorAction SilentlyContinue
    Test-Condition "LoaderApp implements IExternalApplication" (($loaderCs -join " ") -match ": IExternalApplication")
    Test-Condition "LoaderApp has RuntimeManager" (($loaderCs -join " ") -match "RuntimeManager")
    
    $runtimeManagerCs = Get-Content "src/Rca.Loader/RuntimeManager.cs" -ErrorAction SilentlyContinue
    Test-Condition "RuntimeManager has pipe server" (($runtimeManagerCs -join " ") -match "NamedPipeServerStream")
    Test-Condition "RuntimeManager has ALC management" (($runtimeManagerCs -join " ") -match "HotReloadAssemblyLoadContext")
    
    Write-Host ""
}

# Configuration checks
Write-Host "⚙️  Configuration" -ForegroundColor Yellow
Write-Host "---------------" -ForegroundColor Yellow

# Check .addin file content
$addinContent = Get-Content "src/Rca.Loader/Rca.Loader.addin" -ErrorAction SilentlyContinue
Test-Condition ".addin points to Loader assembly" (($addinContent -join " ") -match "Rca\.Loader\\Rca\.Loader\.dll")
Test-Condition ".addin uses LoaderApp class" (($addinContent -join " ") -match "Rca\.Loader\.LoaderApp")

# Check solution file
$slnContent = Get-Content "rca-plugin.sln" -ErrorAction SilentlyContinue
Test-Condition "Solution includes Loader.Contracts" (($slnContent -join " ") -match "Rca\.Loader\.Contracts")
Test-Condition "Solution includes Loader" (($slnContent -join " ") -match '"Rca\.Loader"')
Test-Condition "Solution includes Runtime" (($slnContent -join " ") -match "Rca\.Runtime")

Write-Host ""

# Try basic compilation
Write-Host "🔨 Compilation Test" -ForegroundColor Yellow
Write-Host "------------------" -ForegroundColor Yellow

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    try {
        $buildOutput = dotnet build src/Rca.Loader.Contracts --verbosity quiet --nologo 2>&1
        Test-Condition "Loader.Contracts builds successfully" ($LASTEXITCODE -eq 0) "Build failed: $buildOutput"
        
        if (-not $QuickCheck) {
            $buildOutput = dotnet build src/Rca.Contracts --verbosity quiet --nologo 2>&1
            Test-Condition "Contracts builds successfully" ($LASTEXITCODE -eq 0) "Build failed: $buildOutput"
            
            $buildOutput = dotnet build src/Rca.Core --verbosity quiet --nologo 2>&1
            Test-Condition "Core builds successfully" ($LASTEXITCODE -eq 0) "Build failed: $buildOutput"
            
            $buildOutput = dotnet build src/Rca.Network --verbosity quiet --nologo 2>&1
            Test-Condition "Network builds successfully" ($LASTEXITCODE -eq 0) "Build failed: $buildOutput"
        }
    } catch {
        Test-Condition "Basic build test" $false "Exception: $($_.Exception.Message)"
    }
} else {
    Test-Condition ".NET SDK available" $false ".NET SDK not found in PATH" $true
}

Write-Host ""

# Summary
Write-Host "📊 Validation Summary" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Cyan

if ($ErrorCount -eq 0 -and $WarningCount -eq 0) {
    Write-Host "🎉 All checks passed! Hot reload system is properly configured." -ForegroundColor Green
    exit 0
} elseif ($ErrorCount -eq 0) {
    Write-Host "✅ System is functional with $WarningCount warning(s)." -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "❌ Found $ErrorCount error(s) and $WarningCount warning(s)." -ForegroundColor Red
    Write-Host ""
    Write-Host "🔧 To fix issues:" -ForegroundColor Yellow
    Write-Host "  1. Ensure all projects are created (see DEV_HOT_RELOAD.md)" -ForegroundColor Gray
    Write-Host "  2. Build the solution: dotnet build" -ForegroundColor Gray
    Write-Host "  3. Check project references and dependencies" -ForegroundColor Gray
    Write-Host "  4. Verify .addin file points to correct assembly" -ForegroundColor Gray
    exit 1
}