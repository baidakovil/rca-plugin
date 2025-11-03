# RELEASE конфигурация для Revit App Store — Полный гайд

## Обзор процесса

Для отправки на Autodesk App Store требуется создать **bundle** — специальную структуру папок с DLL, манифестом и метаданными. Ваша текущая система уже имеет хорошую базу для этого благодаря MSBuild интеграции.

## 1. Структура Bundle для App Store

### Целевая структура:
```
MyRevitApp.bundle/
├── PackageContents.xml          (создает Autodesk при первой отправке)
├── Contents/
│   ├── 2026/                     (версия Revit)
│   │   ├── MyRevitApp.dll
│   │   ├── MyRevitApp.addin
│   │   └── [dependency DLLs]
│   └── [другие версии если нужно]
└── Resources/
    ├── MyRevitApp.png            (32x32 или 64x64 icon)
    ├── MyRevitApp_Help.html      (контекстная справка)
    └── [другие ресурсы]
```

### Ключевые моменты:
- **НЕ включайте** RevitAPI.dll и RevitAPIUI.dll (Revit уже их имеет)
- **ДА включайте** все внешние зависимости (Newtonsoft.Json, кастомные библиотеки)
- **ДА включайте** .addin файл в Contents папку (не в Addins)
- **ДА используйте** относительные пути в адин-файле

## 2. Файл PackageContents.xml

Autodesk создаст этот файл при первой отправке, но вот пример структуры для Revit 2026:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage 
    SchemaVersion="1.0"
    AutodeskProduct="Revit"
    ProductType="Application"
    Name="Revit Chat Assistant"
    AppVersion="1.0.0"
    Description="AI-powered chat assistant for Revit"
    Author="Your Company"
    Icon="./Resources/icon.png"
    OnlineDocumentation="https://your-site.com/docs"
    HelpFile="./Resources/help.html"
    ProductCode="{GENERATE-UNIQUE-GUID}"
    UpgradeCode="{GENERATE-UNIQUE-GUID}"
    FriendlyVersion="1.0.0">
    
    <CompanyDetails 
        Name="Your Company" 
        Url="https://your-site.com" 
        Email="support@your-site.com" 
        Phone=""/>
    
    <RuntimeRequirements 
        OS="Win64" 
        Platform="Revit" 
        SeriesMin="R2026" 
        SeriesMax="R2026"/>
    
    <Components Description="Revit 2026">
        <RuntimeRequirements 
            OS="Win64" 
            Platform="Revit" 
            SeriesMin="R2026" 
            SeriesMax="R2026"/>
        <ComponentEntry 
            AppName="RcaRevitAddin"
            Version="1.0.0"
            ModuleName="./Contents/2026/Rca.addin"
            AppDescription="Revit Chat Assistant"/>
    </Components>
</ApplicationPackage>
```

## 3. Файл .addin для Bundle

В папке `Contents/2026/` должен быть файл `Rca.addin`:

```xml
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
    <AddIn Type="Application">
        <Name>Revit Chat Assistant</Name>
        <Assembly>.\Rca.Loader.dll</Assembly>
        <ClientId>GENERATE-GUID-HERE</ClientId>
        <FullClassName>Rca.Loader.LoaderApp</FullClassName>
        <VendorId>RCA</VendorId>
        <VendorDescription>Revit Chat Assistant by Your Company</VendorDescription>
    </AddIn>
</RevitAddIns>
```

**Важно:** Используйте относительный путь `.\Rca.Loader.dll` — Revit загружает addin из ApplicationPlugins и пути вычисляются относительно этого расположения.

## 4. MSBuild RELEASE конфигурация

### 4.1 Создайте новый .props файл: `build/props/release-packaging.props`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <!-- RELEASE packaging configuration for App Store -->
    
    <PropertyGroup Condition="'$(Configuration)' == 'Release'">
        <!-- Create bundle folder structure -->
        <BundleName>Rca.bundle</BundleName>
        <BundleDir>$(OutputPath)$(BundleName)</BundleDir>
        <BundleContentsDir>$(BundleDir)\Contents\2026</BundleContentsDir>
        <BundleResourcesDir>$(BundleDir)\Resources</BundleResourcesDir>
        
        <!-- Exclude Revit API DLLs from packaging -->
        <ExcludedDlls>RevitAPI.dll;RevitAPIUI.dll;RevitAPIUI.r.dll</ExcludedDlls>
        
        <!-- Release optimization flags -->
        <DebugType>embedded</DebugType>
        <DebugSymbols>true</DebugSymbols>
        <Optimize>true</Optimize>
        <TieredCompilation>true</TieredCompilation>
        <TieredCompilationQuickJit>true</TieredCompilationQuickJit>
    </PropertyGroup>
</Project>
```

