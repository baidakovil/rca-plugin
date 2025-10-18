Param(
    [Parameter(Mandatory=$true)] [string]$TargetPath,
    [Parameter(Mandatory=$false)] [int]$TtlSec = 60,
    [Parameter(Mandatory=$false)] [string]$ForceStr = "false"
)

$ErrorActionPreference = 'Stop'

# Parse flags
$force = $false
try { $force = ($ForceStr -eq '1') -or ($ForceStr.ToLower() -eq 'true') } catch {}

# Ensure directory exists
try {
    $dir = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($TargetPath))
    if (![string]::IsNullOrWhiteSpace($dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
} catch {}

# Cross-process coordination
$m = New-Object System.Threading.Mutex($false, 'Global\RCA_BuildStamp')
try {
    $null = $m.WaitOne()

    $writeNew = $force
    if (-not $writeNew) {
        if (Test-Path -LiteralPath $TargetPath) {
            try {
                $age = (Get-Date) - (Get-Item -LiteralPath $TargetPath).LastWriteTime
                if ($age.TotalSeconds -le $TtlSec) { $writeNew = $false } else { $writeNew = $true }
            } catch { $writeNew = $true }
        } else { $writeNew = $true }
    }

    if ($writeNew) {
        $stamp = [DateTime]::Now.ToString('yyyyMMdd_HHmmss')
        Set-Content -LiteralPath $TargetPath -Value $stamp -NoNewline -Force
    }
}
finally {
    try { $m.ReleaseMutex() | Out-Null } catch {}
}
