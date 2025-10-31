## Цель

Реализуй систему, которая позволит мне отслеживать test coverage с AltCover. Он уже установлен. Чтобы понимать, с чем ты работаешь, проверь какая версия установлена. 

## Как будет работать система
1. MSBuild интеграция с флагом управления
Система построена на двух ключевых файлах MSBuild:
	•	`build/props/CodeMetrics.props` - централизованная конфигурация свойств и пороговых значений
	•	`build/targets/CodeMetrics.targets` - цели для сбора метрик и генерации отчетов

2. Флаг включения/выключения метрик

<PropertyGroup>
  <EnableCodeMetrics>true</EnableCodeMetrics>
</PropertyGroup>

3. Интеграция с AltCover
AltCover автоматически интегрируется через MSBuild свойства:
<PropertyGroup Condition="'$(EnableCodeMetrics)' == 'true'">
  <AltCoverEnabled>true</AltCoverEnabled>
</PropertyGroup>


Система использует те же паттерны, что и hot-reload инфраструктура:
	•	Условные MSBuild цели
	•	Централизованные свойства в .props файлах
	•	Интеграция с тестовой системой
	•	Временные папки для артефактов

## Как должен работать AltCover

Я проделал большую работу, чтобы предварительно установить, как именно работает AltCover с моим проектом. Ты должен реализовать то же самое.

### Инструментация сборок в конечном месте deploy 

Если <EnableCodeMetrics>true</EnableCodeMetrics>, то при вызове "dotnet build" после деплоя dll в C:\Users\baidakov\AppData\Roaming\Autodesk\Revit\Addins\2026\<timestamp>, dll должны быть инструментированы. 

Код:
```
 altcover 
 --inputDirectory="C:\Users\baidakov\AppData\Roaming\Autodesk\Revit\Addins\2026\<timestamp>" 
 --assemblyFilter="Rca.*.dll" 
 --inplace
 --save
```


### Сбор coverage

Сбор coverage происходит после выполнения тестов. После выполнения тестов с dotnet test, если <EnableCodeMetrics>true</EnableCodeMetrics>, MSBuild должен выполнить сбор покрытия в 2 этапа:

1. удаление сигнального файла. Перед удалением сигнального файла нужно проверить, что он есть. Если файла нет - нужно выводить warning
2. сбор покрытия в файл

Код:

```
Remove-Item -Path C:\Users\baidakov\AppData\Roaming\Autodesk\Revit\Addins\2026\20251031_195948\coverage.xml.acv    

altcover runner 
--collect 
--recorderDirectory="C:\Users\baidakov\AppData\Roaming\Autodesk\Revit\Addins\2026\<timestamp>"
--outputFile="C:\Users\baidakov\rca-plugin\build\Metrics\coverage_final.xml"
```

Используй именно вызовы powershell, а не targets msbuild. когда наладим powershell, будем переписывать на targets.