### 4.2 Импортируйте в Directory.Build.Props:

```xml
<Import Project="$(SolutionDir)build\props\release-packaging.props" />
```

### 4.3 Создайте targets файл: `build/targets/release-packaging.targets`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <!-- Bundle creation for App Store submission -->
    
    <!-- Run only for Rca.Loader in Release configuration -->
    <Target Name="CreateAppStoreBundle" 
            AfterTargets="Build" 
            Condition="'$(Configuration)' == 'Release' and '$(MSBuildProjectName)' == 'Rca.Loader'">
        
        <!-- Clean previous bundle -->
        <RemoveDir Directories="$(BundleDir)" />
        
        <!-- Create directory structure -->
        <MakeDir Directories="$(BundleContentsDir);$(BundleResourcesDir)" />
        
        <!-- Copy main DLL and dependencies (exclude Revit API) -->
        <ItemGroup>
            <BundleDlls Include="$(TargetDir)*.dll" 
                        Exclude="$(TargetDir)$(ExcludedDlls.Replace(';', ';$(TargetDir)'))" />
            <BundlePdbs Include="$(TargetDir)*.pdb" 
                        Exclude="$(TargetDir)RevitAPI*" />
        </ItemGroup>
        
        <Copy SourceFiles="@(BundleDlls)" DestinationFolder="$(BundleContentsDir)" />
        <Copy SourceFiles="@(BundlePdbs)" DestinationFolder="$(BundleContentsDir)" />
        
        <!-- Copy .addin file -->
        <Copy SourceFiles="$(TargetDir)Rca.addin" 
              DestinationFolder="$(BundleContentsDir)" />
        
        <!-- Copy Resources -->
        <ItemGroup>
            <BundleResources Include="$(SolutionDir)resources\*.*" />
        </ItemGroup>
        <Copy SourceFiles="@(BundleResources)" 
              DestinationFolder="$(BundleResourcesDir)" />
        
        <Message Text="✓ App Store bundle created: $(BundleDir)" Importance="High" />
    </Target>

    <!-- Create ZIP file for distribution -->
    <Target Name="CreateBundleZip" 
            AfterTargets="CreateAppStoreBundle"
            Condition="'$(Configuration)' == 'Release' and '$(MSBuildProjectName)' == 'Rca.Loader'">
        
        <Exec Command="powershell -NoLogo -Command &quot;Compress-Archive -Path '$(BundleDir)' -DestinationPath '$(OutputPath)$(BundleName).zip' -Force&quot;" />
        
        <Message Text="✓ Bundle ZIP created: $(OutputPath)$(BundleName).zip" Importance="High" />
    </Target>

    <!-- Create PackageContents.xml template -->
    <Target Name="GeneratePackageContents"
            AfterTargets="CreateAppStoreBundle"
            Condition="'$(Configuration)' == 'Release' and '$(MSBuildProjectName)' == 'Rca.Loader'">
        
        <PropertyGroup>
            <PackageContentsPath>$(BundleDir)\PackageContents.xml</PackageContentsPath>
        </PropertyGroup>
        
        <WriteLinesToFile 
            File="$(PackageContentsPath)"
            Lines="&lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
&lt;ApplicationPackage SchemaVersion=&quot;1.0&quot; AutodeskProduct=&quot;Revit&quot; ProductType=&quot;Application&quot; Name=&quot;RcaRevitAddin&quot; AppVersion=&quot;1.0.0&quot; Description=&quot;Revit Chat Assistant&quot;&gt;
  &lt;RuntimeRequirements OS=&quot;Win64&quot; Platform=&quot;Revit&quot; SeriesMin=&quot;R2026&quot; SeriesMax=&quot;R2026&quot; /&gt;
  &lt;Components Description=&quot;Revit 2026&quot;&gt;
    &lt;ComponentEntry AppName=&quot;RcaRevitAddin&quot; ModuleName=&quot;./Contents/2026/Rca.addin&quot; /&gt;
  &lt;/Components&gt;
&lt;/ApplicationPackage&gt;"
            Overwrite="true" />
        
        <Message Text="✓ PackageContents.xml template generated (заполните вручную в Autodesk)" Importance="High" />
    </Target>
