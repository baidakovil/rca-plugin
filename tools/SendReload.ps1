param(
    [Parameter(Mandatory=$false)]
    [string]$Folder,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force
)

<#
.SYNOPSIS
    Sends a reload command to the RCA Loader via Named Pipe.

.DESCRIPTION
    This script sends a RELOAD command to the running RCA Loader instance through
    the Named Pipe interface. This allows for manual triggering of hot reload
    functionality when automatic build triggering is not working.

.PARAMETER Folder
    Optional folder path containing the runtime assembly to load.
    If not specified, the loader will use the current manifest.

.PARAMETER Force
    Force reload even if no changes are detected.

.EXAMPLE
    .\SendReload.ps1
    Triggers reload using current manifest

.EXAMPLE
    .\SendReload.ps1 -Folder "C:\Users\Developer\AppData\Local\RCA\LiveCore\build_20241201_143052"
    Reloads from specific folder

.EXAMPLE
    .\SendReload.ps1 -Force
    Forces reload even if no changes detected
#>

try {
    $pipeName = "rca.hotreload"
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', $pipeName, 'InOut')
    
    Write-Host "Connecting to RCA Loader..."
    $pipe.Connect(5000)  # 5 second timeout
    
    $payload = @{
        folder = $Folder
        force = $Force.IsPresent
    }
    
    $command = @{
        type = "COMMAND"
        command = "RELOAD"
        payload = $payload
        timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    } | ConvertTo-Json -Compress
    
    $bytes = [Text.Encoding]::UTF8.GetBytes($command + "`n")
    $pipe.Write($bytes, 0, $bytes.Length)
    $pipe.Flush()
    
    Write-Host "Reload command sent successfully"
    
    # Read response
    $buffer = New-Object byte[] 4096
    $bytesRead = $pipe.Read($buffer, 0, $buffer.Length)
    if ($bytesRead -gt 0) {
        $response = [Text.Encoding]::UTF8.GetString($buffer, 0, $bytesRead)
        Write-Host "Response: $response"
    }
    
    $pipe.Close()
    Write-Host "Reload request completed"
}
catch [System.TimeoutException] {
    Write-Error "Timeout connecting to RCA Loader. Is Revit running with the loader?"
    exit 1
}
catch [System.IO.IOException] {
    Write-Error "Failed to connect to RCA Loader. Is Revit running with the loader?"
    exit 1
}
catch {
    Write-Error "Error sending reload command: $($_.Exception.Message)"
    exit 1
}