## Компоненты новой системы

### 1. Monitor Service

Monitor Service - новый проект в текущем Solution. Разрабатывается как Windows Service на .NET 8 и выполняет:

1. **Мониторинг `$TEMPDLL`**  
   - Использует FileSystemWatcher для обнаружения новых или изменённых DLL: `Rca.Loader.dll`, `Rca.Loader.Contracts.dll`, `Rca.Runtime.dll`.

2. **Поддержка JSON-файла состояния**  
   - Хранит единый раздел `loader_info`, включающий оба файла (Loader и Contracts), и раздел `runtime_info`.  
   - Для каждого файла сохраняет SHA256-хэш, версию сборки и статус (`current` или `outdated`).  
   - Итоговый статус `loader_info.status` равен `outdated`, если хотя бы один файл неактуален.

3. **IPC с Rca.Loader (PipeServerService)**  
   - Убирает старый AfterTargets-код из Rca.Runtime.csproj.  
   - По изменению Runtime отправляет команду `ReloadRuntime` в PipeServerService внутри Rca.Loader и ждёт ответа `ReloadCompleted` или `ReloadFailed`.  
   - Не отправляет команду при изменении Loader/Contracts.

4. Обработчик IPC-команды **RestartRevitGracefully** должен:  
   - Завершить процесс Revit gracefully.  
   - Убедившись, что процесс завершен, скопировать `Rca.Loader.dll` и `Rca.Loader.Contracts.dll` из `$TEMPDLL` в `$REVITADDINDIR`.  
   - Запустить Revit заново.  

5. **Отложенный деплой Loader/Contracts**  
   - Постоянно проверяет процесс Revit. Как только Revit завершается, копирует из `$TEMPDLL` в `$REVITADDINDIR` только `Rca.Loader.dll` и `Rca.Loader.Contracts.dll`.  
   - Обновляет `loader_info.status = current`.

6. **Логирование и надёжность**  
   - Логи операций и ошибок через Windows Event Log.  
   - Таймауты IPC, повторные попытки и обработка ошибок reload.  

### 2. Изменения в PipeServerService (Rca.Loader)

- Обновить обработку IPC-команд:  
  - При получении `ReloadRuntime` загружать новые Runtime-DLL из `$TEMPDLL`.  
  - Возвращать в Monitor Service результат выполнения.  
- Исключить любые прямые MSBuild-триггеры hot-reload.
- Расширить обработку команды **ReloadRuntimeCommand.cs** таким образом:  
  - Если `loader_info.status == outdated`, при вызове ReloadRuntimeCommand показывать окно-предупреждение с текстом **“Revit will be restarted”** и кнопкой **“Cancel (3)”**, где цифра в скобках — таймер обратного отсчёта в секундах.  
  - По истечении таймера без нажатия Cancel отправлять в Monitor Service команду **RestartRevitGracefully** по Named Pipe.  
  - Если Cancel нажата, отменять рестарт и выполнять только обычный hot-reload Runtime.


### 3. UI в Rca.UI (RcaDockablePanel.xaml)

- Добавить статус-панель в основное окно через условную компиляцию DEBUG:
  ```xml
  #if DEBUG
    <!-- Простой текстовый статус: Current или Outdated -->
    <StatusPanelControl x:Name="CiCdStatusPanel" />
  #endif
  ```
- Панель показывает:
  - Для Loader/Contracts: **Current** или **Outdated — перезапустить Revit**.  
  - Для Runtime: **Current** или **Outdated — нажмите Reload**.  
- В релизе панель не отображается; статус можно посмотреть только в JSON.

## Ожидаемые сценарии

**Сценарий Runtime**  
1. Новый билд копируется в `$TEMPDLL`.  
2. Monitor Service помечает `runtime_info.status = outdated`.  
3. UI в DEBUG показывает «Outdated — нажмите Reload».  
4. При нажатии кнопки панель вызывает IPC-команду ReloadRuntime.  
5. По ответу Monitor Service меняет `runtime_info.status = current`.  
6. UI показывает «Current».

**Сценарий Loader/Contracts**  
1. Новый билд Loader или Contracts копируется в `$TEMPDLL`.  
2. Monitor Service помечает `loader_info.status = outdated`.  
3. UI в DEBUG показывает «Outdated — перезапустить Revit».  
4. После закрытия Revit Monitor Service копирует эти DLL в `$REVITADDINDIR` и меняет статус на `current`.  

Реализуй систему с учётом всех указанных требований и сценариев, обеспечив надёжность и прозрачность процессов.
Остановись, когда минимальный MVP будет готов, а все проекты будут собираться через `dotnest build` без ошибок и предупреждений.
