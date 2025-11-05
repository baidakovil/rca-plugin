## Контекст
В .NET 8 решении (Revit add-in) нужны метрики качества с одного взгляда: покрытие тестами, статические нарушения и кодовые метрики, плюс дельта относительно baseline, чтобы быстро оценивать эффект последних изменений. 

## Цель
Сделать легковесный локальный HTML-дашборд, который агрегирует три источника метрик, строит сравнение с baseline предыдущего состояния и не требует серверной инфраструктуры или сложной поддержки.  
Идея: 
1. взять метрики с трех источников (xml, xml, json) и спарсить их в единый `metrics-report.json` файл с помощью консольного приложения 
2. взять единый json из предыдущего состояния проекта (в дальнейшем он называется `metrics-baseline.json`, необходим для анализа изменений в метриках, baseline сохраняется вручную)
3. сгенерировать html-файл для human-readable анализа на основе `metrics-report.json` и `metrics-baseline.json`.

####

В коде определены следующие уровни, для каждого из которых рассчитывается свое значение метрики:  

# УТОЧНИТЬ!!!

- Solution
- Assembly
- Namespace
- Class
- Method. 

Если для какого-то уровня метрика не определена, в отчете на этом месте будет прочерк: "-".

#### Источник 1 (покрытие)

AltCover выводит покрытие в формате OpenCover XML. 
Отсюда в JSON и HTML нужно выводить:
- Cyclomatic Complexity AltCover (пометить отдельно "AltCover", чтобы не путать с Cyclomatic Complexity от Roslyn. Нужно иметь обе метрики и в JSON и в HTML, даже если они дают одинаковое значение)
- NPath Complexity  
- Sequence Coverage  
- Branch Coverage  


Пример xml (начало файла): 
```xml
<?xml version="1.0" encoding="utf-8" standalone="yes"?>
<CoverageSession xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Summary numSequencePoints="2616" visitedSequencePoints="1271" numBranchPoints="834" visitedBranchPoints="334" sequenceCoverage="48.59" branchCoverage="40.05" maxCyclomaticComplexity="37" minCyclomaticComplexity="1" visitedClasses="57" numClasses="71" visitedMethods="161" numMethods="232" minCrapScore="1" maxCrapScore="462" />
  <Modules>
    <Module hash="7A-3D-D3-85-74-04-6D-5B-97-BA-60-4E-7D-DB-0C-F5-55-72-C3-F3">
      <Summary numSequencePoints="2582" visitedSequencePoints="1250" numBranchPoints="824" visitedBranchPoints="330" sequenceCoverage="48.41" branchCoverage="40.05" maxCyclomaticComplexity="37" minCyclomaticComplexity="1" visitedClasses="55" numClasses="69" visitedMethods="157" numMethods="226" minCrapScore="1" maxCrapScore="462" />
      <ModulePath>C:\Users\baidakov\AppData\Roaming\Autodesk\Revit\Addins\2026\20251105_001521\__Saved\Rca.Loader.dll</ModulePath>
      <ModuleTime>2025-11-04T21:16:00.4319617Z</ModuleTime>
      <ModuleName>Rca.Loader</ModuleName>
      <Files>
        <File uid="3" fullPath="C:\Users\baidakov\rca-plugin\src\Rca.Loader\LoaderApp.cs" />
        <File uid="4" fullPath="C:\Users\baidakov\rca-plugin\src\Rca.Loader\UI\DockablePanelHost.cs" />
        ...
      </Files>
      <Classes>
        <Class>
          <Summary numSequencePoints="201" visitedSequencePoints="112" numBranchPoints="51" visitedBranchPoints="23" sequenceCoverage="55.72" branchCoverage="45.1" maxCyclomaticComplexity="9" minCyclomaticComplexity="1" visitedClasses="1" numClasses="1" visitedMethods="11" numMethods="13" minCrapScore="1" maxCrapScore="20" />
          <FullName>Rca.Loader.LoaderApp</FullName>
          <Methods>
            <Method visited="true" cyclomaticComplexity="1" nPathComplexity="0" sequenceCoverage="100" branchCoverage="0" isConstructor="false" isStatic="false" isGetter="true" isSetter="false" crapScore="1">
              <Summary numSequencePoints="1" visitedSequencePoints="1" numBranchPoints="1" visitedBranchPoints="0" sequenceCoverage="100" branchCoverage="0" maxCyclomaticComplexity="1" minCyclomaticComplexity="1" visitedClasses="0" numClasses="0" visitedMethods="1" numMethods="1" minCrapScore="1" maxCrapScore="1" />
              <MetadataToken>100663298</MetadataToken>
              <Name>Rca.Loader.AssemblyManagement.AssemblyStatusManager Rca.Loader.LoaderApp::get_AssemblyStatusManager()</Name>
              <FileRef uid="3" />
              <SequencePoints>
                <SequencePoint vc="1" uspid="0" ordinal="0" offset="0" sl="48" sc="64" el="48" ec="85" bec="0" bev="0" fileid="3" />
              </SequencePoints>
              <BranchPoints />
              <MethodPoint xsi:type="SequencePoint" vc="1" uspid="0" ordinal="0" offset="0" sl="48" sc="64" el="48" ec="85" bec="0" bev="0" fileid="3" />
            </Method>
            <Method visited="true" cyclomaticComplexity="1" nPathComplexity="0" sequenceCoverage="100" branchCoverage="0" isConstructor="false" isStatic="false" isGetter="true" isSetter="false" crapScore="1">
              <Summary numSequencePoints="1" visitedSequencePoints="1" numBranchPoints="1" visitedBranchPoints="0" sequenceCoverage="100" branchCoverage="0" maxCyclomaticComplexity="1" minCyclomaticComplexity="1" visitedClasses="0" numClasses="0" visitedMethods="1" numMethods="1" minCrapScore="1" maxCrapScore="1" />
              <MetadataToken>100663301</MetadataToken>
              <Name>Autodesk.Revit.UI.UIApplication Rca.Loader.LoaderApp::get_UIApplication()</Name>
              <FileRef uid="3" />
              <SequencePoints>
                <SequencePoint vc="1" uspid="1" ordinal="0" offset="0" sl="59" sc="48" el="59" ec="53" bec="0" bev="0" fileid="3" />
              </SequencePoints>
              <BranchPoints />
              <MethodPoint xsi:type="SequencePoint" vc="1" uspid="1" ordinal="0" offset="0" sl="59" sc="48" el="59" ec="53" bec="0" bev="0" fileid="3" />
            </Method>
```

