## Metrics Reporter Overview

Metrics Reporter — консольное приложение .NET 8, объединяющее метрики покрытия AltCover/OpenCover, кодовые метрики Roslyn и нарушения правил из SARIF в единый файл `metrics-report.json`, после чего генерируется HTML-дашборд.

### Основные артефакты
- `metrics-report.json` — иерархическая модель Solution → Assembly → Namespace → Type → Member с 12 единообразными метриками.
- `metrics-baseline.json` — предыдущий снепшот, служащий источником дельт и пометок `NEW`.
- `metrics-report.html` — визуализация фактических значений и дельт.

### Источники данных
1. **AltCover/OpenCover**: `AltCoverSequenceCoverage`, `AltCoverBranchCoverage`, `AltCoverCyclomaticComplexity`, `AltCoverNPathComplexity`.
2. **Roslyn (Microsoft.CodeAnalysis.Metrics)**: `RoslynMaintainabilityIndex`, `RoslynCyclomaticComplexity`, `RoslynClassCoupling`, `RoslynDepthOfInheritance`, `RoslynSourceLines`, `RoslynExecutableLines`.
3. **SARIF (Roslyn анализаторы)**: количество нарушений `SarifCaRuleViolations`, `SarifIdeRuleViolations`.

### HTML Dashboard Highlights
- **Фильтрация сборок**: при агрегации исключаются сборки, перечисленные в `ExcludedAssemblyNames`; в HTML они не отображаются.
- **Раскрытие узлов**: строки уровней Solution/Assembly/Namespace/Type кликабельны по всей ячейке и по кнопке `+/-`, что ускоряет навигацию.
- **Панель действий**: слайдер `Detailing` управляет глубиной отображения (`Namespace → Type → Member`), слайдер `Awareness` фильтрует строки по наличию Warning/Error (при этом родительские узлы остаются видимыми, если есть видимые потомки), есть поле фильтра и кнопки `Expand all`/`Collapse all`.
- **Статистика под спойлером**: в блоке `meta-details` отображаются подсчитанные количества символов (total/no metric/clear/warning/error) и проценты с дельтами относительно baseline.

### JSON-структура (сокращённо)
```json
{
  "solution": {
    "name": "rca-plugin",
    "generatedAtUtc": "2025-11-06T21:40:00Z",
    "paths": {
      "metricsDirectory": "build/Metrics",
      "baseline": "build/Metrics/Report/metrics-baseline.json",
      "report": "build/Metrics/Report/metrics-report.json",
      "html": "build/Metrics/Report/metrics-report.html"
    },
    "assemblies": [
      {
        "name": "Rca.Loader",
        "kind": "Assembly",
        "fullyQualifiedName": "Rca.Loader",
        "metrics": {
          "AltCoverSequenceCoverage": { "value": 48.59, "delta": -1.41, "status": "Warning", "unit": "percent" },
          "RoslynMaintainabilityIndex": { "value": 81, "delta": 0, "status": "Success", "unit": "score" },
          "SarifCaRuleViolations": { "value": 12, "delta": 2, "status": "Warning", "unit": "count" }
        },
        "namespaces": [
          {
            "name": "Rca.Loader.Infrastructure",
            "kind": "Namespace",
            "metrics": { "AltCoverSequenceCoverage": { "value": null, "status": "NotApplicable", "unit": "percent" } },
            "types": [
              {
                "name": "CommandValidationService",
                "kind": "Type",
                "fullyQualifiedName": "Rca.Loader.Infrastructure.CommandValidationService",
                "source": {
                  "path": "src/Rca.Loader/Infrastructure/CommandValidationService.cs",
                  "startLine": 12,
                  "endLine": 200
                },
                "isNew": false,
                "metrics": {
                  "RoslynCyclomaticComplexity": { "value": 48, "delta": -5, "status": "Warning", "unit": "count" },
                  "SarifCaRuleViolations": { "value": 3, "delta": 1, "status": "Error", "unit": "count" }
                },
                "members": [
                  {
                    "name": "ValidateAsync",
                    "kind": "Member",
                    "fullyQualifiedName": "Rca.Loader.Infrastructure.CommandValidationService.ValidateAsync(System.String)",
                    "isNew": true,
                    "metrics": {
                      "AltCoverSequenceCoverage": { "value": 72.1, "status": "NotApplicable", "unit": "percent" },
                      "RoslynMaintainabilityIndex": { "value": 42, "status": "Error", "unit": "score" }
                    }
                  }
                ]
              }
            ]
          }
        ]
      }
    ]
  }
}
```

