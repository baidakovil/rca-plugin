# Практическая реализация RELEASE конфигурации для RCA

## Интеграция с текущей системой RCA

Ваш проект уже имеет мощную MSBuild систему с timestamp-based деплоем. Для App Store нужно добавить дополнительный слой, который упакует артифакты в bundle структуру.

## Файл 1: build/props/appstore-release.props

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <!-- App Store Release Configuration
         Extends existing hot-reload system with App Store packaging
         Only active when Configuration == Release -->
    
    <PropertyGroup Condition="'$(Configuration)' == 'Release'">
        <!-- App Store Bundle naming and paths -->
        <AppStoreBundleName>Rca</AppStoreBundleName>
        <AppStoreBundle>$(AppStoreBundleName).bundle</AppStoreBundle>
        <AppStoreBundleOutputDir>$(OutDir)$(AppStoreBundle)</AppStoreBundleOutputDir>
        <AppStoreBundleContentsDir>$(AppStoreBundleOutputDir)\Contents\2026</AppStoreBundleContentsDir>
        <AppStoreBundleResourcesDir>$(AppStoreBundleOutputDir)\Resources</AppStoreBundleResourcesDir>
        
        <!-- DLLs to exclude from App Store bundle (Revit provides these) -->
        <AppStoreExcludedDlls>RevitAPI.dll;RevitAPIUI.dll;RevitAPIUI.r.dll</AppStoreExcludedDlls>
        
        <!-- App metadata (fill these with actual values) -->
        <AppStoreProductCode>GENERATE-UNIQUE-GUID-HERE</AppStoreProductCode>
        <AppStoreUpgradeCode>GENERATE-UNIQUE-GUID-HERE</AppStoreUpgradeCode>
        <AppStoreCompanyName>Your Company Name</AppStoreCompanyName>
        <AppStoreCompanyUrl>https://your-company.com</AppStoreCompanyUrl>
        <AppStoreCompanyEmail>support@your-company.com</AppStoreCompanyEmail>
        <AppStoreOnlineDocUrl>https://your-company.com/rca/docs</AppStoreOnlineDocUrl>
        
        <!-- Icon and help resources -->
        <AppStoreIconSource>$(SolutionDir)resources\RcaAppIcon.png</AppStoreIconSource>
        <AppStoreHelpSource>$(SolutionDir)resources\RcaHelp.html</AppStoreHelpSource>
        
        <!-- Release optimization -->
        <DebugType>embedded</DebugType>
        <DebugSymbols>true</DebugSymbols>
        <Optimize>true</Optimize>
        <TieredCompilation>true</TieredCompilation>
        <TieredCompilationQuickJit>true</TieredCompilationQuickJit>
        <TieredCompilationQuickJitForLoops>true</TieredCompilationQuickJitForLoops>
        
        <!-- Version for App Store submission -->
        <AppStoreVersion>1.0.0</AppStoreVersion>
    </PropertyGroup>
    
    <!-- Expose properties to Source Generators if needed -->
    <ItemGroup Condition="'$(Configuration)' == 'Release'">
        <CompilerVisibleProperty Include="AppStoreBundleOutputDir" />
        <CompilerVisibleProperty Include="AppStoreVersion" />
    </ItemGroup>
