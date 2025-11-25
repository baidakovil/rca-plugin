param(
  [string] $Filter = "ValidationResult"
)

$cliAssemblyPath = "C:/Users/baidakov/.nuget/packages/spectre.console.cli/1.0.0-alpha.0.7/lib/net8.0/Spectre.Console.Cli.dll"

if (-not (Test-Path $cliAssemblyPath)) {
  Write-Error "Spectre.Console.Cli assembly not found."
  exit 1
}

[System.Reflection.Assembly]::LoadFrom($cliAssemblyPath) | Out-Null
[System.Reflection.Assembly]::Load("System.Runtime") | Out-Null

[AppDomain]::CurrentDomain.GetAssemblies() | Out-Null

[Reflection.Assembly]::LoadFrom("C:/Users/baidakov/.nuget/packages/spectre.console/0.54.0/lib/net8.0/Spectre.Console.dll") | Out-Null

$types = [Reflection.Assembly]::LoadFrom($cliAssemblyPath).GetTypes() | Where-Object { $_.FullName -like "*$Filter*" }

if (-not $types) {
  Write-Host "No types matched '$Filter'."
  exit 0
}

Write-Host "Types matching '$Filter':"
$types | ForEach-Object { Write-Host " - $($_.FullName)" }

