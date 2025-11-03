# Полный гайд: Управление SARIF выводом из Roslyn в .NET 8

## Что выводится в SARIF по умолчанию?

**SARIF содержит результаты всех включённых Roslyn анализаторов:**

### 1. **Code Quality Rules** (CAxxxx)[259][260][262]

Примеры из стандартного набора:
- **CA1000**: Do not declare static members on generic types
- **CA1001**: Types that own disposable fields should be disposable
- **CA1051**: Do not declare visible instance fields
- **CA1502**: Method is too complex (Cyclomatic complexity)
- **CA1505**: Avoid unmaintainable code (Maintainability index)
- **CA1506**: Avoid excessive class coupling (Coupling)
- И ещё ~200+ правил для Design, Naming, Usage, Reliability, Globalization и т.д.

### 2. **Code Style Rules** (IDExxxx)[117][251][260]

- **IDE0001-IDE0999**: Все правила стиля кода (naming, spacing, indentation и т.д.)
- По умолчанию многие **скрыты** (severity=silent) или отключены

### 3. **Security Rules** (подмножество CA)[251]

- **CA2000**: Dispose objects before losing scope
- **CA3075**: Unsafe XML parsing
- И другие security-related правила

### 4. **Performance Rules** (подмножество CA)[251]

- Рекомендации по производительности

---

## Как управлять: Конфигурация в .editorconfig

### Базовая структура

```editorconfig
[*.cs]
# Включить все анализаторы на build
dotnet_analyzer_diagnostic.severity = warning

# Или на file-level
dotnet_diagnostic.CA1502.severity = warning
dotnet_diagnostic.IDE0055.severity = suggestion
```

### Категории правил[263][265]

Можно настраивать целые категории одновременно:

```editorconfig
# Design rules
dotnet_code_quality.Design.api_surface = public

# Naming rules
dotnet_code_quality.Naming.api_surface = internal_or_private

# Maintainability rules
dotnet_code_quality.Maintainability.api_surface = public

# Performance rules
dotnet_code_quality.Performance.api_surface = public

# Security rules
dotnet_code_quality.Security.api_surface = all
```

---

## Основные метрики кода (не Security/Performance)

### 1. **Cyclomatic Complexity** (CA1502)[17][253]

**Что это:** Количество независимых путей выполнения в коде.

**Формула:** C = E - N + 2P, где
- E = рёбра в графе потока
- N = узлы
- P = компоненты связности

**Рекомендуемый порог:** 10 (по NIST)

**Конфигурация в .editorconfig:**

```editorconfig
# Threshold для циклической сложности
dotnet_code_quality.ca1502.threshold = 10

# Severity
dotnet_diagnostic.CA1502.severity = warning
```

**Что выдаёт в SARIF:**

```json
{
  "ruleId": "CA1502",
  "message": {
    "text": "Method has cyclomatic complexity of 12, which exceeds the threshold of 10"
  },
  "level": "warning"
}
```

### 2. **Maintainability Index** (CA1505)[253][255]

**Что это:** Комбинированная метрика (0-100), показывающая насколько легко поддерживать код.

**Формула:**
```
MI = MAX(0, (171 - 5.2*ln(Halstead) - 0.23*CC - 16.2*ln(LOC)) * 100 / 171)

Где:
- Halstead Volume = мера размера программы
- CC = Cyclomatic Complexity
- LOC = Lines of Code
```

**Диапазоны:**
- 0-9: Red (low maintainability)
- 10-19: Yellow (moderate)
- 20-100: Green (good)

**Конфигурация:**

```editorconfig
# Maintainability Index threshold
dotnet_code_quality.ca1505.threshold = 20

# Severity
dotnet_diagnostic.CA1505.severity = warning
```

**Что выдаёт в SARIF:**

```json
{
  "ruleId": "CA1505",
  "message": {
    "text": "Method has a maintainability index of 15, which is below the target of 20"
  },
  "level": "warning"
}
```

### 3. **Lines of Code** (CA1506 - Class coupling, LOC related)[17]

**Что это:** Количество строк кода в методе/классе. Большие методы сложнее поддерживать.

