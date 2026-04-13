# Pivot to MCP

## 1. Цель Pivot

Перевести проект из модели `custom AI assistant with Python/C# execution inside Revit` в модель `MCP-first platform extension over Revit Assistant / MCP tools`, максимально сохранив уже написанное ядро:

- hot reload runtime без перезапуска Revit;
- тестирование внутри Revit через named pipes;
- loader/runtime split;
- transport, logging и orchestration слой.

Итоговая идея проекта:

- проект не конкурирует с официальными MCP tools на уровне базового доступа к модели;
- проект становится надстройкой над MCP для кастомных workflow, orchestration, approval UX, корпоративных правил и dev productivity;
- chat UI остается обязательной частью POC, но не как Python shell, а как MCP-oriented task/approval console.

## 2. Продуктовая гипотеза

Если в Revit уже есть официальный MCP tool layer, то низкоуровневый доступ к модели не нужно поддерживать самостоятельно. Ценность проекта смещается в следующие зоны:

- orchestration поверх MCP tools;
- кастомные корпоративные workflow;
- безопасное применение изменений с preview/approval;
- быстрая разработка и отладка agent-driven сценариев;
- test harness для сценариев внутри Revit;
- hot reload для быстрой итерации runtime-логики.

Проект должен стать:

- не `еще одним AI-чатом для Revit`;
- а `developer and workflow platform over official MCP`.

## 3. Архитектурная модель: Host vs Modules

### 3.1. Host

`Host` — это постоянный слой, который живет с момента запуска Revit до его закрытия и не должен зависеть от прикладной логики конкретного workflow.

Host отвечает за:

- startup и shutdown add-in;
- ribbon registration;
- dockable pane registration;
- pipe/logging servers;
- runtime loading/unloading;
- version/hash/status tracking;
- contracts bridge между постоянным кодом и runtime.

Текущие кандидаты в host:

- `Rca.Loader`
- `Rca.Loader.Contracts`
- `Rca.Logging.Contracts`
- часть build/runtime tooling

### 3.2. Modules

`Module` — это capability-слой, подключаемый к host по контракту. Модуль не обязан быть hot-reloadable. Модульность нужна для отделения ответственности, а не только для выгрузки.

В рамках этого pivot используются три типа модулей.

#### A. Reloadable modules

Модули, которые живут в runtime и могут перегружаться вместе с runtime.

Примеры:

- MCP orchestration logic;
- workflow executors;
- custom tool composition;
- часть non-startup UI content внутри уже зарегистрированной панели;
- domain-specific business logic.

#### B. Non-reloadable modules

Модули, которые архитектурно являются отдельными capability, но живут в host и не должны перегружаться отдельно.

Примеры:

- chat UI shell для POC;
- pane registration layer;
- ribbon shell;
- approval surface, если он жестко привязан к startup API Revit.

#### C. Sticky modules

Модули, которые теоретически могут считаться optional capability, но технически плохо выгружаются или требуют special lifetime management.

Примеры:

- Python engine, если он остается в проекте;
- любые внешние runtime/dependency, требующие default context;
- тяжелые integration bridge слои.

Sticky module не означает, что его надо размазывать по host. Это значит, что у него должен быть отдельный контракт и отдельная ownership boundary, но lifetime у него длиннее, чем у reloadable runtime.

## 4. Что считать базовой целевой архитектурой

Целевая форма проекта:

- `Host Layer`
  - Revit startup lifecycle
  - Ribbon / dockable pane registration
  - Pipe servers / logging / status
  - Runtime loader
  - Non-reloadable UI shell

- `Runtime Layer`
  - Reloadable orchestration runtime
  - Workflow engine over MCP
  - Module composition
  - Approval/task logic
  - MCP-aware services

- `Capability Modules`
  - `McpCapability` — основной capability layer
  - `ChatUiCapability` — обязательный POC-модуль, но non-reloadable
  - `WorkflowCapability` — reloadable
  - `TestCapability` — host + runtime support
  - `PythonCapability` — sticky optional legacy/experimental capability

## 5. MCP-first позиционирование

Проект должен опираться на следующие принципы:

1. MCP — основной способ взаимодействия с моделью.
2. Собственный C#/Python execution больше не является основной продуктовой осью.
3. Chat UI существует не как REPL, а как task/approval/orchestration surface.
4. Python, если сохраняется, живет как sticky optional capability для исследований, быстрого прототипирования или unsupported сценариев.
5. Основная ценность — orchestration, custom workflows, governance и dev productivity.

## 6. Ограничения и допущения для POC

Для снижения трудозатрат POC должен быть намеренно узким.

Обязательные элементы POC:

- host продолжает запускаться в Revit;
- hot reload для runtime остается рабочим;
- in-Revit test transport остается рабочим;
- chat UI остается обязательным, но только как non-reloadable POC shell;
- MCP рассматривается как базовый источник model actions;
- минимум один end-to-end workflow через чат должен работать поверх MCP-oriented orchestration.