### Семантика ключевых полей
- `kind` — тип узла (`Solution`, `Assembly`, `Namespace`, `Type`, `Member`), требуется для HTML-дрила.
- `fullyQualifiedName` — FQN в формате `Namespace.Type.Member(args)`, пусто для Solution/Namespace когда неприменимо.
- `metrics` — словарь по метрике (строковый идентификатор `MetricIdentifier`) и объекту значения:
  - `value` (`number?`) — фактическое значение, `null`, если данных нет.
  - `delta` (`number?`) — отклонение от baseline, `null`, если элемент новый или baseline отсутствует.
  - `status` — результат сравнения с порогом (`NotApplicable`, `Success`, `Warning`, `Error`).
  - `unit` — `percent`, `count` или `score`, помогает HTML отформатировать значение.
- `isNew` — пометка новых элементов, для HTML добавляется префикс `NEW`; дельты не отображаются.
- `source` — сведения о файле/диапазоне строк, используются для сопоставления SARIF и подсказок в отчёте.

### JSON Schema
- Файл `Model/metrics-report.schema.json` фиксирует обязательные поля и допустимые перечисления.
- DTO сериализуются через `System.Text.Json` (camelCase).

### Автоматическая генерация через MSBuild
- В `build/Targets/code-metrics.targets` добавлен таргет `GenerateMetricsDashboard`, который срабатывает после сборки проекта `Rca.MetricsReporter.Tests`.
- Таргет:
  - Строит набор проектов и тестов, чтобы гарантировать актуальные метрики (через `MSBuild` по списку зависимостей).
  - Формирует аргументы (AltCover, все Roslyn XML, SARIF, baseline, пороги) с правильными разделителями.
  - Вызывает `Rca.MetricsReporter.exe` из `src/Tools/Rca.MetricsReporter/bin/<Configuration>/net8.0`.
  - Создает каталог отчётов (`$(MetricsDir)\Report`) и записывает JSON/HTML + лог.
- Благодаря этому, после стандартного `dotnet build --no-incremental` в `build/Metrics/Report` автоматически появляются `metrics-report.json` и `metrics-report.html`.

### Автоматическое управление Baseline

Система автоматического управления baseline создает `metrics-baseline.json` из предыдущего отчета перед генерацией нового отчета, обеспечивая автоматический расчет дельт между запусками.

#### Настройка

В `build/Props/code-metrics.props` установите:
```xml
<ReplaceMetricsBaseline>true</ReplaceMetricsBaseline>
```

#### Логика работы

1. **Создание baseline из предыдущего отчета (если baseline не существует)**: 
   - Если `ReplaceMetricsBaseline=true`, путь к baseline задан, но baseline не существует, система проверяет наличие предыдущего `metrics-report.json`.
   - Если предыдущий отчет существует, он копируется в `metrics-baseline.json` **ДО генерации нового отчета**.
   - Это позволяет новому отчету сразу генерироваться с дельтами, рассчитанными относительно предыдущего отчета.

2. **Генерация нового отчета**: Создается новый `metrics-report.json` с текущими метриками на основе baseline (если он существует или был создан на шаге 1).

3. **Архивация и замена baseline**: После генерации нового отчета, если `ReplaceMetricsBaseline=true`:
   - Если старый baseline существует, он архивируется в директорию хранения (`MetricsReportStoragePath`, по умолчанию `C:\Users\<username>\AppData\Local\RCA\Metrics`) с добавлением timestamp к имени файла (формат: `metrics-baseline-YYYYMMDD-HHMMSS.json`).
   - Новый `metrics-report.json` копируется в `metrics-baseline.json`, подготавливая baseline для следующего цикла генерации.

4. **Завершение**: После замены baseline процесс завершается. При следующем запуске baseline будет создан из этого отчета (шаг 1), а текущий baseline будет заархивирован (шаг 3).

#### Важные детали

