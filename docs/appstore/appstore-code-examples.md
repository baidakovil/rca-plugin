# Практические примеры кода для Revit App Store

## 1. Создание Ribbon UI с требованиями App Store

```csharp
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

public class RcaLoaderApp : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            // Создать tab если не существует
            const string tabName = "Revit Chat Assistant";
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch
            {
                // Tab уже существует
            }

            // Получить или создать panel
            var panel = application.CreateRibbonPanel(tabName, "Chat Assistant");

            // Подготовить иконку (ОБЯЗАТЕЛЬНО!)
            // Иконка должна быть в ресурсах проекта
            var largeImage = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Rca.Loader;component/Resources/RcaIcon_32x32.png"));
            
            var smallImage = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Rca.Loader;component/Resources/RcaIcon_16x16.png"));

            // Создать button с иконкой
            var pbData = new PushButtonData(
                "RcaMainButton",
                "Chat Assistant",
                typeof(RcaLoaderApp).Assembly.Location,
                "Rca.Runtime.Commands.ShowChatCommand")
            {
                LargeImage = largeImage,
                Image = smallImage,
                ToolTip = "Open Revit Chat Assistant"
            };

            var button = panel.AddItem(pbData) as PushButton;

            if (button != null)
            {
                // ТРЕБУЕТСЯ: Установить справку для F1
                // Это проверяют на App Store!
                button.SetContextualHelp(new ContextualHelp(
                    ContextualHelpType.Url,
                    "https://docs.your-company.com/rca"));
                
                // Альтернатива - локальный CHM файл:
                // button.SetContextualHelp(new ContextualHelp(
                //     ContextualHelpType.ChmFile,
                //     @"C:\path\to\help.chm"));

                button.AvailabilityClassName = typeof(RcaCommandAvailability).FullName;
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Error", $"Failed to load RCA: {ex.Message}");
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }
}
```

## 2. Правильный .addin файл для App Store

```xml
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
    <AddIn Type="Application">
        <!-- Этот Name используется только для отладочных сообщений -->
        <Name>Revit Chat Assistant</Name>
        
        <!-- ВАЖНО: Относительный путь - Revit подставляет папку ApplicationPlugins -->
        <!-- Correct: .\Rca.Loader.dll -->
        <!-- Wrong: C:\Program Files\... -->
        <!-- Wrong: Rca.Loader.dll (должна быть точка-слэш в начале) -->
        <Assembly>.\Rca.Loader.dll</Assembly>
        
        <!-- GUID класса, который реализует IExternalApplication -->
        <!-- Генерируйте новый: [guid]::NewGuid().ToString() -->
        <ClientId>12345678-1234-1234-1234-123456789ABC</ClientId>
        
        <!-- Полное имя класса, реализующего IExternalApplication -->
        <FullClassName>Rca.Loader.RcaLoaderApp</FullClassName>
        
        <!-- Vendor ID - короткий код вашей компании (3-4 буквы) -->
        <VendorId>RCA</VendorId>
        
        <!-- Описание вашей компании -->
        <VendorDescription>Revit Chat Assistant by Your Company</VendorDescription>
        
        <!-- В Revit 2014+ можно использовать: -->
        <AllowLoadIntoExistingSession>true</AllowLoadIntoExistingSession>
    </AddIn>
</RevitAddIns>
```

## 3. Проверка требований перед отправкой (PowerShell)

```powershell
# Script: Validate-AppStoreBundle.ps1
# Проверяет bundle перед отправкой на Autodesk

param(
    [Parameter(Mandatory=$true)]
    [string]$BundlePath
)

$errors = @()
$warnings = @()

Write-Host "🔍 Validating App Store Bundle..." -ForegroundColor Cyan
Write-Host "Path: $BundlePath`n"

# 1. Проверить структуру
$packageContents = Join-Path $BundlePath "PackageContents.xml"
$contents2026 = Join-Path $BundlePath "Contents\2026"
$resources = Join-Path $BundlePath "Resources"

if (-not (Test-Path $packageContents)) {
    $errors += "✗ PackageContents.xml not found"
}

if (-not (Test-Path $contents2026)) {
    $errors += "✗ Contents\2026 folder not found"
}

# 2. Проверить DLLs
$dlls = Get-ChildItem $contents2026 -Filter "*.dll" -ErrorAction SilentlyContinue
Write-Host "Found DLLs: $($dlls.Count)"

# Проверить на Revit API
$revitDlls = $dlls | Where-Object { $_.Name -match "RevitAPI" }
if ($revitDlls) {
    $errors += "✗ Found Revit API DLLs (should NOT be included):"
    $revitDlls | ForEach-Object { $errors += "  - $($_.Name)" }
}

