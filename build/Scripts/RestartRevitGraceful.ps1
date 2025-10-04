# RestartRevitGraceful.ps1
# Gracefully restarts Revit with updated Loader assembly
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
    # 1. Check for merged Loader assembly
    $loaderFile = "Rca.Loader.dll"
    $loaderSourcePath = Join-Path $SourcePath $loaderFile
    
    if (!(Test-Path $loaderSourcePath)) {
        $errorMsg = "Required merged Loader assembly not found at $loaderSourcePath"
        Write-RcaLog $errorMsg -Level "Error" -Category $logCategory
        throw $errorMsg
    }
    
    Write-RcaLog "Found Loader assembly at $loaderSourcePath" -Level "Debug" -Category $logCategory
    
    # 2. Find Revit process
    Write-RcaLog "Searching for Revit process: $RevitExecutable" -Level "Debug" -Category $logCategory
    $revitProcess = Get-Process | Where-Object { $_.Path -eq $RevitExecutable }
    
    if (!$revitProcess) {
        $errorMsg = "Revit process not found with path: $RevitExecutable"
        Write-RcaLog $errorMsg -Level "Error" -Category $logCategory
        exit 1
    }

    Write-RcaLog "Found Revit process (PID: $($revitProcess.Id))" -Level "Information" -Category $logCategory

    # 3. Gracefully close Revit
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

    # 4. Force close if not exited
    if (!$closed) {
        Write-RcaLog "Revit did not close gracefully within $timeoutSeconds seconds, forcing close" -Level "Warning" -Category $logCategory
        $revitProcess.Kill()
        Start-Sleep -Seconds 2
        Write-RcaLog "Revit process terminated forcefully" -Level "Information" -Category $logCategory
    }

    # 5. Copy updated assembly
    if (!(Test-Path $TargetPath)) {
        Write-RcaLog "Creating target directory: $TargetPath" -Level "Debug" -Category $logCategory
        New-Item -Path $TargetPath -ItemType Directory -Force | Out-Null
    }
    
    Write-RcaLog "Copying merged Loader assembly from $loaderSourcePath to $TargetPath" -Level "Information" -Category $logCategory
    Copy-Item -Path $loaderSourcePath -Destination (Join-Path $TargetPath $loaderFile) -Force
    
    # Verify the copy was successful
    $targetLoaderPath = Join-Path $TargetPath $loaderFile
    if (!(Test-Path $targetLoaderPath)) {
        $errorMsg = "Failed to copy Loader assembly to $targetLoaderPath"
        Write-RcaLog $errorMsg -Level "Error" -Category $logCategory
        throw $errorMsg
    }
    
    $fileSize = (Get-Item $targetLoaderPath).Length
    Write-RcaLog "Successfully copied Loader assembly ($fileSize bytes)" -Level "Information" -Category $logCategory

    # 6. Restart Revit
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
