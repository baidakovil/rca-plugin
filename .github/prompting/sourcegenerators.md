## Пересмотренное решение: Source Generators (актуально БЕЗ ILRepack)

Source Generator — **идеальное решение** для вашей ситуации без ILRepack, потому что:

### Почему это решает вашу проблему

**Проблема:** Loader-проекты с кастомными `Common.targets` игнорируют `<AssemblyAttribute>` через `GenerateAssemblyInfo`, поэтому атрибуты с хэшами не попадают в DLL[1][2].

**Решение Source Generator:**
- Генератор работает **внутри процесса компиляции Roslyn**, не зависит от MSBuild-таргетов
- Срабатывает **до CoreCompile** автоматически для ЛЮБОГО проекта (SDK, WPF, с кастомными targets)
- Генерирует `.g.cs` файл с атрибутами, который **гарантированно компилируется** в DLL

### Архитектура решения

#### 1. Создайте Source Generator

```csharp
// src/Tools/Rca.SourceGenerator/AssemblyMetadataGenerator.cs
using Microsoft.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

[Generator]
public class AssemblyMetadataGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Читаем список корневых папок из MSBuild-свойства
        var sourceRootsProvider = context.AnalyzerConfigOptionsProvider
            .Select((opts, _) =>
            {
                opts.GlobalOptions.TryGetValue("build_property.RcaSourceRoots", out var roots);
                return roots ?? "";
            });

        // Инкрементально вычисляем хэш
        var hashProvider = sourceRootsProvider
            .Select((roots, ct) =>
            {
                if (string.IsNullOrEmpty(roots)) return "NO_HASH";
                
                var paths = roots.Split(';', StringSplitOptions.RemoveEmptyEntries);
                return ComputeSourceHash(paths, ct);
            });

        // Генерируем код
        context.RegisterSourceOutput(hashProvider, (spc, hash) =>
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            
            var code = $$"""
                using System.Reflection;

                [assembly: AssemblyMetadata("SourceHash", "{{hash}}")]
                [assembly: AssemblyMetadata("DeployFolder", "{{timestamp}}")]
                [assembly: AssemblyInformationalVersion("Hash: {{hash}}, Folder: {{timestamp}}")]
                """;
            
            spc.AddSource("AssemblyMetadata.g.cs", code);
        });
    }

    private static string ComputeSourceHash(string[] roots, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        var files = new List<string>();
        
        foreach (var root in roots)
        {
            if (Directory.Exists(root))
            {
                files.AddRange(Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\")));
            }
        }
        
        files.Sort();
        
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var content = File.ReadAllText(file).Replace("\r\n", "\n");
            var bytes = Encoding.UTF8.GetBytes(content);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash).Substring(0, 12).ToLower();
    }
}
```

#### 2. Подключите к проектам

```xml
<!-- Rca.Loader.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\Tools\Rca.SourceGenerator\Rca.SourceGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
  <CompilerVisibleProperty Include="RcaSourceRoots" />
</ItemGroup>

<PropertyGroup>
  <RcaSourceRoots>$(MSBuildProjectDirectory);$(MSBuildProjectDirectory)\..\Rca.Loader.Contracts</RcaSourceRoots>
</PropertyGroup>
```

```xml
<!-- Rca.Runtime.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\Tools\Rca.SourceGenerator\Rca.SourceGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
  <CompilerVisibleProperty Include="RcaSourceRoots" />
</ItemGroup>

<PropertyGroup>
  <RcaSourceRoots>$(MSBuildProjectDirectory);$(MSBuildProjectDirectory)\..\Rca.Core;$(MSBuildProjectDirectory)\..\Rca.UI</RcaSourceRoots>
</PropertyGroup>
```

### Что это даёт

