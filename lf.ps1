# convert-changed-cs-to-lf.ps1
# Конвертирует все изменённые .cs файлы в LF

# 1. Получаем список изменённых/добавленных .cs файлов из git
$files = git diff --name-only --diff-filter=ACM | Where-Object { $_ -like '*.cs' }

if (-not $files) {
    Write-Host "Изменённых .cs файлов не найдено."
    exit 0
}

Write-Host "Будут конвертированы в LF следующие файлы:`n"
$files | ForEach-Object { Write-Host " - $_" }

$answer = Read-Host "`nПродолжить? (y/n)"
if ($answer -ne 'y' -and $answer -ne 'Y') {
    Write-Host "Отменено."
    exit 0
}

foreach ($f in $files) {
    Write-Host "Обработка $f"
    $path = Resolve-Path $f
    $content = Get-Content $path -Raw
    # заменяем CRLF -> LF
    $content = $content -replace "`r`n", "`n"
    # сохраняем как UTF-8 без BOM
    [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "`nГотово. Проверьте diff в Git."