Необязательные элементы POC:

- полноценная extensibility marketplace-модель;
- production-grade Python support;
- сложная совместимость со старым assistant UX;
- полная очистка всех legacy следов за один проход;
- полностью независимая reloadability каждого capability.

## 7. Главный принцип снижения трудозатрат

Pivot не должен быть переписыванием с нуля. Нужно максимально использовать уже готовые блоки.

### 7.1. Сохраняем без радикальной переделки

- `Rca.Loader` как host base;
- hot reload и runtime lifecycle;
- named pipe infrastructure;
- logging transport;
- integration test transport;
- status/hash/version logic.

### 7.2. Переделываем точечно

- assistant-centric naming и semantics;
- runtime bootstrap;
- contracts layer;
- UI semantics;
- Python-centric runtime assumptions.

### 7.3. Не делаем в первой волне

- полноценный новый Python runtime;
- сложную модульную систему с независимой установкой пакетов;
- многослойную authorization/security model;
- полное переосмысление build-system, если текущий hot reload уже работает.

## 8. Обновленный план Pivot

### Этап 0. Freeze и Decision Record

Цель:

- зафиксировать новую архитектурную ось проекта до начала массовых правок.

Сделать:

- записать, что проект переходит в `MCP-first extension platform`;
- записать, что chat UI обязателен для POC, но является `non-reloadable module`;
- записать, что Python становится `sticky optional capability`, а не core axis;
- записать, что generic model access больше не является зоной дифференциации.

Выход:

- утвержденная терминология: `host`, `reloadable module`, `non-reloadable module`, `sticky module`, `capability`.

Оценка:

- `2-4` часа с AI-agent support.

### Этап 1. Разделить проект на Host / Runtime / Capabilities на уровне архитектуры

Цель:

- не переписывая всё, ввести правильные boundaries.

Сделать:

- определить, какие текущие проекты являются host-level;
- определить, какие части runtime действительно reloadable;
- отделить startup-sensitive UI shell от UI content;
- явно пометить Python как sticky capability, если он остается;
- убрать смешение `assistant app` и `platform infrastructure` в базовых контрактах.

Практически:

- `Rca.Loader` закрепляется как host;
- `Rca.Runtime` закрепляется как reloadable orchestration runtime;
- `Rca.UI` режется на host-shell и runtime-content, где применимо;
- `Rca.Core` перестает быть автоматически тождественным `PythonExecutionService`.

Выход:

- архитектурная карта слоев;
- минимальный refactor contracts и bootstrap.

Оценка:

- `20-40` часов.

### Этап 2. Переопределить chat UI как MCP-oriented POC shell

Цель:

- не удалять UI, а быстро переориентировать его под новую product logic.

Сделать:

- оставить chat UI обязательным, но non-reloadable;
- перестать трактовать его как Python/chat assistant shell;
- перевести UI semantics в task console / approval console / orchestration console;
- подготовить UI к работе с MCP-oriented actions, history, tool approvals и status.

Важно:

- панель регистрируется на startup в host;
- содержимое может меняться частично, но для POC сам shell считается non-reloadable;
- не нужно добиваться полного reload UI architecture в первой волне.

Выход:

- рабочий POC UI без старой product semantics.

Оценка:

- `20-35` часов.

### Этап 3. Ввести MCP capability как главный runtime capability

Цель:

- сделать MCP базовым способом model interaction.

Сделать:

- создать capability abstraction для MCP-oriented operations;
- привязать orchestration runtime к MCP tool groups;
- реализовать хотя бы минимальный workflow layer поверх MCP;
- оставить возможность future extension для custom tools и enterprise workflows.

Важно:

- не строить конкурирующий layer generic model access;
- использовать MCP как foundation, а не как временный adapter;
- не смешивать MCP напрямую с legacy Python contract.

Выход:

- `McpCapability` как центральная capability-поверхность POC.

Оценка:

- `35-60` часов.

### Этап 4. Перевести runtime в orchestration-first модель

Цель:

- сделать runtime полезным не как исполнятор произвольного кода, а как координатор workflow.

Сделать:

- убрать assumption, что runtime обязательно поднимает Python service;
- переписать bootstrap так, чтобы runtime регистрировал capabilities, а не фиксированный assistant stack;
- оставить runtime reloadable;
- завязать один или несколько POC workflow на MCP capability и UI shell.

Выход:

- reloadable orchestration runtime.

Оценка:

- `30-50` часов.

### Этап 5. Сохранить Python только как sticky optional capability

Цель:

- не тратить силы на полный новый Python pivot, но не выбросить полезные наработки без необходимости.

Сделать:

