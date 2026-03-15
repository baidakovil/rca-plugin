# Python Runtime

This file is the single source of truth for Python runtime decisions in RCA.

## Runtime Policy

- RCA uses `pythonnet` and `Python.Runtime.dll` for Python execution.
- CPython is not bundled with the addin.
- The user must install Python `3.11` separately.
- Only Python `3.11` is considered supported. Other installed versions must not be auto-accepted.
- Target install is Windows x64 Python 3.11.

## Packaging

- The addin installer must ship `Python.Runtime.dll` together with the runtime assemblies.
- The installer must not ship CPython, an embedded `python/` folder, or a private Python distribution.
- Runtime deployment should assume `pythonnet` is local to the addin, but CPython is external to it.

## When To Check Python

- Python availability is checked only when the user actually requests Python execution.
- RCA must not block addin startup, panel creation, or runtime reload just because Python is missing.
- CPython initialization is lazy and happens on first real execution attempt.

## User Experience

- If Python 3.11 is missing, RCA shows a Revit `TaskDialog` before execution.
- The dialog explains that RCA already includes `pythonnet`, but CPython itself must be installed by the user.
- The dialog includes a link to the official Python download page:

`https://www.python.org/downloads/windows/`

- If the user cancels, execution does not start.
- The panel output should also show the runtime-status message.
- After Python installation, the expected flow is to restart Revit and try again.

## Detection Rules

Python 3.11 is resolved in this order:

1. `RCA_PYTHONNET_PYDLL`
2. `PYTHONNET_PYDLL`
3. `RCA_PYTHONNET_HOME`
4. `PYTHONNET_PYTHONHOME`
5. `PYTHONHOME`
6. `PATH` entries that directly contain `python311.dll`
7. `PATH` entries ending in `Scripts`, using their parent as Python home when it contains `python311.dll`
8. Windows registry: `Software\Python\PythonCore\3.11\InstallPath`
9. Standard install locations:
	`%LOCALAPPDATA%\Programs\Python\Python311`
	`%ProgramFiles%\Python311`
	`%ProgramFiles(x86)%\Python311`

Rules:

- Expected DLL name is exactly `python311.dll`.
- A home without `python311.dll` is invalid.
- A DLL override pointing to another version is invalid.
- `RCA_PYTHONNET_EXTRA_PYTHONPATH` and `PYTHONPATH` may extend module search paths, but they do not satisfy runtime detection on their own.

## Runtime Setup Notes

- `Python.Runtime.dll` must be loaded in the default `AssemblyLoadContext`.
- `Python.Runtime.dll` is treated as non-collectible by the loader.
- Python scope creation is persistent per service instance.
- Search paths added to Python include the resolved Python home, `python311.zip` if present, `Lib`, `DLLs`, `Lib\site-packages`, and extra paths from env vars.

## Code Touchpoints

If this policy changes, update these files first:

- `src/Rca.Contracts/PythonRuntimeStatus.cs`
- `src/Rca.Contracts/IPythonExecutionService.cs`
- `src/Rca.Core/PythonRuntimeLocator.cs`
- `src/Rca.Core/PythonExecutionService.cs`
- `src/Rca.UI/RcaDockablePanelViewModel.cs`
- build/deploy files that include `Python.Runtime.dll`

## Testing Policy

- Unit tests may fake runtime detection.
- Integration tests skip when usable Python 3.11 is unavailable.