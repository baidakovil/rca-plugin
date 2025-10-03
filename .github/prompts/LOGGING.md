## 1. КОНТЕКСТ
(Обновлено после миграции unified logging v1)

В addin-проекте две части: Loader (стабильный домен) и Runtime (горяче‑перезагружаемый через ALC). Требовалась система логирования, работающая:
- до загрузки Runtime (инициализация Loader)
- во время hot-reload Runtime
- без дублирования контрактов и потери типовой идентичности

## 2. ОБЩЕЕ ОПИСАНИЕ СИСТЕМЫ
Единый канал: Runtime формирует структурированные записи → отправляет строками JSON (JSONL) в Loader по выделенному `NamedPipe` (`RCA_LOG_PIPE`). Loader:
1. Принимает
2. Десериализует
3. Обогащает (enrichment)
4. Записывает в sinks (файл + Debug)
5. Использует внутренний логгер (LoaderInternalLogger) для собственных сообщений с теми же sinks

В Runtime есть fallback и emergency файлы при сбоях транспорта / сериализации.

## 3. ТЕХНОЛОГИИ
- .NET 8 / C# 12
- `Microsoft.Extensions.Logging.Abstractions` только на стороне Runtime / UI (минимум зависимостей)
- Named Pipes (`System.IO.Pipes`)
- `System.Text.Json` (camelCase, ignore null)
- Потокобезопасные счетчики (`Interlocked`)
- Локальные файлы: `%LOCALAPPDATA%/RCA/Logs`

## 4. АРХИТЕКТУРА (ФАКТИЧЕСКОЕ СОСТОЯНИЕ ПОСЛЕ МИГРАЦИИ)

### 4.1 Контракты (`Rca.Logging.Contracts`)
Record `LogEntryDto` + флаги (`LoggingFlags`), `LoggingSchema.Version`.
Основные поля (Runtime → Loader):
- SchemaVersion
- TimestampTicks (Local)
- Level (string)
- Category
- Message
- Exception (flattened string)
- RuntimeSessionId
- SequenceId (per runtime)
- RuntimeProcessId
- ALCInstanceId? (optional)
- IsFallback
- Flags (битовые: 1=SerializationFailed, 2=FallbackUsed)
- IsPing (служебный keepalive)

Enrichment (Loader‑only, в отдельной структуре):
- GlobalSequenceId
- ReceivedTimestamp
- LoaderProcessId

### 4.2 Runtime
Компоненты:
- `NamedPipeLoggerProvider` / `NamedPipeLogger` – интеграция с `ILogger`
- `PipeLogTransport` – подключение, backoff, пинг, fallback, emergency
- Backoff: 50 → 200 → 500 → 1000 → 2000 → 5000 ms (+/-20% jitter) с ресетом при успехе
- Fallback rotation: новый part после 50MB или смены даты
- Emergency файл: plain text строка при неудачной сериализации / ошибках fallback
- Keepalive Ping каждые 10s (`IsPing=true`, `Category="__ping"`) – Loader игнорирует

### 4.3 Loader
Компоненты:
- `LoggingPipeServerService` – постоянный цикл accept/read (один клиент). После disconnect – ждет новое подключение Runtime
- `LoaderLog` (статический фасад) + `LoaderInternalLogger` – внутренний логгер Loader; пишет преждевременно (до прихода Runtime) в файл и Debug через те же sinks
- Sinks: `FileLogSink`, `DebugSink`
- Порядок: чтение строки → Json deserialize → версия проверяется → фильтр Ping → enrichment → запись sinks

### 4.4 UI (Rca.UI)
Для устранения `Debug.WriteLine` добавлен легкий адаптер на основе именованного пайпа (использует те же контракты). Он формирует собственный SessionId (например `UI-<guid>`) и последовательность — это НЕ влияет на RuntimeSessionId (разделение источников видно в логе). UI сообщения появляются в том же файле, что и Runtime/Loader.

### 4.5 Отказоустойчивость
| Сбой | Обнаружение | Реакция | Потеря данных |
|------|-------------|---------|---------------|
| Сериализация (JsonException) | try/catch на сериализации | Emergency файл + флаг SerializationFailed | Конкретная запись (структура не уходит) |
| Pipe connect timeout / отказ | Исключение при Connect | Backoff + fallback запись | Нет (запись в fallback) |
| Pipe write IOException | Исключение при WriteLine | ForceDisconnect + fallback | Возможна 1 запись (partial) |
| Перегрузка runtime | Disconnect pipe | Loader ждет; Runtime переподключается | Нет, пока fallback работает |
| Fallback файл > 50MB | Перед записью проверка размера | Новый partN файл | Нет |
| Повреждение emergency | Последний барьер – игнорируем | Потеря только этой строки |