**Встроенное правило:** CA1506 также смотрит на Lines of Code как часть анализа сложности.

**Конфигурация:**

```editorconfig
# Максимальное количество строк в методе
# Через параметры правила CA1506
dotnet_code_quality.ca1506.excluded_symbol_names = Main

# Severity
dotnet_diagnostic.CA1506.severity = warning
```

### 4. **Class Coupling** (CA1506)[255]

**Что это:** Количество классов, от которых зависит данный класс.

**Рекомендуемое значение:** < 10 зависимостей

**Конфигурация:**

```editorconfig
dotnet_code_quality.ca1506.threshold = 10
dotnet_diagnostic.CA1506.severity = warning
```

### 5. **Code Duplication** (нет встроенного правила CA, используй StyleCop/Roslynator)

**Встроенный Roslyn:** НЕ имеет встроенного правила для дубликатов.

**Решение:** Установить дополнительный анализатор:

```bash
dotnet add package StyleCop.Analyzers
# или
dotnet add package Roslynator.Analyzers
```

Затем в .editorconfig:

```editorconfig
# StyleCop - нет явного правила, но анализирует структуру
dotnet_diagnostic.SA1516.severity = warning  # Blank lines between elements

# Roslynator - может проверять дубликаты
dotnet_diagnostic.RCS1140.severity = warning  # Hide unused member
```

---

## Полная конфигурация .editorconfig для максимальных метрик

### Рекомендуемый набор

```editorconfig
[*.cs]

# ============ АНАЛИЗАТОРЫ НА BUILD ============
dotnet_analyzer_diagnostic.severity = warning
EnforceCodeStyleInBuild = true

# ============ CYCLOMATIC COMPLEXITY ============
dotnet_diagnostic.CA1502.severity = warning
dotnet_code_quality.ca1502.threshold = 10

# ============ MAINTAINABILITY INDEX ============
dotnet_diagnostic.CA1505.severity = warning
dotnet_code_quality.ca1505.threshold = 20

# ============ CLASS COUPLING ============
dotnet_diagnostic.CA1506.severity = warning
dotnet_code_quality.ca1506.threshold = 10

# ============ DESIGN RULES ============
dotnet_diagnostic.CA1000.severity = warning  # Static members on generic types
dotnet_diagnostic.CA1001.severity = warning  # Disposable fields
dotnet_diagnostic.CA1051.severity = warning  # Visible instance fields

# ============ NAMING RULES ============
dotnet_diagnostic.CA1707.severity = warning  # Identifiers should not contain underscores
dotnet_diagnostic.CA1711.severity = warning  # Identifiers should not have incorrect suffix
dotnet_diagnostic.CA1715.severity = warning  # Identifiers should have correct prefix

# ============ CODE STYLE (IDE) - ТОЛЬКО НА BUILD ============
dotnet_diagnostic.IDE0055.severity = silent  # Formatting (не нужно на build)
dotnet_diagnostic.IDE0044.severity = warning # Make field readonly

# ============ КАТЕГОРИИ ============
# Можно выключить целые категории
# dotnet_analyzer_diagnostic.category-Security.severity = error
# dotnet_analyzer_diagnostic.category-Performance.severity = warning

# ============ ИСКЛЮЧЕНИЯ ============
# Исключить определённые символы
dotnet_code_quality.ca1505.excluded_symbol_names = Main|Program
```

---

## SARIF Output: Что ты получишь

### Структура результатов

```json
{
  "version": "2.1.0",
  "runs": [
    {
      "tool": {
        "driver": {
          "name": "Microsoft.CodeAnalysis.CSharp",
          "rules": [
            {
              "id": "CA1502",
              "shortDescription": {
                "text": "Avoid excessive complexity"
              },
              "properties": {
                "category": "Maintainability",
                "subcategory": "Complexity"
              }
            },
            {
              "id": "CA1505",
              "shortDescription": {
                "text": "Avoid unmaintainable code"
              },
              "properties": {
                "category": "Maintainability"
              }
            }
          ]
        }
      },
      "results": [
        {
          "ruleId": "CA1502",
          "level": "warning",
          "message": {
            "text": "Method 'ProcessData' has a cyclomatic complexity of 12, which exceeds the configured threshold of 10"
          },
          "locations": [
            {
              "physicalLocation": {
                "artifactLocation": {
                  "uri": "file:///C:/Project/DataProcessor.cs"
                },
                "region": {
                  "startLine": 42,
                  "endLine": 127
                }
              }
            }
          ],
          "properties": {
            "cyclomatic_complexity": "12",
            "maintainability_index": "15"
          }
        },
        {
          "ruleId": "CA1505",
          "level": "warning",
          "message": {
            "text": "Method 'ProcessData' has a maintainability index of 15, which is below the target of 20"
          },
          "locations": [...]
        }
      ]
    }
  ]
}
```

