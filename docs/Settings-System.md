# Settings System

The Revit Chat Assistant plugin uses a simple JSON-based settings system to configure runtime behavior.

## Overview

Settings are stored in a JSON file and automatically loaded on startup with fallback to default values if the file is missing or corrupted.

**Settings File Location:**
```
%ProgramData%\Autodesk\Revit\Addins\2026\Revit Chat Assistant\settings.json
```

## Architecture

The settings system consists of three main components:

### 1. Settings Model ([Settings.cs](../src/Rca.Loader/Configuration/Settings.cs))

Defines the structure of available settings:
- **General settings**: Available in both DEBUG and RELEASE builds
- **Debug settings**: Only available in DEBUG builds (conditionally compiled with `#if DEBUG`)

```csharp
public class Settings
{
    public bool AutoLoadRuntimeOnStartup { get; set; } = true;
    
#if DEBUG
    public DebugSettings Debug { get; set; } = new DebugSettings();
#endif
}
```

### 2. Settings Service ([SettingsService.cs](../src/Rca.Loader/Configuration/SettingsService.cs))

Provides thread-safe loading and caching of settings:
- Automatically creates settings file on first build if it doesn't exist
- Never overwrites existing settings (preserves user customization)
- Falls back to default values if file is corrupted or missing
- Supports JSON comments and trailing commas

```csharp
var settings = SettingsService.LoadSettings();
if (settings.AutoLoadRuntimeOnStartup)
{
    // Auto-load runtime on startup
}
```

### 3. Settings File ([settings.json](../src/Rca.Loader/Resources/settings.json))

Default settings template deployed during build:

```json
{
  "autoLoadRuntimeOnStartup": true,
  
  "debug": {
    "verboseLogging": true,
    "autoShowPanelOnLoad": false
  }
}
```

## Available Settings

### General Settings (All Builds)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `autoLoadRuntimeOnStartup` | bool | `true` | Automatically load the runtime assembly when Revit starts |

### Debug Settings (DEBUG Build Only)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `debug.verboseLogging` | bool | `true` | Enable verbose logging for debugging |
| `debug.autoShowPanelOnLoad` | bool | `false` | Automatically show the dockable panel after runtime loads |
| `debug.restartScriptPath` | string | `%USERPROFILE%\rca-plugin\build\Scripts\RestartRevitGraceful.ps1` | Path to the PowerShell restart script (supports environment variables) |
| `debug.revitProjectFilePath` | string | `null` | Revit project file to open on restart (supports environment variables and network paths, e.g., `\\Mac\Home\Documents\Project1.rvt`) |

## Usage Example

### Loading Settings

Settings are automatically loaded in [LoaderApp.cs](../src/Rca.Loader/LoaderApp.cs) constructor:

```csharp
public class LoaderApp : IExternalApplication
{
    private Settings settings;
    
    public LoaderApp()
    {
        settings = SettingsService.LoadSettings();
    }
    
    private void OnApplicationIdling(object? sender, IdlingEventArgs e)
    {
        if (settings.AutoLoadRuntimeOnStartup)
        {
            AutoLoadRuntime();
        }
    }
}
```

### Clearing Cache

If you need to reload settings (e.g., after user edits the file):

```csharp
SettingsService.ClearCache();
var freshSettings = SettingsService.LoadSettings();
```

## Build Integration

Both settings and addin files are managed through Resources and deployed automatically by MSBuild:

### Project Configuration

In [Rca.Loader.csproj](../src/Rca.Loader/Rca.Loader.csproj):

```xml
<ItemGroup>
  <!-- Settings file -->
  <Content Include="Resources\settings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>settings.json</Link>
  </Content>
  
  <!-- Addin manifest -->
  <Content Include="Resources\Rca.addin">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>Rca.addin</Link>
  </Content>
</ItemGroup>
```

### Deployment Targets

#### Settings Deployment

The `DeploySettingsJson` target copies settings to the installation directory only if the file doesn't already exist:

```xml
<Target Name="DeploySettingsJson" AfterTargets="Build">
  <Copy SourceFiles="$(SettingsSourcePath)" 
        DestinationFiles="$(SettingsDestPath)" 
        SkipUnchangedFiles="true"
        Condition="!Exists('$(SettingsDestPath)')" />
</Target>
```

This ensures user customizations are never overwritten during plugin updates.

#### Addin Deployment

The `GenerateRcaAddinFile` target in [Common.targets](../build/Common.targets) deploys the addin manifest:

```xml
<Target Name="GenerateRcaAddinFile" AfterTargets="Build">
  <Copy SourceFiles="$(TargetDir)Rca.addin" 
        DestinationFiles="$(RcaRevitAddinsDir)\Rca.addin" 
        SkipUnchangedFiles="true" />
</Target>
```

Both configuration files are stored in `Resources` for consistency and version control.

## Adding New Settings

To add a new setting:

1. **Update [Settings.cs](../src/Rca.Loader/Configuration/Settings.cs)**:
   ```csharp
   public class Settings
   {
       public bool AutoLoadRuntimeOnStartup { get; set; } = true;
       public bool ShowWelcomeDialog { get; set; } = true; // New setting
   }
   ```

2. **Update [settings.json](../src/Rca.Loader/Resources/settings.json)**:
   ```json
   {
     "autoLoadRuntimeOnStartup": true,
     "showWelcomeDialog": true
   }
   ```

3. **Use the setting in your code**:
   ```csharp
   if (settings.ShowWelcomeDialog)
   {
       ShowWelcomeDialog();
   }
   ```

## Error Handling

The settings system is designed to be resilient:

- **Missing file**: Uses default values from `Settings` class
- **Invalid JSON**: Logs error and falls back to defaults
- **Missing properties**: Uses property default values
- **Extra properties**: Ignored by JSON deserializer

All errors are logged but never block plugin initialization.

## Best Practices

1. **Always provide default values** in the `Settings` class
2. **Document new settings** in this file and in XML comments
3. **Use DEBUG-only settings** for development/testing features
4. **Never commit user-specific settings** to source control
5. **Test settings with missing/corrupted files** to ensure fallback works

## See Also

- [LoaderApp.cs](../src/Rca.Loader/LoaderApp.cs) - Settings usage example
- [Hot Reload System](HRS.md) - Related runtime loading documentation
- [Logging System](Logging-System.md) - How to log settings-related events