# 3. Проверить .addin файл
$addinFiles = Get-ChildItem $contents2026 -Filter "*.addin" -ErrorAction SilentlyContinue
if ($addinFiles.Count -eq 0) {
    $errors += "✗ No .addin file found in Contents\2026"
} else {
    Write-Host "Found .addin files: $($addinFiles.Count)"
    
    # Проверить содержимое .addin
    foreach ($addin in $addinFiles) {
        [xml]$addinXml = Get-Content $addin.FullName
        
        # Проверить Assembly path
        $assemblyPath = $addinXml.RevitAddIns.AddIn.Assembly
        if (-not $assemblyPath.StartsWith(".\")) {
            $warnings += "⚠ Assembly path should use relative path (.\ prefix): $assemblyPath"
        }
        
        # Проверить FullClassName
        $className = $addinXml.RevitAddIns.AddIn.FullClassName
        if (-not $className) {
            $errors += "✗ FullClassName not found in .addin"
        }
    }
}

# 4. Проверить ресурсы
if (Test-Path $resources) {
    $icons = Get-ChildItem $resources -Filter "*.png" -ErrorAction SilentlyContinue
    if ($icons.Count -eq 0) {
        $warnings += "⚠ No PNG icon found in Resources"
    } else {
        Write-Host "Found icons: $($icons.Count)"
    }
    
    $helpFiles = Get-ChildItem $resources -Filter "*.html" -ErrorAction SilentlyContinue
    if ($helpFiles.Count -eq 0) {
        $warnings += "⚠ No HTML help file found in Resources"
    } else {
        Write-Host "Found help files: $($helpFiles.Count)"
    }
} else {
    $warnings += "⚠ Resources folder not found"
}

# 5. Проверить PackageContents.xml
if (Test-Path $packageContents) {
    [xml]$pkgXml = Get-Content $packageContents
    
    $productCode = $pkgXml.ApplicationPackage.ProductCode
    $upgradeCode = $pkgXml.ApplicationPackage.UpgradeCode
    
    if (-not $productCode -or $productCode -eq "{GENERATE-UNIQUE-GUID}") {
        $warnings += "⚠ ProductCode not customized"
    }
    if (-not $upgradeCode -or $upgradeCode -eq "{GENERATE-UNIQUE-GUID}") {
        $warnings += "⚠ UpgradeCode not customized"
    }
    
    # Проверить версию
    $appVersion = $pkgXml.ApplicationPackage.AppVersion
    if (-not $appVersion -match "^\d+\.\d+\.\d+") {
        $warnings += "⚠ AppVersion not in Semantic Versioning format (use X.Y.Z)"
    }
}

# Вывести результаты
Write-Host "`n" + ("="*50) -ForegroundColor Cyan

if ($errors.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host "✓ Bundle validation passed!" -ForegroundColor Green
} else {
    if ($errors.Count -gt 0) {
        Write-Host "`n❌ ERRORS (must fix):" -ForegroundColor Red
        $errors | ForEach-Object { Write-Host $_ }
    }
    
    if ($warnings.Count -gt 0) {
        Write-Host "`n⚠ WARNINGS (review):" -ForegroundColor Yellow
        $warnings | ForEach-Object { Write-Host $_ }
    }
}

Write-Host ("="*50) -ForegroundColor Cyan
```

## 4. .csproj конфигурация для версионирования

```xml
<!-- В Rca.Loader.csproj добавьте: -->

<PropertyGroup>
    <!-- App Store версия -->
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
    <InformationalVersion>1.0.0</InformationalVersion>
    
    <!-- Company info для Properties в Windows -->
    <Company>Your Company Name</Company>
    <Product>Revit Chat Assistant</Product>
    <Description>AI-powered chat assistant for Revit</Description>
    <Copyright>Copyright © 2024 Your Company</Copyright>
    
    <!-- App Store требует 64-bit -->
    <PlatformTarget>x64</PlatformTarget>
    
    <!-- Release конфигурация -->
    <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
        <DebugType>embedded</DebugType>
        <Optimize>true</Optimize>
        <TieredCompilation>true</TieredCompilation>
    </PropertyGroup>
</PropertyGroup>

<!-- Убедитесь что иконки включены как ресурсы -->
<ItemGroup>
    <EmbeddedResource Include="Resources\RcaIcon_16x16.png" />
    <EmbeddedResource Include="Resources\RcaIcon_32x32.png" />
    <EmbeddedResource Include="Resources\RcaIcon_64x64.png" />
</ItemGroup>

<!-- .addin файл для развертывания -->
<ItemGroup>
    <Content Include="Resources\Rca.addin" 
             CopyToOutputDirectory="PreserveNewest" 
             Link="Rca.addin" />
</ItemGroup>
```

## 5. Вспомогательный скрипт для сборки Bundle

```powershell
# Script: Build-AppStoreBundle.ps1
# Собирает Release конфигурацию и готовит bundle для отправки

param(
    [Parameter(Mandatory=$false)]
    [string]$SolutionPath = ".",
    
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateZip = $true
)

$ErrorActionPreference = "Stop"

Write-Host "🔨 Building App Store Bundle..." -ForegroundColor Cyan

# 1. Clean
Write-Host "`n1️⃣ Cleaning previous build..."
dotnet clean $SolutionPath -c $Configuration -q

# 2. Build
Write-Host "2️⃣ Building Release configuration..."
$buildOutput = dotnet build $SolutionPath -c $Configuration -q
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

# 3. Validate
Write-Host "3️⃣ Validating bundle..."
$bundlePath = Join-Path $SolutionPath "bin\Release\Rca.bundle"

if (-not (Test-Path $bundlePath)) {
    Write-Host "❌ Bundle not created!" -ForegroundColor Red
    exit 1
}

# Запустить валидацию
$validationScript = @"
# Inline validation - see previous script
"@

# 4. Create ZIP
if ($CreateZip) {
    Write-Host "4️⃣ Creating ZIP archive..."
    $zipPath = Join-Path $SolutionPath "bin\Release\Rca.bundle.zip"
    
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    Compress-Archive -Path $bundlePath -DestinationPath $zipPath -Force
    
    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Host "✓ Created: $zipPath ($([Math]::Round($zipSize, 2)) MB)"
}

# 5. Ready for submission
Write-Host "`n✅ Bundle ready for App Store submission!" -ForegroundColor Green
Write-Host "`nNext steps:"
Write-Host "1. Review PackageContents.xml metadata"
Write-Host "2. Go to https://apps.autodesk.com"
Write-Host "3. Upload $zipPath"
Write-Host "4. Wait for review (2-3 weeks)"
```

## 6. Пример HTML справки

```html
<!-- Resources/RcaHelp.html -->
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Revit Chat Assistant - Help</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        h1 { color: #1f77d4; }
        .section { margin-bottom: 20px; }
        code { background-color: #f0f0f0; padding: 2px 5px; }
    </style>
</head>
<body>
    <h1>Revit Chat Assistant Help</h1>
    
    <div class="section">
        <h2>Overview</h2>
        <p>The Revit Chat Assistant is an AI-powered tool for...</p>
    </div>
    
    <div class="section">
        <h2>Getting Started</h2>
        <ol>
            <li>Click the "Chat Assistant" button on the ribbon</li>
            <li>Enter your question</li>
            <li>The assistant will provide suggestions</li>
        </ol>
    </div>
    
    <div class="section">
        <h2>Features</h2>
        <ul>
            <li>Real-time suggestions</li>
            <li>Context-aware responses</li>
            <li>Multi-language support</li>
        </ul>
    </div>
    
    <div class="section">
        <h2>Support</h2>
        <p>For issues or questions, contact: <code>support@your-company.com</code></p>
    </div>
</body>
</html>
```

## 7. Проверка перед финальной отправкой

```bash
# Минимальный чеклист перед отправкой
# Запустите в PowerShell

$bundlePath = ".\bin\Release\Rca.bundle"

# ✅ Структура
test-path "$bundlePath\PackageContents.xml" | Write-Host -Object "PackageContents.xml: $_"
test-path "$bundlePath\Contents\2026" | Write-Host -Object "Contents\2026: $_"
test-path "$bundlePath\Resources" | Write-Host -Object "Resources: $_"

# ✅ Содержимое
(Get-ChildItem "$bundlePath\Contents\2026\*.dll").Count | Write-Host -Object "DLLs: $_"
(Get-ChildItem "$bundlePath\Contents\2026\*.addin").Count | Write-Host -Object "Addin files: $_"
(Get-ChildItem "$bundlePath\Resources\*.png").Count | Write-Host -Object "Icons: $_"
(Get-ChildItem "$bundlePath\Resources\*.html").Count | Write-Host -Object "Help files: $_"

# ✅ Нет Revit API
(Get-ChildItem "$bundlePath\Contents\2026\RevitAPI*.dll" -ErrorAction SilentlyContinue).Count | 
    Write-Host -Object "RevitAPI DLLs (should be 0): $_"

# ✅ ZIP готов
test-path ".\bin\Release\Rca.bundle.zip" | Write-Host -Object "ZIP ready: $_"
```

---

## Шпаргалка: Типичные пути ошибок

```
❌ Assembly>.\path\to\dll.dll          → ✅ Assembly>.\Rca.Loader.dll
❌ Assembly>Rca.Loader.dll             → ✅ Assembly>.\Rca.Loader.dll
❌ No F1 help setup                    → ✅ button.SetContextualHelp(...)
❌ External Tools only                 → ✅ Ribbon Panel + Button
❌ RevitAPI.dll in bundle              → ✅ Exclude RevitAPI DLLs
❌ 16-bit icon                         → ✅ 32x32 PNG with alpha
❌ Version 1.0                         → ✅ Version 1.0.0
❌ Wrong FullClassName                → ✅ Exact class name from code
❌ Absolute paths in .addin            → ✅ Relative paths (.\\)
```
