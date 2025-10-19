Я прошу тебя заняться большой задачей по улучшению системы интеграционных тестов Revit Addin Rca. Ниже я описал контекст, проблему, и предлагаемые изменения. Пожалуйста, внимательно прочитай и реализуй их.

## Контекст
 Revit Addin Rca включает встроенный тестовый адаптер для запуска интеграционных NUnit-тестов внутри запущенного Revit через Named Pipes - #file:'C:\Users\baidakov\rca-plugin\src\Rca.TestAdapter\Rca.TestAdapter.csproj' . Runtime загружается в собственный AssemblyLoadContext (collectible) в #file:'C:\Users\baidakov\rca-plugin\src\Rca.Loader\Infrastructure\AssemblyLoadService.cs' , а тестовые DLL сейчас лежат в проекте /tests/Rca.Integration.Revit.Tests и загружаются из исходной папки, что приводит к блокировкам и усложняет CI.

 ## Описание проблемы
 - После первого прогона интеграционных тестов Revit блокирует DLL в исходной папке, и последующие билды падают - что приводит к отмене билда
 - даже если DLL были бы не заблокированы, отсутствует механизм для перезагрузки тестов без перезапуска Revit, как это реализовано для Runtime. это приводит к затратам времени на перезапуск Revit после каждого билда тстов

## Описание предлагаемого решения
 - В Post-Build шаге #file:'C:\Users\baidakov\rca-plugin\tests\Rca.Integration.Revit.Tests\Rca.Integration.Revit.Tests.csproj'  копировать все выходные DLL и зависимости в temp-папку `%LOCALAPPDATA%\RCA\Runtime\<latest_timestamp>` - ту же папку, куда сложены наиболее актуальные dll (путь хранится в #field:'Rca.Loader.Infrastructure.LoaderConstants.RuntimeDeployRoot':910-1080 ). Для этого реализовать механизм поиска последней по алфавиту папки в `%LOCALAPPDATA%\RCA\Runtime\`. Предложение по сортировке папок смотри в [sort-folders.csproj](sort-folders.csproj)
 - В #class:'Rca.TestAdapter.RevitTestDiscoverer':452-2515  заменить foreach(sources) на перебор DLL из temp-папки
 - В #class:'Rca.Loader.Testing.RevitTestExecutor':320-10668  при старте тестов создавать новый collectible AssemblyLoadContext для тестов и загружать DLL из temp-папки.
 - После выполнения тестов вызывать Unload() только для тестового контекста и запускать GC.Collect()/WaitForPendingFinalizers(), но не выгружать Runtime.
 - Кнопка #field:'Rca.Loader.Infrastructure.PipeCommands.ReloadRuntime':6702-6755  должна вызывать Unload() и для основного ALC - RuntimeLoadContext, и для тестового ALC - TestLoadContext (принудительно! чтобы выгрузить "застрявшие" тесты при необходимости), затем загружать только Runtime.
 - Все описанные здесь изменения действуют для тестов проекта #file:'C:\Users\baidakov\rca-plugin\tests\Rca.Integration.Revit.Tests\Rca.Integration.Revit.Tests.csproj' , которые обслуживаются адаптером #file:'C:\Users\baidakov\rca-plugin\src\Rca.TestAdapter\Rca.TestAdapter.csproj'  не затрагивая остальные тестовые проекты.

 ## Метод работы для изменений с тестами
 - При внесении изменений, важно не только построить систему, которая компилируется и отдать мне на тесты, но и проверить, что она находит тесты с помощью кастомного тестового адаптера Rca.TestAdapter
 - Эту работу ты должен взять на себя сам - с помощью чтения логов от Tests/Test Explorer, необходимо убедиться, что тесты обнаруживаются
 - Вероятной проблемой при использовании адаптера будет наличие кэша с тестами от предыдущей системой тестирования, поэтому кэш тестов и вообще весь возможный тест MSBuild/Test Explorer необходимо очищать
 - Останавливайся и проси меня о необходимых действиях с Visual Studio или Revit, если какие-то изменения невозможно выполнить через CLI или скрипты.

 ## Изменения в README
 - Никаких изменений в документации не требуется

 ## Миграция
 - Никакой системы миграции или Feature Flags не предусматривается, предыдущая система тестов просто перестает использоваться. Fallbacks не нужны (если интеграционные тесты в temp folder не найдены, значит интеграционные тесты не найдены вообще).

 В своем следующем ответе не делай изменения в файлы, но только создай один md файл в /.github/prompting/integration-tests-plan.md, с планом. План должен быть не очень подробным, но с детализацией задач. Уточни у меня приципиальные моменты, если требуется. Используй правила в #file:'C:\Users\baidakov\rca-plugin\.github\instructions\dotnet-best-practices.prompt.md'  и #file:'C:\Users\baidakov\rca-plugin\.github\instructions\dotnet-design-pattern-review.prompt.md' для следования лучшим практикам, где это применимо