</Project>
```

## Файл 2: build/targets/appstore-packaging.targets

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <!-- App Store Packaging Targets
         Creates bundle structure required by Autodesk Exchange Store
         Only for Release configuration, Rca.Loader project -->
    
    <!-- Main target: Create App Store bundle structure -->
    <Target Name="CreateAppStoreBundle"
            Condition="'$(Configuration)' == 'Release' and '$(MSBuildProjectName)' == 'Rca.Loader'"
            AfterTargets="Build">
        
        <Message Text="========================================" Importance="High" />
        <Message Text="Creating App Store Bundle..." Importance="High" />
        <Message Text="Bundle: $(AppStoreBundleOutputDir)" Importance="High" />
        <Message Text="========================================" Importance="High" />
        
        <!-- Clean previous bundle -->
        <RemoveDir Directories="$(AppStoreBundleOutputDir)" />
        
        <!-- Create directory structure -->
        <MakeDir Directories="$(AppStoreBundleContentsDir)" />
        <MakeDir Directories="$(AppStoreBundleResourcesDir)" />
        
        <!-- Task 1: Copy main DLLs (exclude Revit API) -->
        <ItemGroup>
            <AppStoreDlls Include="$(TargetDir)*.dll">
                <Exclude>$(TargetDir)RevitAPI.dll</Exclude>
                <Exclude>$(TargetDir)RevitAPIUI.dll</Exclude>
                <Exclude>$(TargetDir)RevitAPIUI.r.dll</Exclude>
            </AppStoreDlls>
        </ItemGroup>
        
        <Copy SourceFiles="@(AppStoreDlls)" 
              DestinationFolder="$(AppStoreBundleContentsDir)" 
              SkipUnchangedFiles="true" />
        
        <Message Text="✓ Copied $(AppStoreDlls->Count()) DLLs" Importance="Normal" />
        
        <!-- Task 2: Copy PDBs for debugging (optional, can strip for smaller package) -->
        <ItemGroup>
            <AppStorePdbs Include="$(TargetDir)*.pdb">
                <Exclude>$(TargetDir)RevitAPI*</Exclude>
            </AppStorePdbs>
        </ItemGroup>
        
        <Copy SourceFiles="@(AppStorePdbs)" 
              DestinationFolder="$(AppStoreBundleContentsDir)" 
              SkipUnchangedFiles="true" />
        
        <Message Text="✓ Copied $(AppStorePdbs->Count()) PDB files" Importance="Normal" />
        
        <!-- Task 3: Copy .addin manifest -->
        <Copy SourceFiles="$(TargetDir)Rca.addin"
              DestinationFile="$(AppStoreBundleContentsDir)\Rca.addin"
              SkipUnchangedFiles="true" />
        
        <Message Text="✓ Copied Rca.addin manifest" Importance="Normal" />
        
        <!-- Task 4: Copy resources (icon, help) -->
        <Copy SourceFiles="$(AppStoreIconSource)"
              DestinationFile="$(AppStoreBundleResourcesDir)\RcaAppIcon.png"
              SkipUnchangedFiles="true"
              Condition="Exists('$(AppStoreIconSource)')" />
        
        <Copy SourceFiles="$(AppStoreHelpSource)"
              DestinationFile="$(AppStoreBundleResourcesDir)\RcaHelp.html"
              SkipUnchangedFiles="true"
              Condition="Exists('$(AppStoreHelpSource)')" />
        
        <Message Text="✓ Copied resources" Importance="Normal" />
        
        <Message Text="========================================" Importance="High" />
        <Message Text="✓ App Store bundle ready for submission" Importance="High" />
        <Message Text="Location: $(AppStoreBundleOutputDir)" Importance="High" />
        <Message Text="========================================" Importance="High" />
    </Target>
    
    <!-- Generate PackageContents.xml template for manual editing -->
    <Target Name="GeneratePackageContentsXml"
            Condition="'$(Configuration)' == 'Release' and '$(MSBuildProjectName)' == 'Rca.Loader'"
            AfterTargets="CreateAppStoreBundle">
        
        <PropertyGroup>
            <PackageContentsFile>$(AppStoreBundleOutputDir)\PackageContents.xml</PackageContentsFile>
            <PackageContentsContent>
&lt;?xml version="1.0" encoding="utf-8"?&gt;
&lt;ApplicationPackage 
    SchemaVersion="1.0"
    AutodeskProduct="Revit"
    ProductType="Application"
    Name="Revit Chat Assistant"
    AppVersion="$(AppStoreVersion)"
    Description="AI-powered chat assistant for Revit"
    Author="$(AppStoreCompanyName)"
    Icon="./Resources/RcaAppIcon.png"
    OnlineDocumentation="$(AppStoreOnlineDocUrl)"
    HelpFile="./Resources/RcaHelp.html"
    ProductCode="{$(AppStoreProductCode)}"
    UpgradeCode="{$(AppStoreUpgradeCode)}"
    FriendlyVersion="$(AppStoreVersion)"&gt;
    
    &lt;CompanyDetails 
        Name="$(AppStoreCompanyName)"
        Url="$(AppStoreCompanyUrl)"
        Email="$(AppStoreCompanyEmail)"
        Phone=""/&gt;
    
    &lt;RuntimeRequirements 
        OS="Win64"
        Platform="Revit"
        SeriesMin="R2026"
        SeriesMax="R2026"/&gt;
    
    &lt;Components Description="Revit 2026"&gt;
        &lt;RuntimeRequirements 
            OS="Win64"
            Platform="Revit"
            SeriesMin="R2026"
            SeriesMax="R2026"/&gt;
        &lt;ComponentEntry 
            AppName="RcaRevitAddin"
            Version="$(AppStoreVersion)"
            ModuleName="./Contents/2026/Rca.addin"
            AppDescription="Revit Chat Assistant"/&gt;
    &lt;/Components&gt;
&lt;/ApplicationPackage&gt;
            </PackageContentsContent>
        </PropertyGroup>
        
        <WriteLinesToFile 
            File="$(PackageContentsFile)"
            Lines="$(PackageContentsContent)"
            Overwrite="true" />
        
        <Message Text="⚠ PackageContents.xml created (review and customize metadata)" Importance="High" />
    </Target>
    
    <!-- Create ZIP file for distribution -->
    <Target Name="CreateBundleZip"
            Condition="'$(Configuration)' == 'Release' and '$(MSBuildProjectName)' == 'Rca.Loader'"
            AfterTargets="GeneratePackageContentsXml">
        
        <PropertyGroup>
            <BundleZipFile>$(OutDir)$(AppStoreBundle).zip</BundleZipFile>
        </PropertyGroup>
        
        <!-- PowerShell-based compression (cross-platform compatible) -->
        <Exec Command="powershell -NoLogo -NoProfile -Command &quot;Compress-Archive -Path '$(AppStoreBundleOutputDir)' -DestinationPath '$(BundleZipFile)' -Force&quot;" />
        
        <Message Text="✓ Bundle ZIP created" Importance="High" />
        <Message Text="File: $(BundleZipFile)" Importance="High" />
    </Target>
    
    <!-- Validation checklist before submission -->
    <Target Name="ValidateAppStoreBundle"
            Condition="'$(Configuration)' == 'Release' and '$(MSBuildProjectName)' == 'Rca.Loader'"
            AfterTargets="CreateBundleZip">
        
        <PropertyGroup>
            <ValidationReport>$(OutDir)AppStoreValidationReport.txt</ValidationReport>
        </PropertyGroup>
        
        <!-- Check for required files -->
        <ItemGroup>
            <BundleDllFiles Include="$(AppStoreBundleContentsDir)\*.dll" />
            <BundleAddinFiles Include="$(AppStoreBundleContentsDir)\*.addin" />
            <BundleIconFiles Include="$(AppStoreBundleResourcesDir)\*.png" />
            <BundleHelpFiles Include="$(AppStoreBundleResourcesDir)\*.html" />
        </ItemGroup>
        
        <Message Text="📋 App Store Bundle Validation" Importance="High" />
        <Message Text="================================" Importance="High" />
        
        <Message Text="✓ DLLs found: $(BundleDllFiles->Count())" Importance="Normal" />
        <Message Text="✓ Addin manifests found: $(BundleAddinFiles->Count())" Importance="Normal" />
        <Message Text="✓ Icon files found: $(BundleIconFiles->Count())" Importance="Normal" />
        <Message Text="✓ Help files found: $(BundleHelpFiles->Count())" Importance="Normal" />
        
        <!-- Validation warnings -->
        <Warning Text="⚠ No icon found in Resources/" 
                 Condition="'$(BundleIconFiles->Count())' == '0'" />
        <Warning Text="⚠ No help file found in Resources/" 
                 Condition="'$(BundleHelpFiles->Count())' == '0'" />
        <Warning Text="⚠ Revit API DLLs detected - these should NOT be included!" 
                 Condition="Exists('$(AppStoreBundleContentsDir)\RevitAPI.dll')" />
        
        <Message Text="================================" Importance="High" />
        <Message Text="Bundle ready for Autodesk submission" Importance="High" />
    </Target>
</Project>
```

