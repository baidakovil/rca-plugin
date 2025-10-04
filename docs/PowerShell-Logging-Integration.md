# PowerShell Logging Integration

**Date:** 2024-10-05  
**Component:** Build Scripts  
**Integration:** Unified Logging System

## Overview

PowerShell scripts used during the Revit restart process now send logs to the unified logging system via Named Pipe, making all script activity visible in Visual Studio Output window and other logging destinations.

## Components

### 1. RcaLogging Module ([RcaLogging.psm1](../build/Scripts/RcaLogging.psm1))

Reusable PowerShell module for logging integration.

**Functions:**

#### `Send-LogToRca`
Sends a log entry to `RCA_LOG_PIPE` named pipe.

```powershell
Send-LogToRca -Message "Operation completed" -Level "Information" -Category "MyScript"
```

**Parameters:**
- `Message` (required): Log message text
- `Level` (optional): Trace|Debug|Information|Warning|Error|Critical (default: Information)
- `Category` (optional): Log category/source (default: PowerShell)

**Behavior:**
- Connects to pipe with 500ms timeout
- Sends JSON matching `LogEntryDto` structure from C#
- Silently fails if pipe unavailable (allows standalone execution)

#### `Write-RcaLog`
Primary logging function with dual output.

```powershell
Write-RcaLog "Script started" -Level "Information" -Category "RestartScript"
```

**Outputs to:**
1. **Named Pipe** (unified logging) - if available
2. **File** (`%LocalAppData%\RCA\Logs\RestartScript.log`) - always (fallback)

### 2. RestartRevitGraceful Script ([RestartRevitGraceful.ps1](../build/Scripts/RestartRevitGraceful.ps1))

Updated to use `RcaLogging` module for all logging operations.

**Integration:**
```powershell
# Import module
Import-Module (Join-Path $scriptDir "RcaLogging.psm1") -Force

# Use unified logging
Write-RcaLog "Revit closed gracefully" -Level "Information" -Category "RestartScript"
Write-RcaLog "Forcing Revit to close" -Level "Warning" -Category "RestartScript"
Write-RcaLog "Failed to copy file" -Level "Error" -Category "RestartScript"
```

**Log Levels Used:**
- `Debug`: Internal state, parameter values, file paths
- `Information`: Major steps (found process, copied files, restarted Revit)
- `Warning`: Non-critical issues (force close Revit)
- `Error`: Recoverable errors (file not found, JSON update failed)
- `Critical`: Fatal errors that abort the script

## Data Flow

```
PowerShell Script
    ↓
Write-RcaLog function
    ↓
    ├─→ Send-LogToRca → Named Pipe (RCA_LOG_PIPE)
    │                        ↓
    │                   LoggingPipeServerService (C#)
    │                        ↓
    │                   Unified Logging System
    │                        ↓
    │                   Visual Studio Output, File, etc.
    │
    └─→ Fallback File (%LocalAppData%\RCA\Logs\RestartScript.log)
```

## JSON Format

PowerShell sends JSON matching C# `LogEntryDto`:

```json
{
  "Timestamp": "2024-10-05T12:34:56.789Z",
  "Level": "Information",
  "Category": "RestartScript",
  "Message": "Revit closed gracefully after 3.2 seconds",
  "Exception": null,
  "State": null,
  "EventId": 0,
  "SessionId": "PowerShell-12345"
}
```

## Benefits

1. **Visibility**: All script activity visible in Visual Studio Output
2. **Real-time**: Logs appear as script executes (no waiting for completion)
3. **Unified**: Same logging format as C# components
4. **Resilient**: Falls back to file if pipe unavailable
5. **Debuggable**: Include process IDs, timing info, file sizes

## Usage Example

From C# ([RestartManager.cs](../src/Rca.Loader/Restart/RestartManager.cs)):

```csharp
// Start PowerShell script - logs automatically appear in Output window
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" ...",
        // ...
    }
};
process.Start();
```

Logs in Visual Studio Output:
```
[12:34:56] [Information] [RestartScript] RestartRevitGraceful script started
[12:34:56] [Debug] [RestartScript] Parameters: SourcePath=C:\..., TargetPath=C:\...
[12:34:57] [Information] [RestartScript] Found Revit process (PID: 15132)
[12:34:57] [Information] [RestartScript] Closing Revit gracefully...
[12:35:00] [Information] [RestartScript] Revit closed gracefully after 3.2 seconds
[12:35:01] [Information] [RestartScript] Successfully copied Loader assembly (245760 bytes)
[12:35:02] [Information] [RestartScript] Restarting Revit: C:\Program Files\Autodesk\Revit 2026\Revit.exe
[12:35:02] [Information] [RestartScript] RestartRevitGraceful script completed successfully
```

## Error Handling

- **Pipe unavailable**: Silently falls back to file logging
- **Pipe timeout**: 500ms timeout prevents blocking
- **JSON serialization fails**: Script continues (logs to file only)
- **All errors logged**: Stack traces included for debugging

## Future Enhancements

- Support for structured state (key-value pairs)
- Correlation IDs for tracking operations across C#/PowerShell
- Batching for high-volume logging scenarios
- Compression for large log messages

## See Also

- [Logging-System.md](Logging-System.md) - Unified logging architecture
- [HRS-Developer-Guide.md](HRS-Developer-Guide.md) - Hot-reload system overview
- [RestartManager.cs](../src/Rca.Loader/Restart/RestartManager.cs) - C# restart orchestration