---

## Способы проверки метрик

### 1. **Visual Studio SARIF Viewer**
- Откроет файл .sarif и покажет все results
- Double-click на результат → перейдёт в код

### 2. **Microsoft SARIF Web Component**
- Загрузить SARIF на https://microsoft.github.io/sarif-web-component/
- Увидеть интерактивное дерево с фильтрацией

### 3. **Парсить JSON программно**

```csharp
var json = File.ReadAllText("metrics.sarif");
var doc = JsonDocument.Parse(json);
var results = doc.RootElement
    .GetProperty("runs")[0]
    .GetProperty("results")
    .EnumerateArray();

foreach (var result in results)
{
    var ruleId = result.GetProperty("ruleId").GetString();
    var message = result.GetProperty("message").GetProperty("text").GetString();
    var level = result.GetProperty("level").GetString();
    
    Console.WriteLine($"{ruleId} [{level}]: {message}");
}
```

### 4. **Prometheus + Grafana**
- Парсить SARIF в JSON метрики
- Отправить в Prometheus
- Визуализировать в Grafana

---

## Пример: Полная MSBuild конфигурация

### Directory.Build.props

```xml
<PropertyGroup>
  <!-- SARIF Output -->
  <ErrorLog>$(ProjectDir)metrics/sarif/$(MSBuildProjectName).sarif%2cversion=2.1</ErrorLog>
  
  <!-- Enforce code style in build -->
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  
  <!-- Analysis Level - latest rules -->
  <AnalysisLevel>latest</AnalysisLevel>
</PropertyGroup>
```

### .editorconfig

```editorconfig
[*.cs]
# Максимум метрик
dotnet_analyzer_diagnostic.severity = warning

dotnet_diagnostic.CA1502.severity = warning
dotnet_code_quality.ca1502.threshold = 10

dotnet_diagnostic.CA1505.severity = warning
dotnet_code_quality.ca1505.threshold = 20

dotnet_diagnostic.CA1506.severity = warning
dotnet_code_quality.ca1506.threshold = 10
```

---

## Что выключить/включить: Рекомендации

### Рекомендуется ВКЛЮЧИТЬ (для приборной панели)
- CA1502 (Cyclomatic Complexity)
- CA1505 (Maintainability Index)
- CA1506 (Class Coupling + LOC)
- CA1009 (Naming - interfaces should begin with I)
- CA1051 (Avoid visible instance fields)

### Можно ОТКЛЮЧИТЬ (noise)
- IDE0055 (Formatting) - не влияет на функциональность
- IDE0058 (Unused expression) - слишком назойливо
- CA1707 (Underscores in names) - sometimes necessary

### Зависит от проекта
- Security rules (CA2000, CA3075) - для приватного кода можно мягче
- Performance rules - для UI code критичны, для backend optional

---

## Итог

**SARIF по умолчанию включает:**
- ~200 CA rules (Quality, Design, Naming, Usage)
- ~1000 IDE rules (Style) - но большинство скрыты

**Ты управляешь этим через:**
- `.editorconfig` файл (severity и thresholds)
- MSBuild properties (EnforceCodeStyleInBuild, AnalysisLevel)
- Project file (.csproj) для специфичных проектов

**Для приборной панели нужны эти метрики:**
- CA1502 - Cyclomatic Complexity
- CA1505 - Maintainability Index
- CA1506 - Class Coupling & LOC
- Плюс любые дополнительные CA правила, которые тебе интересны