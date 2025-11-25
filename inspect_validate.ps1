$cliAssemblyPath = "C:/Users/baidakov/.nuget/packages/spectre.console.cli/1.0.0-alpha.0.7/lib/net8.0/Spectre.Console.Cli.dll"

if (-not (Test-Path $cliAssemblyPath)) {
  Write-Error "Spectre.Console.Cli.dll not found."
  exit 1
}

[Reflection.Assembly]::LoadFrom("C:/Users/baidakov/.nuget/packages/spectre.console/0.54.0/lib/net8.0/Spectre.Console.dll") | Out-Null
[Reflection.Assembly]::LoadFrom("C:/Users/baidakov/.nuget/packages/njsonschema/11.4.0/lib/netstandard2.0/NJsonSchema.dll") | Out-Null
$cli = [Reflection.Assembly]::LoadFrom($cliAssemblyPath)
$settingsType = $cli.GetType("Spectre.Console.Cli.CommandSettings")
$method = $settingsType.GetMethod("Validate")

Write-Host "CommandSettings.Validate returns: $($method.ReturnType.FullName)"

