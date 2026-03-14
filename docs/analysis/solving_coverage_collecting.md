помоги мне разобраться со сбором покрытия в проекте. 

1. когда я делаю новый билд через dotnet build, при этом создается шаблон C:\Users\baidakov\rca-plugin\build\Metrics\AltCover\CoverageTemplate.g.xml и сигнальный файл C:\Users\baidakov\rca-plugin\build\Metrics\AltCover\CoverageTemplate.g.xml.acv), а также инструментированные сборки плагина во временной папке внутри папки 2026, например C:\Users\baidakov\AppData\Roaming\Autodesk\Revit\Addins\2026\20260314_185225. а также интеграционные тесты (т.е. запускаемые из revit runtime) во временной папке в Test, например C:\Users\baidakov\AppData\Local\RCA\Test\20260314_185245. Это ОК. 

2. я  запускаю revit с этой сборкой, он загружает плагин. После запуска в терминале  "dotnet test --no-build", в C:\Users\baidakov\rca-plugin\build\Metrics\AltCover создаются файлы покрытия 

Coveragetemplate.g.xml.acv - 0 байт
CoverageTemplate.g.xml.0.acv - 0 байт
CoverageTemplate.g.xml.1.acv - 121 байт
CoverageTemplate.g.xml.2.acv - 72 байт
CoverageTemplate.g.xml.3.acv - 151 байт
CoverageTemplate.g.xml.4.acv - 743 байт

Это ОК

3. Я запускаю сбор покрытия командой "dotnet msbuild src/Rca.Runtime/Rca.Runtime.csproj /t:CollectCoverage /p:AltCoverEnabled=true /p:CoverageVerbose=true". В C:\Users\baidakov\rca-plugin\build\Metrics\AltCover\html\index.html я вижу, что собралось 49% покрытия (я сохранил это результат в C:\Users\baidakov\rca-plugin\build\Metrics\AltCover\html_49). Вижу, что покрытие собралось с интеграционных тестов. 
остается только файл CoverageTemplate.g.xml.acv, а остальные (.1.acv и тд) - удалены
Это ОК

4. Теперь я хочу еще раз запустить интеграционные тесты и снова собрать покрытие (это нужно для автоматизации некоторых процессов). Я снова запускаю "dotnet test --no-build", и вижу в C:\Users\baidakov\rca-plugin\build\Metrics\AltCover созданы файлы покрытия 

Coveragetemplate.g.xml.acv - 0 байт
CoverageTemplate.g.xml.0.acv - 121 байт
CoverageTemplate.g.xml.1.acv - 72 байт
CoverageTemplate.g.xml.2.acv - 151 байт
CoverageTemplate.g.xml.3.acv - 743 байт

Я запускаю "dotnet msbuild src/Rca.Runtime/Rca.Runtime.csproj /t:CollectCoverage /p:AltCoverEnabled=true /p:CoverageVerbose=true", и что я вижу? СТРАННОЕ! В C:\Users\baidakov\rca-plugin\build\Metrics\AltCover\html\index.html я вижу, что собралось только 9% покрытия!! Я сохранил это в  C:\Users\baidakov\rca-plugin\build\Metrics\AltCover\html_9 для твоего анализа, если потребуется. Дело в том, что мне хочется запустить полноценный цикл, но я получаю почему-то только 9%, а не 49. 

В чем дело? Я неправильно использую AltCover? Возможно, неправильно инструментирую или работаю с сигнальными файлами? Изучи #file:code-metrics.targets , #file:code-metrics.props , а также как инструментируются сборки и т.д. Далее у меня более сложные случаи будут рассматриваться - с созданием новых runtime-частей сборки и их автоматической загрузкой через скрипты в #file:collect-opencover.ps1 , а также создание новых интеграционных тестов с подгрузкой... Но пока я не разобрался с такими простыми вещами, я не могу переходить дальше. Разберись и предложи варианты решения или сразу решение. Сейчас Revit загружен, в нем загружена сборка C:\Users\baidakov\AppData\Roaming\Autodesk\Revit\Addins\2026\20260314_191203, при необходимости запускай все тесты, обращайся в интернет за справкой AltCover и т.д.