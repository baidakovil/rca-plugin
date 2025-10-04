# Hot-Reloading Developer Guide

This guide provides practical information for developers working with the RCA hot-reloading system, including everyday workflows, troubleshooting tips, and best practices.

## Quick Reference

| Scenario | Action | Requirements |
|----------|--------|-------------|
| Update Runtime Only | Click "Reload Runtime" button in Revit | New build in temp folder |
| Update Loader Components | Must restart Revit | Use dialog from "Reload Runtime" |
| Debug System State | Check Status Display in Debug panel | DEBUG build required |
| Reset System State | Delete LoadedAssemblies.json | Revit must be closed |

## Development Workflow

### 1. Setting Up Build Output

Configure your development environment to output builds to the hot-reload directory:

```xml
<!-- In .csproj file for Runtime project -->
<PropertyGroup>
  <OutputPath>$(LocalAppData)\RCA\Runtime\$(MSBuildProjectName)_$(Configuration)_$(Platform)_$(TargetFramework)_$([System.DateTime]::Now.ToString("yyyyMMdd_HHmmss"))</OutputPath>
</PropertyGroup>
```

### 2. Manual Testing Cycle

1. Make changes to Runtime code
2. Build project (outputs to timestamp folder)
3. In Revit, click "Reload Runtime" button
4. Verify changes are applied without restarting Revit

### 3. CI/CD Integration

For automated builds, send commands to the pipe server:

```csharp
// Example code for build script
using var pipeClient = new NamedPipeClientStream(".", "RCA_PIPE", PipeDirection.InOut);
pipeClient.Connect(5000); // 5 second timeout

using var writer = new StreamWriter(pipeClient);
using var reader = new StreamReader(pipeClient);

// Send reload command with path to new build
var cmd = new { Command = "RELOAD_RUNTIME", Payload = @"C:\Users\...\AppData\Local\RCA\Runtime\20231101-120000" };
var cmdJson = JsonSerializer.Serialize(cmd);
await writer.WriteLineAsync(cmdJson);
await writer.FlushAsync();

// Read response
var response = await reader.ReadLineAsync();
Console.WriteLine($"Response: {response}");
```

## Understanding Hot-Reload Limitations

### What CAN be hot-reloaded:

✅ New internal methods and logic  
✅ Changes to existing method implementations  
✅ New classes that don't affect public interfaces  
✅ Resource changes (images, text, etc.)  
✅ Bug fixes that don't change public APIs  

### What CANNOT be hot-reloaded (requires restart):

❌ Changes to public interfaces used by Loader  
❌ Changes to method signatures of existing public methods  
❌ Structural changes to data models used across assemblies  
❌ Changes to merged Loader assembly  
❌ Database schema changes  

## Understanding the Merged Loader Assembly

The Loader uses ILRepack to merge Rca.Loader.dll and Rca.Loader.Contracts.dll into a single assembly for deployment:

### Benefits of the merged approach:

1. **Simplified deployment**: Only one DLL needs to be managed
2. **Version consistency**: Eliminates version mismatches between Loader and Contracts
3. **Reduced footprint**: Smaller total deployment size
4. **Better encapsulation**: Contract types are internalized where appropriate

### How the merge works:

1. MSBuild process uses ILRepack to combine assemblies at build time
2. Custom MSBuild task handles dependencies like RevitAPI.dll properly
3. The merged assembly includes all types from both original assemblies
4. Public API surface remains the same as before merging

## Debugging the Hot-Reload System

### Debug Status Display

In DEBUG builds, the Debug panel shows:
- **Loader (Merged)**: Shows current/outdated status and directory
- **Runtime**: Shows current/outdated status and directory
- **Last MSBuild signal**: Shows timestamp and status message

### Diagnostic Logging

Add this to your `app.config` for additional logging:

```xml
<configuration>
  <system.diagnostics>
    <trace autoflush="true">
      <listeners>
        <add name="FileListener" 
             type="System.Diagnostics.TextWriterTraceListener" 
             initializeData="RcaLoader.log"/>
      </listeners>
    </trace>
  </system.diagnostics>
</configuration>
```

### Common Error Messages and Solutions

| Error Message | Likely Cause | Solution |
|---------------|--------------|----------|
| "Failed to reload runtime: Could not load file or assembly" | File access issue or missing dependencies | Verify file permissions and dependencies |
| "Failed to reload runtime: Method not found" | Breaking API changes | Restart Revit completely |
| "Failed to create debug UI" | Missing UI components in DEBUG build | Rebuild with correct configuration |
| "Error executing restart script" | PowerShell execution policy or path issues | Check script path and execution policy |
| "ILRepack failed with exit code" | RevitAPI dependencies not found or accessibility issues | Ensure RevitAPI.dll is available during build |

## Managing Assembly State

### Viewing Current State

Examine `%LOCALAPPDATA%\RCA\LoadedAssemblies.json` to see:
- Currently loaded assembly paths
- Hash values for each assembly
- Latest MSBuild signal information

### Resetting the System

If hot-reloading behaves unexpectedly:

1. Close Revit completely
2. Delete `%LOCALAPPDATA%\RCA\LoadedAssemblies.json`
3. Restart Revit
4. System will recalculate all hashes from current state

## Restart Process Details

When the merged Loader assembly needs updating, the restart process:

1. Shows countdown dialog with options to restart now or later
2. If "Restart Now" is chosen, executes `RestartRevitGraceful.ps1`
3. Script saves open documents and closes Revit
4. Updates the merged assembly in Revit's addin directory
5. Updates JSON state file
6. Restarts Revit automatically

## Testing Changes with Manual Deployment

For complex changes, test manually by:

1. Build your solution
2. Copy merged `Rca.Loader.dll` to `%APPDATA%\Autodesk\Revit\Addins\2026\`
3. Copy `Rca.Runtime.dll` to a new folder under `%LOCALAPPDATA%\RCA\Runtime\`
4. Start Revit and verify functionality

## ILRepack Build Considerations

When working with the Loader build system:

1. **RevitAPI Dependencies**:
   - The build requires access to RevitAPI.dll and RevitAPIUI.dll
   - Default location is `libs\Revit\2026\` in the solution directory
   - Build will fail if dependencies cannot be found

2. **Custom MSBuild Task**:
   - Uses a custom task to handle merging with proper dependency resolution
   - Requires ilrepack NuGet package to be installed
   - Strict failure policy - will not produce partial results

3. **Build Configurations**:
   - Debug builds contain additional diagnostic information
   - Release builds are optimized for deployment
   - Both use the same merging process

## Best Practices

1. **Maintain backward compatibility** in Runtime changes to support hot-reload
2. **Update the merged Loader assembly sparingly** to minimize Revit restarts
3. **Add DEBUG logging statements** in critical paths for troubleshooting
4. **Use versioning** in your API methods to maintain compatibility
5. **Monitor the status display** during development to understand system state
6. **Create unit tests** for components to verify compatibility before hot-reload
7. **Ensure RevitAPI.dll is available** during builds for successful merging

## Further Reading

For more detailed information about the hot-reloading system:

- [HOT-RELOADING.md](HOT-RELOADING.md): Complete component documentation
- [REVIT-INTEGRATION.md](REVIT-INTEGRATION.md): Detailed Revit integration information
