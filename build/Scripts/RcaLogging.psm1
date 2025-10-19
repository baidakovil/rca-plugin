# RcaLogging.psm1
# PowerShell module for sending logs to RCA Unified Logging System via Named Pipe

<#
.SYNOPSIS
    Sends a log message to RCA unified logging system via named pipe.

.DESCRIPTION
    Attempts to send a log entry to the RCA_LOG_PIPE named pipe.
    Falls back silently if pipe is unavailable (allows standalone execution).
    
.PARAMETER Message
    The log message text.
    
.PARAMETER Level
    Log level: Trace, Debug, Information, Warning, Error, Critical.
    Default: Information
    
.PARAMETER Category
    Log category/source identifier.
    Default: PowerShell
    
.EXAMPLE
    Send-LogToRca -Message "Script started" -Level "Information"
    
.EXAMPLE
    Send-LogToRca -Message "File not found" -Level "Error" -Category "RestartScript"
#>
function Send-LogToRca {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,
        
        [Parameter(Mandatory=$false)]
        [ValidateSet("Trace", "Debug", "Information", "Warning", "Error", "Critical")]
        [string]$Level = "Information",
        
        [Parameter(Mandatory=$false)]
        [string]$Category = "PowerShell"
    )
    
    try {
        # Create named pipe client
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(
            ".",                    # Server name (local machine)
            "RCA_LOG_PIPE",        # Pipe name (must match C# LoggingPipeServerService)
            [System.IO.Pipes.PipeDirection]::Out
        )
        
        # Try to connect with short timeout (don't block script if pipe unavailable)
        $pipe.Connect(500)
        
        if ($pipe.IsConnected) {
            # Create LogEntryDto JSON matching C# structure from Rca.Logging.Contracts
            $logEntry = @{
                Timestamp = (Get-Date).ToString("o")  # ISO 8601 format
                Level = $Level
                Category = $Category
                Message = $Message
                Exception = $null
                State = $null
                EventId = 0
                SessionId = "PowerShell-$PID"  # Include process ID for correlation
            } | ConvertTo-Json -Compress
            
            # Send JSON to pipe
            $writer = New-Object System.IO.StreamWriter($pipe)
            $writer.AutoFlush = $true
            $writer.WriteLine($logEntry)
            $writer.Dispose()
        }
        
        $pipe.Dispose()
    }
    catch {
        # Silently ignore pipe connection failures
        # This allows script to run even if logging pipe is unavailable
        # Fallback file logging will still work
    }
}

<#
.SYNOPSIS
    Writes a log message to both unified logging and fallback file.

.DESCRIPTION
    Primary logging function that sends logs to:
    1. RCA unified logging via named pipe (if available)
    2. Fallback text file (always)
    
.PARAMETER Message
    The log message text.
    
.PARAMETER Level
    Log level: Trace, Debug, Information, Warning, Error, Critical.
    Default: Information
    
.PARAMETER Category
    Log category/source identifier.
    Default: PowerShell
    
.PARAMETER LogFile
    Path to fallback log file.
    Default: C:\Users\{username}\AppData\Local\RCA\Logs\RestartScript.log
    
.EXAMPLE
    Write-RcaLog "Script started"
    
.EXAMPLE
    Write-RcaLog "Error occurred" -Level "Error" -Category "RestartScript"
#>
function Write-RcaLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,
        
        [Parameter(Mandatory=$false)]
        [ValidateSet("Trace", "Debug", "Information", "Warning", "Error", "Critical")]
        [string]$Level = "Information",
        
        [Parameter(Mandatory=$false)]
        [string]$Category = "PowerShell",
        
        [Parameter(Mandatory=$false)]
        [string]$LogFile = "$env:LOCALAPPDATA\RCA\Logs\RestartScript.log"
    )
    
    # Try unified logging via pipe
    Send-LogToRca -Message $Message -Level $Level -Category $Category
    
    # Always write to fallback file
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logLine = "$timestamp [$Level] [$Category] - $Message"
    
    try {
        # Ensure log directory exists
        $logDir = Split-Path -Parent $LogFile
        if (!(Test-Path $logDir)) {
            New-Item -Path $logDir -ItemType Directory -Force | Out-Null
        }
        
        $logLine | Out-File -FilePath $LogFile -Append -Encoding UTF8
    }
    catch {
        # Even fallback failed - nothing we can do
        # Don't throw - script must continue
    }
}

# Export functions
Export-ModuleMember -Function Send-LogToRca, Write-RcaLog
