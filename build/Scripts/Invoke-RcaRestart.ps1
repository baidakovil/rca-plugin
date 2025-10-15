# powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "C:\Users\baidakov\rca-plugin\build\Scripts\Invoke-RcaRestart.ps1

param(
    [string]$RuntimeRoot = (Join-Path $env:LOCALAPPDATA 'RCA\Runtime'),
    [string]$TargetAddinDir = (Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2026\Rca'),
    [string]$RevitExecutable,
    [string]$FilePath
)

# Resolve path to main restart script (prefer alongside this helper)
$scriptPath = Join-Path $PSScriptRoot 'RestartRevitGraceful.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) {
    $scriptPath = 'C:\Users\baidakov\rca-plugin\build\Scripts\RestartRevitGraceful.ps1'
}
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Restart script not found at: $scriptPath"
}

# Get latest runtime deploy folder
if (-not $RuntimeRoot) { throw 'RuntimeRoot not provided' }
$latest = Get-ChildItem -LiteralPath $RuntimeRoot -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending | Select-Object -First 1 | ForEach-Object { $_.FullName }
if (-not $latest) { throw "Latest deploy folder not found under $RuntimeRoot" }

# Resolve Revit.exe
if (-not $RevitExecutable) {
    try { $RevitExecutable = (Get-Process -Name Revit -ErrorAction Stop | Select-Object -First 1).MainModule.FileName } catch { }
    if (-not $RevitExecutable) { $RevitExecutable = 'C:\Program Files\Autodesk\Revit 2026\Revit.exe' }
}
if (-not (Test-Path -LiteralPath $RevitExecutable)) { throw "Revit executable not found: $RevitExecutable" }

# Ensure target addin dir exists
if (-not (Test-Path -LiteralPath $TargetAddinDir)) {
    New-Item -ItemType Directory -Path $TargetAddinDir -Force | Out-Null
}

# Build argument list
$argList = @(
    '-NoLogo','-NoProfile','-ExecutionPolicy','Bypass',
    '-File', ('"{0}"' -f $scriptPath),
    '-SourcePath', ('"{0}"' -f $latest),
    '-TargetPath', ('"{0}"' -f $TargetAddinDir),
    '-RevitExecutable', ('"{0}"' -f $RevitExecutable)
)
if ($FilePath) { $argList += @('-FilePath', ('"{0}"' -f $FilePath)) }

Write-Host "Script: $scriptPath"
Write-Host "SourcePath: $latest"
Write-Host "TargetPath: $TargetAddinDir"
Write-Host "RevitExecutable: $RevitExecutable"
Write-Host "Invoking: powershell $($argList -join ' ')"

# Invoke main script
& powershell @argList

if ($LASTEXITCODE -ne 0) {
    Write-Error "Restart script failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
