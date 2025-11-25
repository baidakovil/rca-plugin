param(
  [string] $SpectreConsoleVersion = "0.54.0",
  [string] $SpectreCliVersion = "1.0.0-alpha.0.7",
  [string] $NJsonSchemaVersion = "11.4.0"
)

$nugetRoot = Join-Path $env:USERPROFILE ".nuget\\packages"

$assemblyMap = @{
  "Spectre.Console" = Join-Path $nugetRoot "spectre.console\\$SpectreConsoleVersion\\lib\\net8.0\\Spectre.Console.dll"
  "NJsonSchema" = Join-Path $nugetRoot "njsonschema\\$NJsonSchemaVersion\\lib\\netstandard2.0\\NJsonSchema.dll"
}

# Preload assemblies commonly required by Spectre.Console.Cli
foreach ($assembly in $assemblyMap.GetEnumerator()) {
  if (Test-Path $assembly.Value) {
    [System.Reflection.Assembly]::LoadFrom($assembly.Value) | Out-Null
  }
}

$resolver = [System.ResolveEventHandler]{
  param($sender, $args)
  $requestedName = (New-Object System.Reflection.AssemblyName($args.Name)).Name
  if ([string]::IsNullOrWhiteSpace($requestedName)) {
    return $null
  }

  if ($assemblyMap.ContainsKey($requestedName)) {
    return [System.Reflection.Assembly]::LoadFrom($assemblyMap[$requestedName])
  }

  return $null
}

[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver) | Out-Null

$cliAssemblyPath = Join-Path $nugetRoot "spectre.console.cli\\$SpectreCliVersion\\lib\\net8.0\\Spectre.Console.Cli.dll"
$cliAssembly = [System.Reflection.Assembly]::LoadFrom($cliAssemblyPath)
$exceptionType = $cliAssembly.GetType("Spectre.Console.Cli.CommandRuntimeException")

if (-not $exceptionType) {
  Write-Host "CommandRuntimeException type could not be resolved."
  exit 1
}

Write-Host "Constructors for $($exceptionType.FullName):"
$bindingFlags = [System.Reflection.BindingFlags] "Public, NonPublic, Instance"
$constructors = $exceptionType.GetConstructors($bindingFlags)
Write-Host ("Total: {0}" -f $constructors.Length)
$constructors | ForEach-Object { Write-Host (" - {0}" -f $_.ToString()) }