Фактическая схема может отличаться от примера, поэтому изучай реальные файлы после написания первых примеров для отладки парсинга. Адрес реального файла для отладки консольного приложения: `$(SolutionDir)build\Metrics\AltCover\coverage.xml`.

#### Источник 2 (кодовые метрики)
Microsoft.CodeAnalysis.Metrics выдает следующие метрики:
- Maintainability Index  
- Cyclomatic Complexity   (пометить отдельно "Roslyn", чтобы не путать с Cyclomatic Complexity от AltCover. Нужно иметь обе метрики и в JSON и в HTML, даже если они дают одинаковое значение)
- Class Coupling  
- Depth Of Inheritance  
- Source Lines  
- Executable Lines.

Адрес файла для отладки консольного приложения: `$(SolutionDir)\build\Metrics\Roslyn\Rca.Loader.xml`

Пример xml (начало):
```xml
<?xml version="1.0" encoding="utf-8"?>
<CodeMetricsReport Version="1.0">
  <Targets>
    <Target Name="Rca.Loader.csproj">
      <Assembly Name="Rca.Loader, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null">
        <Metrics>
          <Metric Name="MaintainabilityIndex" Value="81" />
          <Metric Name="CyclomaticComplexity" Value="748" />
          <Metric Name="ClassCoupling" Value="204" />
          <Metric Name="DepthOfInheritance" Value="9" />
          <Metric Name="SourceLines" Value="4246" />
          <Metric Name="ExecutableLines" Value="1506" />
        </Metrics>
        <Namespaces>
          <Namespace Name="Rca.Loader">
            <Metrics>
              <Metric Name="MaintainabilityIndex" Value="66" />
              <Metric Name="CyclomaticComplexity" Value="48" />
              <Metric Name="ClassCoupling" Value="40" />
              <Metric Name="DepthOfInheritance" Value="1" />
              <Metric Name="SourceLines" Value="0" />
              <Metric Name="ExecutableLines" Value="111" />
            </Metrics>
            <Types>
              <NamedType Name="LoaderApp">
                  <Field Name="AssemblyStatusManager? LoaderApp.assemblyStatusManager" File="C:\Users\baidakov\rca-plugin\src\Rca.Loader\LoaderApp.cs" Line="27">
                    <Metrics>
                      <Metric Name="MaintainabilityIndex" Value="100" />
                      <Metric Name="CyclomaticComplexity" Value="0" />
                      <Metric Name="ClassCoupling" Value="1" />
                      <Metric Name="SourceLines" Value="1" />
                      <Metric Name="ExecutableLines" Value="0" />
                    </Metrics>
```

Поля внутри xml могут также уходить дальше вглубь через `<Accessors><Method Name="..."><Metrics><Metric Name="..." Value="..." />`, поэтому изучай фактический файл и его схему. 

