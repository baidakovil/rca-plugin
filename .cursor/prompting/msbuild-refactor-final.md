# Рефакторинг MSBuild Build System

## Цель
Реорганизовать Directory.Build.targets и /tests/Directory.Build.targets, по SOLID/DRY принципам и MSBuild best practices с централизацией конфигурации через Source Generator.

## Текущая структура
- Directory.Build.props и Directory.Build.targets - монолитные
- build/ содержит *.props файлы, но не содержит *.targets
- tests/ имеет свои Directory.Build.props и .targets

## Требуемая структура папок
```
build/
├── props/                           # Переместить сюда существующие .props
│   ├── paths.props                  
│   ├── versioning.props
│   ├── compilation.props
│   ├── hot-reloading-system.props
│   └── testing.props                # Создать новый
└── targets/                         # Создать и разбить Directory.Build.targets
    ├── timestamp-management.targets
    ├── hash-generation.targets
    ├── metadata-emission.targets
    ├── deployment.targets
    ├── build-notification.targets
    └── testing.targets
```

## Рефакторинг Directory.Build.targets - разбить по модулям:

**timestamp-management.targets**: EnsureRcaTimestamp, GenerateRcaAddinFile

**hash-generation.targets**: BuildSourceHashGenerator, EnsureRuntimeDeployFolder, GenerateHash
- PropertyGroup с SourceHashProject, SourceHashExe вынести в paths.props

**metadata-emission.targets**: EmitAssemblyMetadataSource
- Заменить множественные Condition="'$(MSBuildProjectName)' == 'Rca.Loader' or ..." на ItemGroup (ниже пример. твои исправления должны следовать этому примеру, но не повторять его в точности):
```xml
<ItemGroup>
  <LoaderProjects Include="Rca.Loader;Rca.Loader.Contracts;Rca.Logging.Contracts" />
  <RuntimeProjects Include="Rca.Core;Rca.Runtime;Rca.Network;Rca.UI;Rca.Contracts" />
</ItemGroup>
<PropertyGroup>
  <IsLoaderGroupProject Condition="@(LoaderProjects->Contains('$(MSBuildProjectName)'))">true</IsLoaderGroupProject>
  <IsRuntimeGroupProject Condition="@(RuntimeProjects->Contains('$(MSBuildProjectName)'))">true</IsRuntimeGroupProject>
</PropertyGroup>
```

**deployment.targets**: DeployLoaderGroup, DeployRuntimeGroup

**build-notification.targets**: NotifyBuildCompleted

## КРИТИЧНО - Single Source of Truth для списков проектов

LoaderProjects и RuntimeProjects в MSBuild должны быть **ЕДИНСТВЕННЫМ** местом определения этих списков.

### Требование 1: Экспорт в Source Generator
- LoaderProjects и RuntimeProjects должны быть доступны Source Generator через MSBuild API
- Используй CompilerVisibleProperty или AdditionalFiles в зависимости от версии .NET SDK
- Сделай эти значения видимыми в context.AnalyzerConfigOptions.GlobalOptions

### Требование 2: Генерация C# кода
- Source Generator должен читать LoaderProjects и RuntimeProjects из MSBuild
- Генерировать файл BuildConfiguration.g.cs с публичным API для доступа к спискам assembling
- Генерируемый код должен содержать ТОЛЬКО значения из MSBuild, БЕЗ hardcoded данных

### Требование 3: Удаление дублирования
- УДАЛИТЬ все hardcoded списки LoaderAssemblies, RuntimeAssemblies и аналогичные константы из кода
- Заменить их на использование сгенерированного BuildConfiguration
- Проверить ВСЕ проекты (Rca.Loader, Rca.Core и т.д.) на предмет дублей

### Требование 4: Increment work
- Все изменения нужно делать постепенно, проверяя через dotnet build --no-incremental, что все работает
- При наличии спорных вопросов или наличия различных принципиальных способов решения задачи следует останавливать работу и спрашивать, как правильно поступить

## Обновить корневые файлы

**Directory.Build.props** - только импорты (ниже пример, исправь если будут другие импорты):
```xml
<Project>
  <Import Project="$(SolutionDir)build\props\paths.props" />
  <Import Project="$(SolutionDir)build\props\versioning.props" />
  <Import Project="$(SolutionDir)build\props\compilation.props" />
  <Import Project="$(SolutionDir)build\props\hot-reloading-system.props" />
</Project>
```

**Directory.Build.targets** - только импорты (ниже пример, исправь если будут другие импорты):
```xml
<Project>
  <Import Project="$(SolutionDir)build\targets\timestamp-management.targets" />
  <Import Project="$(SolutionDir)build\targets\hash-generation.targets" />
  <Import Project="$(SolutionDir)build\targets\metadata-emission.targets" />
  <Import Project="$(SolutionDir)build\targets\deployment.targets" />
  <Import Project="$(SolutionDir)build\targets\build-notification.targets" />
</Project>
```

## Тестовые файлы

**tests/Directory.Build.props** (ниже пример, исправь если будут другие импорты):
```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <Import Project="$(SolutionDir)build\props\testing.props" />
  <PropertyGroup>
    <IsLoaderGroupProject>false</IsLoaderGroupProject>
    <IsRuntimeGroupProject>false</IsRuntimeGroupProject>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
</Project>
```

**tests/Directory.Build.targets** (ниже пример, исправь если будут другие импорты):
```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.targets', '$(MSBuildThisFileDirectory)../'))" />
  <Import Project="$(SolutionDir)build\targets\testing.targets" />
  <Target Name="NotifyBuildCompleted" />
  <Target Name="DeployLoaderGroup" />
  <Target Name="DeployRuntimeGroup" />
</Project>
```

## Технологии и ограничения
- Все изменения нужно делать постепенно, проверяя через dotnet build --no-incremental, что все работает
- При наличии спорных вопросов или наличия различных принципиальных способов решения задачи следует останавливать работу и спрашивать, как правильно поступить
- Все пути хранятся в paths.props
- При внесении изменений в Source Generator имей ввиду, что пересборка Source Generator может не произойти автоматически, нужно использовать /build/Scripts/Prepare-Projects.ps1 с корректными параметрами для cleaning/rebuild/restore Source Generator 

## Принципы
- Каждый targets-файл = одна ответственность (SRP)
- Все пути в paths.props (DRY)
- Все изменения следуют правилам в /.cursor
- LoaderProjects/RuntimeProjects ItemGroup = единственный источник истины для списков проектов
- Source Generator преобразует MSBuild конфигурацию в C# код
- Directory.Build.* = только оркестраторы импортов
- tests/Directory.Build.* = явный импорт родительских + переопределения
- НИКАКИХ дублированных списков проектов в коде
