# RestartRevitGraceful.ps1
# Gracefully restarts Revit with updated Loader and Runtime assemblies (separate DLLs)
# Integrates with RCA unified logging system via named pipe

param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,
    
    [Parameter(Mandatory=$true)]
    [string]$TargetPath,
    
    [Parameter(Mandatory=$true)]
    [string]$RevitExecutable,
    
    [Parameter(Mandatory=$false)]
    [string]$FilePath = $null
)

# Import RCA logging module
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $scriptDir "RcaLogging.psm1") -Force

# Use unified logging with fallback to file
$logCategory = "RestartScript"

Write-RcaLog "RestartRevitGraceful script started" -Level "Information" -Category $logCategory
Write-RcaLog "Parameters: SourcePath=$SourcePath, TargetPath=$TargetPath, RevitExecutable=$RevitExecutable" -Level "Debug" -Category $logCategory

try {
    # Ensure required source files exist
    $required = @('Rca.Loader.dll','Rca.Loader.Contracts.dll','Rca.Logging.Contracts.dll')

    foreach ($f in $required) {
        $fp = Join-Path $SourcePath $f
        if (!(Test-Path $fp)) {
            $errorMsg = "Required assembly not found at $fp"
            Write-RcaLog $errorMsg -Level "Error" -Category $logCategory
            throw $errorMsg
        }
    }

    # Find Revit process
    Write-RcaLog "Searching for Revit process: $RevitExecutable" -Level "Debug" -Category $logCategory
    $revitProcess = Get-Process | Where-Object { $_.Path -eq $RevitExecutable }
    if (!$revitProcess) {
        $errorMsg = "Revit process not found with path: $RevitExecutable"
        Write-RcaLog $errorMsg -Level "Error" -Category $logCategory
        exit 1
    }

    Write-RcaLog "Found Revit process (PID: $($revitProcess.Id))" -Level "Information" -Category $logCategory

    # Gracefully close Revit
    Write-RcaLog "Closing Revit gracefully..." -Level "Information" -Category $logCategory
    $revitProcess.CloseMainWindow() | Out-Null

    # Wait for Revit to close gracefully (up to 30 seconds)
    $timeoutSeconds = 30
    $startTime = Get-Date
    $closed = $false
    
    while ((Get-Date).Subtract($startTime).TotalSeconds -lt $timeoutSeconds) {
        if ($revitProcess.HasExited) {
            $closed = $true
            $elapsedSeconds = [math]::Round((Get-Date).Subtract($startTime).TotalSeconds, 1)
            Write-RcaLog "Revit closed gracefully after $elapsedSeconds seconds" -Level "Information" -Category $logCategory
            break
        }
        Start-Sleep -Seconds 1
    }

    # Force close if not exited
    if (!$closed) {
        Write-RcaLog "Revit did not close gracefully within $timeoutSeconds seconds, forcing close" -Level "Warning" -Category $logCategory
        $revitProcess.Kill()
        Start-Sleep -Seconds 2
        Write-RcaLog "Revit process terminated forcefully" -Level "Information" -Category $logCategory
    }

    if (!(Test-Path $TargetPath)) {
        Write-RcaLog "Creating target directory: $TargetPath" -Level "Debug" -Category $logCategory
        New-Item -Path $TargetPath -ItemType Directory -Force | Out-Null
    }

    # Copy all required DLLs from source to target
    foreach ($f in $required) {
        $src = Join-Path $SourcePath $f
        $dst = Join-Path $TargetPath $f
        Write-RcaLog "Copying $f" -Level "Information" -Category $logCategory
        Copy-Item -Path $src -Destination $dst -Force
        if (!(Test-Path $dst)) { throw "Failed to copy $f to $dst" }
    }

    # Restart Revit
    if ($FilePath) {
        Write-RcaLog "Restarting Revit with file: $FilePath" -Level "Information" -Category $logCategory
        Start-Process -FilePath $RevitExecutable -ArgumentList "`"$FilePath`""
    } else {
        Write-RcaLog "Restarting Revit: $RevitExecutable" -Level "Information" -Category $logCategory
        Start-Process -FilePath $RevitExecutable
    }

    Write-RcaLog "RestartRevitGraceful script completed successfully" -Level "Information" -Category $logCategory
    exit 0
} 
catch {
    Write-RcaLog "Fatal error in RestartRevitGraceful script: $_" -Level "Critical" -Category $logCategory
    Write-RcaLog "Stack trace: $($_.ScriptStackTrace)" -Level "Error" -Category $logCategory
    exit 1
}
finally {
    # Clean up module
    Remove-Module RcaLogging -ErrorAction SilentlyContinue
}