#### Источник 3 (нарушения)
SARIF 2.x файл со списком rule violations (performance/security/quality), который должен быть агрегирован и показан с группировкой по проекту/файлу/символу. Адрес файла для отладки консольного оприложения: `$(SolutionDir)build\Metrics\Sarif\Rca.Loader.sarif`

Из этого файла должно выводиться две метрики для каждого уровня: 

CA Rules Violation;
IDE rules Violation.

Метрики определяются как количество rules violations с префиксами CA и IDE для данного . Например, если у метода два rule violation CA1510, и одно CA1822, то должна  метрика "CA rules" со значением 3, и "IDE Rules" со значением 0. Если вдобавок у метода есть 5 rule viloation IDE0060, то метрика "IDE rules" будет иметь значение 5.

## Консольный агрегатор
Небольшое .NET 8 консольное приложение, принимающее:
- три пути к исходным данным, т.е. OpenCover XML, SARIF, metrics-JSON/CSV,
- путь к output json, 
- путь к baseline JSON,
- путь к  и ouput HTML.
- пороговые значения метрик 

Результатом работы приложения являются:
- JSON с актуальными метриками `metrics-report.json`
- HTML с актуальными метриками и их изменениями `metrics-report.html`. 

Приложение должно быть расположено в `$(SolutionDir)\src\Tools\Rca.MetricsReporter`. Агрегатор должен принимать пути к исходным файлам, пути к выходным файлам, пороговые значения метрик.  

Пороговые статусы должны храниться в `$(SolutionDir)\build\Props\code-metrics.props` и передаваться в консольное приложение из MSBuild-таргета, следует передавать пороговые статусы как один MSBuild property. Дефолтные значения порогов ориентируются на рекомендации Microsoft. 

Агрегатор должен выводить логи в `$(MetricsDir)Report\metrics-reporter.log`

Стабильность путей: Все пути должны вычисляться относительно `$(SolutionDir)` или `$(MetricsDir)`. 

Два Json файла (baseline и текущий) и html файл должны храниться в директории `$(MetricsDir)Report`.

Все пути должны храниться в `$(SolutionDir)\build\Props\paths.props` как single source of truth, и передаваться в аггрегатор через msbuild-properties, и далее в Html.

Для аггрегатора следует добавить unit и Integration Тесты. 

Требований по производительности нет - отчеты в сумме не занимают больше 3 мегабайт.

Консольное приложение должно выдавать выходные коды  (0 OK, 1 парсинг, 2 IO, 3 validation), которые учитывает MSBuild для своего output.

#### JSON Schema

Миграция json-схемы не предусматривается, трассировка и совместимость с тулом не требуется. 

# УТОЧНИТЬ ПОЛЯ!!!


#### Изменения в коде
При появлении в коде новых методов (например, при написании новых, или при переименовании), агрегатор должен отреагировать нормально, пометив изменения текстом "NEW" рядом с основным значением метрики, но более мелким шрифтом зеленого цвета, как индекс в верхнем регистре. 

При пропадании методов, (например, при удалении, или при переименовании), агрегатор не должен отображать их в отчете (даже если обнаружит их в baseline файле).

#### Отсутствие входных данных

Если входные данные переданы как параметр MSBuild, но не были найдены, то агрегатор должен упасть и выдать код ошибки в MSBuild - и отобразить в логах, почему упал.

## Конечный результат (HTML)

Формат выходных данных: Один HTML-отчет `metrics-report.html` с drill-down секциями Solution/Assembly/Namespace/Class/Method. Отмечать сущности с метриками ниже порога красным цветом. 

Безопасность HTML через экранирование данных и CSP не требуется, т.к. вся разработка локально одним разработчиком.

#### Delta сравнение
Для каждой сущности в html выводить изменения метрик относительно baseline файла. Вывод изменений метрик - через текст со знаком "+" или "-" и число-дельту, например "+10" рядом с основным значением метрики, но более мелким шрифтом другого цвета, как индекс в верхнем регистре. Для целей отладки сгенерируй произвольный baseline самостоятельно из metrics-report.json. Обновление baseline в дальнейшем будет происходить вручную.

## MSBuild интеграция

Когда JSON-парсинг и генерация HTML Будут готовы, Добавить кастомную цель в `code-metrics.targets`, которая после тестов вызывает агрегатор, генерирует JSON-снапшот, вычисляет дельту относительно baseline и выдает HTML-дашборд. 


## Порядок работы

Учти, что в проекте проблематичен билд standalone-приложений, т.к. корневой `Directory.Build.targets` тянет за собой общие таргеты, такие как создание timestamp-папок, генерацию хэшей и т.д. через обычный `dotnet build` корректно работает только билд для всего solution. Предусмотри это при отладке агрегатора, используя `/p:SkipGlobalTargets=true` или другие методы.