# Параметры
$Source      = 'C:\Users\baidakov\rca-plugin'
$DestDir     = 'C:\Mac\Home\Documents\RCA Backup'
$Stamp       = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$TmpZip      = Join-Path $env:TEMP "RcaBackup_$Stamp.zip"
$FinalZip    = Join-Path $DestDir (Split-Path $TmpZip -Leaf)
$LogFile     = 'C:\Users\baidakov\RcaBackup.log'


Add-Content $LogFile "`n----- $(Get-Date) -----"

# 1) Упаковка в ZIP (встроенный Compress-Archive)
Compress-Archive -Path $Source -DestinationPath $TmpZip -CompressionLevel Fastest -Force

# 2) Перенос архива в папку на Mac
Move-Item -Path $TmpZip -Destination $FinalZip -Force

# 3) (Опционально) Ротация: храним только 3 последних архива
Get-ChildItem $DestDir -Filter '*.zip' |
  Sort-Object LastWriteTime -Descending |
  Select-Object -Skip 3 |
  Remove-Item -Force -ErrorAction SilentlyContinue

Add-Content $LogFile -Text "Архив создан и скопирован: $FinalZip" -Title "Backup Completed"