- убрать Python из core-required startup path;
- убрать Python из обязательного UI flow;
- оставить Python только как optional / experimental capability;
- зафиксировать, что Python не является главным способом работы с моделью;
- при необходимости перевести Python code path в disabled-by-default режим.

Выход:

- Python не мешает MCP-first архитектуре;
- legacy код не блокирует pivot.

Оценка:

- `12-24` часа.

### Этап 6. Укрепить test loop под MCP-first POC

Цель:

- сохранить главный технический актив проекта: быстрый feedback loop внутри Revit.

Сделать:

- не ломать existing named-pipe test transport;
- добавить tests на новые capability boundaries;
- добавить smoke tests на host startup + runtime reload + UI shell availability;
- добавить tests на MCP-first orchestration path там, где это возможно в текущей среде.

Выход:

- POC, который можно итерировать через AI-agents без ручной деградации качества.

Оценка:

- `20-35` часов.

## 9. Что в этом плане считается reloadable, non-reloadable и sticky

### Reloadable

- runtime orchestration logic;
- capability composition;
- workflow execution;
- часть runtime services;
- custom domain logic поверх MCP.

### Non-reloadable

- Loader host;
- ribbon registration;
- dockable panel registration;
- chat UI shell для POC;
- pipe/logging servers;
- status/version infrastructure.

### Sticky

- Python capability, если сохраняется;
- любые тяжёлые external runtime integrations, которые плохо переживают unload;
- некоторые future native bridges, если появятся.

## 10. Как использовать AI-агентов для этого Pivot

Так как pivot планируется выполнять AI-агентами, архитектура и delivery должны быть ориентированы на agent-friendly execution.

### 10.1. Что агентам подходит лучше всего

- массовый refactor naming и semantics;
- вынос capability boundaries;
- переписывание bootstrap и contracts;
- адаптация UI semantics;
- обновление тестов;
- протягивание новых интерфейсов и удаления legacy связей.

### 10.2. Что должен утверждать человек

- финальные boundaries host/runtime/capability;
- какой код реально остается sticky;
- что именно входит в POC;
- какие сценарии считаются обязательными;
- где допустим компромисс ради скорости.

### 10.3. Как разбивать работу для AI-агентов

Работу нужно вести не одним гигантским запросом, а серией небольших эпиков:

1. Normalize naming and semantics for MCP-first pivot.
2. Separate host startup UI shell from runtime content.
3. Remove Python as required core dependency.
4. Introduce MCP capability boundary.
5. Rework runtime bootstrap to capability orchestration.
6. Adapt chat UI for task/approval POC.
7. Add/repair tests for new architecture.

### 10.4. Как снизить риск agent-driven разработки

- не просить агента сразу "сделать весь pivot";
- после каждого эпика прогонять targeted tests;
- поддерживать короткий architecture note в репозитории;
- фиксировать acceptance criteria на каждую стадию;
- не делать одновременно глубокий build-system refactor и product pivot.

## 11. Минимальный POC scope

Pivot считается успешным на уровне POC, если выполнено следующее:

1. Loader продолжает стабильно стартовать в Revit.
2. Chat UI существует как обязательный non-reloadable shell.
3. Runtime остается reloadable.
4. Python больше не обязателен для core path.
5. MCP-first capability layer встроен как основной способ orchestration.
6. Хотя бы один workflow проходит через `chat -> orchestration -> MCP capability -> result back to UI`.
7. In-Revit testing и hot reload не деградировали критически.

## 12. Что НЕ делать в этом Pivot

- не конкурировать с official MCP на уровне базовых операций модели;
- не пытаться сделать каждую часть системы independently reloadable;
- не пытаться сразу сделать production-grade plugin marketplace;
- не строить новый Python-first assistant параллельно с MCP-first pivot;
- не переписывать с нуля весь Loader/build/test stack, если он уже работает.

## 13. Предварительная оценка трудозатрат

С учетом MCP-first направления и ограничения POC, трудозатраты должны быть ниже, чем у предыдущего полного platform pivot.

Оценка для AI-agent driven delivery:

- человеческое время: `35-80` часов;
- суммарная работа с участием AI-агентов: `90-180` часов эквивалента implementation effort`;
- если Python целиком уходит из POC path, можно стремиться к нижней половине диапазона.

Оценка для pure human middle-dev delivery без активного agent workflow:

- `160-280` часов.

## 14. Решение по умолчанию

Если по ходу реализации появится спор о направлении, использовать следующее правило:

- все, что связано с startup lifecycle, registration и постоянной инфраструктурой — `host`;
- все, что связано с workflow/orchestration и может жить в runtime — `reloadable module`;
- все, что полезно как capability, но плохо выгружается — `sticky module`;
- все, что касается generic model access — делать через MCP-first подход;
- chat UI для POC сохранять, но не строить вокруг него старую assistant/Python semantics.