## Файл 3: Обновите Directory.Build.Props

Добавьте после существующих импортов:

```xml
<Import Project="$(SolutionDir)build\props\appstore-release.props" />
```

## Файл 4: Обновите Directory.Build.Targets

Добавьте после существующих импортов:

```xml
<Import Project="$(SolutionDir)build\targets\appstore-packaging.targets" />
```

## Файл 5: build/props/appstore-values.props

Создайте этот файл с актуальными значениями (не коммитьте в git настоящие GUID-ы):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <!-- App Store metadata - customize for your application -->
    
    <PropertyGroup Condition="'$(Configuration)' == 'Release'">
        <!-- Generate new GUIDs via: [guid]::NewGuid().ToString() in PowerShell -->
        <AppStoreProductCode>12345678-1234-1234-1234-123456789012</AppStoreProductCode>
        <AppStoreUpgradeCode>87654321-4321-4321-4321-210987654321</AppStoreUpgradeCode>
        
        <!-- Company information -->
        <AppStoreCompanyName>Your Company Legal Name</AppStoreCompanyName>
        <AppStoreCompanyUrl>https://your-company.com</AppStoreCompanyUrl>
        <AppStoreCompanyEmail>revit-support@your-company.com</AppStoreCompanyEmail>
        <AppStoreOnlineDocUrl>https://your-company.com/products/rca</AppStoreOnlineDocUrl>
        
        <!-- Current version -->
        <AppStoreVersion>1.0.0</AppStoreVersion>
        
        <!-- Resource file locations -->
        <AppStoreIconSource>$(SolutionDir)resources\RcaAppIcon_64x64.png</AppStoreIconSource>
        <AppStoreHelpSource>$(SolutionDir)resources\RcaHelp.html</AppStoreHelpSource>
    </PropertyGroup>
