# RCA Plugin Hot-Reload Script
# This script builds the plugin and triggers a reload in Revit

param(
    [string]$Configuration = "Debug",
    [string]$PipeName = "RcaPluginReloader",
    [switch]$BuildOnly,
    [switch]$ReloadOnly,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SolutionDir = Split-Path -Parent $ScriptDir

# Paths
$SolutionFile = Join-Path $SolutionDir "rca-plugin.sln"
$ReloadTriggerExe = Join-Path $SolutionDir "bin\$Configuration\net8.0-windows\RcaReloadTrigger.exe"
$PluginAssembly = Join-Path $SolutionDir "bin\$Configuration\net8.0-windows\RcaPlugin.dll"

function Write-Message {
    param([string]$Message, [string]$Color = "White")
    if ($Verbose -or $Color -ne "White") {
        Write-Host $Message -ForegroundColor $Color
    }
}

function Build-Solution {
    Write-Message "Building solution in $Configuration configuration..." "Yellow"
    
    try {
        $buildOutput = dotnet build $SolutionFile -c $Configuration --no-restore 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Message "Build failed:" "Red"
            Write-Host $buildOutput
            return $false
        }
        
        Write-Message "Build completed successfully!" "Green"
        if ($Verbose) {
            Write-Host $buildOutput
        }
        return $true
    }
    catch {
        Write-Message "Build error: $($_.Exception.Message)" "Red"
        return $false
    }
}

function Trigger-Reload {
    Write-Message "Triggering hot-reload..." "Yellow"
    
    if (-not (Test-Path $ReloadTriggerExe)) {
        Write-Message "Reload trigger executable not found at: $ReloadTriggerExe" "Red"
        Write-Message "Make sure the solution is built first." "Red"
        return $false
    }
    
    try {
        $reloadOutput = & $ReloadTriggerExe reload --pipe $PipeName --assembly $PluginAssembly 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Message "Hot-reload completed successfully!" "Green"
            if ($Verbose) {
                Write-Host $reloadOutput
            }
            return $true
        } else {
            Write-Message "Hot-reload failed:" "Red"
            Write-Host $reloadOutput
            return $false
        }
    }
    catch {
        Write-Message "Hot-reload error: $($_.Exception.Message)" "Red"
        return $false
    }
}

function Test-Connection {
    Write-Message "Testing connection to RCA Loader..." "Yellow"
    
    if (-not (Test-Path $ReloadTriggerExe)) {
        Write-Message "Reload trigger executable not found. Building solution first..." "Yellow"
        if (-not (Build-Solution)) {
            return $false
        }
    }
    
    try {
        $pingOutput = & $ReloadTriggerExe ping --pipe $PipeName 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Message "Connection test successful!" "Green"
            return $true
        } else {
            Write-Message "Connection test failed:" "Red"
            Write-Host $pingOutput
            return $false
        }
    }
    catch {
        Write-Message "Connection test error: $($_.Exception.Message)" "Red"
        return $false
    }
}

# Main execution
Write-Message "RCA Plugin Hot-Reload Script" "Cyan"
Write-Message "Configuration: $Configuration" "Gray"
Write-Message "Solution: $SolutionFile" "Gray"

if ($ReloadOnly) {
    # Only trigger reload
    $success = Trigger-Reload
} elseif ($BuildOnly) {
    # Only build
    $success = Build-Solution
} else {
    # Build and reload
    Write-Message "Starting build and hot-reload process..." "Cyan"
    
    # Test connection first
    if (-not (Test-Connection)) {
        Write-Message "Warning: Could not connect to RCA Loader. Make sure Revit is running with the RCA Loader plugin." "Yellow"
        Write-Message "Continuing with build only..." "Yellow"
        $success = Build-Solution
    } else {
        # Build solution
        $success = Build-Solution
        
        # Trigger reload if build succeeded
        if ($success) {
            $success = Trigger-Reload
        }
    }
}

if ($success) {
    Write-Message "Operation completed successfully!" "Green"
    exit 0
} else {
    Write-Message "Operation failed!" "Red"
    exit 1
}