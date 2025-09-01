param(
    [Parameter(Mandatory = $false)]
    [string]$Folder = ""
)

<#
.SYNOPSIS
    Sends a reload command to the RCA Hot Reload Loader via Named Pipe.

.DESCRIPTION
    This script connects to the RCA Loader's named pipe and sends a RELOAD command.
    If no folder is specified, the loader will use the current manifest.

.PARAMETER Folder
    Optional. Specific folder containing the runtime to reload.

.EXAMPLE
    .\SendReload.ps1
    # Reloads using current manifest

.EXAMPLE
    .\SendReload.ps1 -Folder "C:\Users\User\AppData\Local\RCA\LiveCore\build_20240101_120000"
    # Reloads specific build folder
#>

try {
    Write-Host "Connecting to RCA Hot Reload Loader..." -ForegroundColor Yellow
    
    # Create named pipe client
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'rca.hotreload', 'InOut')
    
    # Connect with timeout
    $pipe.Connect(5000)
    Write-Host "Connected successfully." -ForegroundColor Green
    
    # Create stream readers/writers
    $writer = New-Object System.IO.StreamWriter($pipe)
    $reader = New-Object System.IO.StreamReader($pipe)
    $writer.AutoFlush = $true
    
    # Create command payload
    $command = @{
        command = 'RELOAD'
        payload = @{}
    }
    
    if (-not [string]::IsNullOrEmpty($Folder)) {
        $command.payload.folder = $Folder
        Write-Host "Requesting reload of folder: $Folder" -ForegroundColor Cyan
    } else {
        Write-Host "Requesting reload using current manifest..." -ForegroundColor Cyan
    }
    
    # Send command
    $commandJson = $command | ConvertTo-Json -Compress
    $writer.WriteLine($commandJson)
    
    # Read responses
    $timeout = 10
    $timeoutTime = (Get-Date).AddSeconds($timeout)
    
    Write-Host "Waiting for responses (timeout: ${timeout}s)..." -ForegroundColor Yellow
    
    while ((Get-Date) -lt $timeoutTime) {
        if ($pipe.IsConnected -and $reader.Peek() -ge 0) {
            $response = $reader.ReadLine()
            
            if (-not [string]::IsNullOrEmpty($response)) {
                try {
                    $responseObj = $response | ConvertFrom-Json
                    
                    switch ($responseObj.Event) {
                        "RELOAD_ACCEPTED" {
                            Write-Host "✓ Reload accepted by loader" -ForegroundColor Green
                        }
                        "RELOAD_START" {
                            Write-Host "⚡ Reload started..." -ForegroundColor Yellow
                        }
                        "RELOAD_DONE" {
                            Write-Host "✅ Reload completed successfully!" -ForegroundColor Green
                            break
                        }
                        "RELOAD_FAIL" {
                            Write-Host "❌ Reload failed:" -ForegroundColor Red
                            if ($responseObj.Data) {
                                Write-Host $responseObj.Data.Message -ForegroundColor Red
                            }
                            break
                        }
                        "RUNTIME_ERROR" {
                            Write-Host "❌ Runtime error occurred:" -ForegroundColor Red
                            if ($responseObj.Data) {
                                Write-Host $responseObj.Data.Message -ForegroundColor Red
                            }
                            break
                        }
                        default {
                            Write-Host "📨 $($responseObj.Event): $response" -ForegroundColor Gray
                        }
                    }
                } catch {
                    Write-Host "📨 Raw response: $response" -ForegroundColor Gray
                }
            }
        }
        
        Start-Sleep -Milliseconds 100
    }
    
    Write-Host "Closing connection..." -ForegroundColor Yellow
    
} catch [System.TimeoutException] {
    Write-Host "❌ Connection timeout. Is Revit running with RCA Loader?" -ForegroundColor Red
    exit 1
} catch [System.IO.IOException] {
    Write-Host "❌ Pipe connection failed. Is Revit running with RCA Loader?" -ForegroundColor Red
    exit 1
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    # Clean up
    if ($reader) { $reader.Dispose() }
    if ($writer) { $writer.Dispose() }
    if ($pipe) { $pipe.Dispose() }
}

Write-Host "Done." -ForegroundColor Green