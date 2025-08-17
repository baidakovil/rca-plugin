## Environment Setup Tips

- Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0  
- Install Visual Studio Code and add:
  - C# Dev Kit  
  - XML Tools  
  - Revit API Snippets (community extension)  
  - NuGet Package Manager  
- Verify .NET version:  
  ```bash
  dotnet --version
  ```
- Download Revit 2026 SDK from Autodesk Developer Portal and extract to `C:\RevitSDK\2026`.

***

## Prompt 1: Project Skeleton and .NET 8 Setup

**Role:** You are Copilot, an expert .NET and Revit API assistant.  
**Context:** Developer wants a Revit 2026 plugin using .NET 8 in VS Code on Windows 11 ARM.  
**Goal:** Generate a new C# class library targeting .NET 8, named `RCA`, with the correct Revit API references.  
**Requirements:**
- Use `Directory.Build.props` to fix ARM64 target.
- Reference `RevitAPI.dll` and `RevitAPIUI.dll` from `C:\RevitSDK\2026`.
- Create an `RCA.csproj` with plugin manifest stub.
- Do not implement functionality yet.  
**Stop:** After generating the complete `.csproj` file and `Plugin.cs` stub.

***

## Prompt 2: ICommand Implementation and External XAML Panel

**Role:** You are Copilot, a Revit UI expert.  
**Context:** We need a dockable panel implemented via an external XAML file.  
**Goal:**  
- Create `RCA_ExternalDockablePanel.xaml` and its code-behind `RCA_ExternalDockablePanel.xaml.cs`.  
- Implement `DockablePaneProvider` interface.  
- Name classes `RCA_PanelProvider` and `RCA_Panel`.  
- The XAML must contain a single button labeled “Hello”.  
- Wire the button’s click event to display a Revit TaskDialog “hello, world”.  
**Requirements:**
- Target .NET 8.
- Use `Autodesk.Revit.UI` and `Autodesk.Revit.DB`.
- Keep XAML minimal but complete.
**Stop:** After full XAML and code-behind showing `TaskDialog.Show`.

***

### Environment Setup Tip

- In VS Code, enable “XAML Preview” extension to live-preview WPF XAML.

***

## Prompt 3: Ribbon Icon and Command Registration

**Role:** You are Copilot, an Autodesk Revit UI ribbon expert.  
**Context:** The plugin must add a ribbon button under “Add-ins → RCA”.  
**Goal:**  
- In `Plugin.cs`, override `IExternalApplication.OnStartup` to:
  - Create a new Revit RibbonPanel “RCA”
  - Add a `PushButton` with icon `Resources/rca_icon.png`
  - Wire it to the `RCA_PanelProvider` to show the dockable pane.
- Add `OnShutdown` stub.  
**Requirements:**
- Use `.AddPushButtonData`.
- Include how to embed the PNG resource.
- Ensure .NET 8 build configuration supports ARM64.  
**Stop:** After complete `OnStartup` and resource embedding instructions.

***

## Prompt 4: Manifest and Developer Tips for Future Extensions

**Role:** You are Copilot, a plugin packaging specialist.  
**Context:** Plugin will later include IronPython and Roslyn scripting.  
**Goal:**  
- Generate the `add-in` manifest XML (`RCA.addin`) for Revit 2026.
- Include `Assembly`, `FullClassName`, `Text` and `VendorId`.
- Add comments where to plug in IronPython and Roslyn references.
- Provide tips in comments on:
  - Where to load Python assemblies at runtime.
  - How to configure Roslyn scripting host.
**Stop:** After full manifest with inline comments.

***

### Final Environment Tip

- To debug in VS Code, install the “Revit Debug Launcher” extension and configure `launch.json`:
  ```jsonc
  {
    "configurations": [
      {
        "name": "Revit Debug",
        "type": "coreclr",
        "request": "launch",
        "program": "C:\\Program Files\\Autodesk\\Revit 2026\\Revit.exe",
        "args": [],
        "cwd": "${workspaceFolder}/bin/Debug/net8.0"
      }
    ]
  }
  ```

***

With these prompts, Copilot GPT-5 will generate a robust RCA “Hello World” plugin on .NET 8, ready for future IronPython and Roslyn integration.

Sources
