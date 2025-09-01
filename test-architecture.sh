#!/bin/bash

# Quick integration test to verify hot reload architecture

echo "RCA Hot Reload Architecture Validation"
echo "======================================"

# Check that all required projects exist
echo "✓ Checking project structure..."

PROJECTS=(
    "src/Rca.Loader.Contracts/Rca.Loader.Contracts.csproj"
    "src/Rca.Loader/Rca.Loader.csproj"
    "src/Rca.Runtime/Rca.Runtime.csproj"
    "src/Rca.Contracts/Rca.Contracts.csproj"
    "src/Rca.Core/Rca.Core.csproj"
    "src/Rca.UI/Rca.UI.csproj"
    "src/Rca.Network/Rca.Network.csproj"
    "src/RcaPlugin/RcaPlugin.csproj"
)

for project in "${PROJECTS[@]}"; do
    if [ -f "$project" ]; then
        echo "  ✓ $project"
    else
        echo "  ✗ $project (missing)"
        exit 1
    fi
done

# Check key files exist
echo "✓ Checking key files..."

KEY_FILES=(
    "src/RcaPlugin/Rca.Loader.addin"
    "tools/SendReload.ps1"
    "DEV_HOT_RELOAD.md"
    "src/Rca.Loader.Contracts/IPluginRuntime.cs"
    "src/Rca.Loader/LoaderApp.cs"
    "src/Rca.Runtime/RcaRuntime.cs"
)

for file in "${KEY_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo "  ✓ $file"
    else
        echo "  ✗ $file (missing)"
        exit 1
    fi
done

# Check that contracts interface is properly defined
echo "✓ Checking interface definitions..."

if grep -q "interface IPluginRuntime" src/Rca.Loader.Contracts/IPluginRuntime.cs; then
    echo "  ✓ IPluginRuntime interface found"
else
    echo "  ✗ IPluginRuntime interface not found"
    exit 1
fi

if grep -q "class RcaRuntime : IPluginRuntime" src/Rca.Runtime/RcaRuntime.cs; then
    echo "  ✓ RcaRuntime implementation found"
else
    echo "  ✗ RcaRuntime implementation not found"
    exit 1
fi

# Check MSBuild hot reload target exists
echo "✓ Checking MSBuild integration..."

if grep -q "HotReloadDeploy" src/Rca.Runtime/Rca.Runtime.csproj; then
    echo "  ✓ HotReloadDeploy target found"
else
    echo "  ✗ HotReloadDeploy target not found"
    exit 1
fi

if grep -q "ILRepack" src/Rca.Runtime/Rca.Runtime.csproj; then
    echo "  ✓ ILRepack integration found"
else
    echo "  ✗ ILRepack integration not found"
    exit 1
fi

# Check pipe protocol definitions
echo "✓ Checking pipe protocol..."

if grep -q "PipeMessage" src/Rca.Loader.Contracts/PipeMessages.cs; then
    echo "  ✓ Pipe message classes found"
else
    echo "  ✗ Pipe message classes not found"
    exit 1
fi

# Test compilation of basic projects (Linux compatible)
echo "✓ Testing compilation..."

cd src/Rca.Loader.Contracts
if dotnet build --nologo --verbosity quiet; then
    echo "  ✓ Rca.Loader.Contracts compiles"
else
    echo "  ✗ Rca.Loader.Contracts compilation failed"
    exit 1
fi
cd ../..

cd src/Rca.Contracts
if dotnet build --nologo --verbosity quiet; then
    echo "  ✓ Rca.Contracts compiles"
else
    echo "  ✗ Rca.Contracts compilation failed"
    exit 1
fi
cd ../..

cd src/Rca.Network
if dotnet build --nologo --verbosity quiet; then
    echo "  ✓ Rca.Network compiles"
else
    echo "  ✗ Rca.Network compilation failed"
    exit 1
fi
cd ../..

echo ""
echo "🎉 Hot Reload Architecture Validation PASSED!"
echo ""
echo "Next steps for Windows development:"
echo "1. Build solution on Windows with Revit 2026"
echo "2. Test loader deployment: dotnet build src/Rca.Loader"
echo "3. Test runtime hot reload: dotnet build src/Rca.Runtime"
echo "4. Launch Revit and verify RCA Loader tab appears"
echo "5. Test manual reload via ribbon or PowerShell script"
echo ""
echo "See DEV_HOT_RELOAD.md for detailed usage instructions."