# Диагностика времени сборки в Visual Studio

Этот документ описывает различные способы диагностики времени сборки проекта с использованием специализированных средств Visual Studio и MSBuild.

## 1. MSBuild Binary Logs (.binlog) - Рекомендуемый метод

### Что это такое?

MSBuild Binary Logs — это самый мощный инструмент для диагностики сборки. Он записывает полную информацию о каждом target, задаче, времени выполнения и зависимостях в структурированный формат.

### Как включить?

#### Метод 1: Через командную строку (рекомендуется)

```powershell
# Полная сборка с binary log
dotnet build rca-plugin.sln -bl

# Или с указанием пути к логу
dotnet build rca-plugin.sln -bl:build.binlog

# Сборка через MSBuild напрямую
msbuild rca-plugin.sln -bl:build.binlog
```

#### Метод 2: Через Visual Studio

1. **Tools → Options → Projects and Solutions → Build and Run**
2. Включите **"MSBuild project build output verbosity"** → **"Diagnostic"**
3. Или используйте параметр командной строки при запуске из Visual Studio

#### Метод 3: Добавить в проект (автоматическое включение)

Можно добавить свойство в `Directory.Build.props` для автоматического создания binary log:

```xml
<PropertyGroup>
  <!-- Enable binary logs for Debug configuration -->
  <BinLog Condition="'$(Configuration)' == 'Debug' and '$(EnableBinLog)' == 'true'">true</BinLog>
  <BinLogPath Condition="'$(BinLog)' == 'true'">$(SolutionDir)build\Logs\build-$(Configuration)-$(Platform).binlog</BinLogPath>
</PropertyGroup>
```

### Как просмотреть?

