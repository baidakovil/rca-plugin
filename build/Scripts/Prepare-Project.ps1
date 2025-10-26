# Prepare-Project.ps1
# Script to clean up project files and directories
# Ctrl+Alt+C to run only cleaning
# Ctrl+Alt+B to run cleaning and building

param(
    [switch]$NoPause,
    [switch]$EnableLog,
    [switch]$BuildSolution,
    [string]$LogFile = "$env:TEMP\Prepare-Project.log"
)

# Initialize logging
$loggingEnabled = $false
if ($EnableLog) {
    try {
        Start-Transcript -Path $LogFile -Append -Force | Out-Null
        $loggingEnabled = $true
        Write-Host "Logging enabled: $LogFile" -ForegroundColor Cyan
    } catch {
        Write-Host "Warning: could not start transcript: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "Logging disabled (use -EnableLog to enable)" -ForegroundColor Gray
}

Write-Host "Starting project preparation..." -ForegroundColor Green

# Helper: safe Remove-Item wrapper
function Safe-Remove($path) {
    # suppress progress output for large recursive deletes
    $oldProgress = $null
    try {
        $oldProgress = Get-Variable -Name ProgressPreference -Scope Script -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Value -ErrorAction SilentlyContinue
    } catch { }
    if ($null -eq $oldProgress) { $oldProgress = $Global:ProgressPreference }
    $Global:ProgressPreference = 'SilentlyContinue'
    try {
        # suppress any progress or output produced by Remove-Item
        Remove-Item $path -Recurse -Force -ErrorAction Stop *> $null
        return $true
    } catch {
        Write-Host "Warning: failed to remove $path - $($_.Exception.Message)" -ForegroundColor Yellow
        return $false
    } finally {
        try { $Global:ProgressPreference = $oldProgress } catch { }
    }
}

# Helper: print multline build output with simple coloring
function Write-BuildOutput([object]$text) {
    if ($null -eq $text) { return }
    # If the output is an array (common when capturing), treat each element as a line
    if ($text -is [System.Array]) {
        $lines = $text
    } else {
        $lines = ($text -split "\r?\n")
    }
    foreach ($line in $lines) {
        if (-not $line) { continue }
        if ($line -match "(?i)\berror\b") {
            Write-Host $line -ForegroundColor Red
        } elseif ($line -match "(?i)\bwarning\b" -or $line -match "(?i)EXEC : warning") {
            Write-Host $line -ForegroundColor Yellow
        } elseif ($line -match "(?i)succeeded" -or $line -match ":\\") {
            # show success lines (with output path) in green; detect paths with :\
            Write-Host $line -ForegroundColor Green
        } else {
            Write-Host $line -ForegroundColor Gray
        }
    }
}

# 0) Clear RCA Logs (local AppData)
$rcaLogsPath = "$env:LOCALAPPDATA\RCA\Logs"
if (Test-Path $rcaLogsPath) {
    Write-Host "Cleaning RCA Logs: $rcaLogsPath" -ForegroundColor Yellow
    try {
        # delete files only, avoid calling Safe-Remove on folders
        Get-ChildItem $rcaLogsPath -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue } | Out-Null
    } catch {
        Write-Host "Error cleaning RCA Logs: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "RCA Logs path not found, skipping" -ForegroundColor Gray
}

# 1) Clear Revit Addins (roaming)
$revitAddinsPath = "$env:APPDATA\Autodesk\Revit\Addins\2026"
if (Test-Path $revitAddinsPath) {
    Write-Host "Cleaning Revit Addins: $revitAddinsPath" -ForegroundColor Yellow
    try {
        Get-ChildItem $revitAddinsPath -Force -ErrorAction SilentlyContinue | ForEach-Object { Safe-Remove $_.FullName } | Out-Null
    } catch {
        Write-Host "Error cleaning Revit Addins: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "Revit Addins path not found, skipping" -ForegroundColor Gray
}

# 2) Clear RCA Test (local AppData)
$rcaTestPath = "$env:LOCALAPPDATA\RCA\Test"
if (Test-Path $rcaTestPath) {
    Write-Host "Cleaning RCA Test: $rcaTestPath" -ForegroundColor Yellow
    try {
        Get-ChildItem $rcaTestPath -Force -ErrorAction SilentlyContinue | ForEach-Object { Safe-Remove $_.FullName } | Out-Null
    } catch {
        Write-Host "Error cleaning RCA Test: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "RCA Test path not found, skipping" -ForegroundColor Gray
}

# 3) Remove all bin and obj directories recursively under project root
$projectRoot = "C:\Users\baidakov\rca-plugin"
Write-Host "Searching for bin and obj directories under $projectRoot" -ForegroundColor Yellow
try {
    $dirs = Get-ChildItem -Path $projectRoot -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq 'bin' -or $_.Name -eq 'obj' }

    $found = if ($dirs) { $dirs.Count } else { 0 }
    Write-Host "Found $found build directories to remove" -ForegroundColor Cyan

    $removed = 0
    foreach ($d in $dirs) {
        if (Safe-Remove $d.FullName) { $removed++ }
    }
    Write-Host "Removed $removed directories" -ForegroundColor Green
} catch {
    Write-Host "Error enumerating/removing build directories: $($_.Exception.Message)" -ForegroundColor Red
}

# 4) Ensure SourceHashGenerator restore + build using msbuild
$shProj = Join-Path $projectRoot "src\Tools\SourceHashGenerator\SourceHashGenerator.csproj"
if (Test-Path $shProj) {
    Write-Host "Restoring and building SourceHashGenerator via msbuild: $shProj" -ForegroundColor Yellow
    try {
        # Prefer msbuild if available, fallback to 'dotnet msbuild'
        $msbuildCmd = Get-Command msbuild -ErrorAction SilentlyContinue
        if ($msbuildCmd) {
            $cmd = "msbuild `"$shProj`" -t:restore,build"
        } else {
            $cmd = "dotnet msbuild `"$shProj`" -t:restore,build"
        }

        Write-Host "Running: $cmd" -ForegroundColor Gray
        $output = Invoke-Expression $cmd 2>&1
        # print full msbuild/dotnet output with highlighting
        Write-BuildOutput $output
        if ($LASTEXITCODE -eq 0) {
            Write-Host "SourceHashGenerator restored and built successfully" -ForegroundColor Green
        } else {
            Write-Host "SourceHashGenerator msbuild finished with exit code $LASTEXITCODE" -ForegroundColor Red
        }
    } catch {
        Write-Host "Error building SourceHashGenerator: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "SourceHashGenerator project not found at $shProj, skipping" -ForegroundColor Gray
}

# 5) Optional: build entire solution (restore + build --no-incremental)
if ($BuildSolution) {
    Write-Host "Building solution: restore then build --no-incremental" -ForegroundColor Yellow
    try {
        Push-Location $projectRoot
        Write-Host "Running: dotnet restore" -ForegroundColor Gray
        $restoreOut = & dotnet restore 2>&1
        Write-BuildOutput $restoreOut
        if ($LASTEXITCODE -ne 0) {
            Write-Host "dotnet restore failed with exit code $LASTEXITCODE" -ForegroundColor Red
        } else {
            Write-Host "dotnet restore completed" -ForegroundColor Green
            Write-Host "Running: dotnet build --no-incremental" -ForegroundColor Gray
            $buildOut = & dotnet build --no-incremental 2>&1
            Write-BuildOutput $buildOut
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Solution build succeeded" -ForegroundColor Green
            } else {
                Write-Host "Solution build failed with exit code $LASTEXITCODE" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "Error during solution build: $($_.Exception.Message)" -ForegroundColor Red
    } finally {
        Pop-Location
    }
}

Write-Host "Prepare-Project finished" -ForegroundColor Green

# Stop transcript if enabled
if ($loggingEnabled) {
    try { Stop-Transcript | Out-Null; Write-Host "Log saved to: $LogFile" -ForegroundColor Cyan } catch { }
}

if (-not $NoPause) {
    Write-Host "Press any key to continue..." -ForegroundColor Magenta
    try { $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') } catch { Read-Host 'Press Enter to continue' }
}