| Аспект | Текущая схема (временные файлы + .g.cs через MSBuild) | Source Generator |
|--------|-------------------------------------------------------|------------------|
| **Надёжность для Loader** | Работает, но требует обходных путей через `BeforeTargets="CoreCompile"`[1] | Работает автоматически, Roslyn гарантирует компиляцию |
| **Код в MSBuild** | ~50 строк кастомных таргетов на проект[2] | ~10 строк конфигурации PropertyGroup |
| **Временные файлы** | Нужны `.txt` как промежуточный канал между таргетами[2] | Не нужны, хэш вычисляется в памяти |
| **Отладка** | Проверка через `ildasm` или файловую систему[2] | Сгенерированный `.g.cs` виден в IDE (Dependencies > Analyzers) |
| **Инкрементальность** | Ручная через сравнение `Ticks`[1] | Встроенная в Roslyn, автоматическая |
| **Переносимость** | Зависит от структуры MSBuild-таргетов проекта[1] | Работает одинаково везде (даже в dotnet watch, Rider) |

### Почему это лучше для вашего случая БЕЗ ILRepack

**Без ILRepack у вас теперь:**
- Каждый проект (Rca.Loader, Rca.Loader.Contracts, Rca.Runtime, Rca.Core и т.д.) деплоится как отдельная DLL[1][2]
- Каждой DLL нужен свой хэш для hot-reload detection[2]

**Source Generator позволяет:**
- Подключить генератор к **любому** проекту через `<ProjectReference OutputItemType="Analyzer">`
- Настроить `RcaSourceRoots` индивидуально для каждого проекта (Loader включает себя + Contracts, Runtime включает все merged-проекты)[2]
- Получить атрибуты **в каждой DLL** автоматически, без дублирования MSBuild-логики

### Оценка решения

**Преимущества:**

**Простота:**
- Убираете все MSBuild-таргеты типа `GenerateLoaderSourceHash`, `BuildAttributeInjector`, `InjectLoaderAttributes`[1]
- Не нужен `AttributeInjector.exe` (800+ строк кода на Mono.Cecil)[1]
- Не нужны промежуточные `source-hash-loader.txt`, `source-hash-runtime.txt`[2]

**Надёжность:**
- Source Generator работает **одинаково** для SDK-проектов (Runtime) и проектов с `Common.targets` (Loader)[1]
- Нет зависимости от порядка выполнения MSBuild-таргетов
- Roslyn гарантирует, что `.g.cs` попадает в компиляцию

**Производительность:**
- Инкрементальные генераторы кэшируются между сборками
- Нет накладных расходов на запуск отдельных утилит через `<Exec>`[1]

**Отладка:**
- Сгенерированный код виден в IDE
- Можно дебажить Source Generator с breakpoints

**Недостатки:**

**Начальная сложность:**
- Нужно написать сам генератор (~150 строк кода)
- Требуется понимание API Source Generators

**Ограничения:**
- Timestamp должен передаваться через MSBuild-property, а не вычисляться в генераторе (детерминизм)
- Нельзя читать файлы произвольно — только через `AdditionalFiles` или свойства

**Решение ограничений:**

Для timestamp используйте:
```xml
<PropertyGroup>
  <RcaDeployTimestamp>$([System.DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss'))</RcaDeployTimestamp>
  <CompilerVisibleProperty Include="RcaDeployTimestamp" />
</PropertyGroup>
```

В генераторе:
```csharp
opts.GlobalOptions.TryGetValue("build_property.RcaDeployTimestamp", out var timestamp);
```

## Итоговая рекомендация

**Переходите на Source Generators** — это стандартный, поддерживаемый Microsoft способ кодогенерации, который:
- Решает проблему с Loader/Common.targets автоматически[1]
- Упрощает билд-систему на ~70% (убираете таргеты и утилиты)[1][2]
- Работает **одинаково** для всех проектов без ILRepack
- Готов к будущему (Microsoft активно развивает Source Generators для .NET)

**Альтернатива:** Если не хотите писать генератор, можно оставить текущую схему с `.g.cs`, но:
- Упростите логику — генерируйте файл через простую утилиту, а не через сложные MSBuild-таргеты
- Используйте `<Target BeforeTargets="CoreCompile" DependsOnTargets="GenerateHash">` вместо сложных цепочек[1]

Но Source Generator всё равно **надёжнее**, потому что не зависит от кастомных targets.