- **Порядок операций**: Baseline создается из предыдущего отчета **ДО** генерации нового отчета, чтобы новый отчет сразу строился на основе предыдущего.
- **Без сравнений**: Система не сравнивает содержимое файлов. Если предыдущий отчет существует и baseline отсутствует, он становится baseline.
- **Автоматическое создание**: При каждом запуске, если baseline не существует, он автоматически создается из предыдущего отчета. Это означает, что baseline всегда актуален и соответствует последнему сгенерированному отчету.
- **Условие создания baseline из предыдущего отчета**: 
  - `ReplaceMetricsBaseline=true`
  - Путь к baseline задан (не null и не пустой)
  - Baseline не существует
  - Предыдущий `metrics-report.json` существует
- **Путь к baseline**: Должен быть задан через параметр `--baseline` или MSBuild property `MetricsBaselineJson`. MSBuild target автоматически передает путь к baseline в `Rca.MetricsReporter` когда `ReplaceMetricsBaseline=true`, даже если файл еще не существует.

#### Пример использования

```bash
# Включить автоматическое управление baseline
dotnet msbuild rca-plugin.sln /t:Build /p:ReplaceMetricsBaseline=true
```

### Дополнительно
- **Автоматическое управление baseline**: При включенной опции `ReplaceMetricsBaseline=true` baseline автоматически создается из предыдущего отчета перед генерацией нового отчета. Подробности см. в разделе "Автоматическое управление Baseline" выше.
- Приложение логирует шаги в `$(MetricsDir)\Report\metrics-reporter.log` и возвращает коды: 0 — OK, 1 — parsing error, 2 — IO error, 3 — validation error.
- Пороговые значения хранятся в `build/MetricsRules/MetricsReporterThresholds.json`; путь до файла конфигурируется через `build/Props/paths.props` (свойство `MetricsThresholdsPath`) и передается агрегатору.

### HTML Dashboard UI
- Панель действий содержит кнопки `Expand all` / `Collapse all` и компактный слайдер **Detailing**. Диапазон `[Namespace → Type → Member]` управляет максимальной глубиной видимых строк — дерево не перестраивается, а существующие строки Solution → Member повторно используются и скрываются через `data-hiddenByDetail`, поэтому дублирования метрик или символов в DOM нет.
- Слайдер снапится к ближайшему уровню при клике по треку и использует кэшированную иерархию строк в JavaScript. Это обеспечивает быстрые переключения без повторных обходов DOM.
- Узлы `Namespace` и `Type` без дочерних элементов отображаются с тем же форматированием, что и структурные узлы с детьми, но вместо кнопки раскрытия показывают серый символ `∅`, иллюстрирующий отсутствие дочерних элементов.
- Скрипт `HtmlScriptGenerator` кэширует сопоставление `parent → children`, вычисляет полосатость видимых листовых строк и состояние экспандеров в одном проходе, придерживаясь принципов SOLID: отдельные функции отвечают за детализацию, визуальное оформление и манипуляцию состояниями.

### Запуск из командной строки
Инструмент можно запустить вручную:

```bash
dotnet run --project src/Tools/Rca.MetricsReporter/Rca.MetricsReporter.csproj -- \
  --solution-name "rca-plugin" \
  --metrics-dir "build/Metrics" \
  --altcover "build/Metrics/AltCover/coverage.xml" \
  --roslyn "build/Metrics/Roslyn/Rca.Loader.xml" \
  --sarif "build/Metrics/Sarif/Rca.Loader.sarif" \
  --baseline "build/Metrics/Report/metrics-baseline.json" \
  --baseline-ref "origin/main" \
  --output-json "build/Metrics/Report/metrics-report.json" \
  --output-html "build/Metrics/Report/metrics-report.html" \
  --thresholds "{'AltCoverSequenceCoverage':{'warning':75,'error':60,'higherIsBetter':true}}"
```

Параметры `--roslyn` и `--sarif` допускают множественные значения; `--thresholds` принимает JSON-строку (символ `'` автоматически заменяется на `"`).

Информация о CLI и MSBuild обновляется по мере развития инструмента.

## Symbol Normalization

Metrics Reporter объединяет метрики из разных источников (AltCover, Roslyn, SARIF), которые описывают одни и те же символы (классы, методы) в разных форматах. Для корректного объединения метрик символы нормализуются к единому формату.

