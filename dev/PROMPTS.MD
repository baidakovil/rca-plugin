## 1. Prompt: Create the External Application Shell

**Role:**  
You are an expert C#/.NET 8 developer specializing in Autodesk Revit plugins.

**Context:**  
A Visual Studio solution was created from Nice3point/RevitTemplates “Revit AddIn Solution” template. Revit 2026 (or fall back to 2025 API) and its SDK are installed. .NET 8.0 is the target framework.

**Goal:**  
Generate a C# `IExternalApplication` class named `RcaPluginApp` in namespace `RcaPlugin` that:
- Implements `OnStartup` and `OnShutdown`,
- Registers a dockable panel via XAML file `RcaDockablePanel.xaml`,
- Adds a ribbon button with an icon to launch the panel,
- Uses MVVM pattern.

**Conditions & Constraints:**  
- Use `.NETCoreApp,Version=v8.0` target in the `.csproj`.  
- Reference `Autodesk.Revit.UI` and `Autodesk.Revit.Attributes`.  
- Hardcode full paths only via macros: `$(TargetDir)RcaPlugin.addin` in build events.  
- Stop after generating a complete C# class and necessary registration code.

```text
<STOP> 
```

***

## 2. Prompt: Define the Dockable Panel XAML View and ViewModel

**Role:**  
You are a UI/UX and MVVM specialist for WPF within Revit plugins.

**Context:**  
In the same project, create `RcaDockablePanel.xaml` and `RcaDockablePanelViewModel.cs`.

**Goal:**  
- In XAML define a `DockablePane` with a single `Button` labeled “Hello”.  
- Bind `ClickCommand` in ViewModel to display `TaskDialog.Show("hello, world")`.  
- Name ViewModel class `RcaDockablePanelViewModel` in `RcaPlugin.ViewModels` namespace.

**Conditions & Constraints:**  
- Use only WPF `Button` and MVVM `RelayCommand` implementation.  
- Do not include unrelated controls.  
- Stop after generating both XAML and ViewModel code including necessary using statements.

```text
<STOP>
```

***

## 3. Prompt: Add the `.addin` Registration File

**Role:**  
You are a Revit Add-In registration expert.

**Context:**  
The project uses Nice3point/RevitTemplates and targets Revit 2026 if available, otherwise 2025.

**Goal:**  
Create `RcaPlugin.addin` XML that:
- Registers `RcaPluginApp` for `IExternalApplication`,
- Points to your output DLL,
- Specifies all Revit versions 2025–2026,
- Uses `Name="rca-plugin"` and `Assembly="RcaPlugin.dll"`.

**Conditions & Constraints:**  
- Must follow Autodesk `.addin` schema.
- Do not add extra `<Assembly>` entries.
- Stop after complete XML registration.

```text
<STOP>
```

***

## 4. Prompt: Configure the Project File for .NET 8 and Multi-Version

**Role:**  
You are a build configuration specialist for Revit multi-version support.

**Context:**  
Using the default `.csproj` from Nice3point/RevitTemplates.

**Goal:**  
Modify `<PropertyGroup>` to:
1. Target `net8.0-windows`,
2. Add conditional properties for `RevitVersion` (2025, 2026),
3. Include `<DeployRevitAddin>` step copying `.addin` to `%APPDATA%\Autodesk\Revit\Addins\$(RevitVersion)\`,
4. Embed the icon resource `Resources\rca-icon.png`.

**Conditions & Constraints:**  
- Keep the file under 50 lines added.  
- Use MSBuild conditions for version-specific paths.  
- Stop after producing the edited `.csproj` snippet.

```text
<STOP>
```

***

## 5. Prompt: NuGet & Tooling Setup Tips

**Role:**  
You are a Visual Studio environment setup tutor.

**Context:**  
Windows 11 ARM, Visual Studio 2022, Revit SDK and .NET 8 installed.

**Goal:**  
List shell commands and VS actions to:
- Install Nice3point.Revit.Templates v5.0.0 via `dotnet new install`,
- Confirm .NET 8 via `dotnet --list-sdks`,
- Add Revit SDK references,
- Enable “Use WPF .NET Core” in Project Settings.

**Conditions & Constraints:**  
- Keep to 6 bullet-style commands.
- Stop after environment checkout steps.

```text
<STOP>
```

***

## 6. Prompt: Build & Deployment Instructions

**Role:**  
You are a DevOps engineer for Autodesk plugins.

**Context:**  
Project is complete with DLL, XAML, addin file.

**Goal:**  
Provide:
1. Optional MSI installer outline using WiX Toolset (short snippet),
2. VS build and post-build event to automate deployment.

**Conditions & Constraints:**  
- Stop after showing MSI fragment.

```text
<STOP>
```

***

# Deployment

1. **Local Script:**  
   - Save above PowerShell as `Deploy-RcaPlugin.ps1`.  
   - Run in Administrator mode:  
     ```powershell
     .\Deploy-RcaPlugin.ps1
     ```

2. **MSI (Optional):**  
   - Install WiX Toolset.  
   - Include `.dll`, `.addin`, and icon under `<Component>`.  
   - Build via `candle.exe` and `light.exe`.

After deployment, launch Revit 2026 (or 2025) to see the **rca-plugin** panel icon. Click it, open the panel, and press **Hello** to get “hello, world.”

Sources
[1] Create a Plugin in Revit 2026 with .NET8 - AYDrafting https://www.aydrafting.com/blog/starting-a-project-in-revit-2026-net8-api
[2] What's New in Revit 2026? A deep dive on new features - ArchiLabs https://archilabs.ai/posts/whats-new-in-revit-2026
[3] Nice3point.Revit.Templates 5.0.0 - NuGet https://www.nuget.org/packages/Nice3point.Revit.Templates
[4] C# + Revit: Lesson 7.5 - Adding Revit 2026 support! - YouTube https://www.youtube.com/watch?v=IbTz79qLfO4
[5] Solved: Plugin not auto-loading in AutoCAD Architecture 2026 from ... https://forums.autodesk.com/t5/objectarx-forum/plugin-not-auto-loading-in-autocad-architecture-2026-from/td-p/13423206
[6] Nice3point/RevitTemplates: Templates for creating Revit add-ins https://github.com/Nice3point/RevitTemplates
[7] Can you run .NET Standard 2.0 in Revit? - LinkedIn https://www.linkedin.com/pulse/can-you-run-net-standard-20-revit-how-going-2025-80-torres-sagredo-dlvhe
[8] Revit SDKs and Tools - Autodesk Platform Services https://aps.autodesk.com/developer/overview/revit
[9] Nice3point.Revit.Templates 1.3.3 - NuGet Gallery https://nugetprodusnc.azure-api.net/packages/Nice3point.Revit.Templates/1.3.3
[10] Solved: .NET 8 migration - Autodesk Community https://forums.autodesk.com/t5/revit-api-forum/net-8-migration/td-p/13301211
[11] Como usar nice3Point Template para Revit, programar ... - YouTube https://www.youtube.com/watch?v=h8eIIdR3ms8
[12] Multiversion for revit addin. : r/bim - Reddit https://www.reddit.com/r/bim/comments/1j13rwp/multiversion_for_revit_addin/
[13] Updating an existing .NET Framework project into a .NET Core one https://forums.autodesk.com/t5/revit-api-forum/updating-an-existing-net-framework-project-into-a-net-core-one/td-p/12682053
