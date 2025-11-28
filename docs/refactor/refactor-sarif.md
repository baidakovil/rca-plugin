Проведи рефакторинг кода в namespace `Rca.Tools.MetricsReporter.Rendering.HtmlTableGenerator` для устранения нарушений правил анализатора кода (SARIF). Рефакторинг выполняется для символов уровня Member (методы), не для классов.

## Требования

- Используй в работе специальную утилиту `metrics-reader`, которая позволяет по одному запросу обновлять и получать значения метрик для символов, требующих рефакторинга. Описание и примеры использования даны в `@docs/Metrics-Reporter.md`.
- Строго следуй порядку работы, описанному ниже, чтобы достичь требуемой цели: устранить все нарушения правил анализатора для всех символов в указанном выше namespace.
- При устранении нарушений следуй рекомендациям анализатора из сообщения `message` и описания правила `shortDescription`. Для деталей по конкретному правилу обращайся к документации Microsoft (например, `https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/CAxxxx` для правил CAxxxx).
- Следуй принципам SOLID и правилам, установленным в проекте.

## Строго следуй этой инструкции, пока не исправишь все нарушения

### 1. Получение проблемной группы нарушений

Используя команду `metrics-reader readsarif`, получи первую проблемную группу нарушений по `ruleId`. Пример запроса к `metrics-reader` с нужными опциями:

```powershell
.\src\Tools\Rca.MetricsReporter\bin\Debug\net8.0\Rca.MetricsReporter.exe metrics-reader readsarif --namespace Rca.Tools.MetricsReporter.Rendering.HtmlTableGenerator --metric SarifCaRuleViolations --symbol-kind Member
```

Если получаешь сообщение о том, что нарушений не найдено (вместо объекта с полями `ruleId`, `shortDescription`, `count`, `violations`), это значит, что проблемных нарушений нет: закончи работу.

### 2. Анализ нарушения

Возьми первое нарушение из массива `violations` в работу. Изучи код по пути `uri`, строкам `startLine`–`endLine`, сообщение `message` и описание правила `shortDescription`. Прими решение о возможности рефакторинга.

Есть только одна причина для отмены рефакторинга: рефакторинг не нужно проводить, если исправление приводит к ухудшению читаемости и поддержки кода. В случае отмены рефакторинга нужно сделать suppression для символа с указанием `ruleId`. Не забудь добавить using директиву `using System.Diagnostics.CodeAnalysis;` в начало файла, если её там ещё нет. Пример suppression:

```csharp
[SuppressMessage(
    "Microsoft.CodeAnalysis.CSharp",
    "CA1506:Avoid excessive class coupling",
    Justification = "Orchestration point that coordinates multiple services; coupling is inherent to the design.")]
```

При отмене рефакторинга внеси suppression в код и переходи к следующему нарушению из группы. Если группа обработана, начинай с пункта 1.

### 3. Выполнение рефакторинга

Если рефакторинг возможен, поступай так:

1. Спланируй рефакторинг на основе сообщения анализатора.
2. Выполни рефакторинг.
3. Проверь, что билд проекта успешен. Используй билд всего solution `dotnet build --no-incremental` или билд отдельно взятого проекта для экономии времени. Если билд падает, исправляй код, пока билд не станет зелёным.
4. После проверки билда проверь, что тесты проходят. Используй тесты всего solution `dotnet test --no-build` или тесты для отдельно взятого проекта для экономии времени. Если тесты падают, исправляй код, как описано в предыдущем пункте.

### 4. Проверка результата

С помощью команды `metrics-reader test` проверь, что нарушение устранено. Пример запроса к `metrics-reader` с нужными опциями:

```powershell
.\src\Tools\Rca.MetricsReporter\bin\Debug\net8.0\Rca.MetricsReporter.exe metrics-reader test --symbol "Rca.Tools.MetricsReporter.Rendering.HtmlTableGenerator.Generate(...)" --metric SarifCaRuleViolations
```

Если в ответе видишь `"isOk": false`, то возвращайся к пункту 2 с данным нарушением. Количество дополнительных попыток рефакторинга: 2 попытки на каждое нарушение. Если после второй дополнительной попытки не удалось устранить нарушение, то следует сделать Suppression с сообщением Justification на английском языке, полностью раскрывающим суть проблемы. Не забудь добавить using директиву `using System.Diagnostics.CodeAnalysis;` в начало файла, если её там ещё нет.

Если `"isOk": true`, то переходи к следующему нарушению из текущей группы. Если группа обработана, приступай к следующей группе, как описано в пункте 1.

