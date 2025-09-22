param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,
    
    [Parameter(Mandatory=$true)]
    [string]$TargetPath,
    
    [Parameter(Mandatory=$true)]
    [string]$RevitExecutable,
    
    [Parameter(Mandatory=$true)]
    [string]$JsonFilePath
)

# Helper function for logging
function Write-LogMessage {
    param(
        [string]$Message,
        [string]$LogFile = "$env:TEMP\RcaRestartLog.txt"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $Message" | Out-File -FilePath $LogFile -Append
}

Write-LogMessage "RestartRevitGraceful script started"
Write-LogMessage "Parameters: SourcePath=$SourcePath, TargetPath=$TargetPath, RevitExecutable=$RevitExecutable"

try {
    # 1. Check for merged Loader assembly
    $loaderFile = "Rca.Loader.dll"
    $loaderSourcePath = Join-Path $SourcePath $loaderFile
    
    if (!(Test-Path $loaderSourcePath)) {
        $errorMsg = "Error: Required merged Loader assembly not found at $loaderSourcePath"
        Write-LogMessage $errorMsg
        throw $errorMsg
    }
    
    # 2. Find Revit process
    $revitProcess = Get-Process | Where-Object { $_.Path -eq $RevitExecutable }
    if (!$revitProcess) {
        Write-LogMessage "Error: Revit process not found with path: $RevitExecutable"
        exit 1
    }

    # 3. Gracefully close Revit
    Write-LogMessage "Closing Revit gracefully..."
    $revitProcess.CloseMainWindow() | Out-Null
    
    # Wait for Revit to close gracefully (up to 30 seconds)
    $timeoutSeconds = 30
    $startTime = Get-Date
    $closed = $false
    
    while ((Get-Date).Subtract($startTime).TotalSeconds -lt $timeoutSeconds) {
        if ($revitProcess.HasExited) {
            $closed = $true
            break
        }
        Start-Sleep -Seconds 1
    }

    # 4. Force close if not exited
    if (!$closed) {
        Write-LogMessage "Forcing Revit to close..."
        $revitProcess.Kill()
        Start-Sleep -Seconds 2
    }

    # 5. Copy updated assembly
    if (!(Test-Path $TargetPath)) {
        Write-LogMessage "Creating target directory: $TargetPath"
        New-Item -Path $TargetPath -ItemType Directory -Force | Out-Null
    }
    
    Write-LogMessage "Copying merged Loader assembly..."
    Copy-Item -Path $loaderSourcePath -Destination (Join-Path $TargetPath $loaderFile) -Force
    
    # Verify the copy was successful
    if (!(Test-Path (Join-Path $TargetPath $loaderFile))) {
        $errorMsg = "Failed to copy Loader assembly to target directory"
        Write-LogMessage $errorMsg
        throw $errorMsg
    }
    
    # 6. Update JSON file
    Write-LogMessage "Updating JSON file: $JsonFilePath"
    
    if (Test-Path $JsonFilePath) {
        try {
            $json = Get-Content -Path $JsonFilePath -Raw | ConvertFrom-Json
            
            # Update the loader path in the JSON file
            
            # Update JSON state
            $json.LoaderComponents.Path = $TargetPath
            $json.LastMSBuildSignal.Time = (Get-Date -Format "HH:mm:ss")
            $json.LastMSBuildSignal.Event = "restart completed"
            
            $json | ConvertTo-Json -Depth 4 | Set-Content -Path $JsonFilePath
        } catch {
            Write-LogMessage "Error updating JSON: $_"
            throw $_
        }
    } else {
        $errorMsg = "JSON file not found: $JsonFilePath"
        Write-LogMessage $errorMsg
        throw $errorMsg
    }

    # 7. Restart Revit
    Write-LogMessage "Restarting Revit..."
    Start-Process -FilePath $RevitExecutable
    
    Write-LogMessage "RestartRevitGraceful script completed successfully"
    exit 0
} 
catch {
    Write-LogMessage "Error in RestartRevitGraceful script: $_"
    exit 1
}
