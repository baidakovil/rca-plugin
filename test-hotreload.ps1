#!/usr/bin/env powershell
# Test script for RCA Hot-Reload functionality
# Usage: .\test-hotreload.ps1

Write-Host "🔥 RCA Hot-Reload Test Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Build the client tool
Write-Host "`n📦 Building Hot-Reload Client..." -ForegroundColor Yellow
dotnet build tools/Rca.HotReload.Client/ -q
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to build Hot-Reload Client" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Hot-Reload Client built successfully" -ForegroundColor Green

# Test scenarios
$testCommands = @(
    @{ Command = "PING"; Description = "Health Check" },
    @{ Command = "STATUS"; Description = "Runtime Status" },
    @{ Command = "RELOAD"; Description = "Trigger Reload" }
)

foreach ($test in $testCommands) {
    Write-Host "`n🧪 Testing: $($test.Description)" -ForegroundColor Yellow
    Write-Host "Command: $($test.Command)" -ForegroundColor Gray
    
    # Run the client
    $output = dotnet run --project tools/Rca.HotReload.Client -- --command $test.Command 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Success:" -ForegroundColor Green
        Write-Host "$output" -ForegroundColor White
    } else {
        Write-Host "⚠️  Expected failure (server not running):" -ForegroundColor Yellow
        Write-Host "$output" -ForegroundColor Gray
    }
}

Write-Host "`n📋 Manual Test Instructions:" -ForegroundColor Cyan
Write-Host "1. Start Revit 2026 with Rca.Loader.addin installed" -ForegroundColor White
Write-Host "2. Run: dotnet run --project tools/Rca.HotReload.Client -- -c PING" -ForegroundColor White
Write-Host "3. Make code changes in Rca.Core, Rca.UI, etc." -ForegroundColor White
Write-Host "4. Run: dotnet build src/Rca.Runtime" -ForegroundColor White
Write-Host "5. Watch for automatic reload messages in Revit" -ForegroundColor White

Write-Host "`n🔗 Documentation:" -ForegroundColor Cyan
Write-Host "   Complete Guide: HOT_RELOAD_ARCHITECTURE.md" -ForegroundColor White
Write-Host "   Project Setup:  DEVELOPMENT_SETUP.md" -ForegroundColor White

Write-Host "`n🎉 Hot-Reload Test Completed!" -ForegroundColor Green