1. **MSBuild Structured Log Viewer** (рекомендуется)
   - Скачайте с [GitHub](https://github.com/KirillOsenkov/MSBuildStructuredLog/releases)
   - **Для Windows 11 ARM (виртуализация на macOS ARM)**: скачайте `MSBuildStructuredLogSetup.exe`
   - **Альтернатива**: `Setup.msi` (MSI установщик)
   - После установки откройте `.binlog` файл
   - Увидите дерево targets, задач, времени выполнения
   - Можно искать по имени target, задачи, файла

2. **Visual Studio Code Extension**
   - Установите расширение "MSBuild Structured Log Viewer"
   - Откройте `.binlog` файл напрямую

3. **Онлайн просмотр**
   - Загрузите `.binlog` на [msbuildlog.com](https://msbuildlog.com/)

### Что искать в binary log?

- **Время выполнения targets**: Найдите `GenerateRcaAddinFile`, `EnsureRcaTimestamp`, `GenerateSolutionMetrics`
- **Порядок выполнения**: Проверьте, выполняются ли targets в правильном порядке
- **Повторные вызовы**: Найдите, не вызывается ли target несколько раз
- **Зависимости**: Проверьте, правильно ли настроены зависимости между targets

### Пример анализа

```
1. Откройте build.binlog в MSBuild Structured Log Viewer
2. Найдите "GenerateRcaAddinFile" в дереве
3. Посмотрите:
   - Сколько раз он выполняется
   - Когда он выполняется (до/после Metrics.exe)
   - Сколько времени занимает
   - Какие входные данные используются
```

---

## 2. Visual Studio Build Performance Summary

### Как включить?

1. **Build → Clean Solution**
2. **Build → Rebuild Solution**
3. После сборки откройте **View → Output**
4. Выберите **"Build"** в dropdown
5. Найдите секцию **"Build Performance Summary"**

### Что показывает?

- Время компиляции каждого проекта
- Время выполнения различных этапов (Compile, Copy, Deploy)
- Общее время сборки

### Пример вывода

```
Build Performance Summary:
  Rca.Loader: 2.5s
  Rca.Runtime: 3.2s
  Rca.MetricsReporter.Tests: 1.8s
Total: 7.5s
```

---

## 3. Diagnostic Build Output в Visual Studio

### Как включить?

1. **Tools → Options → Projects and Solutions → Build and Run**
2. Установите **"MSBuild project build output verbosity"** → **"Diagnostic"**
3. Пересоберите проект
4. Откройте **View → Output** → **"Build"**

### Что показывает?

- Подробные логи каждого target
- Время выполнения каждой задачи
- Все свойства MSBuild
- Все элементы ItemGroup

### Недостатки

- Очень много вывода (может быть трудно найти нужное)
- Медленнее обычной сборки
- Не структурировано (сложнее анализировать)

---

## 4. Performance Profiler для сборки

### Как использовать?

1. **Debug → Performance Profiler** (или Alt+F2)
2. Выберите **"Instrumentation"**
3. Выберите **"Launch executable"** → укажите `dotnet.exe` или `msbuild.exe`
4. Укажите аргументы: `build rca-plugin.sln`
5. Запустите профилирование

### Что показывает?

- CPU время каждого метода
- Время выполнения каждой задачи MSBuild
- Вызовы методов в коде сборки

### Когда использовать?

- Когда нужно найти медленные части в самом коде сборки
- Когда нужно оптимизировать кастомные MSBuild tasks
- Когда binary log недостаточно детален

---

## 5. Профилирование через dotnet CLI

### Встроенные инструменты

```powershell
# Включить timing для сборки
dotnet build rca-plugin.sln /v:detailed /t:Rebuild

# Или с метриками времени
$env:DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build rca-plugin.sln --verbosity detailed
```

---

## 6. Анализ конкретной проблемы: Rca.addin без timestamp

### Шаги для диагностики

1. **Включите binary log**:
   ```powershell
   dotnet build rca-plugin.sln -bl:diagnostic.binlog /p:RoslynMetricsEnabled=true
   ```

2. **Откройте в MSBuild Structured Log Viewer**

3. **Найдите следующие targets**:
   - `EnsureRcaTimestamp` - проверьте, когда выполняется и какой timestamp создается
   - `GenerateRcaAddinFile` - проверьте:
     - Сколько раз выполняется
     - Какое значение `RcaHotReloadTimestamp` используется
     - Читается ли файл timestamp
   - `GenerateSolutionMetrics` - проверьте:
     - Когда выполняется
     - Вызывает ли он rebuild проектов
     - Что происходит с `Rca.addin` после его выполнения

4. **Проверьте порядок выполнения**:
   - Должен быть: `EnsureRcaTimestamp` → `GenerateRcaAddinFile` → `GenerateSolutionMetrics`
   - Если порядок другой, это может быть проблемой

5. **Проверьте повторные вызовы**:
   - Найдите все вызовы `GenerateRcaAddinFile`
   - Проверьте, какой timestamp используется в каждом вызове
   - Если в одном из вызовов timestamp пустой, найдите причину

### Пример запроса в binary log viewer

```
1. Откройте "Search" в MSBuild Structured Log Viewer
2. Поиск: "GenerateRcaAddinFile"
3. Проверьте каждое вхождение:
   - Время выполнения
   - Значение RcaHotReloadTimestamp
   - Читается ли файл timestamp
   - Что записывается в Rca.addin
```

---

## 7. Автоматизация диагностики

### Создайте скрипт для быстрой диагностики

```powershell
# build-diagnostic.ps1
param(
    [switch]$EnableBinLog = $true,
    [switch]$RoslynMetrics = $true
)

$logDir = "build\Logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = "$logDir\build-diagnostic-$timestamp.binlog"

$properties = @()
if ($RoslynMetrics) {
    $properties += "/p:RoslynMetricsEnabled=true"
}

$buildArgs = @(
    "build",
    "rca-plugin.sln",
    "-bl:$logFile",
    "/v:detailed"
) + $properties

Write-Host "Building with diagnostics..." -ForegroundColor Cyan
Write-Host "Binary log: $logFile" -ForegroundColor Yellow

& dotnet $buildArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build completed. Open $logFile in MSBuild Structured Log Viewer" -ForegroundColor Green
} else {
    Write-Host "Build failed. Check $logFile for details" -ForegroundColor Red
}
```

### Использование

```powershell
.\build-diagnostic.ps1 -RoslynMetrics
```

---

## 8. Рекомендации

### Для повседневной работы

- Используйте **Build Performance Summary** в Visual Studio для быстрой проверки
- Включите **verbosity: Normal** для обычной сборки

### Для диагностики проблем

- Используйте **MSBuild Binary Logs** для детального анализа
- Анализируйте порядок выполнения targets
- Проверяйте значения свойств MSBuild в разных контекстах

### Для оптимизации

- Используйте **Performance Profiler** для поиска узких мест
- Анализируйте время выполнения каждого target
- Оптимизируйте медленные targets или задачи

---

## 9. Полезные ссылки

- [MSBuild Structured Log Viewer](https://github.com/KirillOsenkov/MSBuildStructuredLog)
- [MSBuild Binary Log Format](https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki)
- [Visual Studio Build Performance](https://learn.microsoft.com/en-us/visualstudio/ide/reference/options-projects-and-solutions-build-and-run)
- [MSBuild Command-Line Reference](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference)

---

## 10. Примеры использования

### Проверка времени выполнения GenerateRcaAddinFile

```powershell
# Создать binary log
dotnet build rca-plugin.sln -bl:rca-addin-diagnostic.binlog /p:RoslynMetricsEnabled=true

# Открыть в MSBuild Structured Log Viewer
# Найти "GenerateRcaAddinFile"
# Проверить:
# - Время выполнения
# - Значение RcaHotReloadTimestamp
# - Содержимое сгенерированного Rca.addin
```

### Проверка влияния Metrics.exe на сборку

```powershell
# Сборка без Metrics
dotnet build rca-plugin.sln -bl:build-without-metrics.binlog /p:RoslynMetricsEnabled=false

# Сборка с Metrics
dotnet build rca-plugin.sln -bl:build-with-metrics.binlog /p:RoslynMetricsEnabled=true

# Сравнить в MSBuild Structured Log Viewer:
# - Порядок выполнения targets
# - Количество вызовов GenerateRcaAddinFile
# - Значения свойств в разных контекстах
```