### Проблема несовместимости форматов

Разные инструменты используют разные форматы для описания символов:

- **AltCover**: `System.Void Rca.Loader.LoaderApp::OnApplicationIdling(System.Object, Autodesk.Revit.UI.Events.IdlingEventArgs)`
  - Использует полностью квалифицированные имена типов параметров
  - Использует C++-стиль разделителя `::` для пространств имен
  - Всегда включает return type в начале

- **Roslyn**: `void OnApplicationIdling(object? sender, IdlingEventArgs e)`
  - Использует короткие имена типов (без namespace)
  - Использует nullable annotations (`?`)
  - Может не включать полный путь к типу в имени метода

Без нормализации один и тот же метод из разных источников рассматривается как два разных символа, что приводит к дублированию записей в отчете.

### Нормализованный формат

Все символы нормализуются к единому формату:

- **Типы**: `Namespace.Type` (generic параметры удаляются, например `List<string>` → `List`)
- **Методы**: `Namespace.Type.Method(...)` (параметры заменяются на `...`, generic параметры метода удаляются)
- **Конструкторы**: `Namespace.Type..ctor(...)` (статический конструктор: `.cctor(...)`)
- **Операторы**: `Namespace.Type.op_Equality(...)`, `Namespace.Type.op_Inequality(...)`
- **Специальные методы**: `Namespace.Type.<Clone>$(...)`, `Namespace.Type.ToString(...)`

### Процесс нормализации

#### 1. Извлечение имени метода (`ExtractMethodName`)

Извлекает только имя метода без параметров, return type и generic параметров:

- Удаляет return type (например, `void Method(...)` → `Method`)
- Удаляет generic параметры метода (например, `Method<T>(...)` → `Method`)
- Извлекает имя после последней точки (например, `Namespace.Type.Method` → `Method`)
- Сохраняет ведущую точку для конструкторов (`.ctor`, `.cctor`)
- Сохраняет специальные символы в имени (например, `<Clone>$`)

**Примеры:**
- `System.String Rca.Logging.Contracts.LogEntryDto::ToString()` → `ToString`
- `System.Void Rca.UI.Services.ServiceResolver::.ctor(...)` → `.ctor`
- `Rca.Logging.Contracts.LogEntryDto Rca.Logging.Contracts.LogEntryDto::<Clone>$()` → `<Clone>$`
- `TInterface IServiceResolver.Resolve<TInterface>()` → `Resolve`

#### 2. Нормализация сигнатуры метода (`NormalizeMethodSignature`)

Заменяет параметры на placeholder `(...)`:

- Находит открывающую скобку параметров `(`
- Находит соответствующую закрывающую скобку `)`, обрабатывая вложенные скобки в generic типах
- Заменяет все содержимое между скобками на `...`

**Примеры:**
- `Method(System.Object, System.String)` → `Method(...)`
- `Method(object? sender, string name)` → `Method(...)`
- `Method(System.Collections.Generic.List<System.String>)` → `Method(...)`

#### 3. Нормализация FQN метода (`NormalizeFullyQualifiedMethodName`)

Применяет нормализацию сигнатуры и удаляет generic параметры метода:

- Удаляет generic параметры метода (например, `Process<T>` → `Process`)
- Отличает generic параметры от части имени метода (например, `<Clone>$` не является generic параметром)
- Применяет нормализацию сигнатуры для замены параметров

**Примеры:**
- `IServiceRegistrar.Register<TInterface>(TInterface implementation)` → `IServiceRegistrar.Register(...)`
- `UiPipeLogger.Log<TState>(LogLevel logLevel, ...)` → `UiPipeLogger.Log(...)`
- `LogEntryDto.<Clone>$()` → `LogEntryDto.<Clone>$(...)`

#### 4. Нормализация имени типа (`NormalizeTypeName`)

Удаляет generic параметры из имени типа:

- Находит первую открывающую угловую скобку `<`
- Удаляет все до конца типа (включая вложенные generic параметры)

**Примеры:**
- `List<string>` → `List`
- `Dictionary<string, int>` → `Dictionary`
- `List<Dictionary<string, int>>` → `List`

