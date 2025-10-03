## 1. КОНТЕКСТ
В моем addin-проекте на .net 8 есть две ключевые части:  Loader и Runtime. Такое разделение позволяет производить разработку, не перезагружая Revit целиком, что долго и неудобно. Система работает.

Loader состоит из двух проектов: Rca.Loader и Rca.Loader.Contracts. 

Runtime состоит из нескольких проектов, включая Rca.Contracts, Rca.Core и Rca.UI, которые собираются в один dll через ILRepack и используя ALC  загружаются и перезагружаются во время Runtime с помощью Loader.

Важно, что Rca.Contracts не зависит от Rca.Loader, а Rca.Loader не зависит от Rca.Contracts. Таким образом, я могу во время разработка вносить изменения в конракты и на лету перезагружать их в Runtime.

Теперь мне нужно сделать систему логгирования для нужд отладки, и для пользователя тоже. Система логгирования обязательно должна работать еще на этапе до загрузки Rca.Runtime, т.к. многие ключевые события происходят при загрузке плагина и при загрузке/перезагрузке Runtime. То есть, мне нужна сложная сущность, которая используется одновременно Loader'ом и Runtime'ом.

## 2. ОБЩЕЕ ОПИСАНИЕ СИСТЕМЫ
Требуется реализовать унифицированную, безопасную для горячей перегрузки (ALC-safe) систему логгирования с разделением обязанностей:
- Runtime отвечает за формирование структурированных логов и их потоковую отправку в Loader по выделенному устойчивому каналу.
- Loader выступает как единый приемник, обогащает лог-записи (enrichment), распределяет их в подключаемые приемники (sinks) и управляет политиками хранения.
- Выделенный постоянный Named Pipe канал для логов отделен от канала управляющих команд (разные pipe name). Высокая живучесть: падение / перезагрузка Runtime не нарушает работу основной системы — Runtime переподключается.
- Основные цели: детерминированность, отказоустойчивость, простота расширения, контролируемое потребление памяти, отсутствие циклических зависимостей.

## 3. ПРИМЕНЯЕМЫЕ ТЕХНОЛОГИИ
- .NET 8 / C# 12
- `Microsoft.Extensions.Logging.Abstractions` (Runtime)
- Named Pipes (`System.IO.Pipes`) — отдельный лог-пайп `RCA_LOG_PIPE`
- `System.Text.Json` с настроенными опциями
- Потокобезопасные счетчики через `Interlocked`
- При необходимости (этап 2) `ArrayPool<byte>` и примитивный пул объектов
- `StreamWriter` для файлового лога
- Минимум внешних библиотек
- Unit-тесты — в последующих этапах, НЕ в первой итерации

## 4. АРХИТЕКТУРА РЕШЕНИЯ

### 4.1 Логические Компоненты
1. `Rca.Logging.Contracts`
   - DTO: `LogEntryDto`
     - Поля: SchemaVersion, TimestampTicks (Local time), Level, Category, Message, Exception, Scope (Dictionary<string, object?>?), RuntimeSessionId, SequenceId, RuntimeProcessId, ALCInstanceId?, IsFallback, Flags (битовые признаки: 1=SerializationFailed, 2=FallbackUsed)
   - (Оставляем только необходимые для первой версии типы: DTO + возможно enum уровня; лишних интерфейсов не вводить.)

2. Runtime:
   - `NamedPipeLoggerProvider` (реализует `ILoggerProvider`)
   - `NamedPipeLogger` (реализует `ILogger`)
   - Генерация SequenceId: статический `long` с `Interlocked.Increment`
   - Отправка: немедленная сериализация и запись строки JSON в pipe (одна запись — одна строка)
   - Нет очереди и нет flush worker
   - При ошибке транспорта → переключение в fallback-режим (запись в локальный файл), периодические попытки переподключения при следующих лог-вызовах
   - Fallback файл: `%LOCALAPPDATA%\RCA\Logs\runtime-fallback-YYYYMMDD.log`