</Project>
```

Затем импортируйте в Directory.Build.Props ПОСЛЕ appstore-release.props:

```xml
<Import Project="$(SolutionDir)build\props\appstore-values.props" />
```

## Команды для сборки

```bash
# Clean Release build с App Store packaging
dotnet clean -c Release
dotnet build -c Release

# Или через Visual Studio
# Build > Configuration Manager > выбрать Release > Build

# Результат:
# bin\Release\Rca.bundle\
#   ├── PackageContents.xml
#   ├── Contents\2026\
#   │   ├── *.dll
#   │   └── Rca.addin
#   └── Resources\
#       ├── RcaAppIcon_64x64.png
#       └── RcaHelp.html
#
# bin\Release\Rca.bundle.zip  <- для отправки на App Store
```

## Интеграция с текущей системой hot-reload

Ваша система timestamp-based деплоя для разработки остается **полностью независимой** от App Store упаковки:

- **Debug/Test**: Использует `%AppData%\RcaRevitVersion\timestamp\`
- **Release**: Создает `.\bin\Release\Rca.bundle\` для отправки

Они не конфликтуют потому что:
1. Разные Configuration (Debug vs Release)
2. Разные OutputPath (по умолчанию `bin\Debug` vs `bin\Release`)
3. Release targets срабатывают только для `Rca.Loader` проекта

## Проверка перед отправкой

```powershell
# Распаковать ZIP и проверить структуру
$bundlePath = ".\bin\Release\Rca.bundle"
Get-ChildItem -Recurse $bundlePath | Format-Table -AutoSize

# Проверить содержимое Rca.addin
Get-Content "$bundlePath\Contents\2026\Rca.addin"

# Проверить, что нет Revit API DLLs
Get-ChildItem "$bundlePath\Contents\2026\*.dll" | Where {$_ -match "RevitAPI"}
```

## Следующие шаги

1. Выполните Release build
2. Проверьте структуру bundle в `bin\Release\Rca.bundle\`
3. Отредактируйте PackageContents.xml (Autodesk даст шаблон при первой отправке)
4. Создайте ZIP: `Compress-Archive -Path .\bin\Release\Rca.bundle -DestinationPath Rca.bundle.zip`
5. Отправьте на https://apps.autodesk.com