</Project>
```

### 4.4 Импортируйте targets в Directory.Build.Targets:

```xml
<Import Project="$(SolutionDir)build\targets\release-packaging.targets" />
```

## 5. Требования Autodesk к контролю качества

### Что проверяют при отправке:

1. **Структура Bundle**
   - ✅ Папка с расширением `.bundle`
   - ✅ PackageContents.xml в корне
   - ✅ Contents/YYYY с .addin файлом и DLL
   - ✅ Resources с иконкой и справкой

2. **Ribbon Button**
   - ❌ **ОШИБКА**: Использовать External Tools
   - ✅ Ribbon button обязателен (не может быть только в External Tools)
   - ✅ Иконка 16x16 или 32x32 пикселей
   - ✅ Следовать Autodesk Icon Guidelines

3. **Иконка приложения**
   - Размер: 32x32 или 64x64 пикселей
   - Формат: PNG с прозрачностью
   - Путь: `Resources/AppIcon.png`

4. **Справка (F1 Help)**
   - Требуется файл HTML или ссылка на онлайн-справку
   - Установить через `RibbonItem.SetContextualHelp()`

5. **Компилирование и версионирование**
   - Assembly version в формате Semantic Versioning (1.0.0)
   - ProductCode и UpgradeCode должны быть уникальными GUID-ы
   - Включить PDB для отладки

6. **Зависимости**
   - НЕ включайте RevitAPI.dll (Revit их уже имеет)
   - Включите все сторонние DLL (Newtonsoft.Json, etc.)
   - Используйте только 64-bit сборки

## 6. Типичные причины отказа по данным сообщества

### ❌ Частые ошибки:

1. **Отсутствие Ribbon Button**
   - Autodesk требует видимую кнопку на ленте
   - External Tools недостаточно

2. **Неправильные пути в .addin**
   - Абсолютные пути вместо относительных
   - Несоответствие между PackageContents.xml и фактическим расположением

3. **Отсутствие иконок и справки**
   - Иконка должна быть в Resources
   - Справка (HTML или URL) обязательна

4. **Включены Revit API DLL**
   - Увеличивает размер без необходимости
   - Autodesk их проверяет и может отклонить

5. **Ошибки в PackageContents.xml**
   - Неправильные GUID
   - Неверные версии Revit (SeriesMin/SeriesMax)
   - Неправильные пути к ModuleName

6. **Проблемы с загрузкой**
   - FullClassName не найден в DLL
   - Assembly не может быть загружена
   - Зависимости не скопированы

## 7. Чеклист перед отправкой

```
□ DLL компилируется в Release конфигурации без ошибок
□ Все ribbon button'ы имеют иконки
□ Установлена F1 справка через SetContextualHelp()
□ Иконка приложения присутствует в Resources/ (32x32 или 64x64)
□ Все зависимости (кроме RevitAPI) включены в bundle
□ .addin файл использует относительные пути (.\*.dll)
□ PackageContents.xml валидный XML с правильными GUID-ами
□ Версия в ProductVersion, AssemblyVersion и PackageContents совпадают
□ Проверено на Revit 2026 (или целевой версии)
□ Bundle ZIP создан и распакован — все файлы на месте
□ Проверена справка (HTML открывается, ссылки работают)
```

## 8. Команды для сборки

```bash
# Чистая Release сборка
dotnet clean
dotnet build -c Release

# Создать bundle (автоматически с targets)
# После Build в $(OutputPath)Rca.bundle/ будет структура

# Для ручной проверки перед отправкой
# Распаковать Rca.bundle.zip и проверить структуру
```

## 9. Где получить дополнительную информацию

### Официальные источники:
- **Autodesk Exchange Store Guidelines**: Документ "Preparing Apps for the Store" (в вашем web:4)
- **Revit API Documentation**: Getting Started with the Revit API
- **Icon Guidelines**: Autodesk Icon Guidelines.pdf в Revit SDK

### Сообщество и форумы:
- **Autodesk Revit API Forum**: forums.autodesk.com (ищите результаты отказов других разработчиков)
- **The Building Coder** (Jeremy Tammik): thebuildingcoder.typepad.com
- **ADN DevBlog**: adndevblog.typepad.com

### Практические примеры:
- GitHub проекты с PackageContents.xml (ricaun-io/Autodesk.PackageBuilder)
- Успешные приложения на App Store (изучите их структуру)
