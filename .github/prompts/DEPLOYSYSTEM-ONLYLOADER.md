## Компоненты новой системы

1. Изменение места копирования новых DLL в MSBuild (Rca.Runtime.csproj)  
   - Настроить копирование новых билдов `Rca.Loader.dll`, `Rca.Loader.Contracts.dll` и `Rca.Runtime.dll` в `$TEMPDLL` (`C:\Users\baidakov\AppData\Local\RCA\Runtime\$TEMPDLL`), чтобы новые билды могли создаваться независимо от статуса Revit запущен/не запущен.

2. Проверка обновлений **Loader/Contracts** в Rca.Loader:
   - При загрузке плагина рассчитывать хэши загруженных в данный момент в память `Rca.Loader.dll`, `Rca.Loader.Contracts.dll` и `Rca.Runtime.dll` (по ссылкам на файлы в файловой системе, в `$TEMPDLL` и в `$REVITADDINDIR`). Сохранять адреса и хэши, откуда загружены DLL, в `C:\Users\baidakov\AppData\Local\RCA\LoadedAssemblies.json` - 6 полей с тремя адресами и тремя хэшами.
   - По сигналу от MSBuild через Named Pipe обновлять статус `Rca.Loader.dll`, `Rca.Loader.Contracts.dll` и `Rca.Runtime.dll` через сравнение хэшей в `$TEMPDLL` и в памяти.

3. Отображение информации о сборках в **TextBox в Ribbon** (Rca.Loader)  
   - Отображает статус загруженных DLL в Ribbon в Revit.  
   - В релизе не отображается, только в DEBUG.
   - Три строчки:        
       1. Статус `Rca.Loader.dll` и `Rca.Loader.Contracts.dll`: $status - $path.
       - $status имеет два значения:
         - `current` - если хэши в `$TEMPDLL` не совпадают с загруженными
         - `outdated` - если хэши в `$TEMPDLL` не совпадают с загруженными
       - $path - имя папки в `$TEMPDLL`, из которой загружены DLL (при загрузке Revit это значение считывается из `LoadedAssemblies.json`).

       2. Статус `Rca.Runtime.dll`: $status - $path.
       - Аналогично первому пункту.

       3. Время последнего сигнала от MSBuild в формате $time - $event:
        - $time - время в формате "Last MSBuild signal: HH:MM:SS".
        - $event - результат проверки новой папки MSBuild.$event имеет три значения:
          - `no changes` - если хэши всех трех DLL в `$TEMPDLL` совпадают с хэшами загруженных DLL
          - `only runtime outdated` - если хэши для `Rca.Runtime.dll` не совпадают с загруженными dll
          - `only loader outdated` - если хэши для новых `Rca.Loader.dll` и/или `Rca.Loader.Contracts.dll` не совпадают с хэшами загруженных dll
          - `both loader and runtime outdated` - если оба предыдущих события случились одновременно

4. Скрипт **RestartRevitGraceful** (powershell):
   - Если хэши для новых `Rca.Loader.dll` или `Rca.Loader.Contracts.dll` (в папке, полученной от MSBuild) не совпадают с хэшами загруженных dll, то по кнопке "Restart Assemblies" предложить запустить **RestartRevitGraceful**.
   - При запуске скрипта, завершить процесс Revit gracefully.
   - Убедившись, что процесс завершен, скопировать `Rca.Loader.dll` и `Rca.Loader.Contracts.dll` из `$TEMPDLL` в `$REVITADDINDIR`.
   - Перезаписать в `LoadedAssemblies.json` пути для `Rca.Loader.dll` и `Rca.Loader.Contracts.dll`.
   - Запустить Revit заново.


### 2. Убедиться в логике работы PipeServerService (Rca.Loader)

- Убедиться в логике обработки IPC-команд:  
  - При получении `ReloadRuntime`:
    - сравнивать хэши `Rca.Loader.dll`, `Rca.Loader.Contracts.dll` в `$TEMPDLL` и в `$REVITADDINDIR`.
    - сравнивать хэши `Rca.Runtime.dll` в `$TEMPDLL_LAST` и в `$TEMPDLL_JSON`.
  - загружать новые Runtime-DLL из `$TEMPDLL`.  
  - Обновлять статус в TextBox в Ribbon.


## Ожидаемые сценарии

**Загрузка плагина**  
1. При загрузке Revit Rca.Loader загружается из `$REVITADDINDIR`.
2. Rca.Runtime загружается из последней по алфавиту папки в `$TEMPDLL`.
3. Если `LoadedAssemblies.json` не существует, он создаётся с путем к папке  Rca.Runtime.DLL (последняя по алфавиту папка в `$TEMPDLL`). Путь для Rca.Loader не указывается.
4. Если `LoadedAssemblies.json` существует, но путь для Rca.Runtime в `LoadedAssemblies.json` не совпадает с последней по алфавиту папкой в `$TEMPDLL`, то json перезаписывается - путь заменяется на последнюю по алфавиту папку.
5. Рассчитывается хэш Rca.Runtime.dll и перезаписывается в `LoadedAssemblies.json`, если не совпадает.
6. Если хэши для `Rca.Loader.dll` и/или `Rca.Loader.Contracts.dll` в `$TEMPDLL` не совпадают с хэшами в `LoadedAssemblies.json`, статус первой строчки в TextBox в Ribbon становится `outdated`, иначе `current`.

**Сценарий с обновлением Runtime**  
1. Новый билд копируется в `$TEMPDLL`.  
2. MSBuild посылает сигнал `ReloadRuntime`.  
3. Rca.Loader рассчитывает хэш и обнаруживает, что Rca.Runtime.dll обновился (для текущего dll хэш берется из `LoadedAssemblies.json`).
4. Перезагружает Runtime
5. Перезаписывает пути и хэш в json файле
6. Сравнивает хэши для `Rca.Loader.dll` и `Rca.Loader.Contracts.dll` в `$TEMPDLL` и в `LoadedAssemblies.json`, обновляет статус первой строчки в TextBox в Ribbon - и если все совпадает, то обновляет вторую и третью строчку в TextBox в Ribbon

**Сценарий Loader/Contracts**  
1. Новый билд Loader или Contracts копируется в `$TEMPDLL`.  
2. Rca.Loader рассчитывает хэш и обнаруживает, что Rca.Loader.dll или Rca.Loader.Contracts.dll обновился (для текущего dll хэш берется из `LoadedAssemblies.json`). 
3. UI в Ribbon обновляется - первая строчка становится `outdated`. 
4. По кнопке "Restart Assemblies" предлагается вопрос - "Loader outdated. Do you want to restart app or just reload Rca.Runtime.dll?" с вариантами ответа - только перезагрузить runtime, перезагрузить revit, или cancel. При выборе "перезагрузить revit" запускается скрипт **RestartRevitGraceful**.

Реализуй систему с учётом всех указанных требований и сценариев, обеспечив надёжность и прозрачность процессов.
Остановись, когда минимальный MVP будет готов, а все проекты будут собираться через `dotnest build` без ошибок и предупреждений.
