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

### Дополнительно
- Baseline хранится вручную, обновляется копированием свежего `metrics-report.json` поверх `metrics-baseline.json`.
- Приложение логирует шаги в `$(MetricsDir)\Report\metrics-reporter.log` и возвращает коды: 0 — OK, 1 — parsing error, 2 — IO error, 3 — validation error.
- Пороговые значения задаются в `build/Props/code-metrics.props` (свойство `MetricsThresholds`) и передаются агрегатору.

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

