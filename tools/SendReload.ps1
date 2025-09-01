param(
    [string]$Folder = $null
)

# PowerShell script to send manual reload command to RCA Hot Reload Loader
# Usage: pwsh -File tools/SendReload.ps1 [-Folder <timestamp_folder>]

$pipeName = "rca.hotreload"

try {
    Write-Host "Connecting to hot reload pipe: $pipeName"
    
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, "Out")
    $pipe.Connect(5000)  # 5 second timeout
    
    $writer = New-Object System.IO.StreamWriter($pipe)
    
    $command = @{
        type = "RELOAD"
        payload = @{}
    }
    
    if ($Folder) {
        $command.payload.folder = $Folder
        Write-Host "Sending reload command with folder override: $Folder"
    } else {
        Write-Host "Sending reload command (will use current manifest)"
    }
    
    $json = $command | ConvertTo-Json -Compress
    $writer.WriteLine($json)
    $writer.Flush()
    
    $pipe.Close()
    Write-Host "Reload command sent successfully!" -ForegroundColor Green
}
catch {
    Write-Host "Failed to send reload command: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure Revit is running with the RCA Loader add-in." -ForegroundColor Yellow
    exit 1
}