### 4.6 Решения по загрузке сборок
`Rca.Logging.Contracts` добавлен в `NonCollectibleAssemblies` → загружается в Default ALC. Это устраняет FileLoad и гарантирует единую идентичность типов DTO при hot-reload. В деплой каталог runtime копия DLL кладётся для резолюции на старте, но RuntimeLoadContext переиспользует уже загруженную версию.

### 4.7 LoaderInternalLogger / LoaderLog
`LoaderLog` обеспечивает:
- Ранний лог (до подключения Runtime)
- Единый формат записей LOADER|...
- Переиспользование тех же sinks без дополнительной конфигурации
Логгер реализован без `ILoggerFactory` для минимизации зависимостей и упрощения (прямая запись в sinks).

### 4.8 Удаление Debug.WriteLine
Заменено в Loader, Runtime, Service слоях. Остатки в UI мигрированы через адаптер (см. 4.4). Прямое использование `Debug.WriteLine` осталось только внутри `DebugSink` (осознанно – завершающий consumer).

## 5. ОТКЛОНЕНИЯ ОТ ИЗНАЧАЛЬНОГО ПЛАНА
| План | Фактическое | Причина |
|------|-------------|---------|
| Отдельный LogDispatcher + Loader внутренний лог через тот же dispatcher | Объединено через `LoaderLog` (минимальная прослойка) | Снижение связности, ранний лог до старта pipe сервера |
| Ping policy + disconnect по таймауту >30s (этап 2) | Таймаут отключен (только пинг) | Будет добавлено позднее – не критично для этапа 1 миграции |
| Интерфейсы sinks | Отложено | Простота и производительность |
| Binary protocol | Отложено | Достаточно JSON при малом объёме |

## 6. ПОТЕНЦИАЛЬНЫЕ УЛУЧШЕНИЯ (BACKLOG)
1. Watchdog времени последнего пакета (idle timeout → форсировать reconnect)
2. Политика retention (очистка старых логов по возрасту / размеру)
3. Structured scopes (сервер расширяет JSON) – сейчас scope не сериализуется
4. Управляемый уровень логирования (dynamic level switch через командный pipe)
5. Binary framing (length-prefixed) для снижения накладных расходов при burst
6. Сбор метрик: счетчик пропущенных, сериализационных ошибок, объём fallback
7. Пакетный flush для fallback (сейчас sync запись каждой строки)
8. UI sink для отображения последних N логов прямо в панеле

## 7. ИЗВЕСТНЫЕ ОГРАНИЧЕНИЯ
| Область | Ограничение | Риск | Митигация |
|---------|-------------|------|-----------|
| Нет retention | Неограниченный рост каталога | Переполнение диска | Periodic cleanup task (backlog #2) |
| Single-client pipe | Один Runtime в момент времени | Масштабирование | Именовать pipe с SessionId при multi-runtime |
| Нет scope сериализации | Потеря контекста запросов | Меньше семантики | Реализовать scope capture |
| Fallback JSON без компрессии | Большой размер при длительном офлайн'e | Диск | Добавить сжатие / max parts |
| Нет бинарного протокола | Накладные расходы JSON | Производительность | Backlog #5 |
| Простая backoff policy | Нет long-sleep / metrics | Увеличен шум | Расширить стратегию |

## 8. GUIDELINES ДЛЯ НОВОГО КОДА
- Никогда не бросать исключение наружу из `ILogger.Log`
- Любая новая подсистема логируется через `LoaderLog.GetLogger<T>()` (на стороне Loader) или через `NamedPipeLoggerProvider` (Runtime / UI)
- Не добавлять зависимости от `ILoggerFactory` без веской причины (ограничение hot-reload издержек)
- Для крупных операций логировать: start, success, fail (с opId)

## 9. ПРИМЕРЫ
### Runtime
```csharp
var provider = new NamedPipeLoggerProvider("RCA_LOG_PIPE", sessionId);
var log = provider.CreateLogger("MyFeature.Startup");
log.LogInformation("Runtime feature initialized {Version}", version);
```
### Loader
```csharp
private static readonly ILogger Log = LoaderLog.GetLogger<HotReloadService>();
Log.LogInformation("Reload request received path={Path}", path);
```
### UI
```csharp
private static readonly ILogger Log = UiLog.GetLogger<RcaDockablePanel>();
Log.LogDebug("Panel XAML loaded variant={Variant}");
```

## 10. РЕЗЮМЕ
Система достигает целей первой итерации: детерминированная доставка, отсутствие циклов зависимостей, устойчивость к hot-reload, fallback / emergency каналы, унификация источников. Дальнейшие улучшения сконцентрированы вокруг наблюдаемости, управления объёмом и динамической конфигурации.