### Обработка edge cases

#### Generic параметры

Методы с generic параметрами нормализуются одинаково независимо от источника:
- AltCover: `Register(TInterface)` → `Register(...)`
- Roslyn: `Register<TInterface>(TInterface implementation)` → `Register(...)`

#### Конструкторы

Конструкторы идентифицируются по паттерну `TypeName.TypeName(...)`. Имя метода для конструкторов извлекается как имя типа:
- AltCover: `System.Void ServiceResolver::.ctor(...)` → `ServiceResolver..ctor(...)`
- Roslyn: `ServiceResolver.ServiceResolver(...)` → `ServiceResolver.ServiceResolver(...)`

#### Операторы и специальные методы

Операторы и методы компилятора сохраняют специальные имена:
- `op_Equality`, `op_Inequality` → `Namespace.Type.op_Equality(...)`
- `<Clone>$` (для record типов) → `Namespace.Type.<Clone>$(...)`

#### Сложные возвращаемые типы

Методы с tuple или generic возвращаемыми типами обрабатываются корректно:
- `Task<string> ExecuteAsync(...)` → `ExecuteAsync(...)`
- `Task<(bool, string?)> LoadRuntimeAsync(...)` → `LoadRuntimeAsync(...)`

#### Nested типы

Вложенные типы используют разделитель `+`: AltCover `Outer/Nested` → `Outer+Nested`

### Результат нормализации

После нормализации методы из разных источников с одинаковой сигнатурой объединяются в одну запись в отчете:

**До нормализации:**
- AltCover: `Rca.Loader.LoaderApp.OnApplicationIdling(System.Object, Autodesk.Revit.UI.Events.IdlingEventArgs)`
- Roslyn: `Rca.Loader.LoaderApp.OnApplicationIdling(object? sender, IdlingEventArgs e)`
- Результат: Две отдельные записи в отчете

**После нормализации:**
- AltCover: `Rca.Loader.LoaderApp.OnApplicationIdling(...)`
- Roslyn: `Rca.Loader.LoaderApp.OnApplicationIdling(...)`
- Результат: Одна запись с метриками из обоих источников

### Реализация

Нормализация реализована в классе `SymbolNormalizer` (`src/Tools/Rca.MetricsReporter/Processing/SymbolNormalizer.cs`):

- `NormalizeMethodSignature(string?)` — нормализует сигнатуру метода
- `ExtractMethodName(string?)` — извлекает имя метода
- `NormalizeFullyQualifiedMethodName(string?)` — нормализует FQN метода
- `NormalizeTypeName(string?)` — нормализует имя типа

Парсеры (`AltCoverMetricsParser`, `RoslynMetricsParser`) используют `SymbolNormalizer` для нормализации символов перед агрегацией.

Все edge cases покрыты unit-тестами в `tests/Rca.MetricsReporter.Tests/Processing/SymbolNormalizerTests.cs` с использованием реальных примеров из метрик проекта.

## Member Filtering

Metrics Reporter автоматически исключает методы конструктора и компилятора из отчетов, так как они не представляют интереса для анализа качества кода.

### Исключаемые методы

Фильтруются и не попадают в JSON/HTML отчеты: конструкторы (`.ctor`, `.cctor`) и методы компилятора (`MoveNext`, `SetStateMachine`, `MoveNextAsync`, `DisposeAsync`). Список захардкожен в классе `MemberFilter` для простоты и прозрачности.

### Механизм работы

Фильтрация происходит в `MetricsAggregationService.MergeMember()` после нормализации FQN. Конструкторы AltCover: `Namespace.Type..ctor(...)`. Конструкторы Roslyn: `Namespace.Type.Type(...)` (имя метода совпадает с типом). Методы компилятора определяются по имени. Исключенные методы не добавляются в структуру отчета и отсутствуют в JSON/HTML.

### Реализация

Фильтрация в `MemberFilter` (`src/Tools/Rca.MetricsReporter/Processing/MemberFilter.cs`): `ShouldExcludeMethod(string?)` — проверка по имени, `ShouldExcludeMethodByFqn(string?)` — проверка по FQN с поддержкой AltCover и Roslyn. Тесты: `MemberFilterTests.cs` и `MetricsAggregationServiceTests.cs`.

