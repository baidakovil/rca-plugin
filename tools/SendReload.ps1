# SendReload.ps1 - Manual hot reload trigger for RCA Plugin
param(
    [string]$Folder = $null,
    [int]$TimeoutMs = 5000
)

function Send-ReloadCommand {
    param(
        [string]$FolderPath,
        [int]$TimeoutMs
    )

    try {
        Write-Host "Attempting to send reload command..." -ForegroundColor Yellow
        
        # Create named pipe client
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'rca.hotreload', 'Out')
        
        # Connect to pipe
        $pipe.Connect($TimeoutMs)
        Write-Host "Connected to reload pipe" -ForegroundColor Green
        
        # Prepare command message
        $command = @{
            command = "RELOAD"
            payload = @{
                folder = $FolderPath
            }
        }
        
        $json = $command | ConvertTo-Json -Compress
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        
        # Send command
        $pipe.Write($bytes, 0, $bytes.Length)
        $pipe.Flush()
        
        Write-Host "Reload command sent successfully" -ForegroundColor Green
        
        # Read response
        $buffer = New-Object byte[] 4096
        $bytesRead = $pipe.Read($buffer, 0, $buffer.Length)
        
        if ($bytesRead -gt 0) {
            $response = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $bytesRead)
            $responseObj = $response | ConvertFrom-Json
            
            Write-Host "Response: $($responseObj.Event)" -ForegroundColor Cyan
            if ($responseObj.Payload) {
                Write-Host "Details: $($responseObj.Payload | ConvertTo-Json)" -ForegroundColor Gray
            }
        }
        
        $pipe.Close()
        
    }
    catch [System.TimeoutException] {
        Write-Host "Timeout: No RCA Loader found or pipe server not responding" -ForegroundColor Red
        Write-Host "Make sure Revit with RCA Loader is running" -ForegroundColor Yellow
    }
    catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }
    finally {
        if ($pipe) {
            $pipe.Dispose()
        }
    }
}

# Main execution
if (-not $Folder) {
    # Try to find latest runtime folder
    $stagingRoot = Join-Path $env:LOCALAPPDATA "RCA\LiveCore"
    
    if (Test-Path $stagingRoot) {
        $manifestFile = Join-Path $stagingRoot "current.json"
        
        if (Test-Path $manifestFile) {
            try {
                $manifest = Get-Content $manifestFile | ConvertFrom-Json
                $Folder = $manifest.folder
                Write-Host "Using folder from manifest: $Folder" -ForegroundColor Cyan
            }
            catch {
                Write-Host "Warning: Could not read manifest file" -ForegroundColor Yellow
            }
        }
        
        if (-not $Folder) {
            # Find most recent build folder
            $buildFolders = Get-ChildItem $stagingRoot -Directory | Where-Object { $_.Name -match "^build_\d{8}_\d{6}$" } | Sort-Object Name -Descending
            
            if ($buildFolders) {
                $Folder = $buildFolders[0].FullName
                Write-Host "Using most recent build folder: $Folder" -ForegroundColor Cyan
            }
        }
    }
    
    if (-not $Folder) {
        Write-Host "Error: No runtime folder specified and none found automatically" -ForegroundColor Red
        Write-Host ""
        Write-Host "Usage:" -ForegroundColor White
        Write-Host "  .\SendReload.ps1 -Folder 'C:\Path\To\Runtime\Folder'" -ForegroundColor Gray
        Write-Host "  .\SendReload.ps1" -ForegroundColor Gray -NoNewline
        Write-Host " (uses latest built runtime)" -ForegroundColor Yellow
        exit 1
    }
}

if (-not (Test-Path $Folder)) {
    Write-Host "Error: Folder not found: $Folder" -ForegroundColor Red
    exit 1
}

Write-Host "RCA Plugin Hot Reload Tool" -ForegroundColor Magenta
Write-Host "Runtime Folder: $Folder" -ForegroundColor White
Write-Host ""

Send-ReloadCommand -FolderPath $Folder -TimeoutMs $TimeoutMs