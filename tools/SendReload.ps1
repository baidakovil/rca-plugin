param(
    [Parameter(Mandatory=$false)]
    [string]$Folder = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$Force = $false
)

# Manual reload helper for RCA Plugin Hot Reload
Write-Host "RCA Plugin Hot Reload Helper" -ForegroundColor Green
Write-Host "============================" -ForegroundColor Green

try {
    # Connect to the named pipe
    $pipeName = "rca.hotreload"
    Write-Host "Connecting to pipe: $pipeName" -ForegroundColor Yellow
    
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(5000)  # 5 second timeout
    
    Write-Host "Connected successfully" -ForegroundColor Green
    
    # Create stream readers/writers
    $writer = New-Object System.IO.StreamWriter($pipe)
    $reader = New-Object System.IO.StreamReader($pipe)
    
    # Build the command payload
    $reloadPayload = @{
        Command = "RELOAD"
    }
    
    if ($Folder) {
        $reloadPayload.Folder = $Folder
        Write-Host "Using specific folder: $Folder" -ForegroundColor Cyan
    } else {
        Write-Host "Using manifest folder" -ForegroundColor Cyan
    }
    
    if ($Force) {
        $reloadPayload.Force = $true
        Write-Host "Force reload enabled" -ForegroundColor Cyan
    }
    
    # Build the full message
    $command = @{
        Type = "COMMAND"
        Payload = ($reloadPayload | ConvertTo-Json)
        Timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    }
    
    $commandJson = $command | ConvertTo-Json -Compress
    Write-Host "Sending command: $commandJson" -ForegroundColor Yellow
    
    # Send the command
    $writer.WriteLine($commandJson)
    $writer.Flush()
    
    # Wait for response
    Write-Host "Waiting for response..." -ForegroundColor Yellow
    $response = $reader.ReadLine()
    
    if ($response) {
        Write-Host "Response received: $response" -ForegroundColor Green
        
        # Try to parse the response
        try {
            $responseObj = $response | ConvertFrom-Json
            if ($responseObj.Event -eq "RELOAD_ACCEPTED") {
                Write-Host "Reload command accepted by loader" -ForegroundColor Green
            } elseif ($responseObj.Event -eq "RELOAD_FAIL") {
                Write-Host "Reload failed: $($responseObj.Payload)" -ForegroundColor Red
            } else {
                Write-Host "Received event: $($responseObj.Event)" -ForegroundColor Cyan
            }
        } catch {
            Write-Host "Response received but could not parse as JSON" -ForegroundColor Yellow
        }
    } else {
        Write-Host "No response received" -ForegroundColor Yellow
    }
    
} catch [System.TimeoutException] {
    Write-Host "Timeout connecting to pipe. Is Revit running with the RCA Loader?" -ForegroundColor Red
    exit 1
} catch [System.IO.IOException] {
    Write-Host "IO Error: Pipe server may not be running. Is Revit running with the RCA Loader?" -ForegroundColor Red
    exit 1
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    # Clean up
    if ($reader) { $reader.Dispose() }
    if ($writer) { $writer.Dispose() }
    if ($pipe) { $pipe.Dispose() }
}

Write-Host "Manual reload command completed" -ForegroundColor Green