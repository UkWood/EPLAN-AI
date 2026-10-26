param(
    [string]$EplanApi29Dir = 'C:\Program Files\EPLAN\Platform\2.9.4\Bin'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $repoRoot 'src\Audeu.EplanAi.LegacyHost\bin\Release\net472'
$hostExe = Join-Path $outputDir 'Audeu.EplanAi.LegacyHost.exe'

if (-not (Get-Process -Name 'EPLAN' -ErrorAction SilentlyContinue)) {
    throw 'EPLAN.exe is not running. Start EPLAN normally, open the safe test project, and leave EPLAN running.'
}

if (-not (Test-Path $hostExe)) {
    throw "Task 7 LegacyHost executable not found: $hostExe. Run scripts\verify-task7-build.ps1 first."
}

foreach ($name in @('Eplan.EplApi.RemoteClientu.dll', 'Eplan.EplApi.Remotingu.dll')) {
    $deployed = Join-Path $outputDir $name
    if (-not (Test-Path $deployed)) {
        throw "LegacyHost runtime assembly is missing: $deployed. Run scripts\verify-task7-build.ps1 first."
    }
}

$oldPath = $env:Path
$oldOutputEncoding = [Console]::OutputEncoding
$env:Path = "$EplanApi29Dir;$env:Path"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

try {
    Write-Host 'Starting persistent Task 7 LegacyHost.'
    Write-Host 'Fixed pipe: Audeu.EplanAi.LegacyHost.v1'
    Write-Host 'Leave this window open while the client probe runs in another PowerShell window.'
    Write-Host 'After the client probe passes, press Ctrl+C here to exercise graceful RemoteClient shutdown.'
    & $hostExe
    if ($LASTEXITCODE -ne 0) {
        throw "Task 7 LegacyHost exited with code $LASTEXITCODE."
    }
    Write-Host 'PASS: Task 7 LegacyHost stopped cleanly.'
}
finally {
    $env:Path = $oldPath
    [Console]::OutputEncoding = $oldOutputEncoding
}