3. Loader:
   - `LoggingPipeServerService` — persistent соединение (один клиент в типичном сценарии). При разрыве ждёт повторного подключения.
   - Построчное чтение (JSONL). Каждая строка → попытка десериализации → обогащение → запись.
   - Enrichment:
     - LoaderProcessId
     - GlobalSequenceId (`Interlocked.Increment` внутри Loader)
     - ReceivedTimestamp
   - Выводы (встроенные, без интерфейсов sink'ов):
     - FileLogWriter (один файл на запуск Revit; каталог `%LOCALAPPDATA%\RCA\Logs\`, формат имени `rca-logs-<timestamp>.log`)
     - DebugWriter (`System.Diagnostics.Debug.WriteLine`)
   - Возможность позже добавить новый вывод будет оформлена через рефакторинг (не заранее).
4. Sinks:
   - `FileSink` (один файл на каждый запуск Revit. файлы записываются в По умолчанию FileSink должен писать в папку `%LOCALAPPDATA%\RCA\Logs\`)
   - `DebugSink` (`System.Diagnostics.Debug.WriteLine`)
   - Расширяемость без правок в `LogDispatcher`
5. Мониторинг / Keepalive:
   - Runtime отправляет каждые N секунд служебную "PING" запись (SchemaVersion та же, Level="Trace", Category="__ping") либо специальный флаг.
   - Loader отслеживает таймаут и закрывает соединение при отсутствии активности > 30s.
   - Runtime при ошибке записи → помечает состояние как “Disconnected” → вступает политика backoff
6. Политика Повторных Попыток / Backoff:
   - Экспоненциальный рост: 50ms → 200ms → 500ms → 1s → 2s → 5s (cap)
   - Jitter: +/- 20% случайное отклонение
   - Reset backoff при первой успешной передаче батча
   - Максимум попыток подряд безуспешных: например 300 (≈ несколько минут) → переход в “LongSleepMode” (например 30s пауза) пока пользователь не возобновит работу
7. Flags:
   - `SerializationFailed` (1)
   - `FallbackUsed` (2) — проставляется в Runtime при записи в fallback
8. Удаление устаревшей системы:
   - Старый `DebugLogService` полностью заменяется новой системой
   - Все вызовы прямого вывода переводятся на `ILogger`
   - Этап миграции через адаптер — исключен (не использовать)
9. Сериализация:
  - Каждый `LogEntryDto` → одна строка JSON UTF-8
   - Ошибка сериализации → emergency файл `%LOCALAPPDATA%\RCA\Logs\runtime-emergency-YYYYMMDD.log` + флаг SerializationFailed (запись НЕ отправляется)

10. Производительность:
    - Текущий объем логов невелик → отказ от очередей и батчей.
    - На этапе 2 можно добавить интернирование категорий и аренду буферов.
11. Версионирование:
- `SchemaVersion = "1"`
   - Несовпадение схемы → Loader игнорирует и пишет строку в `incompatible-schema.log` (реализовать на этапе 2)
12. Безопасность / Стойкость:
    - Отказ при неконтролируемом росте файла fallback (> фиксированного размера, напр. 50MB) — начать новый файл с суффиксом `_partN`
    - Защита от “бурста”: ограничить одну операцию flush не более чем X ms (предотвращение голодания фонового потока)

### 4.2 Потоки и Жизненный Цикл
- Runtime: вызов `Logger.Log` → формирование DTO → сериализация → (попытка отправки) или fallback.
- Loader: один поток чтения из pipe → синхронная обработка/запись.
- Unload ALC: Dispose провайдера закрывает соединение (best effort).

### 4.3 Надежность и Ошибки
- Сериализация: JsonException → emergency файл + инкремент счетчика → запись пропущена.
- Транспорт: IOException/ObjectDisposedException → переключение в fallback, при следующей записи пробуем переподключение.
- Никогда не бросаем исключения наружу из `Logger.Log`.
- Успешная отправка сбрасывает параметры backoff.

### 4.4 Расширяемость (минималистичная)
- Позже можно выделить интерфейсы для sink'ов и сериализации — сейчас отказаться для простоты.
- Потенциальный шаг: добавить бинарный протокол без изменения внешнего API `ILogger`.

### 4.5 Принципы SOLID (адаптация к упрощению)
- SRP соблюдён: провайдер отвечает только за интеграцию с `ILogger`, transport — в одном небольшом классе.
- OCP: Потенциальное расширение через последующее выделение sink-интерфейсов (отложено).
- DIP: На данном этапе сознательно упрощено (минимум абстракций). Позже возможно внедрение.
- Исключённые интерфейсы (`ILogSink`, `ILogSequenceProvider`) признаны преждевременными.

### 4.6 Формат LogEntryDto (упрощённый)
- string SchemaVersion
- long TimestampTicks
- string Level
- string Category
- string Message
- string? Exception
- Dictionary<string, object?>? Scope (примитивы: string,bool,long,double)
- string RuntimeSessionId
- long SequenceId
- int RuntimeProcessId
- int? ALCInstanceId
- bool IsFallback
- int Flags (битовые признаки: 1=SerializationFailed, 2=FallbackUsed)

(Enriched Loader-only: GlobalSequenceId, ReceivedTimestamp, LoaderProcessId — не входят в DTO Runtime.)

## 5. ЗАВИСИМОСТИ ПРОЕКТОВ (ДО / ПОСЛЕ)

### 5.1 ДО
- Нет проекта логгирования
- Присутствует устаревший `DebugLogService` (локальный Singleton)

### 5.2 ПОСЛЕ
- Новый проект: `Rca.Logging.Contracts` (никаких зависимостей на Loader / Runtime / Core)
- Runtime → `Rca.Logging.Contracts` + `Microsoft.Extensions.Logging.Abstractions`
- Loader → `Rca.Logging.Contracts`
- Полное удаление прямого использования `DebugLogService` (в конце этапа 2)
- Запрещено добавлять `Rca.Logging.Contracts` в `Rca.Contracts`


---

## ТРЕБОВАНИЯ К ОБРАБОТКЕ ОШИБОК (УТОЧНЕНИЕ)
1. Сериализация:
   - Любой `JsonException` → emergency файл + инкремент счетчика + флаг SerializationFailed
   - Запись не попадает в основной pipe
2. Транспорт:
   - IOException / ObjectDisposedException → закрыть текущее соединение, перейти в fallback, инициировать backoff
3. Очередь:
   - При purge логировать событие (одна служебная запись в emergency)
4. Ограничения на silent swallow: Ничто не исчезает без следа — либо файл emergency, либо счетчик

## ПОЛИТИКА BACKOFF (ДЕТАЛИ)
- Начальная задержка: 50ms
- Рост: 50 → 200 → 500 → 1000 → 2000 → 5000 (cap)
- Сброс после успешной отправки
- Jitter и long-sleep — на этапе 2
- Keepalive: ping каждые 10s (этап 2)

## ПРОИЗВОДИТЕЛЬНОСТЬ
- Нет очереди → минимальные накладные расходы
- Логи предполагаются низкообъёмными

## ИНСТРУКЦИИ ДЛЯ ГЕНЕРАЦИИ КОДА
Не вводить интерфейсы sink'ов и провайдеров последовательностей — упростить
Подготовить код к возможному расширению (методами разбиения, но без преждевременных абстракций)
Не добавлять лишних зависимостей; не усложнять архитектуру.

Генерация кода должна быть разбита на этапы. После каждого этапа проверяй, что билд проходит с помощью команды `dotnet build`.

ЭТАП 1:
- Создать проект `Rca.Logging.Contracts` (TargetFramework net8.0-windows, Nullable enable)
- Добавить `LogEntryDto` + константы (уровни, флаги)

ЭТАП 2:
Runtime: `NamedPipeLoggerProvider` + `NamedPipeLogger`:
   - Генерация последовательного `SequenceId` через `Interlocked.Increment`
   - Немедленная сериализация и запись в pipe; при недоступности → fallback

ЭТАП 3:
Реализовать подключение pipe с ленивым установлением и backoff при ошибках

ЭТАП 4:
- Loader: `LoggingPipeServerService` + чтение построчно + обогащение + запись в файл и Debug
- Реализовать FileLogWriter (один файл на запуск) + DebugWriter
- Добавить fallback файл в Runtime
- Emergency лог для ошибок сериализации

ЭТАП 5:
- Добавить jitter в backoff
- Добавить keepalive ping. Keepalive не должна быть в пользовательских логах
- Emergency лог при ошибках сериализации
- Enrichment в Loader (GlobalSequenceId, ReceivedTimestamp). Использовать Local Time в Timestamp
- Ограничение размера файлов
- Дополнительные флаги и совместимость схем
- Удалить вызовы `DebugLogService` (этап 2)

Теперь приступай к генерации кода. Начни с этапа 1. Жду готовый логгер!
