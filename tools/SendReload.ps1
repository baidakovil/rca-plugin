param(
    [string]$Folder = $null,
    [string]$PipeName = "rca.hotreload"
)

Write-Host "RCA Hot Reload Client" -ForegroundColor Green
Write-Host "===================="

try {
    # Prepare the message
    $payload = @{}
    if ($Folder) {
        $payload["folder"] = $Folder
    }

    $message = @{
        type = "RELOAD"
        payload = $payload
    } | ConvertTo-Json -Compress

    Write-Host "Connecting to pipe: $PipeName"
    Write-Host "Sending message: $message"

    # Connect to the named pipe
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $PipeName, [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(5000)  # 5 second timeout

    # Send the message
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($message)
    $pipe.Write($bytes, 0, $bytes.Length)
    $pipe.Flush()

    # Read response
    $buffer = New-Object byte[] 4096
    $bytesRead = $pipe.Read($buffer, 0, $buffer.Length)
    
    if ($bytesRead -gt 0) {
        $response = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $bytesRead)
        Write-Host "Response: $response" -ForegroundColor Yellow
    }

    $pipe.Close()
    Write-Host "Hot reload request sent successfully!" -ForegroundColor Green
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure the RCA Loader is running in Revit." -ForegroundColor Yellow
    exit 1
}